using System.Buffers.Text;
using System.Net;
using System.Text.Json;
using Agency.Huddle.App.Gateway;
using Agency.Huddle.Tests.Ui;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>
/// Spec 6.6, 8.3, 8.4 and C-7, C-9: the HTTP contract of the handshake endpoints on the real pipeline. The gate and the session
/// resolver are Stage C, so these tests reach <c>/auth/*</c> directly; a header gives a <c>TestServer</c> request a LAN address.
/// </summary>
public sealed class ConnectEndpointsTests
{
    private const string Token = "0123456789abcdef0123456789abcdef0123456789abcdef";

    private const string LanAddress = "203.0.113.9";

    private static readonly JsonSerializerOptions Json = GatewayTestClient.Json;

    private static string ExpectedCookieName(GatewayHost host) => $"huddle_session_{host.Settings.InstanceId}";

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response, CancellationToken ct)
        => await response.Content.ReadFromJsonAsync<JsonElement>(Json, ct);

    /// <summary>Reads a string property of a response body, failing the test when it is absent or null.</summary>
    private static string StringProperty(JsonElement body, string name)
    {
        string? value = body.GetProperty(name).GetString();
        Assert.NotNull(value);
        return value;
    }

    private static string? SessionCookie(HttpResponseMessage response, GatewayHost host)
        => response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? values)
            ? values.FirstOrDefault(cookie => cookie.StartsWith(ExpectedCookieName(host) + "=", StringComparison.Ordinal))
            : null;

    /// <summary>The app with the gateway token fixed and, optionally, extra settings, plus a client that does not follow redirects.</summary>
    private sealed class GatewayHost : IDisposable
    {
        private readonly TeamWebApplicationFactory root = new();
        private readonly WebApplicationFactory<Program> factory;

        public GatewayHost(string? mode = null, string? password = null)
        {
            this.factory = this.root.WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Team:Gateway:Auth:Token", Token);
                builder.UseSetting("Team:Gateway:StrictLocal", "false");
                if (mode is not null)
                {
                    builder.UseSetting("Team:Gateway:Auth:Mode", mode);
                }

                if (password is not null)
                {
                    builder.UseSetting("Team:Gateway:Auth:Password", password);
                }

                builder.ConfigureTestServices(services => services.AddSingleton<IStartupFilter, TestRemoteAddressStartupFilter>());
            });
            this.Client = this.factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
            this.Client.DefaultRequestHeaders.Add("Origin", "http://localhost");
        }

        public HttpClient Client { get; }

        public GatewaySettings Settings => this.factory.Services.GetRequiredService<GatewaySettings>();

        public PairingService Pairing => this.factory.Services.GetRequiredService<PairingService>();

        public BootstrapTokenStore Bootstrap => this.factory.Services.GetRequiredService<BootstrapTokenStore>();

        /// <summary>A client whose requests come from <see cref="LanAddress"/>.</summary>
        public GatewayTestClient Lan(TestDevice? device = null)
        {
            HttpClient lan = this.factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
            lan.DefaultRequestHeaders.Add(TestRemoteAddressStartupFilter.HeaderName, LanAddress);
            return new GatewayTestClient(lan, device);
        }

        public GatewayTestClient Local(TestDevice? device = null) => new(this.Client, device);

        public void Dispose()
        {
            this.Client.Dispose();
            this.factory.Dispose();
            this.root.Dispose();
        }
    }

    /// <summary>The challenge is a 32-byte base64url nonce and the server clock in milliseconds.</summary>
    [Fact]
    public async Task Challenge_ReturnsNonceAndServerTime()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayHost host = new();

        using HttpResponseMessage response = await host.Client.GetAsync(new Uri("/auth/challenge", UriKind.Relative), ct);
        JsonElement body = await BodyAsync(response, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string? nonce = body.GetProperty("nonce").GetString();
        Assert.NotNull(nonce);
        Assert.Equal(32, Base64Url.DecodeFromChars(nonce).Length);
        Assert.InRange(body.GetProperty("ts").GetInt64(), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 60_000, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 60_000);
    }

    /// <summary>A loopback handshake with the gateway token connects as admin and answers a device token, the scopes and the return path.</summary>
    [Fact]
    public async Task Connect_GatewayTokenOnLoopback_Returns200WithDeviceTokenAndScopes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayHost host = new();

        using HttpResponseMessage response = await host.Local().ConnectAsync(new ConnectAuth(Token: Token), ct);
        JsonElement body = await BodyAsync(response, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrEmpty(body.GetProperty("deviceToken").GetString()));
        Assert.Contains(GatewayScopes.Admin, body.GetProperty("scopes").EnumerateArray().Select(scope => scope.GetString()));
        Assert.Equal("/", body.GetProperty("return").GetString());
    }

    /// <summary>The session cookie is named for the Instance, HttpOnly, SameSite=Lax, Path=/, and has neither Max-Age nor Expires (Spec 7.4).</summary>
    [Fact]
    public async Task Connect_SessionCookie_HasTheSpecAttributes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayHost host = new();

        using HttpResponseMessage response = await host.Local().ConnectAsync(new ConnectAuth(Token: Token), ct);
        string? cookie = SessionCookie(response, host);

        Assert.NotNull(cookie);
        Assert.Matches("^huddle_session_[0-9a-f]{8}=[A-Za-z0-9_-]{20,}", cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase); // contains-ok: Set-Cookie attribute order is not part of the contract
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase); // contains-ok: Set-Cookie attribute order is not part of the contract
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase); // contains-ok: Set-Cookie attribute order is not part of the contract
        Assert.DoesNotContain("max-age", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("expires", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secure", cookie, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The return path is kept when it is local, and replaced by <c>/</c> when it is not (C-14).</summary>
    [Theory]
    [InlineData("/settings?tab=remote", "/settings?tab=remote")]
    [InlineData("//evil.example/x", "/")]
    [InlineData("https://evil.example/", "/")]
    [InlineData("settings", "/")]
    public async Task Connect_ReturnPath_IsLocalOnly(string requested, string expected)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayHost host = new();

        using HttpResponseMessage response = await host.Local().ConnectAsync(new ConnectAuth(Token: Token), ct, returnPath: requested);
        JsonElement body = await BodyAsync(response, ct);

        Assert.Equal(expected, body.GetProperty("return").GetString());
    }

    /// <summary>A wrong gateway token is a 401 whose JSON body carries the OpenClaw-style code, and no session cookie.</summary>
    [Fact]
    public async Task Connect_WrongToken_Returns401WithCode()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayHost host = new();

        using HttpResponseMessage response = await host.Lan().ConnectAsync(new ConnectAuth(Token: "wrong"), ct);
        JsonElement body = await BodyAsync(response, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(ConnectOutcome.TokenMismatch, body.GetProperty("code").GetString());
        Assert.Null(SessionCookie(response, host));
    }

    /// <summary>Ten wrong tokens from one address lock it out: the next handshake is 429 <c>AUTH_RATE_LIMITED</c> with <c>retryAfterMs</c>.</summary>
    [Fact]
    public async Task Connect_TenWrongTokens_Returns429WithRetryAfter()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayHost host = new();
        GatewayTestClient lan = host.Lan();
        for (int attempt = 0; attempt < 10; attempt++)
        {
            using HttpResponseMessage wrong = await lan.ConnectAsync(new ConnectAuth(Token: "wrong"), ct);
            Assert.Equal(attempt < 9 ? HttpStatusCode.Unauthorized : HttpStatusCode.TooManyRequests, wrong.StatusCode);
        }

        using HttpResponseMessage locked = await lan.ConnectAsync(new ConnectAuth(Token: Token), ct);
        JsonElement body = await BodyAsync(locked, ct);

        Assert.Equal(HttpStatusCode.TooManyRequests, locked.StatusCode);
        Assert.Equal(AuthRateLimiter.ErrorCode, body.GetProperty("code").GetString());
        Assert.True(body.GetProperty("retryAfterMs").GetInt64() > 0);
    }

    /// <summary>Password mode connects with the password and refuses the gateway token.</summary>
    [Fact]
    public async Task Connect_PasswordMode_AcceptsThePassword()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayHost host = new(mode: "password", password: "correct horse battery staple");

        using HttpResponseMessage good = await host.Local().ConnectAsync(new ConnectAuth(Password: "correct horse battery staple"), ct);
        using HttpResponseMessage wrong = await host.Local().ConnectAsync(new ConnectAuth(Token: Token), ct);

        Assert.Equal(HttpStatusCode.OK, good.StatusCode);
        Assert.NotNull(SessionCookie(good, host));
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
    }

    /// <summary>Mode none lets loopback in without a credential and refuses every other address.</summary>
    [Fact]
    public async Task Connect_ModeNone_IsLoopbackOnly()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayHost host = new(mode: "none");

        using HttpResponseMessage local = await host.Local().ConnectAsync(new ConnectAuth(), ct);
        using HttpResponseMessage lan = await host.Lan().ConnectAsync(new ConnectAuth(), ct);
        JsonElement body = await BodyAsync(lan, ct);

        Assert.Equal(HttpStatusCode.OK, local.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, lan.StatusCode);
        Assert.Equal(ConnectOutcome.AuthRequired, body.GetProperty("code").GetString());
    }

    /// <summary>
    /// A LAN device is <c>PAIRING_REQUIRED</c> with a request id; the request is pending; approval alone grants no session (the status
    /// endpoint sets no cookie); the next handshake gets the session cookie and the device token; the one after that gets no token.
    /// </summary>
    [Fact]
    public async Task Pairing_RequiredThenApprovedThenHandshake_GrantsASession()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayHost host = new();
        GatewayTestClient lan = host.Lan();

        using HttpResponseMessage required = await lan.ConnectAsync(new ConnectAuth(Token: Token), ct);
        JsonElement pending = await BodyAsync(required, ct);
        string requestId = StringProperty(pending, "requestId");
        using HttpResponseMessage status = await host.Client.GetAsync(new Uri($"/auth/pairing/{requestId}", UriKind.Relative), ct);
        JsonElement statusBody = await BodyAsync(status, ct);
        _ = host.Pairing.Approve(requestId, [GatewayScopes.Admin]);
        using HttpResponseMessage approvedStatus = await host.Client.GetAsync(new Uri($"/auth/pairing/{requestId}", UriKind.Relative), ct);
        JsonElement approvedBody = await BodyAsync(approvedStatus, ct);
        using HttpResponseMessage connected = await lan.ConnectAsync(new ConnectAuth(Token: Token), ct);
        JsonElement connectedBody = await BodyAsync(connected, ct);
        using HttpResponseMessage later = await lan.ConnectAsync(new ConnectAuth(Token: Token), ct);
        JsonElement laterBody = await BodyAsync(later, ct);

        Assert.Equal(HttpStatusCode.OK, required.StatusCode);
        Assert.Equal(ConnectOutcome.PairingRequired, pending.GetProperty("code").GetString());
        Assert.Equal(lan.Device.DeviceId, pending.GetProperty("deviceId").GetString());
        Assert.Null(SessionCookie(required, host));
        Assert.Equal("pending", statusBody.GetProperty("status").GetString());
        Assert.Equal("approved", approvedBody.GetProperty("status").GetString());
        Assert.False(approvedStatus.Headers.Contains("Set-Cookie"));
        Assert.Equal(HttpStatusCode.OK, connected.StatusCode);
        Assert.NotNull(SessionCookie(connected, host));
        Assert.False(string.IsNullOrEmpty(connectedBody.GetProperty("deviceToken").GetString()));
        Assert.NotNull(SessionCookie(later, host));
        Assert.False(laterBody.TryGetProperty("deviceToken", out JsonElement again) && again.ValueKind != JsonValueKind.Null);
    }

    /// <summary>Rejected and unknown request ids read as <c>rejected</c> and <c>expired</c>.</summary>
    [Fact]
    public async Task PairingStatus_RejectedAndUnknown_AreReported()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayHost host = new();
        GatewayTestClient lan = host.Lan();
        using HttpResponseMessage required = await lan.ConnectAsync(new ConnectAuth(Token: Token), ct);
        string requestId = StringProperty(await BodyAsync(required, ct), "requestId");
        _ = host.Pairing.Reject(requestId, [GatewayScopes.Admin]);

        using HttpResponseMessage rejected = await host.Client.GetAsync(new Uri($"/auth/pairing/{requestId}", UriKind.Relative), ct);
        using HttpResponseMessage unknown = await host.Client.GetAsync(new Uri("/auth/pairing/no-such-request", UriKind.Relative), ct);

        Assert.Equal("rejected", (await BodyAsync(rejected, ct)).GetProperty("status").GetString());
        Assert.Equal("expired", (await BodyAsync(unknown, ct)).GetProperty("status").GetString());
    }

    /// <summary>A paired device connects with its device token alone, and is issued no new one.</summary>
    [Fact]
    public async Task Connect_DeviceToken_ConnectsWithoutANewToken()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayHost host = new();
        TestDevice device = new();
        using HttpResponseMessage first = await host.Local(device).ConnectAsync(new ConnectAuth(Token: Token), ct);
        string deviceToken = StringProperty(await BodyAsync(first, ct), "deviceToken");

        using HttpResponseMessage second = await host.Lan(device).ConnectAsync(new ConnectAuth(DeviceToken: deviceToken), ct);
        JsonElement body = await BodyAsync(second, ct);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.NotNull(SessionCookie(second, host));
        Assert.False(body.TryGetProperty("deviceToken", out JsonElement again) && again.ValueKind != JsonValueKind.Null);
    }

    /// <summary>A bootstrap token connects an admin from the LAN, and a burned one (C-9) is refused with 401 and 204 on burn.</summary>
    [Fact]
    public async Task BootstrapBurn_MakesTheTokenUnusable()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayHost host = new();
        string usable = host.Bootstrap.Issue(BootstrapTokenStore.Kind.Admin);
        string burned = host.Bootstrap.Issue(BootstrapTokenStore.Kind.Admin);

        using HttpResponseMessage burn = await host.Client.PostAsJsonAsync(new Uri("/auth/bootstrap/burn", UriKind.Relative), new { bootstrapToken = burned }, Json, ct);
        using HttpResponseMessage refused = await host.Lan().ConnectAsync(new ConnectAuth(BootstrapToken: burned), ct);
        using HttpResponseMessage accepted = await host.Lan().ConnectAsync(new ConnectAuth(BootstrapToken: usable), ct);

        Assert.Equal(HttpStatusCode.NoContent, burn.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
        Assert.Equal(ConnectOutcome.BootstrapTokenInvalid, (await BodyAsync(refused, ct)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }

    /// <summary>A forged signature is 401 <c>DEVICE_AUTH_SIGNATURE_INVALID</c>; a replayed nonce is 401 <c>DEVICE_AUTH_NONCE_MISMATCH</c>.</summary>
    [Fact]
    public async Task Connect_BadSignatureAndReplayedNonce_AreRefusedWithDeviceAuthCodes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayHost host = new();
        GatewayTestClient client = host.Local();
        ChallengeNonceStore.Challenge first = await client.GetChallengeAsync(ct);
        ChallengeNonceStore.Challenge second = await client.GetChallengeAsync(ct);
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        ConnectRequest forgedBody = client.Device.Build(first.Nonce, now, [GatewayScopes.Read], new ConnectAuth(Token: Token)) with { Signature = "c2hvcnQ" };
        ConnectRequest good = client.Device.Build(second.Nonce, now, [GatewayScopes.Read], new ConnectAuth(Token: Token));

        using HttpResponseMessage forged = await client.PostAsync(forgedBody, ct);
        using HttpResponseMessage accepted = await client.PostAsync(good, ct);
        using HttpResponseMessage replayed = await client.PostAsync(good, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, forged.StatusCode);
        Assert.Equal(DeviceAuth.SignatureInvalid, (await BodyAsync(forged, ct)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, replayed.StatusCode);
        Assert.Equal(DeviceAuth.NonceMismatch, (await BodyAsync(replayed, ct)).GetProperty("code").GetString());
    }

    /// <summary>A body that is not JSON, or lacks the device fields, is a 400 and never a 500.</summary>
    [Fact]
    public async Task Connect_MalformedBody_Returns400()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayHost host = new();
        using StringContent junk = new("not json", System.Text.Encoding.UTF8, "application/json");
        using StringContent empty = new("{}", System.Text.Encoding.UTF8, "application/json");

        using HttpResponseMessage first = await host.Client.PostAsync(new Uri("/auth/connect", UriKind.Relative), junk, ct);
        using HttpResponseMessage second = await host.Client.PostAsync(new Uri("/auth/connect", UriKind.Relative), empty, ct);

        Assert.Equal(HttpStatusCode.BadRequest, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    /// <summary>C-7: <c>/auth/status</c> is unauthenticated, says false without a session cookie and true with the one the handshake set.</summary>
    [Fact]
    public async Task AuthStatus_ReflectsTheSessionCookie()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayHost host = new();
        using HttpResponseMessage connect = await host.Local().ConnectAsync(new ConnectAuth(Token: Token), ct);
        string? setCookie = SessionCookie(connect, host);
        Assert.NotNull(setCookie);
        string cookie = setCookie.Split(';')[0];

        using HttpResponseMessage anonymous = await host.Client.GetAsync(new Uri("/auth/status", UriKind.Relative), ct);
        using HttpRequestMessage withSession = new(HttpMethod.Get, new Uri("/auth/status", UriKind.Relative));
        withSession.Headers.Add("Cookie", cookie);
        using HttpResponseMessage authed = await host.Client.SendAsync(withSession, ct);

        Assert.Equal(HttpStatusCode.OK, anonymous.StatusCode);
        Assert.False((await BodyAsync(anonymous, ct)).GetProperty("authenticated").GetBoolean());
        Assert.True((await BodyAsync(authed, ct)).GetProperty("authenticated").GetBoolean());
    }
}

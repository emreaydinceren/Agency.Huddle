using System.Net;
using Agency.Huddle.App.Desktop;
using Agency.Huddle.App.Gateway;
using Microsoft.Extensions.DependencyInjection;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>
/// Spec 6.8, 6.15, 8.7 (rules 2, 5, 6 only; the full origin table is Stage D), C-1, C-2, C-3: the gate on the real pipeline. The host runs with
/// <c>ConnectedAsAdmin = false</c>, so the real session resolver decides, and the gateway token mode, so a handshake yields a real session.
/// </summary>
public sealed class GatewayGateTests
{
    /// <summary>
    /// P-2 (C-2): with <c>Auth:Mode = none</c> and the real resolver, a request with no session is redirected to <c>/auth</c>. No configuration
    /// key reaches the test seam, so production never skips the gate.
    /// </summary>
    [Fact]
    public async Task Get_NoneModeRealResolverNoSession_Redirects302ToAuth()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayGateHost host = new(mode: "none");

        using HttpResponseMessage response = await host.Client.SendAsync(GatewayGateHost.Request(HttpMethod.Get, "/", html: true), ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/auth?return=%2F", response.Headers.Location?.OriginalString);
    }

    /// <summary>An HTML navigation without a session is sent to <c>/auth</c> with its own path as <c>return</c>.</summary>
    /// <param name="path">The requested path.</param>
    /// <param name="expectedLocation">The redirect target.</param>
    [Theory]
    [InlineData("/", "/auth?return=%2F")]
    [InlineData("/settings", "/auth?return=%2Fsettings")]
    public async Task Get_HtmlWithoutSession_Redirects302ToAuthWithReturn(string path, string expectedLocation)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayGateHost host = new();

        using HttpResponseMessage response = await host.Client.SendAsync(GatewayGateHost.Request(HttpMethod.Get, path, html: true), ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(expectedLocation, response.Headers.Location?.OriginalString);
    }

    /// <summary>Anything that is not an HTML navigation (assets, avatars, Library files, the circuit negotiate) is answered 401 without a session.</summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The requested path.</param>
    [Theory]
    [InlineData("GET", "/_framework/blazor.web.js")]
    [InlineData("GET", "/agent-avatars/x.png")]
    [InlineData("GET", "/library-files/root/notes.md")]
    [InlineData("POST", "/_blazor/negotiate?negotiateVersion=1")]
    public async Task Request_NotHtmlWithoutSession_Returns401(string method, string path)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayGateHost host = new();

        using HttpResponseMessage response = await host.Client.SendAsync(GatewayGateHost.Request(new HttpMethod(method), path), ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>The connect page, its endpoints, its scripts and the vendored noble files need no session.</summary>
    /// <param name="path">The requested path.</param>
    [Theory]
    [InlineData("/auth")]
    [InlineData("/auth/challenge")]
    [InlineData("/auth.js")]
    [InlineData("/auth-core.js")]
    [InlineData("/auth.css")]
    [InlineData("/lib/noble/ed25519/index.js")]
    public async Task PublicRoutes_WithoutSession_Return200(string path)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayGateHost host = new();

        using HttpResponseMessage response = await host.Client.SendAsync(GatewayGateHost.Request(HttpMethod.Get, path), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary><c>/health</c> is liveness for <c>test-health.ps1</c>: unauthenticated, from loopback.</summary>
    [Fact]
    public async Task Health_OnLoopbackWithoutSession_Returns200()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayGateHost host = new();

        using HttpResponseMessage response = await host.Client.SendAsync(GatewayGateHost.Request(HttpMethod.Get, "/health"), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary><c>/health</c> is loopback-only: a LAN client is refused with 403, even with a session.</summary>
    [Fact]
    public async Task Health_FromLan_Returns403()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayGateHost host = new();
        string cookie = await host.ConnectAsync(new TestDevice(), ct);

        using HttpResponseMessage response = await host.Client.SendAsync(GatewayGateHost.Request(HttpMethod.Get, "/health", cookie, GatewayGateHost.LanAddress), ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>C-1: <c>/_desktop/stop</c> with the instance token and no session is accepted from loopback; the endpoint keeps its own token check.</summary>
    [Fact]
    public async Task DesktopStop_LoopbackNoSessionWithInstanceToken_Returns202()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayGateHost host = new();
        string token = host.Services.GetRequiredService<DesktopSession>().StopToken;
        using HttpRequestMessage request = GatewayGateHost.Request(HttpMethod.Post, DesktopStopEndpoint.Route);
        request.Headers.Add(DesktopStopEndpoint.TokenHeader, token);

        using HttpResponseMessage response = await host.Client.SendAsync(request, ct);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(1, host.Shutdown.StopCalls);
    }

    /// <summary>C-1: the same request, with the right token, from a LAN client is refused with 403 and does not stop the app.</summary>
    [Fact]
    public async Task DesktopStop_FromLanWithInstanceToken_Returns403AndDoesNotStop()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayGateHost host = new();
        string token = host.Services.GetRequiredService<DesktopSession>().StopToken;
        using HttpRequestMessage request = GatewayGateHost.Request(HttpMethod.Post, DesktopStopEndpoint.Route, ip: GatewayGateHost.LanAddress);
        request.Headers.Add(DesktopStopEndpoint.TokenHeader, token);

        using HttpResponseMessage response = await host.Client.SendAsync(request, ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, host.Shutdown.StopCalls);
    }

    /// <summary>C-1: a request that arrives with a forwarding header is not Loopback to the classifier, so <c>/_desktop/stop</c> refuses it with 403 even with the token.</summary>
    [Fact]
    public async Task DesktopStop_LoopbackWithForwardedFor_Returns403()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayGateHost host = new();
        string token = host.Services.GetRequiredService<DesktopSession>().StopToken;
        using HttpRequestMessage request = GatewayGateHost.Request(HttpMethod.Post, DesktopStopEndpoint.Route);
        request.Headers.Add(DesktopStopEndpoint.TokenHeader, token);
        request.Headers.Add("X-Forwarded-For", "203.0.113.9");

        using HttpResponseMessage response = await host.Client.SendAsync(request, ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, host.Shutdown.StopCalls);
    }

    /// <summary>A session from <c>POST /auth/connect</c> admits <c>/</c>.</summary>
    [Fact]
    public async Task Get_WithSessionFromConnect_Returns200()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayGateHost host = new();
        string cookie = await host.ConnectAsync(new TestDevice(), ct);

        using HttpResponseMessage response = await host.Client.SendAsync(GatewayGateHost.Request(HttpMethod.Get, "/", cookie, html: true), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>A cookie that names no session is the same as no cookie.</summary>
    [Fact]
    public async Task Get_WithUnknownSessionCookie_Redirects302ToAuth()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayGateHost host = new();
        string cookie = $"huddle_session_{host.Settings.InstanceId}=not-a-session";

        using HttpResponseMessage response = await host.Client.SendAsync(GatewayGateHost.Request(HttpMethod.Get, "/", cookie, html: true), ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    /// <summary>§8.7 rule 7: a foreign <c>Origin</c> is refused with 403, even on a route that needs no session and even with a session.</summary>
    /// <param name="origin">The Origin header value.</param>
    [Theory]
    [InlineData("http://evil.example")]
    [InlineData("null")]
    public async Task Get_ForeignOrigin_Returns403(string origin)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayGateHost host = new();
        string cookie = await host.ConnectAsync(new TestDevice(), ct);
        using HttpRequestMessage withSession = GatewayGateHost.Request(HttpMethod.Get, "/", cookie, html: true);
        withSession.Headers.Add("Origin", origin);
        using HttpRequestMessage publicRoute = GatewayGateHost.Request(HttpMethod.Get, "/auth/challenge");
        publicRoute.Headers.Add("Origin", origin);

        using HttpResponseMessage first = await host.Client.SendAsync(withSession, ct);
        using HttpResponseMessage second = await host.Client.SendAsync(publicRoute, ct);

        Assert.Equal(HttpStatusCode.Forbidden, first.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, second.StatusCode);
    }

    /// <summary>§8.7 rule 6: a loopback socket with a loopback <c>Origin</c> passes.</summary>
    [Fact]
    public async Task Get_LoopbackOriginOnLoopback_Returns200()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayGateHost host = new();
        string cookie = await host.ConnectAsync(new TestDevice(), ct);
        using HttpRequestMessage request = GatewayGateHost.Request(HttpMethod.Get, "/", cookie, html: true);
        request.Headers.Add("Origin", "http://localhost");

        using HttpResponseMessage response = await host.Client.SendAsync(request, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>§8.7 rule 5: a LAN origin that names a public host is not the same-origin default and is refused.</summary>
    [Fact]
    public async Task Get_PublicHostOriginFromLan_Returns403()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayGateHost host = new();
        string cookie = await host.ConnectAsync(new TestDevice(), ct);
        using HttpRequestMessage request = GatewayGateHost.Request(HttpMethod.Get, "/", cookie, GatewayGateHost.LanAddress, html: true);
        request.Headers.Add("Origin", "http://localhost.evil.example");

        using HttpResponseMessage response = await host.Client.SendAsync(request, ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>The classifier runs inside the gate: a loopback request with <c>X-Forwarded-For</c> is refused with 403 <c>proxy_attribution_required</c>, not redirected.</summary>
    [Fact]
    public async Task Get_LoopbackWithForwardedFor_Returns403ProxyAttributionRequired()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayGateHost host = new();
        using HttpRequestMessage request = GatewayGateHost.Request(HttpMethod.Get, "/", html: true);
        request.Headers.Add("X-Forwarded-For", "203.0.113.9");

        using HttpResponseMessage response = await host.Client.SendAsync(request, ct);
        string body = await response.Content.ReadAsStringAsync(ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using System.Text.Json.JsonDocument json = System.Text.Json.JsonDocument.Parse(body);
        Assert.Equal(IngressClassifier.ProxyAttributionRequired, json.RootElement.GetProperty("code").GetString());
    }

    /// <summary>
    /// C-3: the authentication handler reads the classifier's ingress. A session issued over the managed listener and bound to a Tailscale login
    /// is refused when it arrives on another ingress (here loopback, which carries no verified login), while a loopback-issued session is admitted.
    /// </summary>
    [Fact]
    public async Task Get_ManagedBoundSessionOnLoopbackIngress_Redirects302ToAuth()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayGateHost host = new();
        TestDevice device = new();
        string loopbackCookie = await host.ConnectAsync(device, ct);
        string managedCookie = $"huddle_session_{host.Settings.InstanceId}=" + host.Services.GetRequiredService<SessionRegistry>()
            .Issue(device.DeviceId, [GatewayScopes.Read, GatewayScopes.Write], IngressKindText.Of(IngressKind.Managed), "ada@example.com");

        using HttpResponseMessage refused = await host.Client.SendAsync(GatewayGateHost.Request(HttpMethod.Get, "/", managedCookie, html: true), ct);
        using HttpResponseMessage admitted = await host.Client.SendAsync(GatewayGateHost.Request(HttpMethod.Get, "/", loopbackCookie, html: true), ct);

        Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
        Assert.Equal(HttpStatusCode.OK, admitted.StatusCode);
    }

    /// <summary>Revoking the device ends its session for HTTP too: the next request is redirected to <c>/auth</c>.</summary>
    [Fact]
    public async Task Get_AfterDeviceRevoked_Redirects302ToAuth()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayGateHost host = new();
        TestDevice device = new();
        string cookie = await host.ConnectAsync(device, ct);

        _ = host.Services.GetRequiredService<DeviceRegistry>().Revoke(device.DeviceId);
        using HttpResponseMessage response = await host.Client.SendAsync(GatewayGateHost.Request(HttpMethod.Get, "/", cookie, html: true), ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
}

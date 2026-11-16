using System.Net;
using System.Text.Json;
using Agency.Huddle.App.Gateway;
using Microsoft.Extensions.DependencyInjection;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>
/// Classify once, by the listener the connection arrived on (D-2), and the effective scheme on managed ingress (D-8), through the real pipeline
/// with a test <c>GatewayListener</c> and a startup filter that makes a request arrive on the managed endpoint.
/// </summary>
public sealed class GatewayManagedIngressTests
{
    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response, CancellationToken ct)
    {
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        return body.TryGetProperty("code", out JsonElement code) ? code.GetString() : null;
    }

    /// <summary>A managed-listener request without the forwarded address is refused 403 <c>proxy_attribution_required</c>; the same request on the app listener is served.</summary>
    [Fact]
    public async Task Managed_WithoutForwardedFor_IsRefusedAndAppListenerIsNot()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ManagedIngressHost host = new();
        using HttpClient managed = host.CreateClient(managed: true, forwarded: false);
        using HttpClient app = host.CreateClient(managed: false, forwarded: false);

        using HttpResponseMessage refused = await managed.GetAsync(new Uri("/auth/challenge", UriKind.Relative), ct);
        using HttpResponseMessage served = await app.GetAsync(new Uri("/auth/challenge", UriKind.Relative), ct);

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal(IngressClassifier.ProxyAttributionRequired, await ErrorCodeAsync(refused, ct));
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
    }

    /// <summary>A managed request with full headers and a matching <c>whois</c> reaches the connect endpoint as a Tailscale identity: the handshake is neither refused 401 nor 403.</summary>
    [Fact]
    public async Task Managed_FullHeadersAndWhois_ConnectEndpointSeesTheIdentity()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ManagedIngressHost host = new();
        host.Whois.With(IPAddress.Parse("100.64.0.7"), "ada@example.com");
        using HttpClient client = host.CreateClient(managed: true, forwarded: true);
        GatewayTestClient connect = new(client, new TestDevice());

        using HttpResponseMessage response = await connect.ConnectAsync(new ConnectAuth(), ct);

        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(IPAddress.Parse("100.64.0.7"), host.Whois.Lookups);
    }

    /// <summary>Over the managed listener the session cookie is <c>Secure</c> with <c>X-Forwarded-Proto: https</c> and not with <c>http</c> (D-8).</summary>
    [Fact]
    public async Task Managed_SessionCookie_FollowsTheForwardedScheme()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ManagedIngressHost host = new();
        BootstrapTokenStore bootstrap = host.Services.GetRequiredService<BootstrapTokenStore>();
        using HttpClient https = host.CreateClient(managed: true, forwarded: true, proto: "https");
        using HttpClient http = host.CreateClient(managed: true, forwarded: true, proto: "http");

        using HttpResponseMessage secure = await new GatewayTestClient(https, new TestDevice())
            .ConnectAsync(new ConnectAuth(BootstrapToken: bootstrap.Issue(BootstrapTokenStore.Kind.Admin)), ct);
        using HttpResponseMessage plain = await new GatewayTestClient(http, new TestDevice())
            .ConnectAsync(new ConnectAuth(BootstrapToken: bootstrap.Issue(BootstrapTokenStore.Kind.Admin)), ct);

        string secureCookie = Assert.Single(secure.Headers.GetValues("Set-Cookie"), cookie => cookie.StartsWith("huddle_session_", StringComparison.Ordinal));
        string plainCookie = Assert.Single(plain.Headers.GetValues("Set-Cookie"), cookie => cookie.StartsWith("huddle_session_", StringComparison.Ordinal));
        Assert.Contains("secure", secureCookie, StringComparison.OrdinalIgnoreCase); // contains-ok: Set-Cookie attribute order is not part of the contract
        Assert.DoesNotContain("secure", plainCookie, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A managed browser navigation without a session is redirected to the relative <c>/auth?return=</c>, never to an absolute http URL (D-8).</summary>
    [Fact]
    public async Task Managed_NavigationWithoutSession_RedirectsToTheRelativeAuthPage()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ManagedIngressHost host = new();
        using HttpClient client = host.CreateClient(managed: true, forwarded: true, proto: "https");
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri("/", UriKind.Relative));
        request.Headers.Add("Accept", "text/html,application/xhtml+xml");

        using HttpResponseMessage response = await client.SendAsync(request, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        string? location = response.Headers.Location?.OriginalString;
        Assert.NotNull(location);
        Assert.Equal("/auth?return=%2F", location);
    }
}

using System.Net;
using System.Text.Json;
using Agency.Huddle.Tests.Ui;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>
/// Strict local mode on the real pipeline (ra-strict-local.md): the gate admits only the loopback ingress and gives it the host principal
/// with no session; every other ingress is refused with <c>strict_local</c>; <c>/auth</c> sends the host to the app.
/// </summary>
public sealed class StrictLocalGateTests : IDisposable
{
    private readonly TeamWebApplicationFactory root = new() { ConnectedAsAdmin = false };
    private readonly List<WebApplicationFactory<Program>> apps = [];

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (WebApplicationFactory<Program> app in this.apps)
        {
            app.Dispose();
        }

        this.root.Dispose();
    }

    /// <summary>Builds the real app with strict local at its default (on) and simulated remote addresses.</summary>
    private HttpClient Client(params (string Key, string Value)[] settings)
    {
        WebApplicationFactory<Program> app = this.root.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Team:Gateway:Auth:Token", "0123456789abcdef0123456789abcdef0123456789abcdef");
            foreach ((string key, string value) in settings)
            {
                builder.UseSetting(key, value);
            }

            builder.ConfigureTestServices(services => services.AddSingleton<IStartupFilter, TestRemoteAddressStartupFilter>());
        });
        this.apps.Add(app);
        return app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
    }

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response, CancellationToken ct)
    {
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return body.RootElement.GetProperty("code").GetString();
    }

    private static HttpRequestMessage Get(string path, string? ip = null, bool html = false)
        => GatewayGateHost.Request(HttpMethod.Get, path, null, ip, html);

    /// <summary>A loopback caller is told it is authenticated, so the reconnect modal never bounces to <c>/auth</c>.</summary>
    [Fact]
    public async Task Loopback_Status_ReportsAuthenticated()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient client = this.Client();

        using HttpResponseMessage response = await client.SendAsync(Get("/auth/status"), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        Assert.True(body.RootElement.GetProperty("authenticated").GetBoolean());
    }

    /// <summary>A loopback page navigation is served with no session and no sign-in redirect.</summary>
    [Fact]
    public async Task Loopback_PageNavigation_NeedsNoSession()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient client = this.Client();

        using HttpResponseMessage response = await client.SendAsync(Get("/", html: true), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>The sign-in page redirects the host to the app root.</summary>
    [Fact]
    public async Task Loopback_AuthPage_RedirectsToRoot()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient client = this.Client();

        using HttpResponseMessage response = await client.SendAsync(Get("/auth", html: true), ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
    }

    /// <summary>The sign-in page redirects the host to the safe <c>return</c> path.</summary>
    [Fact]
    public async Task Loopback_AuthPageWithReturn_RedirectsToThePath()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient client = this.Client();

        using HttpResponseMessage response = await client.SendAsync(Get("/auth?return=%2Fchats", html: true), ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/chats", response.Headers.Location?.OriginalString);
    }

    /// <summary>A LAN client is refused everywhere, the connect routes included, with code <c>strict_local</c>.</summary>
    /// <param name="path">The path.</param>
    /// <param name="post">Whether to POST.</param>
    [Theory]
    [InlineData("/", false)]
    [InlineData("/auth", false)]
    [InlineData("/auth/status", false)]
    [InlineData("/auth/challenge", false)]
    [InlineData("/auth/connect", true)]
    public async Task Lan_Request_IsRefusedAsStrictLocal(string path, bool post)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient client = this.Client();
        using HttpRequestMessage request = GatewayGateHost.Request(post ? HttpMethod.Post : HttpMethod.Get, path, null, GatewayGateHost.LanAddress, html: false);

        using HttpResponseMessage response = await client.SendAsync(request, ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("strict_local", await CodeOfAsync(response, ct));
    }

    /// <summary>A configured trusted proxy is ignored: a request from its address is just another non-loopback client.</summary>
    [Fact]
    public async Task TrustedProxyConfigured_ItsRequestsAreRefusedAsStrictLocal()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient client = this.Client(("Team:Gateway:TrustedProxies:0", GatewayGateHost.LanAddress));

        using HttpResponseMessage response = await client.SendAsync(Get("/auth/status", GatewayGateHost.LanAddress), ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("strict_local", await CodeOfAsync(response, ct));
    }

    /// <summary>A request that arrives on the managed (Tailscale) listener is refused as strict local.</summary>
    [Fact]
    public async Task Managed_Request_IsRefusedAsStrictLocal()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ManagedIngressHost host = new(new Dictionary<string, string>(StringComparer.Ordinal) { ["Team:Gateway:StrictLocal"] = "true" });
        using HttpClient client = host.CreateClient(managed: true, forwarded: true);

        using HttpResponseMessage response = await client.GetAsync(new Uri("/auth/status", UriKind.Relative), ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("strict_local", await CodeOfAsync(response, ct));
    }

    /// <summary>Forwarding headers on a loopback socket are still refused as unattributed proxy traffic.</summary>
    [Fact]
    public async Task Loopback_WithForwardingHeaders_StillRefusedAsProxyAttributionRequired()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient client = this.Client();
        using HttpRequestMessage request = Get("/auth/status");
        request.Headers.Add("X-Forwarded-For", "198.51.100.4");

        using HttpResponseMessage response = await client.SendAsync(request, ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("proxy_attribution_required", await CodeOfAsync(response, ct));
    }
}

using System.Net;
using Agency.Huddle.App.Desktop;
using Agency.Huddle.App.Gateway;
using Agency.Huddle.Tests.Desktop;
using Agency.Huddle.Tests.Ui;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>
/// The real application with <c>ConnectedAsAdmin = false</c> (the real session resolver), gateway token mode, the gateway token fixed,
/// simulated remote addresses, a fake <c>whois</c> and desktop mode on, plus helpers to connect a device and send a request with a cookie.
/// </summary>
internal sealed class GatewayGateHost : IDisposable
{
    /// <summary>The configured gateway token.</summary>
    public const string Token = "0123456789abcdef0123456789abcdef0123456789abcdef";

    /// <summary>A LAN address (the simulated remote address of a non-loopback client).</summary>
    public const string LanAddress = "203.0.113.9";

    private readonly TeamWebApplicationFactory root = new() { ConnectedAsAdmin = false };
    private readonly WebApplicationFactory<Program> app;

    /// <summary>Builds the host. <paramref name="configure"/> may replace services (for example a fake clock for the sessions).</summary>
    /// <param name="mode">The <c>Team:Gateway:Auth:Mode</c> value.</param>
    /// <param name="configure">Extra service replacements.</param>
    public GatewayGateHost(string mode = "token", Action<IServiceCollection>? configure = null)
    {
        this.app = this.root.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Team:Gateway:Auth:Token", Token);
            builder.UseSetting("Team:Gateway:Auth:Mode", mode);
            builder.UseSetting("Team:Desktop:Enabled", "true");
            builder.UseSetting("Team:Desktop:OpenBrowser", "false");
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IStartupFilter, TestRemoteAddressStartupFilter>();
                services.AddSingleton<IIngressWhois>(this.Whois);
                services.AddSingleton<IDesktopShutdown>(this.Shutdown);
                configure?.Invoke(services);
            });
        });
        this.Client = this.app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
    }

    /// <summary>The fake <c>whois</c> the classifier asks.</summary>
    public FakeIngressWhois Whois { get; } = new();

    /// <summary>The shutdown seam <c>/_desktop/stop</c> calls.</summary>
    public FakeDesktopShutdown Shutdown { get; } = new();

    /// <summary>A client that follows no redirect and keeps no cookie.</summary>
    public HttpClient Client { get; }

    /// <summary>The composed application.</summary>
    public IServiceProvider Services => this.app.Services;

    /// <summary>The factory, for <c>Server.CreateWebSocketClient()</c>.</summary>
    public WebApplicationFactory<Program> Factory => this.app;

    /// <summary>The gateway settings (the Instance id names the cookie).</summary>
    public GatewaySettings Settings => this.app.Services.GetRequiredService<GatewaySettings>();

    /// <summary>Connects <paramref name="device"/> over loopback with the gateway token and returns the <c>Cookie</c> header value the session cookie makes.</summary>
    /// <param name="device">The device.</param>
    /// <param name="ct">Cancellation.</param>
    public async Task<string> ConnectAsync(TestDevice device, CancellationToken ct)
    {
        GatewayTestClient client = new(this.Client, device);
        using HttpResponseMessage response = await client.ConnectAsync(new ConnectAuth(Token: Token), ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string? set = response.Headers.GetValues("Set-Cookie").FirstOrDefault(cookie => cookie.StartsWith("huddle_session_", StringComparison.Ordinal));
        Assert.NotNull(set);
        return set[..set.IndexOf(';', StringComparison.Ordinal)];
    }

    /// <summary>Builds a request. <paramref name="html"/> sends <c>Accept: text/html</c>, as a browser navigation does.</summary>
    /// <param name="method">The method.</param>
    /// <param name="path">The path and query.</param>
    /// <param name="cookie">The <c>Cookie</c> header value, or <see langword="null"/>.</param>
    /// <param name="ip">The simulated remote address, or <see langword="null"/> for loopback.</param>
    /// <param name="html">Whether the request is an HTML navigation.</param>
    public static HttpRequestMessage Request(HttpMethod method, string path, string? cookie = null, string? ip = null, bool html = false)
    {
        HttpRequestMessage request = new(method, new Uri(path, UriKind.Relative));
        if (method != HttpMethod.Get)
        {
            request.Headers.Add("Origin", "http://localhost");
        }

        if (cookie is not null)
        {
            request.Headers.Add("Cookie", cookie);
        }

        if (ip is not null)
        {
            request.Headers.Add(TestRemoteAddressStartupFilter.HeaderName, ip);
        }

        if (html)
        {
            request.Headers.Add("Accept", "text/html,application/xhtml+xml");
        }

        return request;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.Client.Dispose();
        this.app.Dispose();
        this.root.Dispose();
    }
}

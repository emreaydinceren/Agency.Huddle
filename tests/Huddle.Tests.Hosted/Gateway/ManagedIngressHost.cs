using System.Net;
using Agency.Huddle.App.Gateway;
using Agency.Huddle.Tests.Ui;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>
/// The real application on a <c>TestServer</c> with token auth, a fake <c>whois</c>, and a managed endpoint registered in a test <c>GatewayListener</c>:
/// a request with the <see cref="TestManagedListenerStartupFilter.HeaderName"/> header arrives on it (D-2).
/// </summary>
internal sealed class ManagedIngressHost : IDisposable
{
    /// <summary>The managed endpoint the test <c>GatewayListener</c> records.</summary>
    public static readonly IPEndPoint ManagedEndpoint = new(IPAddress.Loopback, 45678);

    private readonly TeamWebApplicationFactory root = new() { ConnectedAsAdmin = false };
    private readonly WebApplicationFactory<Program> app;

    /// <summary>Builds the host. <paramref name="settings"/> overrides configuration.</summary>
    /// <param name="settings">Configuration overrides.</param>
    public ManagedIngressHost(IReadOnlyDictionary<string, string>? settings = null)
    {
        GatewayListener listener = new();
        listener.RecordManaged(ManagedEndpoint);
        this.app = this.root.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Team:Gateway:Auth:Token", "0123456789abcdef0123456789abcdef0123456789abcdef");
            builder.UseSetting("Team:Gateway:Auth:Mode", "token");
            builder.UseSetting("AllowedHosts", "*");
            foreach ((string key, string value) in settings ?? new Dictionary<string, string>())
            {
                builder.UseSetting(key, value);
            }

            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(listener);
                services.AddSingleton<IStartupFilter>(new TestManagedListenerStartupFilter(ManagedEndpoint));
                services.AddSingleton<IIngressWhois>(this.Whois);
            });
        });
    }

    /// <summary>The fake <c>whois</c> the classifier asks.</summary>
    public FakeIngressWhois Whois { get; } = new();

    /// <summary>The composed application's services.</summary>
    public IServiceProvider Services => this.app.Services;

    /// <summary>A client that follows no redirect and keeps no cookie, with the headers a Serve request carries when <paramref name="managed"/> is set.</summary>
    /// <param name="managed">Whether requests arrive on the managed listener.</param>
    /// <param name="forwarded">Whether they carry full attribution (<c>X-Forwarded-For/Proto/Host</c> and the Tailscale login).</param>
    /// <param name="proto">The <c>X-Forwarded-Proto</c> value.</param>
    public HttpClient CreateClient(bool managed, bool forwarded, string proto = "https")
    {
        HttpClient client = this.app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        if (managed)
        {
            client.DefaultRequestHeaders.Add(TestManagedListenerStartupFilter.HeaderName, "1");
        }

        if (forwarded)
        {
            client.DefaultRequestHeaders.Add("X-Forwarded-For", "100.64.0.7");
            client.DefaultRequestHeaders.Add("X-Forwarded-Proto", proto);
            client.DefaultRequestHeaders.Add("X-Forwarded-Host", "desk.tail1.ts.net");
            client.DefaultRequestHeaders.Add("Tailscale-User-Login", "ada@example.com");
        }

        return client;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.app.Dispose();
        this.root.Dispose();
    }
}

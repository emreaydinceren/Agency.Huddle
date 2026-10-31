using System.Net;
using System.Net.Sockets;
using Agency.Huddle.App.Desktop;
using Agency.Huddle.App.Gateway;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>
/// The listener plan on real sockets (Spec 6.2; D-4, D-5, D-6): the app answers on the bound URL, the managed listener has a recorded port of its own,
/// the desktop record uses the app URL, a busy or refused bind starts the app loopback-only, and the desktop port is stable. Every port is 0 or
/// freshly probed; only the lan bind test (trait <c>FirewallPrompt</c>) opens an any-address socket, and it is skipped on Windows by default.
/// </summary>
public sealed class GatewayListenerSocketTests
{
    private static int FreePort()
    {
        TcpListener probe = new(IPAddress.Loopback, 0);
        probe.Start();
        try
        {
            return ((IPEndPoint)probe.LocalEndpoint).Port;
        }
        finally
        {
            probe.Stop();
        }
    }

    private static Dictionary<string, string?> Desktop() => new()
    {
        ["Team:Desktop:Enabled"] = "true",
        ["Team:Desktop:OpenBrowser"] = "false",
    };

    private static async Task<HttpStatusCode> StatusAsync(HttpClient client, string url, CancellationToken ct)
    {
        using HttpResponseMessage response = await client.GetAsync(new Uri(url, UriKind.RelativeOrAbsolute), ct);
        return response.StatusCode;
    }

    private static bool IsAnyAddress(IPEndPoint endpoint) => endpoint.Address.Equals(IPAddress.Any) || endpoint.Address.Equals(IPAddress.IPv6Any);

    /// <summary>With <c>urls = http://127.0.0.1:0</c> the plan records the bound app URL (a non-zero port) and the app answers on it.</summary>
    [Fact]
    public async Task Start_PortZeroUrl_RecordsTheBoundAppUrlAndAnswers()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using KestrelAppHost host = new();

        HttpStatusCode status = await StatusAsync(host.Client, "/health", ct);

        Assert.NotEqual(0, host.AppUrl.Port);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Null(host.Listener.ManagedEndpoint);
    }

    /// <summary>In Serve mode a managed loopback listener exists with a recorded port distinct from the app's (Kestrel writes the bound port back), and a request on it is classified Managed.</summary>
    [Fact]
    public async Task Serve_ManagedListenerHasItsOwnRecordedPortAndClassifiesManaged()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using KestrelAppHost host = new(new Dictionary<string, string?> { ["Team:Gateway:Tailscale:Mode"] = "serve" });
        IPEndPoint? managed = host.Listener.ManagedEndpoint;

        Assert.NotNull(managed);
        Assert.NotEqual(0, managed.Port);
        Assert.NotEqual(host.AppUrl.Port, managed.Port);
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(host.Client, "/health", ct));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(host.Client, $"http://127.0.0.1:{managed.Port}/health", ct));
    }

    /// <summary>The desktop instance record uses the app URL and never the managed endpoint, even in Serve mode where both are loopback.</summary>
    [Fact]
    public void Desktop_InstanceRecordUsesTheAppUrlNotTheManagedEndpoint()
    {
        Dictionary<string, string?> settings = Desktop();
        settings["Team:Gateway:Tailscale:Mode"] = "serve";
        using KestrelAppHost host = new(settings);

        InstanceInfo? info = InstanceFile.TryRead(host.DataDir);

        Assert.NotNull(info);
        Assert.Equal(host.AppUrl.Port, new Uri(info.Url).Port);
        Assert.NotEqual(host.Listener.ManagedEndpoint?.Port, new Uri(info.Url).Port);
    }

    /// <summary>A busy <c>Team:Gateway:Port</c> does not stop the app: it starts loopback-only, answers, and records why.</summary>
    [Fact]
    public async Task Start_BusyConfiguredPort_StartsLoopbackOnlyWithAReason()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TcpListener held = new(IPAddress.Loopback, 0);
        held.Start();
        int busy = ((IPEndPoint)held.LocalEndpoint).Port;
        using KestrelAppHost host = new(new Dictionary<string, string?> { ["Team:Gateway:Bind"] = "lan", ["Team:Gateway:Port"] = busy.ToString(System.Globalization.CultureInfo.InvariantCulture) });

        HttpStatusCode status = await StatusAsync(host.Client, "/health", ct);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.NotNull(host.Listener.Reason);
        Assert.DoesNotContain(host.Listener.AppEndpoints, IsAnyAddress);
    }

    /// <summary>A non-loopback bind with <c>Auth:Mode = none</c> is refused: the app starts loopback-only and records why.</summary>
    [Fact]
    public void Start_LanWithAuthNone_StartsLoopbackOnlyWithAReason()
    {
        using KestrelAppHost host = new(new Dictionary<string, string?>
        {
            ["Team:Gateway:Bind"] = "lan",
            ["Team:Gateway:Port"] = FreePort().ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Team:Gateway:Auth:Mode"] = "none",
        });

        Assert.NotNull(host.Listener.Reason);
        Assert.DoesNotContain(host.Listener.AppEndpoints, IsAnyAddress);
    }

    /// <summary>
    /// Whether the any-address socket test runs: always off Windows (CI), and on Windows only with <c>HUDDLE_FIREWALL_TESTS=1</c>, because the first
    /// any-address listen there opens a firewall prompt that an unattended run cannot answer.
    /// </summary>
    public static bool AnyAddressAllowed =>
        !OperatingSystem.IsWindows() || string.Equals(Environment.GetEnvironmentVariable("HUDDLE_FIREWALL_TESTS"), "1", StringComparison.Ordinal);

    /// <summary>
    /// A lan bind opens one any-address endpoint on the configured port, reachable through 127.0.0.1. Skipped on Windows by default, where the first
    /// any-address listen makes Windows show its firewall prompt (see <see cref="AnyAddressAllowed"/>).
    /// </summary>
    [Fact(SkipUnless = nameof(AnyAddressAllowed), Skip = "Opens the Windows firewall prompt; set HUDDLE_FIREWALL_TESTS=1 to run")]
    [Trait("Category", "FirewallPrompt")]
    public async Task Start_Lan_BindsOneAnyAddressEndpointAndAnswersOnLoopback()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        int port = FreePort();
        using KestrelAppHost host = new(new Dictionary<string, string?>
        {
            ["Team:Gateway:Bind"] = "lan",
            ["Team:Gateway:Port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture),
        });

        HttpStatusCode status = await StatusAsync(host.Client, $"http://127.0.0.1:{port}/health", ct);

        IPEndPoint endpoint = Assert.Single(host.Listener.AppEndpoints);
        Assert.True(IsAnyAddress(endpoint));
        Assert.Equal(port, endpoint.Port);
        Assert.Equal(port, host.AppUrl.Port);
        Assert.Equal(HttpStatusCode.OK, status);
    }

    /// <summary>Desktop mode binds the stored free port, and the port is remembered after start.</summary>
    [Fact]
    public void Desktop_FreeStoredPort_IsBoundAndRemembered()
    {
        int stored = FreePort();
        using KestrelAppHost host = new(Desktop(), dir => new GatewaySettingsStore(dir).SetDesktopPort(stored));

        Assert.Equal(stored, host.AppUrl.Port);
        Assert.Equal(stored, new GatewaySettingsStore(host.DataDir).DesktopPort);
    }

    /// <summary>A busy stored desktop port falls back to another port, which is then stored.</summary>
    [Fact]
    public void Desktop_BusyStoredPort_FallsBackAndRemembersTheNewPort()
    {
        using TcpListener held = new(IPAddress.Loopback, 0);
        held.Start();
        int busy = ((IPEndPoint)held.LocalEndpoint).Port;
        using KestrelAppHost host = new(Desktop(), dir => new GatewaySettingsStore(dir).SetDesktopPort(busy));

        Assert.NotEqual(busy, host.AppUrl.Port);
        Assert.Equal(host.AppUrl.Port, new GatewaySettingsStore(host.DataDir).DesktopPort);
    }

    /// <summary><c>Team:Gateway:Port</c> wins over the stored desktop port.</summary>
    [Fact]
    public void Desktop_ConfiguredPort_WinsOverTheStoredPort()
    {
        int stored = FreePort();
        int configured = FreePort();
        Dictionary<string, string?> settings = Desktop();
        settings["Team:Gateway:Port"] = configured.ToString(System.Globalization.CultureInfo.InvariantCulture);
        using KestrelAppHost host = new(settings, dir => new GatewaySettingsStore(dir).SetDesktopPort(stored));

        Assert.Equal(configured, host.AppUrl.Port);
    }
}

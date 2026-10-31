using System.Globalization;
using System.Net;
using Agency.Huddle.App.Gateway;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>
/// The pure listener plan (Spec 6.2 and 8.1; corrections D-3 and D-4): URL sources, URL parsing, the bind modes, the stable desktop port,
/// the refusals that start the app loopback-only, and the no-op case that leaves Kestrel alone.
/// </summary>
public sealed class GatewayListenerPlanTests
{
    private sealed class FakeProbe : GatewayListenerPlan.IProbe
    {
        public HashSet<int> Busy { get; } = [];

        public IPAddress? Tailnet { get; set; }

        public List<int> Probed { get; } = [];

        public bool IsPortFree(int port)
        {
            this.Probed.Add(port);
            return !this.Busy.Contains(port);
        }

        public IPAddress? TailnetAddress() => this.Tailnet;
    }

    private static GatewayListenerPlan.Input Base(string? urls = "http://localhost:5101") =>
        new(urls, null, null, "loopback", null, null, GatewayAuthMode.Token, false, false, null, false);

    private static string Describe(GatewayListenerPlan.Endpoint endpoint) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{endpoint.Role} {endpoint.Bind} {endpoint.Address?.ToString() ?? "-"}:{endpoint.Port}{(endpoint.Https ? " https" : string.Empty)}");

    private static List<string> Plan(GatewayListenerPlan.Input input, FakeProbe? probe = null) =>
        [.. GatewayListenerPlan.Create(input, probe ?? new FakeProbe()).Endpoints.Select(Describe)];

    /// <summary>With Bind loopback, Tailscale off and desktop mode off the plan adds nothing, so Kestrel binds exactly as configured.</summary>
    [Fact]
    public void Create_LoopbackNoTailscaleNoDesktop_IsANoOp()
    {
        GatewayListenerPlan.Result result = GatewayListenerPlan.Create(Base(), new FakeProbe());

        Assert.Empty(result.Endpoints);
        Assert.Null(result.Reason);
    }

    /// <summary>The managed listener is one loopback endpoint on an ephemeral port, added after the re-applied configured URLs.</summary>
    [Fact]
    public void Create_ManagedListener_ReappliesConfiguredUrlsThenAddsLoopbackPortZero()
    {
        List<string> plan = Plan(Base() with { ManagedListener = true });

        Assert.Equal(["App Localhost -:5101", "Managed Address 127.0.0.1:0"], plan);
    }

    /// <summary><c>urls</c> wins over <c>http_ports</c> and <c>https_ports</c>.</summary>
    [Fact]
    public void Create_UrlsAndPorts_UrlsWin()
    {
        List<string> plan = Plan(Base() with { ManagedListener = true, HttpPorts = "6000", HttpsPorts = "6443" });

        Assert.Equal(["App Localhost -:5101", "Managed Address 127.0.0.1:0"], plan);
    }

    /// <summary>Without <c>urls</c>, <c>http_ports</c> and <c>https_ports</c> each become an any-address listener, https with <c>UseHttps</c>.</summary>
    [Fact]
    public void Create_HttpPortsAndHttpsPorts_AreAnyAddressListeners()
    {
        List<string> plan = Plan(Base(null) with { ManagedListener = true, HttpPorts = "6000;6001", HttpsPorts = "6443" });

        Assert.Equal(
            ["App AnyAddress -:6000", "App AnyAddress -:6001", "App AnyAddress -:6443 https", "Managed Address 127.0.0.1:0"],
            plan);
    }

    /// <summary>With no source at all Kestrel's default <c>http://localhost:5000</c> is re-applied.</summary>
    [Fact]
    public void Create_NoUrlSource_DefaultsToLocalhost5000()
    {
        List<string> plan = Plan(Base(null) with { ManagedListener = true });

        Assert.Equal(["App Localhost -:5000", "Managed Address 127.0.0.1:0"], plan);
    }

    /// <summary>Each URL form maps to the Kestrel call the correction names; <c>localhost</c> with port 0 never becomes <c>ListenLocalhost(0)</c>.</summary>
    [Theory]
    [InlineData("http://127.0.0.1:5101", "App Address 127.0.0.1:5101")]
    [InlineData("http://[::1]:5101", "App Address ::1:5101")]
    [InlineData("http://*:5101", "App AnyAddress -:5101")]
    [InlineData("http://+:5101", "App AnyAddress -:5101")]
    [InlineData("http://myhost:5101", "App AnyAddress -:5101")]
    [InlineData("https://localhost:5443", "App Localhost -:5443 https")]
    [InlineData("http://localhost:0", "App Address 127.0.0.1:0")]
    public void Create_UrlForm_MapsToTheKestrelCall(string url, string expected)
    {
        List<string> plan = Plan(Base(url) with { ManagedListener = true });

        Assert.Equal(expected, plan[0]);
    }

    /// <summary>Several URLs are all re-applied in order, and a repeated URL is applied once.</summary>
    [Fact]
    public void Create_SeveralUrls_AreAppliedInOrderAndDeduplicated()
    {
        List<string> plan = Plan(Base("http://localhost:5101;http://127.0.0.1:5102;http://localhost:5101") with { ManagedListener = true });

        Assert.Equal(["App Localhost -:5101", "App Address 127.0.0.1:5102", "Managed Address 127.0.0.1:0"], plan);
    }

    /// <summary>A <c>unix:</c> URL makes remote access unavailable: no listener is added, so Kestrel binds as configured, and the reason is recorded.</summary>
    [Fact]
    public void Create_UnixSocketUrl_IsUnavailableAndLoopbackOnly()
    {
        GatewayListenerPlan.Result result = GatewayListenerPlan.Create(Base("http://unix:/tmp/huddle.sock") with { ManagedListener = true }, new FakeProbe());

        Assert.Empty(result.Endpoints);
        Assert.NotNull(result.Reason);
    }

    /// <summary><c>lan</c> is one any-address listener on the app port; a configured loopback URL on the same port is dropped.</summary>
    [Fact]
    public void Create_Lan_IsOneAnyAddressListenerAndDropsTheSamePortLoopbackUrl()
    {
        List<string> plan = Plan(Base() with { Bind = "lan" });

        Assert.Equal(["App AnyAddress -:5101"], plan);
    }

    /// <summary>A loopback URL on a different port than the any-address bind stays.</summary>
    [Fact]
    public void Create_LanWithAnotherPortUrl_KeepsTheLoopbackUrl()
    {
        List<string> plan = Plan(Base() with { Bind = "lan", ConfiguredPort = 5201 });

        Assert.Equal(["App Localhost -:5101", "App AnyAddress -:5201"], plan);
    }

    /// <summary><c>tailnet</c> keeps the configured loopback URL and adds the tailnet address on the app port.</summary>
    [Fact]
    public void Create_TailnetWithAddress_AddsTheTailnetListener()
    {
        FakeProbe probe = new() { Tailnet = IPAddress.Parse("100.101.102.103") };

        List<string> plan = Plan(Base() with { Bind = "tailnet" }, probe);

        Assert.Equal(["App Localhost -:5101", "App Address 100.101.102.103:5101"], plan);
    }

    /// <summary><c>tailnet</c> without a tailnet address falls back to loopback and says why.</summary>
    [Fact]
    public void Create_TailnetWithoutAddress_FallsBackToLoopbackWithAReason()
    {
        GatewayListenerPlan.Result result = GatewayListenerPlan.Create(Base() with { Bind = "tailnet", ManagedListener = true }, new FakeProbe());

        Assert.Equal(["App Localhost -:5101", "Managed Address 127.0.0.1:0"], [.. result.Endpoints.Select(Describe)]);
        Assert.NotNull(result.Reason);
    }

    /// <summary><c>custom</c> binds the custom host on the app port next to the configured loopback URL.</summary>
    [Fact]
    public void Create_Custom_AddsTheCustomHostOnTheAppPort()
    {
        List<string> plan = Plan(Base() with { Bind = "custom", CustomBindHost = "192.168.1.20" });

        Assert.Equal(["App Localhost -:5101", "App Address 192.168.1.20:5101"], plan);
    }

    /// <summary><c>auto</c> binds any address inside a container and nothing extra elsewhere.</summary>
    [Fact]
    public void Create_Auto_IsAnyAddressOnlyInAContainer()
    {
        List<string> container = Plan(Base() with { Bind = "auto", InContainer = true });
        List<string> host = Plan(Base() with { Bind = "auto", InContainer = false });

        Assert.Equal(["App AnyAddress -:5101"], container);
        Assert.Empty(host);
    }

    /// <summary>Desktop mode replaces <c>127.0.0.1:0</c> with the stored port when it is free.</summary>
    [Fact]
    public void Create_DesktopWithFreeStoredPort_BindsTheStablePort()
    {
        List<string> plan = Plan(Base("http://127.0.0.1:0") with { DesktopMode = true, StoredDesktopPort = 5300 });

        Assert.Equal(["App Address 127.0.0.1:5300"], plan);
    }

    /// <summary>A busy stored port, or none stored, leaves port 0; desktop mode still adds the listener in code.</summary>
    [Fact]
    public void Create_DesktopWithBusyOrNoStoredPort_KeepsPortZero()
    {
        FakeProbe busy = new();
        busy.Busy.Add(5300);

        List<string> busyPlan = Plan(Base("http://127.0.0.1:0") with { DesktopMode = true, StoredDesktopPort = 5300 }, busy);
        List<string> nonePlan = Plan(Base("http://127.0.0.1:0") with { DesktopMode = true });

        Assert.Equal(["App Address 127.0.0.1:0"], busyPlan);
        Assert.Equal(["App Address 127.0.0.1:0"], nonePlan);
    }

    /// <summary><c>Team:Gateway:Port</c>, when set, wins over the stored desktop port.</summary>
    [Fact]
    public void Create_DesktopWithConfiguredPort_TheConfiguredPortWins()
    {
        List<string> plan = Plan(Base("http://127.0.0.1:0") with { DesktopMode = true, StoredDesktopPort = 5300, ConfiguredPort = 5400 });

        Assert.Equal(["App Address 127.0.0.1:5400"], plan);
    }

    /// <summary>A non-loopback bind in desktop mode needs <c>Team:Gateway:Port</c>; without it the app binds loopback-only on the stable port and says why.</summary>
    [Fact]
    public void Create_DesktopLanWithoutConfiguredPort_IsLoopbackOnlyWithAReason()
    {
        GatewayListenerPlan.Result result = GatewayListenerPlan.Create(
            Base("http://127.0.0.1:0") with { Bind = "lan", DesktopMode = true, StoredDesktopPort = 5300 },
            new FakeProbe());

        Assert.Equal(["App Address 127.0.0.1:5300"], [.. result.Endpoints.Select(Describe)]);
        Assert.NotNull(result.Reason);
    }

    /// <summary>A non-loopback bind in desktop mode with <c>Team:Gateway:Port</c> binds any address on that port, and the same-port loopback is dropped.</summary>
    [Fact]
    public void Create_DesktopLanWithConfiguredPort_BindsAnyAddressOnThatPort()
    {
        List<string> plan = Plan(Base("http://127.0.0.1:0") with { Bind = "lan", DesktopMode = true, ConfiguredPort = 5400 });

        Assert.Equal(["App AnyAddress -:5400"], plan);
    }

    /// <summary>A non-loopback bind with <c>Auth:Mode = none</c> does not bind: loopback-only, with a reason.</summary>
    [Fact]
    public void Create_LanWithAuthNone_IsLoopbackOnlyWithAReason()
    {
        GatewayListenerPlan.Result result = GatewayListenerPlan.Create(
            Base() with { Bind = "lan", AuthMode = GatewayAuthMode.None, ManagedListener = true },
            new FakeProbe());

        Assert.DoesNotContain(result.Endpoints, endpoint => endpoint.Bind == GatewayListenerPlan.Bind.AnyAddress);
        Assert.NotNull(result.Reason);
    }

    /// <summary>A busy app port is probed (never port 0) and starts the app loopback-only, with the reason recorded.</summary>
    [Fact]
    public void Create_LanWithBusyPort_IsLoopbackOnlyWithAReason()
    {
        FakeProbe probe = new();
        probe.Busy.Add(5101);

        GatewayListenerPlan.Result result = GatewayListenerPlan.Create(Base() with { Bind = "lan", ManagedListener = true }, probe);

        Assert.DoesNotContain(result.Endpoints, endpoint => endpoint.Bind == GatewayListenerPlan.Bind.AnyAddress);
        Assert.NotNull(result.Reason);
        Assert.Contains(5101, probe.Probed);
        Assert.DoesNotContain(0, probe.Probed);
    }

    /// <summary>The app port of Spec 8.1 is <c>Port ?? the single non-zero configured port ?? null</c>.</summary>
    [Theory]
    [InlineData("http://localhost:5101", null, 5101)]
    [InlineData("http://localhost:5101", 6000, 6000)]
    [InlineData("http://127.0.0.1:0", null, null)]
    [InlineData("http://localhost:5101;http://localhost:5102", null, null)]
    public void Create_AppPort_FollowsSection81(string urls, int? configured, int? expected)
    {
        GatewayListenerPlan.Result result = GatewayListenerPlan.Create(Base(urls) with { ConfiguredPort = configured }, new FakeProbe());

        Assert.Equal(expected, result.AppPort);
    }
}

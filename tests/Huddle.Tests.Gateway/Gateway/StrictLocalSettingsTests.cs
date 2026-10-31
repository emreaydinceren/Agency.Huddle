using System.Security.Claims;
using Agency.Huddle.App.Cli;
using Agency.Huddle.App.Desktop;
using Agency.Huddle.App.Gateway;
using Agency.Huddle.Tests.Desktop;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>
/// Strict local mode (ra-strict-local.md): the setting, its override precedence, what it forces off at startup, the host principal, the
/// remote-access state, the security check item and the plain URLs the launcher, the dev console and the dashboard open.
/// </summary>
public sealed class StrictLocalSettingsTests
{
    private static IConfigurationRoot Config(params (string Key, string Value)[] pairs) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(pairs.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value)))
            .Build();

    private static GatewaySettings Read(TempDataDir dir, params (string Key, string Value)[] pairs) =>
        GatewaySettings.ReadForStartup(dir.Path, Config(pairs), new FakeGatewayEnvironment());

    /// <summary>With nothing configured and nothing saved, strict local is on.</summary>
    [Fact]
    public void StrictLocal_DefaultsToOn()
    {
        using TempDataDir dir = new();

        Assert.True(new GatewayOptions().StrictLocal);
        Assert.True(Read(dir).Options.StrictLocal);
    }

    /// <summary>The configured value applies when nothing is saved.</summary>
    [Fact]
    public void StrictLocal_ConfigurationFalse_TurnsItOff()
    {
        using TempDataDir dir = new();

        Assert.False(Read(dir, ("Team:Gateway:StrictLocal", "false")).Options.StrictLocal);
    }

    /// <summary>A saved override wins over configuration, in both directions.</summary>
    /// <param name="configured">The configured value.</param>
    /// <param name="saved">The saved override.</param>
    [Theory]
    [InlineData("true", false)]
    [InlineData("false", true)]
    public void StrictLocal_SavedOverrideWinsOverConfiguration(string configured, bool saved)
    {
        using TempDataDir dir = new();
        new GatewaySettingsStore(dir.Path).SetOverrides(saved ? "{\"StrictLocal\":true}" : "{\"StrictLocal\":false}");

        Assert.Equal(saved, Read(dir, ("Team:Gateway:StrictLocal", configured)).Options.StrictLocal);
    }

    /// <summary>While strict local is on, a configured LAN bind, Tailscale Serve and trusted proxies are forced off before anything reads them.</summary>
    [Fact]
    public void ReadForStartup_StrictLocal_ForcesRemoteOptionsOff()
    {
        using TempDataDir dir = new();

        GatewayOptions options = Read(
            dir,
            ("Team:Gateway:Bind", "lan"),
            ("Team:Gateway:Port", "5200"),
            ("Team:Gateway:Tailscale:Mode", "serve"),
            ("Team:Gateway:TrustedProxies:0", "10.0.0.1")).Options;

        Assert.Equal("loopback", options.Bind);
        Assert.Equal("off", options.Tailscale.Mode);
        Assert.Empty(options.TrustedProxies);
    }

    /// <summary>With strict local off, the same configuration is left alone.</summary>
    [Fact]
    public void ReadForStartup_StrictLocalOff_KeepsRemoteOptions()
    {
        using TempDataDir dir = new();

        GatewayOptions options = Read(
            dir,
            ("Team:Gateway:StrictLocal", "false"),
            ("Team:Gateway:Bind", "lan"),
            ("Team:Gateway:Tailscale:Mode", "serve"),
            ("Team:Gateway:TrustedProxies:0", "10.0.0.1")).Options;

        Assert.Equal("lan", options.Bind);
        Assert.Equal("serve", options.Tailscale.Mode);
        Assert.Single(options.TrustedProxies);
    }

    /// <summary>The listener plan for a strict local install with Bind=lan and Tailscale=serve configured adds no listener at all.</summary>
    [Fact]
    public void ListenerPlan_StrictLocal_BehavesAsLoopback()
    {
        using TempDataDir dir = new();
        GatewayOptions options = Read(dir, ("Team:Gateway:Bind", "lan"), ("Team:Gateway:Port", "5200"), ("Team:Gateway:Tailscale:Mode", "serve")).Options;
        GatewayListenerPlan.Input input = new(
            "http://localhost:5200",
            null,
            null,
            options.Bind,
            options.CustomBindHost,
            options.Port,
            options.Auth.Mode,
            GatewayListenerStartup.UsesManagedListener(options),
            false,
            null,
            false);

        GatewayListenerPlan.Result plan = GatewayListenerPlan.Create(input, new FreePortProbe());

        Assert.Empty(plan.Endpoints);
        Assert.Null(plan.Reason);
    }

    /// <summary>Strict local means no remote access is configured, so Open and Browse stay available; with it off a LAN bind is remote access.</summary>
    [Fact]
    public void RemoteAccessState_StrictLocal_IsNeverConfigured()
    {
        GatewayOptions strict = new() { StrictLocal = true, Bind = "lan" };
        GatewayOptions open = new() { StrictLocal = false, Bind = "lan" };

        Assert.False(new RemoteAccessState(strict).IsConfigured);
        Assert.True(new RemoteAccessState(open).IsConfigured);
    }

    /// <summary>The host principal carries the host viewer, the loopback ingress, every scope and the name identifier <c>local</c>, and no session.</summary>
    [Fact]
    public void GatewayPrincipal_Host_HasHostClaimsAndNoSession()
    {
        ClaimsPrincipal host = GatewayPrincipal.CreateHost();

        Assert.True(host.Identity?.IsAuthenticated);
        Assert.Equal("host", host.FindFirstValue(GatewayClaims.Viewer));
        Assert.Equal(IngressKindText.Of(IngressKind.Loopback), host.FindFirstValue(GatewayClaims.Ingress));
        Assert.Equal("local", host.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Null(host.FindFirst(GatewayClaims.Session));
        Assert.Contains(GatewayScopes.Admin, host.FindAll(GatewayClaims.Scope).Select(static claim => claim.Value));
        Assert.True(GatewayScopes.HoldsAll(host.FindAll(GatewayClaims.Scope).Select(static claim => claim.Value), GatewayScopes.AdminSet));
    }

    /// <summary>The security check names strict local as an Info item.</summary>
    [Fact]
    public void SecurityCheck_StrictLocal_RaisesTheInfoItem()
    {
        IReadOnlyList<GatewaySecurityCheck.Item> items = GatewaySecurityCheck.Evaluate(Input(new GatewayOptions { StrictLocal = true }));

        GatewaySecurityCheck.Item item = Assert.Single(items, static item => item.Id == "gateway.strict_local");
        Assert.Equal(GatewaySecurityCheck.Severity.Info, item.Level);
        Assert.Equal("Strict local: no sign-in; only this computer can use Huddle.", item.Condition);
    }

    /// <summary>While strict local is on, the items about remote exposure are not raised even for a LAN bind, Funnel, a wildcard origin or a loopback proxy.</summary>
    [Fact]
    public void SecurityCheck_StrictLocal_DoesNotRaiseRemoteExposureItems()
    {
        GatewayOptions options = new() { StrictLocal = true, Bind = "lan", PublicOrigin = null };
        options.Tailscale.Mode = "funnel";
        options.AllowedOrigins.Add("*");
        options.TrustedProxies.Add("127.0.0.1");
        options.DangerouslyAllowHostHeaderOriginFallback = true;

        IReadOnlyList<GatewaySecurityCheck.Item> items = GatewaySecurityCheck.Evaluate(Input(options, TailscaleState.Running));

        string[] remote =
        [
            "gateway.bind_no_auth",
            "gateway.loopback_no_auth",
            "gateway.tailscale_funnel",
            "gateway.tailscale_serve",
            "gateway.control_ui.allowed_origins_required",
            "gateway.control_ui.allowed_origins_wildcard",
            "gateway.control_ui.host_header_origin_fallback",
            "gateway.trusted_proxy_loopback",
            "gateway.downgraded_transport",
            "gateway.stray_serve_route",
        ];
        Assert.DoesNotContain(items, item => remote.Contains(item.Id, StringComparer.Ordinal));
    }

    /// <summary>With strict local off the item is not raised and the usual items return.</summary>
    [Fact]
    public void SecurityCheck_StrictLocalOff_RaisesNoStrictItemAndKeepsTheRest()
    {
        IReadOnlyList<GatewaySecurityCheck.Item> items = GatewaySecurityCheck.Evaluate(Input(new GatewayOptions { StrictLocal = false, Bind = "lan" }));

        Assert.DoesNotContain(items, static item => item.Id == "gateway.strict_local");
        Assert.Contains(items, static item => item.Id == "gateway.downgraded_transport");
    }

    /// <summary>With strict local on, the admin URL is the plain app address: no sign-in page, no token.</summary>
    [Fact]
    public void BootstrapLinks_StrictLocal_AdminUrlIsThePlainAddress()
    {
        using TempDataDir dir = new();
        BootstrapLinks links = new(new BootstrapTokenStore(), Read(dir));

        Assert.Equal("http://127.0.0.1:5123", links.AdminUrl("http://[::]:5123"));
    }

    /// <summary>With strict local off, the admin URL still carries the bootstrap token.</summary>
    [Fact]
    public void BootstrapLinks_StrictLocalOff_AdminUrlCarriesTheToken()
    {
        using TempDataDir dir = new();
        BootstrapLinks links = new(new BootstrapTokenStore(), Read(dir, ("Team:Gateway:StrictLocal", "false")));

        Assert.Matches(@"^http://127\.0\.0\.1:5123/auth\?return=%2F#bootstrapToken=[A-Za-z0-9_-]{43}$", links.AdminUrl("http://127.0.0.1:5123"));
    }

    /// <summary>The development console prints the plain app URL under strict local.</summary>
    [Fact]
    public void DevConsole_StrictLocal_PrintsThePlainUrl()
    {
        using TempDataDir dir = new();
        StringWriter output = new();

        _ = DevConsole.Announce(["http://localhost:5100"], new BootstrapLinks(new BootstrapTokenStore(), Read(dir)), output);

        Assert.Equal("Huddle is ready: http://localhost:5100", output.ToString().Trim());
    }

    /// <summary>The desktop launcher opens the plain app URL under strict local.</summary>
    [Fact]
    public async Task DesktopLauncher_StrictLocal_OpensThePlainUrl()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using FakeLifetime lifetime = new();
        RecordingBrowserOpener opener = new();
        DesktopLifetimeService service = new(
            new FakeServer("http://127.0.0.1:5123"),
            lifetime,
            Options.Create(new DesktopOptions { OpenBrowser = true }),
            dir.Options(),
            new DesktopSession(),
            opener,
            NullLogger<DesktopLifetimeService>.Instance,
            new BootstrapLinks(new BootstrapTokenStore(), Read(dir)));

        await service.StartAsync(ct);
        lifetime.FireStarted();

        Assert.Equal("http://127.0.0.1:5123", Assert.Single(opener.Opened));
    }

    private static GatewaySecurityCheck.Input Input(GatewayOptions options, TailscaleState tailscale = TailscaleState.Missing) =>
        new(options, 48, null, tailscale, true, [new TailscaleServeStatus.Route(5200, "/", "http://127.0.0.1:5200")]);

    private sealed class FreePortProbe : GatewayListenerPlan.IProbe
    {
        /// <inheritdoc />
        public bool IsPortFree(int port) => true;

        /// <inheritdoc />
        public System.Net.IPAddress? TailnetAddress() => null;
    }

    private sealed class FakeServer : IServer
    {
        public FakeServer(string address)
        {
            FakeAddresses feature = new();
            feature.Addresses.Add(address);
            FeatureCollection features = new();
            features.Set<IServerAddressesFeature>(feature);
            this.Features = features;
        }

        /// <inheritdoc />
        public IFeatureCollection Features { get; }

        /// <inheritdoc />
        public Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken cancellationToken)
            where TContext : notnull => Task.CompletedTask;

        /// <inheritdoc />
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }

    private sealed class FakeAddresses : IServerAddressesFeature
    {
        /// <inheritdoc />
        public ICollection<string> Addresses { get; } = [];

        /// <inheritdoc />
        public bool PreferHostingUrls { get; set; }
    }
}

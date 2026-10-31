using Agency.Huddle.App.Gateway;

namespace Agency.Huddle.Tests;

/// <summary>The Security check (Spec 8.9, Stage E1): one test per row, each pinning the id, the severity and the two sentences the Human reads.</summary>
public sealed class GatewaySecurityCheckTests
{
    private static GatewaySecurityCheck.Item Only(IReadOnlyList<GatewaySecurityCheck.Item> items, string id) =>
        Assert.Single(items, item => string.Equals(item.Id, id, StringComparison.Ordinal));

    /// <summary>A safe default (loopback, token, long token, no Tailscale), changed by <paramref name="change"/>.</summary>
    private static IReadOnlyList<GatewaySecurityCheck.Item> Check(
        Action<GatewayOptions>? change = null,
        int tokenLength = 43,
        int? passwordLength = null,
        TailscaleState tailscale = TailscaleState.Missing,
        bool bareName = false,
        IReadOnlyList<TailscaleServeStatus.Route>? stray = null)
    {
        GatewayOptions options = new() { StrictLocal = false };
        change?.Invoke(options);
        return GatewaySecurityCheck.Evaluate(new GatewaySecurityCheck.Input(options, tokenLength, passwordLength, tailscale, bareName, stray ?? []));
    }

    /// <summary>A loopback bind with a long token and no Tailscale raises nothing.</summary>
    [Fact]
    public void Evaluate_SafeDefaults_ReturnsNothing() => Assert.Empty(Check());

    /// <summary>A LAN bind with sign-in off is Critical: the bind is refused.</summary>
    [Fact]
    public void Evaluate_BindWithoutAuth_IsCritical()
    {
        GatewaySecurityCheck.Item item = Only(Check(options => { options.Bind = "lan"; options.Auth.ModeText = "none"; }), "gateway.bind_no_auth");

        Assert.Equal(GatewaySecurityCheck.Severity.Critical, item.Level);
        Assert.Equal("Huddle is set to listen beyond this computer with sign-in off.", item.Condition);
        Assert.Equal("Choose token or password sign-in. Huddle will not open the network listener until you do.", item.Fix);
    }

    /// <summary>A non-loopback bind with sign-in off raises the bind item and not the loopback one.</summary>
    [Fact]
    public void Evaluate_BindWithoutAuth_DoesNotAlsoRaiseLoopbackNoAuth() =>
        Assert.DoesNotContain(Check(options => { options.Bind = "tailnet"; options.Auth.ModeText = "none"; }), item => item.Id == "gateway.loopback_no_auth");

    /// <summary>Sign-in off on a loopback bind is a Warning.</summary>
    [Fact]
    public void Evaluate_LoopbackWithoutAuth_IsWarning()
    {
        GatewaySecurityCheck.Item item = Only(Check(options => options.Auth.ModeText = "none"), "gateway.loopback_no_auth");

        Assert.Equal(GatewaySecurityCheck.Severity.Warning, item.Level);
        Assert.Equal("Sign-in is off. Anything running on this computer can use Huddle.", item.Condition);
        Assert.Equal("Set the auth mode to token or password.", item.Fix);
    }

    /// <summary>A running Funnel is Critical.</summary>
    [Fact]
    public void Evaluate_FunnelRunning_IsCritical()
    {
        GatewaySecurityCheck.Item item = Only(Check(options => options.Tailscale.Mode = "funnel", tailscale: TailscaleState.Running), "gateway.tailscale_funnel");

        Assert.Equal(GatewaySecurityCheck.Severity.Critical, item.Level);
        Assert.Equal("Tailscale Funnel is running: Huddle is open to the public internet.", item.Condition);
        Assert.Equal("Switch to Tailscale Serve unless you really need the public internet, and keep a strong password.", item.Fix);
    }

    /// <summary>Funnel chosen but Tailscale not running raises nothing: it is not running.</summary>
    [Fact]
    public void Evaluate_FunnelChosenButTailscaleDown_RaisesNothing() =>
        Assert.DoesNotContain(Check(options => options.Tailscale.Mode = "funnel", tailscale: TailscaleState.NeedsLogin), item => item.Id == "gateway.tailscale_funnel");

    /// <summary>A running Serve is Info.</summary>
    [Fact]
    public void Evaluate_ServeRunning_IsInfo()
    {
        GatewaySecurityCheck.Item item = Only(Check(options => options.Tailscale.Mode = "serve", tailscale: TailscaleState.Running), "gateway.tailscale_serve");

        Assert.Equal(GatewaySecurityCheck.Severity.Info, item.Level);
        Assert.Equal("Tailscale Serve is running: Huddle can be reached from inside your tailnet.", item.Condition);
        Assert.Equal("Nothing to fix. Check that only your own devices are on the tailnet.", item.Fix);
    }

    /// <summary>A token under 24 characters is a Warning; 24 is fine.</summary>
    [Fact]
    public void Evaluate_ShortToken_IsWarningAndBoundaryIs24()
    {
        GatewaySecurityCheck.Item item = Only(Check(tokenLength: 23), "gateway.token_too_short");

        Assert.Equal(GatewaySecurityCheck.Severity.Warning, item.Level);
        Assert.Equal("The gateway token is shorter than 24 characters.", item.Condition);
        Assert.Equal("Rotate the token to get a new random one.", item.Fix);
        Assert.DoesNotContain(Check(tokenLength: 24), found => found.Id == "gateway.token_too_short");
    }

    /// <summary>A password under 12 characters in password mode is a Warning; 12 is fine.</summary>
    [Fact]
    public void Evaluate_ShortPassword_IsWarningAndBoundaryIs12()
    {
        GatewaySecurityCheck.Item item = Only(Check(options => options.Auth.ModeText = "password", passwordLength: 11), "gateway.password_too_short");

        Assert.Equal(GatewaySecurityCheck.Severity.Warning, item.Level);
        Assert.Equal("The gateway password is shorter than 12 characters.", item.Condition);
        Assert.Equal("Set a longer password.", item.Fix);
        Assert.DoesNotContain(Check(options => options.Auth.ModeText = "password", passwordLength: 12), found => found.Id == "gateway.password_too_short");
    }

    /// <summary>Reachable from the network, no allowed or public origin, and a bare-name host in use: Warning.</summary>
    [Fact]
    public void Evaluate_NetworkBindWithoutOriginsAndBareNameHost_IsWarning()
    {
        GatewaySecurityCheck.Item item = Only(Check(options => options.Bind = "lan", bareName: true), "gateway.control_ui.allowed_origins_required");

        Assert.Equal(GatewaySecurityCheck.Severity.Warning, item.Level);
        Assert.Equal("Huddle can be reached from the network, but no allowed origin or public origin is set.", item.Condition);
        Assert.Equal("Add the address you open Huddle at to Allowed origins, or set the Public origin.", item.Fix);
    }

    /// <summary>A public origin, an allowed origin, a loopback bind or no bare-name host each silence the required-origins item.</summary>
    [Fact]
    public void Evaluate_OriginsRequired_IsQuietWhenAnyConditionFails()
    {
        Assert.DoesNotContain(Check(options => { options.Bind = "lan"; options.PublicOrigin = "https://huddle.example.ts.net"; }, bareName: true), item => item.Id == "gateway.control_ui.allowed_origins_required");
        Assert.DoesNotContain(Check(options => { options.Bind = "lan"; options.AllowedOrigins.Add("http://192.168.1.20:5101"); }, bareName: true), item => item.Id == "gateway.control_ui.allowed_origins_required");
        Assert.DoesNotContain(Check(bareName: true), item => item.Id == "gateway.control_ui.allowed_origins_required");
        Assert.DoesNotContain(Check(options => options.Bind = "lan"), item => item.Id == "gateway.control_ui.allowed_origins_required");
    }

    /// <summary>An allowed origin of <c>*</c> is a Warning.</summary>
    [Fact]
    public void Evaluate_WildcardOrigin_IsWarning()
    {
        GatewaySecurityCheck.Item item = Only(Check(options => options.AllowedOrigins.Add("*")), "gateway.control_ui.allowed_origins_wildcard");

        Assert.Equal(GatewaySecurityCheck.Severity.Warning, item.Level);
        Assert.Equal("Allowed origins contains *, so any website may talk to Huddle.", item.Condition);
        Assert.Equal("Replace * with the exact addresses you use.", item.Fix);
    }

    /// <summary>The dangerous Host-header fallback being on is a Warning.</summary>
    [Fact]
    public void Evaluate_HostHeaderFallback_IsWarning()
    {
        GatewaySecurityCheck.Item item = Only(Check(options => options.DangerouslyAllowHostHeaderOriginFallback = true), "gateway.control_ui.host_header_origin_fallback");

        Assert.Equal(GatewaySecurityCheck.Severity.Warning, item.Level);
        Assert.Equal("The Host header fallback is on, so the origin check trusts whatever the Host header says.", item.Condition);
        Assert.Equal("Turn the fallback off and list your addresses in Allowed origins.", item.Fix);
    }

    /// <summary>A loopback address among the trusted proxies is a Warning.</summary>
    [Fact]
    public void Evaluate_TrustedProxyLoopback_IsWarning()
    {
        GatewaySecurityCheck.Item item = Only(Check(options => options.TrustedProxies.Add("127.0.0.1")), "gateway.trusted_proxy_loopback");

        Assert.Equal(GatewaySecurityCheck.Severity.Warning, item.Level);
        Assert.Equal("A trusted proxy address is a loopback address, so any program on this computer can act as the proxy.", item.Condition);
        Assert.Equal("Remove the loopback address from Trusted proxies.", item.Fix);
    }

    /// <summary>A LAN, tailnet or custom bind is a Warning that names the bind.</summary>
    [Theory]
    [InlineData("lan", "A LAN bind is active: traffic is not encrypted.")]
    [InlineData("tailnet", "A tailnet bind is active: traffic is not encrypted.")]
    [InlineData("custom", "A custom bind is active: traffic is not encrypted.")]
    public void Evaluate_DowngradedBind_IsWarning(string bind, string condition)
    {
        GatewaySecurityCheck.Item item = Only(Check(options => options.Bind = bind), "gateway.downgraded_transport");

        Assert.Equal(GatewaySecurityCheck.Severity.Warning, item.Level);
        Assert.Equal(condition, item.Condition);
        Assert.Equal("Use Tailscale Serve, which encrypts the connection.", item.Fix);
    }

    /// <summary>A Serve route that points at the app port is a Warning and names the port.</summary>
    [Fact]
    public void Evaluate_StrayServeRoute_IsWarning()
    {
        GatewaySecurityCheck.Item item = Only(
            Check(stray: [new TailscaleServeStatus.Route(443, "/", "http://127.0.0.1:41233")]),
            "gateway.stray_serve_route");

        Assert.Equal(GatewaySecurityCheck.Severity.Warning, item.Level);
        Assert.Equal("A Tailscale Serve route sends traffic straight to Huddle's app port, around the sign-in check.", item.Condition);
        Assert.Equal("Remove that route with tailscale serve reset, and let Huddle manage Serve.", item.Fix);
    }

    /// <summary>Items come Critical first, then Warning, then Info.</summary>
    [Fact]
    public void Evaluate_SeveralItems_AreOrderedBySeverity()
    {
        IReadOnlyList<GatewaySecurityCheck.Item> items = Check(
            options => { options.Tailscale.Mode = "funnel"; options.Bind = "lan"; options.AllowedOrigins.Add("*"); },
            tailscale: TailscaleState.Running);

        Assert.Equal(
            [GatewaySecurityCheck.Severity.Critical, GatewaySecurityCheck.Severity.Warning, GatewaySecurityCheck.Severity.Warning],
            items.Select(item => item.Level).ToList());
        Assert.Equal("gateway.tailscale_funnel", items[0].Id);
    }
}

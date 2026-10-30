namespace Agency.Huddle.App.Gateway;

/// <summary>The Security check (Spec 8.9): the OpenClaw audit items that apply to Huddle, computed on demand. E1 stub: built in the implement step.</summary>
internal static class GatewaySecurityCheck
{
    /// <summary>How serious an item is; the list is ordered Critical first.</summary>
    internal enum Severity
    {
        /// <summary>Worth knowing; nothing is wrong.</summary>
        Info,

        /// <summary>Weakens protection.</summary>
        Warning,

        /// <summary>Exposes Huddle.</summary>
        Critical,
    }

    /// <summary>One finding.</summary>
    /// <param name="Id">The Spec 8.9 id, for example <c>gateway.downgraded_transport</c>.</param>
    /// <param name="Level">The severity.</param>
    /// <param name="Condition">What is wrong, in one plain sentence.</param>
    /// <param name="Fix">What to do about it, in one plain sentence.</param>
    internal sealed record Item(string Id, Severity Level, string Condition, string Fix);

    /// <summary>What the check looks at.</summary>
    /// <param name="Options">The running gateway options.</param>
    /// <param name="TokenLength">The length of the running token.</param>
    /// <param name="PasswordLength">The length of the password when it is known (configuration or environment), else <see langword="null"/>.</param>
    /// <param name="Tailscale">The state of the local Tailscale node.</param>
    /// <param name="BareNameHostInUse">Whether a bare-name host was used.</param>
    /// <param name="StrayRoutes">Serve routes that point at the app port.</param>
    internal sealed record Input(
        GatewayOptions Options,
        int TokenLength,
        int? PasswordLength,
        TailscaleState Tailscale,
        bool BareNameHostInUse,
        IReadOnlyList<TailscaleServeStatus.Route> StrayRoutes);

    /// <summary>Every item whose condition holds, Critical first, then Warning, then Info, each group in Spec order.</summary>
    /// <param name="input">What to look at.</param>
    public static IReadOnlyList<Item> Evaluate(Input input)
    {
        ArgumentNullException.ThrowIfNull(input);

        GatewayOptions options = input.Options;
        bool network = IsNetworkBind(options.Bind);
        bool signInOff = options.Auth.Mode == GatewayAuthMode.None;
        bool tailscaleUp = input.Tailscale == TailscaleState.Running;
        bool hasOrigin = options.AllowedOrigins.Count > 0 || !string.IsNullOrWhiteSpace(options.PublicOrigin);

        List<Item> items = [];
        Add(items, network && signInOff, "gateway.bind_no_auth", Severity.Critical, "Huddle is set to listen beyond this computer with sign-in off.", "Choose token or password sign-in. Huddle will not open the network listener until you do.");
        Add(items, !network && signInOff, "gateway.loopback_no_auth", Severity.Warning, "Sign-in is off. Anything running on this computer can use Huddle.", "Set the auth mode to token or password.");
        Add(items, tailscaleUp && IsTailscale(options, "funnel"), "gateway.tailscale_funnel", Severity.Critical, "Tailscale Funnel is running: Huddle is open to the public internet.", "Switch to Tailscale Serve unless you really need the public internet, and keep a strong password.");
        Add(items, tailscaleUp && IsTailscale(options, "serve"), "gateway.tailscale_serve", Severity.Info, "Tailscale Serve is running: Huddle can be reached from inside your tailnet.", "Nothing to fix. Check that only your own devices are on the tailnet.");
        Add(items, input.TokenLength < MinTokenLength, "gateway.token_too_short", Severity.Warning, "The gateway token is shorter than 24 characters.", "Rotate the token to get a new random one.");
        Add(items, options.Auth.Mode == GatewayAuthMode.Password && input.PasswordLength is < MinPasswordLength, "gateway.password_too_short", Severity.Warning, "The gateway password is shorter than 12 characters.", "Set a longer password.");
        Add(items, network && !hasOrigin && input.BareNameHostInUse, "gateway.control_ui.allowed_origins_required", Severity.Warning, "Huddle can be reached from the network, but no allowed origin or public origin is set.", "Add the address you open Huddle at to Allowed origins, or set the Public origin.");
        Add(items, options.AllowedOrigins.Contains("*", StringComparer.Ordinal), "gateway.control_ui.allowed_origins_wildcard", Severity.Warning, "Allowed origins contains *, so any website may talk to Huddle.", "Replace * with the exact addresses you use.");
        Add(items, options.DangerouslyAllowHostHeaderOriginFallback, "gateway.control_ui.host_header_origin_fallback", Severity.Warning, "The Host header fallback is on, so the origin check trusts whatever the Host header says.", "Turn the fallback off and list your addresses in Allowed origins.");
        Add(items, options.TrustedProxies.Any(IsLoopbackAddress), "gateway.trusted_proxy_loopback", Severity.Warning, "A trusted proxy address is a loopback address, so any program on this computer can act as the proxy.", "Remove the loopback address from Trusted proxies.");
        Add(items, network, "gateway.downgraded_transport", Severity.Warning, $"A {BindName(options.Bind)} bind is active: traffic is not encrypted.", "Use Tailscale Serve, which encrypts the connection.");
        Add(items, input.StrayRoutes.Count > 0, "gateway.stray_serve_route", Severity.Warning, "A Tailscale Serve route sends traffic straight to Huddle's app port, around the sign-in check.", "Remove that route with tailscale serve reset, and let Huddle manage Serve.");

        // OrderBy is stable, so each severity keeps the Spec order above.
        return [.. items.OrderByDescending(static item => item.Level)];
    }

    private const int MinTokenLength = 24;
    private const int MinPasswordLength = 12;

    private static void Add(List<Item> items, bool applies, string id, Severity level, string condition, string fix)
    {
        if (applies)
        {
            items.Add(new Item(id, level, condition, fix));
        }
    }

    private static bool IsNetworkBind(string bind) =>
        bind.Equals("lan", StringComparison.OrdinalIgnoreCase)
        || bind.Equals("tailnet", StringComparison.OrdinalIgnoreCase)
        || bind.Equals("custom", StringComparison.OrdinalIgnoreCase);

    private static bool IsTailscale(GatewayOptions options, string mode) => options.Tailscale.Mode.Equals(mode, StringComparison.OrdinalIgnoreCase);

    private static string BindName(string bind) => bind.ToUpperInvariant() switch
    {
        "LAN" => "LAN",
        "TAILNET" => "tailnet",
        _ => "custom",
    };

    private static bool IsLoopbackAddress(string entry)
    {
        string address = entry.Split('/', 2)[0].Trim().Trim('[', ']');
        return address.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || (System.Net.IPAddress.TryParse(address, out System.Net.IPAddress? parsed) && System.Net.IPAddress.IsLoopback(parsed));
    }
}

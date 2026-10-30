using System.Globalization;

namespace Agency.Huddle.App.Gateway;

/// <summary>
/// The real <see cref="IGatewayRuntimeView"/>: reads the running gateway's listener, Tailscale claim and Tailscale poller, and says in plain
/// words why the gateway is not fully up (Spec 6.12, G10a).
/// </summary>
/// <param name="settings">The running settings.</param>
/// <param name="listener">The recorded listeners.</param>
/// <param name="claim">The Tailscale Serve claim.</param>
/// <param name="runtime">The Tailscale runtime, which holds the latest poll.</param>
internal sealed class GatewayRuntimeView(GatewaySettings settings, GatewayListener listener, TailscaleServeClaim claim, TailscaleRuntime runtime) : IGatewayRuntimeView
{
    /// <summary>The words for a bind refused because sign-in is off.</summary>
    internal const string BindNeedsSignIn = "Huddle stayed on this computer because sign-in is off. Choose token or password sign-in, then restart Huddle.";

    /// <summary>The words for a network bind with no port set.</summary>
    internal const string BindNeedsPort = "Huddle stayed on this computer because no port is set. Set a port, then restart Huddle.";

    /// <summary>The words for a Funnel claim without password sign-in.</summary>
    internal const string FunnelNeedsPassword = "Funnel needs password sign-in. Set a password, choose the password auth mode, then restart Huddle.";

    /// <summary>The words for a Funnel claim on a port Tailscale does not allow.</summary>
    internal const string FunnelPortNotAllowed = "Funnel can only use port 443, 8443 or 10000. Change the Tailscale HTTPS port, then restart Huddle.";

    /// <summary>The words for a Serve route that never appeared.</summary>
    internal const string RouteTimedOut = "Tailscale did not set up the route in time. Check that Tailscale is running, then restart Huddle.";

    /// <inheritdoc />
    public TailscaleState TailscaleState =>
        runtime.Snapshot?.Status.State ?? (TailscaleRuntime.IsOn(settings.Options) ? TailscaleState.NotRunning : TailscaleState.Missing);

    /// <inheritdoc />
    public string? TailscaleOwnerLogin => runtime.Snapshot?.Status.OwnerLogin;

    /// <inheritdoc />
    public string? PublicUrl
    {
        get
        {
            GatewayOptions options = settings.Options;
            if (!string.IsNullOrWhiteSpace(options.PublicOrigin))
            {
                return options.PublicOrigin.TrimEnd('/');
            }

            if (claim.Status.State == TailscaleServeClaim.ClaimState.Running
                && claim.Request is { } request
                && runtime.Snapshot?.Status.DnsName is { Length: > 0 } dns)
            {
                return request.TailscalePort == TailscaleHttpsPort.Default
                    ? $"https://{dns}"
                    : $"https://{dns}:{request.TailscalePort.ToString(CultureInfo.InvariantCulture)}";
            }

            return LanUrl(options, listener);
        }
    }

    /// <inheritdoc />
    public string? ListenerReason => ReasonOf(listener.Reason, claim.Status, claim.Request);

    /// <inheritdoc />
    public IReadOnlyList<TailscaleServeStatus.Route> StrayRoutes => runtime.Snapshot?.StrayRoutes ?? [];

    /// <inheritdoc />
    public bool BareNameHostInUse => false;

    /// <summary>The plain sentence for why the gateway is not fully up, or <see langword="null"/> when nothing is wrong.</summary>
    /// <param name="listenerReason">The reason the listener plan recorded.</param>
    /// <param name="claimStatus">Where the Tailscale claim stands.</param>
    /// <param name="request">What the claim holds.</param>
    internal static string? ReasonOf(string? listenerReason, TailscaleServeClaim.ClaimStatus claimStatus, TailscaleServeClaim.ClaimRequest? request)
    {
        ArgumentNullException.ThrowIfNull(claimStatus);

        if (listenerReason is not null)
        {
            return ListenerText(listenerReason);
        }

        return claimStatus.State switch
        {
            TailscaleServeClaim.ClaimState.TailscalePortTaken => request is null
                ? "The Tailscale HTTPS port is used by something else. Choose another Tailscale HTTPS port, then restart Huddle."
                : $"Tailscale port {request.TailscalePort.ToString(CultureInfo.InvariantCulture)} is used by something else. Choose another Tailscale HTTPS port, then restart Huddle.",
            TailscaleServeClaim.ClaimState.ServeNeedsEnabling => claimStatus.EnableUrl is { Length: > 0 } url
                ? $"HTTPS is not enabled for your tailnet. Turn it on at {url}, then restart Huddle."
                : "HTTPS is not enabled for your tailnet. Turn it on in the Tailscale admin console, then restart Huddle.",
            TailscaleServeClaim.ClaimState.FunnelNeedsPassword => FunnelNeedsPassword,
            TailscaleServeClaim.ClaimState.FunnelPortNotAllowed => FunnelPortNotAllowed,
            TailscaleServeClaim.ClaimState.StartTimedOut => RouteTimedOut,
            _ => null,
        };
    }

    private static string ListenerText(string reason)
    {
        if (reason.Contains("needs gateway authentication", StringComparison.Ordinal))
        {
            return BindNeedsSignIn;
        }

        return reason.Contains("needs one fixed port", StringComparison.Ordinal) ? BindNeedsPort : reason;
    }

    private static string? LanUrl(GatewayOptions options, GatewayListener listener)
    {
        bool network = options.Bind.Equals("lan", StringComparison.OrdinalIgnoreCase) || options.Bind.Equals("tailnet", StringComparison.OrdinalIgnoreCase);
        if (!network || listener.AppEndpoints.Count == 0 || listener.AppEndpoints[0].Port <= 0)
        {
            return null;
        }

        // A LAN bind has no certificate, so the address is plain http by design; the setup-link rule allows a .local name.
        return new UriBuilder(Uri.UriSchemeHttp, Environment.MachineName.ToLowerInvariant() + ".local", listener.AppEndpoints[0].Port).Uri.GetLeftPart(UriPartial.Authority);
    }
}

namespace Agency.Huddle.App.Gateway;

/// <summary>The auto-approval table of Spec 6.7, as a pure function.</summary>
internal static class PairingPolicy
{
    /// <summary>Decides whether a handshake pairs without an approver, and with which scopes.</summary>
    /// <param name="input">The facts of the handshake.</param>
    public static Decision Decide(Input input)
    {
        ArgumentNullException.ThrowIfNull(input);

        IReadOnlyList<string> requested = GatewayScopes.Normalize(input.Requested);
        bool setupLink = input.Bootstrap == BootstrapTokenStore.Kind.SetupLink;

        if (setupLink)
        {
            return Approval(GatewayScopes.Limited, requested);
        }

        if (input.Ingress == IngressKind.Loopback && input.AutoApproveLocal && input.HostAndOriginLoopback && input.GatewayAuthPassed)
        {
            return new Decision(true, GatewayScopes.AdminSet, "loopback");
        }

        if (input.Bootstrap == BootstrapTokenStore.Kind.Admin)
        {
            return new Decision(true, GatewayScopes.AdminSet, "bootstrap");
        }

        if (IsVerifiedTailscale(input) && GatewayScopes.HoldsAll(GatewayScopes.Limited, requested))
        {
            return new Decision(true, requested, "tailscale");
        }

        return input.Ingress == IngressKind.TrustedProxy ? TrustedProxyDecision(input, requested) : new Decision(false, requested, null);
    }

    private static bool IsVerifiedTailscale(Input input)
        => input.Ingress == IngressKind.Managed && input.AllowTailscale && !input.IsFunnel && !string.IsNullOrEmpty(input.TailscaleLogin);

    private static Decision Approval(IReadOnlyList<string> limit, IReadOnlyList<string> requested)
    {
        IReadOnlyList<string> within = [.. requested.Where(scope => limit.Contains(scope, StringComparer.Ordinal))];
        return new Decision(false, within.Count == 0 ? limit : within, null);
    }

    private static Decision TrustedProxyDecision(Input input, IReadOnlyList<string> requested)
    {
        if (input.TrustedProxy is not { Enabled: true } rule)
        {
            return new Decision(false, requested, null);
        }

        IReadOnlyList<string> cap = GatewayScopes.Normalize(rule.Scopes);
        IReadOnlyList<string> granted = [.. requested.Where(scope => cap.Contains(scope, StringComparer.Ordinal))];
        return granted.Count == 0 ? new Decision(false, requested, null) : new Decision(true, granted, "trusted-proxy");
    }

    /// <summary>The facts the table reads.</summary>
    /// <param name="Ingress">How the request arrived.</param>
    /// <param name="HostAndOriginLoopback">Whether <c>Host</c> and <c>Origin</c> are loopback names.</param>
    /// <param name="AutoApproveLocal">The <c>AutoApproveLocal</c> kill switch.</param>
    /// <param name="GatewayAuthPassed">Whether gateway auth passed (credential, Tailscale identity or mode none).</param>
    /// <param name="TailscaleLogin">The verified Tailscale login, or <see langword="null"/>.</param>
    /// <param name="AllowTailscale">The <c>AllowTailscale</c> setting.</param>
    /// <param name="IsFunnel">Whether it came through Funnel.</param>
    /// <param name="Bootstrap">The kind of bootstrap token redeemed, or <see langword="null"/>.</param>
    /// <param name="TrustedProxy">The trusted-proxy auto-approval rule, or <see langword="null"/>.</param>
    /// <param name="Requested">The scopes asked for.</param>
    public sealed record Input(
        IngressKind Ingress,
        bool HostAndOriginLoopback,
        bool AutoApproveLocal,
        bool GatewayAuthPassed,
        string? TailscaleLogin,
        bool AllowTailscale,
        bool IsFunnel,
        BootstrapTokenStore.Kind? Bootstrap,
        TrustedProxyRule? TrustedProxy,
        IReadOnlyList<string> Requested);

    /// <summary><c>TrustedProxy:DeviceAutoApprove</c>.</summary>
    /// <param name="Enabled">Whether auto-approval is on.</param>
    /// <param name="Scopes">The cap.</param>
    public sealed record TrustedProxyRule(bool Enabled, IReadOnlyList<string> Scopes);

    /// <summary>The answer.</summary>
    /// <param name="AutoApprove">Whether to pair now.</param>
    /// <param name="Scopes">When approving, the scopes to grant; otherwise the most a request for this connection may be granted by an approver without raising it.</param>
    /// <param name="PairedVia">The <c>paired_via</c> value when approving; otherwise <see langword="null"/>.</param>
    public sealed record Decision(bool AutoApprove, IReadOnlyList<string> Scopes, string? PairedVia);
}

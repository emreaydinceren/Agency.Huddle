using Agency.Huddle.App.Gateway;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>Spec 6.7: the auto-approval table, one row at a time.</summary>
public sealed class PairingPolicyTests
{
    private static readonly IReadOnlyList<string> AskWriter = [GatewayScopes.Read, GatewayScopes.Write];

    private static PairingPolicy.Input Loopback(Func<PairingPolicy.Input, PairingPolicy.Input>? change = null)
    {
        PairingPolicy.Input input = new(IngressKind.Loopback, true, true, true, null, true, false, null, null, AskWriter);
        return change is null ? input : change(input);
    }

    private static void AssertAdmin(PairingPolicy.Decision decision, string via)
    {
        Assert.True(decision.AutoApprove);
        Assert.Equal(via, decision.PairedVia);
        Assert.True(GatewayScopes.Holds(decision.Scopes, GatewayScopes.Admin));
    }

    /// <summary>A loopback connection with gateway auth passed pairs automatically as admin, via loopback.</summary>
    [Fact]
    public void Loopback_AllConditionsMet_AutoApprovesAsAdmin()
    {
        AssertAdmin(PairingPolicy.Decide(Loopback()), "loopback");
    }

    /// <summary>The <c>AutoApproveLocal</c> kill switch turns the loopback row off.</summary>
    [Fact]
    public void Loopback_AutoApproveLocalOff_NeedsApproval()
    {
        Assert.False(PairingPolicy.Decide(Loopback(input => input with { AutoApproveLocal = false })).AutoApprove);
    }

    /// <summary>A loopback socket whose <c>Host</c> or <c>Origin</c> is not loopback (a tailnet name pointing at this machine) needs approval.</summary>
    [Fact]
    public void Loopback_HostOrOriginNotLoopback_NeedsApproval()
    {
        Assert.False(PairingPolicy.Decide(Loopback(input => input with { HostAndOriginLoopback = false })).AutoApprove);
    }

    /// <summary>Loopback without gateway auth passed needs approval.</summary>
    [Fact]
    public void Loopback_GatewayAuthNotPassed_NeedsApproval()
    {
        Assert.False(PairingPolicy.Decide(Loopback(input => input with { GatewayAuthPassed = false })).AutoApprove);
    }

    /// <summary>A bootstrap token from the launcher pairs as admin, via bootstrap, even from a non-loopback ingress.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AdminBootstrap_AutoApprovesAsAdmin(bool direct)
    {
        PairingPolicy.Decision decision = PairingPolicy.Decide(Loopback(input => input with
        {
            Ingress = direct ? IngressKind.Direct : IngressKind.Loopback,
            AutoApproveLocal = false,
            Bootstrap = BootstrapTokenStore.Kind.Admin,
        }));

        AssertAdmin(decision, "bootstrap");
    }

    /// <summary>A setup link never pairs automatically and its ceiling is the limited scopes.</summary>
    [Fact]
    public void SetupLink_NeverAutoApproves_AndIsLimited()
    {
        PairingPolicy.Decision decision = PairingPolicy.Decide(Loopback(input => input with
        {
            Ingress = IngressKind.Direct,
            Bootstrap = BootstrapTokenStore.Kind.SetupLink,
            Requested = [GatewayScopes.Admin],
        }));

        Assert.False(decision.AutoApprove);
        Assert.False(GatewayScopes.Holds(decision.Scopes, GatewayScopes.Admin));
    }

    /// <summary>A setup link redeemed on a loopback connection is still not auto-approved.</summary>
    [Fact]
    public void SetupLink_OnLoopback_StillNeedsApproval()
    {
        PairingPolicy.Decision decision = PairingPolicy.Decide(Loopback(input => input with { Bootstrap = BootstrapTokenStore.Kind.SetupLink }));

        Assert.False(decision.AutoApprove);
    }

    /// <summary>A verified Tailscale identity over the managed listener with <c>AllowTailscale</c> pairs automatically, limited, via tailscale.</summary>
    [Fact]
    public void Tailscale_VerifiedIdentity_AutoApprovesLimited()
    {
        PairingPolicy.Decision decision = PairingPolicy.Decide(Loopback(input => input with
        {
            Ingress = IngressKind.Managed,
            TailscaleLogin = "ada@example.com",
        }));

        Assert.True(decision.AutoApprove);
        Assert.Equal("tailscale", decision.PairedVia);
        Assert.False(GatewayScopes.Holds(decision.Scopes, GatewayScopes.Admin));
        Assert.True(GatewayScopes.HoldsAll(decision.Scopes, [GatewayScopes.Read, GatewayScopes.Write]));
    }

    /// <summary>A Tailscale device asking for admin is not auto-approved: that is a Request admin for a person to approve.</summary>
    [Fact]
    public void Tailscale_RequestingAdmin_NeedsApproval()
    {
        PairingPolicy.Decision decision = PairingPolicy.Decide(Loopback(input => input with
        {
            Ingress = IngressKind.Managed,
            TailscaleLogin = "ada@example.com",
            Requested = [GatewayScopes.Admin],
        }));

        Assert.False(decision.AutoApprove);
    }

    /// <summary>With <c>AllowTailscale</c> off, or on Funnel, or without a login, a managed connection needs approval.</summary>
    [Fact]
    public void Tailscale_NotAllowedFunnelOrNoLogin_NeedsApproval()
    {
        PairingPolicy.Input managed = Loopback(input => input with { Ingress = IngressKind.Managed, TailscaleLogin = "ada@example.com" });

        Assert.False(PairingPolicy.Decide(managed with { AllowTailscale = false }).AutoApprove);
        Assert.False(PairingPolicy.Decide(managed with { IsFunnel = true, TailscaleLogin = null }).AutoApprove);
        Assert.False(PairingPolicy.Decide(managed with { TailscaleLogin = null }).AutoApprove);
    }

    /// <summary>LAN, tailnet and custom binds (a direct non-loopback socket) need approval, including a same-host address that is not loopback.</summary>
    [Fact]
    public void Direct_NonLoopback_NeedsApproval()
    {
        PairingPolicy.Decision decision = PairingPolicy.Decide(Loopback(input => input with { Ingress = IngressKind.Direct }));

        Assert.False(decision.AutoApprove);
        Assert.Null(decision.PairedVia);
    }

    /// <summary>Funnel traffic needs approval whatever else is true.</summary>
    [Fact]
    public void Funnel_NeedsApproval()
    {
        PairingPolicy.Decision decision = PairingPolicy.Decide(Loopback(input => input with { Ingress = IngressKind.Managed, IsFunnel = true }));

        Assert.False(decision.AutoApprove);
    }

    /// <summary>A trusted proxy pairs automatically only with the opt-in, capped by its scopes.</summary>
    [Fact]
    public void TrustedProxy_AutoApprovesOnlyWithOptInAndIsCapped()
    {
        PairingPolicy.Input proxy = Loopback(input => input with
        {
            Ingress = IngressKind.TrustedProxy,
            Requested = [GatewayScopes.Admin, GatewayScopes.Write],
            TrustedProxy = new PairingPolicy.TrustedProxyRule(true, [GatewayScopes.Read, GatewayScopes.Write]),
        });

        PairingPolicy.Decision enabled = PairingPolicy.Decide(proxy);
        PairingPolicy.Decision disabled = PairingPolicy.Decide(proxy with { TrustedProxy = new PairingPolicy.TrustedProxyRule(false, [GatewayScopes.Read]) });
        PairingPolicy.Decision unset = PairingPolicy.Decide(proxy with { TrustedProxy = null });

        Assert.True(enabled.AutoApprove);
        Assert.Equal("trusted-proxy", enabled.PairedVia);
        Assert.False(GatewayScopes.Holds(enabled.Scopes, GatewayScopes.Admin));
        Assert.False(disabled.AutoApprove);
        Assert.False(unset.AutoApprove);
    }
}

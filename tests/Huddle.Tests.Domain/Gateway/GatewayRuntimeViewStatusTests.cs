using Agency.Huddle.App.Gateway;

namespace Agency.Huddle.Tests;

/// <summary>The plain sentences the Gateway card shows when the gateway is not fully up (wireframe G10a, Stage E1): each says what is wrong and what to do next.</summary>
public sealed class GatewayRuntimeViewStatusTests
{
    private static readonly TailscaleServeClaim.ClaimStatus Idle = new(TailscaleServeClaim.ClaimState.Idle);

    private static TailscaleServeClaim.ClaimRequest Request(int port = 5101) => new(false, port, 41233, GatewayAuthMode.Token);

    /// <summary>With no address to pair against, the setup link offer says why in plain words and what to do.</summary>
    [Fact]
    public void MintSetupLink_NoAddress_SaysWhatToDo()
    {
        using Gateway.GatewayAdminHarness harness = new();
        harness.View.PublicUrl = null;

        GatewayAdminService.SetupLinkOffer offer = harness.Service.MintSetupLink(Gateway.GatewayAdminHarness.Admin());

        Assert.Equal("There is no address to pair against yet. Turn on Tailscale, or set a LAN bind and a public origin.", offer.Reason);
    }

    /// <summary>Nothing wrong gives no reason at all.</summary>
    [Fact]
    public void ReasonOf_NothingWrong_IsNull() => Assert.Null(GatewayRuntimeView.ReasonOf(null, Idle, null));

    /// <summary>A Tailscale port that belongs to something else names the port and says to choose another one.</summary>
    [Fact]
    public void ReasonOf_TailscalePortTaken_NamesThePortAndSaysWhatToDo() =>
        Assert.Equal(
            "Tailscale port 5101 is used by something else. Choose another Tailscale HTTPS port, then restart Huddle.",
            GatewayRuntimeView.ReasonOf(null, new TailscaleServeClaim.ClaimStatus(TailscaleServeClaim.ClaimState.TailscalePortTaken), Request()));

    /// <summary>HTTPS not enabled for the tailnet shows Tailscale's link.</summary>
    [Fact]
    public void ReasonOf_HttpsNotEnabled_ShowsTailscalesLink() =>
        Assert.Equal(
            "HTTPS is not enabled for your tailnet. Turn it on at https://login.tailscale.com/f/serve?node=n123, then restart Huddle.",
            GatewayRuntimeView.ReasonOf(null, new TailscaleServeClaim.ClaimStatus(TailscaleServeClaim.ClaimState.ServeNeedsEnabling, "https://login.tailscale.com/f/serve?node=n123"), Request()));

    /// <summary>A non-loopback bind with no fixed port stays on this computer and says to set a port.</summary>
    [Fact]
    public void ReasonOf_NetworkBindWithoutAPort_SaysToSetAPort() =>
        Assert.Equal(
            "Huddle stayed on this computer because no port is set. Set a port, then restart Huddle.",
            GatewayRuntimeView.ReasonOf("A non-loopback bind needs one fixed port; set Team:Gateway:Port. The app stays loopback-only.", Idle, null));

    /// <summary>A bind refused because sign-in is off says to choose token or password sign-in.</summary>
    [Fact]
    public void ReasonOf_BindRefusedWithoutAuth_SaysToChooseSignIn() =>
        Assert.Equal(
            "Huddle stayed on this computer because sign-in is off. Choose token or password sign-in, then restart Huddle.",
            GatewayRuntimeView.ReasonOf("A non-loopback bind needs gateway authentication, and Auth:Mode is none; the app stays loopback-only.", Idle, null));

    /// <summary>A listener reason wins over a Tailscale claim problem, and an unknown reason is shown as it is.</summary>
    [Fact]
    public void ReasonOf_ListenerReason_WinsAndUnknownTextPassesThrough() =>
        Assert.Equal(
            "Port 5101 is already in use; the app stays loopback-only.",
            GatewayRuntimeView.ReasonOf("Port 5101 is already in use; the app stays loopback-only.", new TailscaleServeClaim.ClaimStatus(TailscaleServeClaim.ClaimState.TailscalePortTaken), Request()));
}

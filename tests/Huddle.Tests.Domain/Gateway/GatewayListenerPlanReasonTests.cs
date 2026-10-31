using System.Net;
using Agency.Huddle.App.Gateway;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>The reasons the listener plan records when a non-loopback bind is refused (Spec 6.2, D-3): the text the Settings page and the log show.</summary>
public sealed class GatewayListenerPlanReasonTests
{
    private sealed class BusyProbe(int busy) : GatewayListenerPlan.IProbe
    {
        public bool IsPortFree(int port) => port != busy;

        public IPAddress? TailnetAddress() => null;
    }

    private static GatewayListenerPlan.Input Lan(GatewayAuthMode mode = GatewayAuthMode.Token, int? port = null) =>
        new("http://localhost:5101", null, null, "lan", null, port, mode, false, false, null, false);

    /// <summary>Auth mode none names the reason.</summary>
    [Fact]
    public void Create_LanWithAuthNone_NamesAuthenticationAsTheReason()
    {
        GatewayListenerPlan.Result result = GatewayListenerPlan.Create(Lan(GatewayAuthMode.None), new BusyProbe(0));

        Assert.Equal("A non-loopback bind needs gateway authentication, and Auth:Mode is none; the app stays loopback-only.", result.Reason);
    }

    /// <summary>No single fixed port names the configuration key to set.</summary>
    [Fact]
    public void Create_LanWithoutAFixedPort_NamesThePortKeyAsTheReason()
    {
        GatewayListenerPlan.Input input = Lan() with { Urls = "http://localhost:5101;http://localhost:5102" };

        GatewayListenerPlan.Result result = GatewayListenerPlan.Create(input, new BusyProbe(0));

        Assert.Equal("A non-loopback bind needs one fixed port; set Team:Gateway:Port. The app stays loopback-only.", result.Reason);
    }

    /// <summary>A busy port names the port in the reason.</summary>
    [Fact]
    public void Create_LanWithABusyPort_NamesThePortAsTheReason()
    {
        GatewayListenerPlan.Result result = GatewayListenerPlan.Create(Lan(), new BusyProbe(5101));

        Assert.Equal("Port 5101 is already in use; the app stays loopback-only.", result.Reason);
    }
}

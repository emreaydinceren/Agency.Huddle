using Agency.Huddle.App.Gateway;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>Funnel always needs the password (correction D-13) and a loopback launcher may use bootstrap and device tokens in trusted-proxy mode (D-14).</summary>
public sealed class GatewayFunnelAndProxyConnectTests
{
    private static GatewayConnectHarness PasswordMode() => new(config =>
    {
        config["Team:Gateway:Auth:Mode"] = "password";
        config["Team:Gateway:Auth:Password"] = GatewayConnectHarness.Password;
    });

    private static GatewayConnectHarness ProxyMode() => new(config => config["Team:Gateway:Auth:Mode"] = "trusted-proxy");

    private static string PairOverLoopback(GatewayConnectHarness harness, TestDevice device)
    {
        ConnectOutcome paired = harness.Connect(device, new ConnectAuth(Password: GatewayConnectHarness.Password), GatewayConnectHarness.Loopback);
        Assert.Equal(ConnectOutcome.Kind.Connected, paired.Result);
        Assert.NotNull(paired.DeviceToken);
        return paired.DeviceToken;
    }

    /// <summary>A Funnel connect with a valid device token but no password is refused: the token does not replace the password (D-13).</summary>
    [Fact]
    public void Connect_FunnelWithDeviceTokenAndNoPassword_IsPasswordMissing()
    {
        using GatewayConnectHarness harness = PasswordMode();
        TestDevice device = new();
        string deviceToken = PairOverLoopback(harness, device);

        ConnectOutcome outcome = harness.Connect(device, new ConnectAuth(DeviceToken: deviceToken), GatewayConnectHarness.Funnel);

        Assert.Equal(ConnectOutcome.Kind.Failed, outcome.Result);
        Assert.Equal(ConnectOutcome.PasswordMissing, outcome.Code);
    }

    /// <summary>A Funnel connect with a valid device token and the password is accepted.</summary>
    [Fact]
    public void Connect_FunnelWithDeviceTokenAndPassword_IsConnected()
    {
        using GatewayConnectHarness harness = PasswordMode();
        TestDevice device = new();
        string deviceToken = PairOverLoopback(harness, device);

        ConnectOutcome outcome = harness.Connect(
            device,
            new ConnectAuth(Password: GatewayConnectHarness.Password, DeviceToken: deviceToken),
            GatewayConnectHarness.Funnel);

        Assert.Equal(ConnectOutcome.Kind.Connected, outcome.Result);
    }

    /// <summary>A Funnel connect with only a bootstrap token is refused with the password code (D-13).</summary>
    [Fact]
    public void Connect_FunnelWithBootstrapAndNoPassword_IsPasswordMissing()
    {
        using GatewayConnectHarness harness = PasswordMode();
        string bootstrap = harness.Bootstrap.Issue(BootstrapTokenStore.Kind.Admin);

        ConnectOutcome outcome = harness.Connect(new TestDevice(), new ConnectAuth(BootstrapToken: bootstrap), GatewayConnectHarness.Funnel);

        Assert.Equal(ConnectOutcome.Kind.Failed, outcome.Result);
        Assert.Equal(ConnectOutcome.PasswordMissing, outcome.Code);
    }

    /// <summary>In trusted-proxy mode a loopback bootstrap token connects, and the device token it earns connects again over loopback (D-14).</summary>
    [Fact]
    public void Connect_TrustedProxyModeOverLoopback_BootstrapAndDeviceTokenAreAccepted()
    {
        using GatewayConnectHarness harness = ProxyMode();
        TestDevice device = new();
        string bootstrap = harness.Bootstrap.Issue(BootstrapTokenStore.Kind.Admin);

        ConnectOutcome first = harness.Connect(device, new ConnectAuth(BootstrapToken: bootstrap), GatewayConnectHarness.Loopback);
        ConnectOutcome second = harness.Connect(device, new ConnectAuth(DeviceToken: first.DeviceToken), GatewayConnectHarness.Loopback);

        Assert.Equal(ConnectOutcome.Kind.Connected, first.Result);
        Assert.Equal(ConnectOutcome.Kind.Connected, second.Result);
    }
}

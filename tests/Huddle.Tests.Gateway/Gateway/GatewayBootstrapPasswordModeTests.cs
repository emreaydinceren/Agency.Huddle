using Agency.Huddle.App.Gateway;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>
/// Bootstrap tokens in <c>Auth:Mode = password</c> (facts decision 10.4): the desktop launcher always presents one, whatever the mode,
/// so a valid token connects without the password and an invalid one is refused with its own code.
/// </summary>
public sealed class GatewayBootstrapPasswordModeTests
{
    private static GatewayConnectHarness PasswordMode() => new(config =>
    {
        config["Team:Gateway:Auth:Mode"] = "password";
        config["Team:Gateway:Auth:Password"] = GatewayConnectHarness.Password;
    });

    /// <summary>An admin bootstrap token pairs the redeeming device as admin in password mode, with no password presented.</summary>
    [Fact]
    public void Connect_AdminBootstrapInPasswordMode_PairsAsAdmin()
    {
        using GatewayConnectHarness harness = PasswordMode();
        string bootstrap = harness.Bootstrap.Issue(BootstrapTokenStore.Kind.Admin);

        ConnectOutcome outcome = harness.Connect(new TestDevice(), new ConnectAuth(BootstrapToken: bootstrap), GatewayConnectHarness.Direct());

        Assert.Equal(ConnectOutcome.Kind.Connected, outcome.Result);
        Assert.Equal("bootstrap", outcome.PairedVia);
        Assert.True(GatewayScopes.Holds(outcome.Scopes, GatewayScopes.Admin));
        Assert.False(string.IsNullOrEmpty(outcome.DeviceToken));
    }

    /// <summary>A bootstrap token that was never issued is refused in password mode with the bootstrap code, not the password one.</summary>
    [Fact]
    public void Connect_UnknownBootstrapInPasswordMode_IsBootstrapTokenInvalid()
    {
        using GatewayConnectHarness harness = PasswordMode();

        ConnectOutcome outcome = harness.Connect(new TestDevice(), new ConnectAuth(BootstrapToken: "never-issued"), GatewayConnectHarness.Direct());

        Assert.Equal(ConnectOutcome.Kind.Failed, outcome.Result);
        Assert.Equal(401, outcome.HttpStatus);
        Assert.Equal(ConnectOutcome.BootstrapTokenInvalid, outcome.Code);
    }
}

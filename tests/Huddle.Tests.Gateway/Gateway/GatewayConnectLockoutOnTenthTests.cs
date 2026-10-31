using Agency.Huddle.App.Gateway;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>Manual-run fix (REMOTE-06): the failure that triggers the lockout is itself answered with the lockout.</summary>
public sealed class GatewayConnectLockoutOnTenthTests
{
    /// <summary>Nine wrong tokens are 401; the tenth is the one that locks the address, so it answers 429 <c>AUTH_RATE_LIMITED</c> with a retry time.</summary>
    [Fact]
    public void Connect_TenthWrongToken_AnswersTheLockout()
    {
        using GatewayConnectHarness harness = new();
        ConnectAuth wrong = new(Token: "wrong");
        for (int attempt = 0; attempt < 9; attempt++)
        {
            ConnectOutcome failed = harness.Connect(new TestDevice(), wrong, GatewayConnectHarness.Direct());
            Assert.Equal(401, failed.HttpStatus);
            Assert.Equal(ConnectOutcome.TokenMismatch, failed.Code);
        }

        ConnectOutcome tenth = harness.Connect(new TestDevice(), wrong, GatewayConnectHarness.Direct());

        Assert.Equal(ConnectOutcome.Kind.Failed, tenth.Result);
        Assert.Equal(429, tenth.HttpStatus);
        Assert.Equal(AuthRateLimiter.ErrorCode, tenth.Code);
        Assert.True(tenth.RetryAfterMs > 0);
    }
}

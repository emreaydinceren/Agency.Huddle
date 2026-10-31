namespace Agency.Huddle.Tests.Acp.Sessions;

/// <summary>
/// Pins the Session settings ranges and refusal texts (LR-R13, Spec §3.3, Settled texts T6 and T7).
/// Each check is a pure function: a valid value returns null, an invalid one returns the refusal text.
/// </summary>
public sealed class SessionSettingsRulesTests
{
    private const string MessagesRefusal = "Enter a number from 1 to 2,000.";
    private const string TokensRefusal = "Enter a number from 1,000 to 150,000.";

    /// <summary>The Message cap accepts 1 through 2,000 and refuses everything outside that range with T6.</summary>
    [Theory]
    [InlineData(0, MessagesRefusal)]
    [InlineData(1, null)]
    [InlineData(2000, null)]
    [InlineData(2001, MessagesRefusal)]
    [InlineData(-5, MessagesRefusal)]
    public void CheckMessages_Value_ReturnsNullOrRefusal(int value, string? expected)
    {
        Assert.Equal(expected, Agency.Huddle.App.Acp.Sessions.SessionSettingsRules.CheckMessages(value));
    }

    /// <summary>The token budget accepts 1,000 through 150,000 and refuses everything outside that range with T7.</summary>
    [Theory]
    [InlineData(999, TokensRefusal)]
    [InlineData(1000, null)]
    [InlineData(150000, null)]
    [InlineData(150001, TokensRefusal)]
    public void CheckTokens_Value_ReturnsNullOrRefusal(int value, string? expected)
    {
        Assert.Equal(expected, Agency.Huddle.App.Acp.Sessions.SessionSettingsRules.CheckTokens(value));
    }
}

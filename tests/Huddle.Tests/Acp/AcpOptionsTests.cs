using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Acp;

/// <summary>Pins <see cref="AcpOptions"/>' Room Sessions keys (RS §6.14) and their defaults.</summary>
public sealed class AcpOptionsTests
{
    /// <summary><see cref="AcpOptions.SessionIdleMinutes"/> defaults to 30.</summary>
    [Fact]
    public void SessionIdleMinutes_Default_Is30()
    {
        var options = new AcpOptions();

        Assert.Equal(30, options.SessionIdleMinutes);
    }

    /// <summary><see cref="AcpOptions.MaxLiveSessions"/> defaults to 3.</summary>
    [Fact]
    public void MaxLiveSessions_Default_Is3()
    {
        var options = new AcpOptions();

        Assert.Equal(3, options.MaxLiveSessions);
    }

    /// <summary><see cref="AcpOptions.MaxConcurrentTurns"/> defaults to 1 — today's serial behaviour.</summary>
    [Fact]
    public void MaxConcurrentTurns_Default_Is1()
    {
        var options = new AcpOptions();

        Assert.Equal(1, options.MaxConcurrentTurns);
    }

    /// <summary><see cref="AcpOptions.TranscriptCatchUpMessages"/> defaults to 20.</summary>
    [Fact]
    public void TranscriptCatchUpMessages_Default_Is20()
    {
        var options = new AcpOptions();

        Assert.Equal(20, options.TranscriptCatchUpMessages);
    }

    /// <summary>
    /// <see cref="AcpOptions.AdvertiseElicitation"/> ships on: Claude's built-in <c>AskUserQuestion</c>, the
    /// refusal-fallback dialog and MCP forms reach the Human unless an operator turns it off (elicitation
    /// bridge E-1b, the last step of its delivery).
    /// </summary>
    [Fact]
    public void AcpOptions_AdvertiseElicitation_DefaultsToTrue()
    {
        var options = new AcpOptions();

        Assert.True(options.AdvertiseElicitation);
    }
}

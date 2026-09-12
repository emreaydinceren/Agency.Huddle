namespace Agency.Huddle.Acp.Tests.Abstractions;

using Agency.Huddle.Acp.Abstractions;
using Xunit;

public sealed class AgentEventTests
{
    [Fact]
    public void TurnCompleted_CarriesStopReason()
    {
        TurnCompleted turnCompleted = new TurnCompleted("session-1", StopReason.MaxTokens);

        Assert.Equal("session-1", turnCompleted.SessionId);
        Assert.Equal(StopReason.MaxTokens, turnCompleted.StopReason);
    }

    [Fact]
    public void MessageChunks_AreValueEqual()
    {
        MessageChunk first = new MessageChunk("s", "a");
        MessageChunk second = new MessageChunk("s", "a");

        Assert.Equal(first, second);
        Assert.True(first == second);
    }
}

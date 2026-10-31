using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.Acp.Sessions;

/// <summary>
/// Pins <see cref="Agency.Huddle.App.Acp.Sessions.TokenEstimate"/>: the three-characters-per-token measure that the context trim and the
/// fresh-start note share (LR-R8, correction C5). One Message costs
/// <c>ceil((SenderName.Length + 2 + Text.Length) / 3)</c> tokens.
/// </summary>
public sealed class TokenEstimateTests
{
    /// <summary>Each character count rounds up to whole tokens at three characters per token.</summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    [InlineData(300, 100)]
    public void OfChars_RoundsUpAtThreeCharactersPerToken(int chars, int expected)
    {
        int actual = Agency.Huddle.App.Acp.Sessions.TokenEstimate.OfChars(chars);

        Assert.Equal(expected, actual);
    }

    /// <summary>A negative character count is not a length, so it is rejected rather than estimated.</summary>
    [Fact]
    public void OfChars_NegativeCount_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(static () => Agency.Huddle.App.Acp.Sessions.TokenEstimate.OfChars(-1));
    }

    /// <summary>A Message costs its sender name, two separator characters and its text, rounded up: "Nova" plus "hello" is 11 characters, so 4 tokens.</summary>
    [Fact]
    public void OfMessage_CountsSenderNameSeparatorAndText()
    {
        DateTimeOffset timestamp = new(2026, 10, 30, 9, 0, 0, TimeSpan.Zero);
        ChatMessage message = new("m1", timestamp, "agent-nova", "Nova", "hello");

        int actual = Agency.Huddle.App.Acp.Sessions.TokenEstimate.OfMessage(message);

        Assert.Equal(4, actual);
    }
}

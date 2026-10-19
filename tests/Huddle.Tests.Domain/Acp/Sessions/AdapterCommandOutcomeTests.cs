using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp.Sessions;

namespace Agency.Huddle.Tests.Acp.Sessions;

/// <summary>
/// Pins <see cref="AdapterCommandOutcome"/>: the fixed line a Teammate posts to say what its Adapter
/// command did. An Adapter's <c>/compact</c> sends no text of its own, so without this line a Room could
/// not tell a finished compaction from a failed one (Commands spec, section 8.3).
/// </summary>
public sealed class AdapterCommandOutcomeTests
{
    private const string Figures = """{"trigger":"manual","preTokens":50624,"postTokens":2964,"durationMs":9453}""";

    /// <summary>The wire sample observed from <c>claude-agent-acp</c> reads as a compaction, with thousands separators and whole seconds.</summary>
    [Fact]
    public void Describe_TheObservedCompactFigures_ReportsTheCompaction()
    {
        string? text = AdapterCommandOutcome.Describe("compact", StopReason.EndTurn, Figures, toolCallFailed: false);

        Assert.Equal("Compacted my conversation: 50,624 → 2,964 tokens in 9 s.", text);
    }

    /// <summary>A sub-second compaction still says one second, never zero.</summary>
    [Theory]
    [InlineData(200, "Compacted my conversation: 1,000 → 100 tokens in 1 s.")]
    [InlineData(1499, "Compacted my conversation: 1,000 → 100 tokens in 1 s.")]
    [InlineData(1500, "Compacted my conversation: 1,000 → 100 tokens in 2 s.")]
    [InlineData(60000, "Compacted my conversation: 1,000 → 100 tokens in 60 s.")]
    public void Describe_Duration_IsWholeSecondsOfAtLeastOne(int durationMs, string expected)
    {
        string json = $$"""{"preTokens":1000,"postTokens":100,"durationMs":{{durationMs}}}""";

        string? text = AdapterCommandOutcome.Describe("compact", StopReason.EndTurn, json, toolCallFailed: false);

        Assert.Equal(expected, text);
    }

    /// <summary>With no duration reported the line simply omits it.</summary>
    [Fact]
    public void Describe_NoDuration_OmitsIt()
    {
        string? text = AdapterCommandOutcome.Describe("compact", StopReason.EndTurn, """{"preTokens":1000,"postTokens":100}""", toolCallFailed: false);

        Assert.Equal("Compacted my conversation: 1,000 → 100 tokens.", text);
    }

    /// <summary>Figures that grew are reported plainly, not as a compaction.</summary>
    [Fact]
    public void Describe_PostGreaterThanPre_IsNotCalledACompaction()
    {
        string? text = AdapterCommandOutcome.Describe("compact", StopReason.EndTurn, """{"preTokens":1000,"postTokens":2000,"durationMs":3000}""", toolCallFailed: false);

        Assert.Equal("Ran /compact: 1,000 → 2,000 tokens.", text);
    }

    /// <summary>Only <c>compact</c> is worded as a compaction; another command with the same figures is named.</summary>
    [Fact]
    public void Describe_AnotherCommandWithFigures_NamesTheCommand()
    {
        string? text = AdapterCommandOutcome.Describe("summarize", StopReason.EndTurn, Figures, toolCallFailed: false);

        Assert.Equal("Ran /summarize: 50,624 → 2,964 tokens.", text);
    }

    /// <summary>No figures at all gives the plain line.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("{}")]
    [InlineData("""{"preTokens":"many","postTokens":2}""")]
    [InlineData("""{"preTokens":10}""")]
    [InlineData("""{"preTokens":-5,"postTokens":2}""")]
    public void Describe_MissingOrMalformedFigures_IsThePlainLine(string? json)
    {
        string? text = AdapterCommandOutcome.Describe("compact", StopReason.EndTurn, json, toolCallFailed: false);

        Assert.Equal("Ran /compact.", text);
    }

    /// <summary>A tool call that failed wins over any figures: the command did not finish.</summary>
    [Fact]
    public void Describe_AFailedToolCall_SaysItDidNotFinish()
    {
        string? text = AdapterCommandOutcome.Describe("compact", StopReason.EndTurn, Figures, toolCallFailed: true);

        Assert.Equal("/compact did not finish.", text);
    }

    /// <summary>A stopped, refused or cut-off Turn posts nothing: the Human pressed Stop, or the existing incomplete-stop reporting applies.</summary>
    [Theory]
    [InlineData(StopReason.Cancelled)]
    [InlineData(StopReason.Refusal)]
    [InlineData(StopReason.MaxTokens)]
    [InlineData(StopReason.MaxTurnRequests)]
    public void Describe_AnyStopButEndTurn_PostsNothing(StopReason reason)
    {
        string? text = AdapterCommandOutcome.Describe("compact", reason, Figures, toolCallFailed: false);

        Assert.Null(text);
    }

    /// <summary>The thousands separator does not follow the machine's culture: the line is read by every Teammate in the Room.</summary>
    [Fact]
    public void Describe_UsesInvariantCulture()
    {
        System.Globalization.CultureInfo previous = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
        try
        {
            string? text = AdapterCommandOutcome.Describe("compact", StopReason.EndTurn, Figures, toolCallFailed: false);

            Assert.Equal("Compacted my conversation: 50,624 → 2,964 tokens in 9 s.", text);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }
}

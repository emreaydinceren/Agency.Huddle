using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;

namespace Agency.Huddle.Tests.Acp.Sessions;

/// <summary>
/// Pins how <see cref="TurnPromptFramer.BuildPrompt"/> renders a trimmed Transcript block (LR-R12): the
/// omitted line, and the own-omitted line that follows it only when some of the left-out Messages
/// were the Agent's own.
/// </summary>
public sealed class TranscriptBlockTests
{
    private const string OmittedSeven = "…7 earlier Messages are not shown.";
    private const string OwnThree = "…3 of those are your own.";

    /// <summary>A trim of 7 with 3 of them the Agent's own prints the own line directly after the omitted line.</summary>
    [Fact]
    public void BuildPrompt_OmittedWithOwn_OwnLineFollowsOmittedLine()
    {
        string[] lines = RenderLines(omitted: 7, omittedOwn: 3);

        int index = Array.IndexOf(lines, OmittedSeven);
        Assert.True(index >= 0, "The omitted line is missing.");
        Assert.Equal(OwnThree, lines[index + 1]);
    }

    /// <summary>With no own Messages among the omitted ones the prompt has no own line and is otherwise unchanged.</summary>
    [Fact]
    public void BuildPrompt_OmittedWithoutOwn_HasNoOwnLine()
    {
        string[] lines = RenderLines(omitted: 7, omittedOwn: 0);
        string[] withoutField = RenderLinesWithoutOwnField(omitted: 7);

        Assert.Contains(OmittedSeven, lines);
        Assert.DoesNotContain(lines, line => line.EndsWith("of those are your own.", StringComparison.Ordinal));
        Assert.Equal(withoutField.Length, lines.Length);
    }

    /// <summary>Nothing omitted and nothing own prints neither line.</summary>
    [Fact]
    public void BuildPrompt_NothingOmitted_HasNeitherLine()
    {
        string[] lines = RenderLines(omitted: 0, omittedOwn: 0);

        Assert.DoesNotContain(lines, line => line.Contains("earlier Messages are not shown", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.EndsWith("of those are your own.", StringComparison.Ordinal));
    }

    private static string[] RenderLines(int omitted, int omittedOwn) =>
        Render(new TranscriptCatchUp(Resumed: false, Messages: OneMessage(), Omitted: omitted, OmittedOwn: omittedOwn));

    private static string[] RenderLinesWithoutOwnField(int omitted) =>
        Render(new TranscriptCatchUp(Resumed: false, Messages: OneMessage(), Omitted: omitted));

    private static string[] Render(TranscriptCatchUp transcript)
    {
        WorkItem item = new WorkItem("01J8PORTO", "Porto trip", "Human", "go with option 2", []) with { Transcript = transcript };
        string prompt = TurnPromptFramer.BuildPrompt(item, new FakePromptSource());
        return prompt.Split('\n', StringSplitOptions.TrimEntries);
    }

    private static ChatMessage[] OneMessage() =>
        [new ChatMessage("m1", DateTimeOffset.UtcNow, "hu-1", "Human", "Three options, please.")];
}

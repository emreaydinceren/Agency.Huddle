using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Agency.Huddle.Tests.Ui.Library;

/// <summary>
/// Behaviour tests for the Library editor's Markdown toolbar commands (<c>markdown-commands.js</c>, Spec §6.7). The
/// transforms are pure text-in, changes-out functions, so <c>markdown-commands.harness.mjs</c> runs them under Node
/// over small documents and these tests assert on the resulting text: they pin what the commands do, not what their
/// source says. CI installs Node for the other Node-backed tests (<c>agents/CIPipeline.md</c>); a machine without
/// <c>node</c> on PATH fails these as it fails those.
/// </summary>
public sealed class MarkdownCommandsScriptTests
{
    /// <summary>One harness run shared by every test in the class: the scenarios are independent.</summary>
    private static readonly Lazy<Task<JsonElement>> Results = new(() => RunHarnessAsync(CancellationToken.None));

    /// <summary>A plain line becomes a <c>## </c> heading.</summary>
    [Fact]
    public async Task Heading_PlainLine_GetsLevelTwo() =>
        Assert.Equal("## Platform", await TextAsync("headingPlain"));

    /// <summary>A line that is already a heading of another level is set to <c>## </c>, never prefixed a second time (<c>## # Title</c>).</summary>
    [Theory]
    [InlineData("headingH1", "## Platform architecture")]
    [InlineData("headingH3", "## Platform")]
    [InlineData("headingH6", "## Platform")]
    public async Task Heading_OtherLevel_IsSetToLevelTwo(string scenario, string expected) =>
        Assert.Equal(expected, await TextAsync(scenario));

    /// <summary>A line that is already a <c>## </c> heading loses the prefix (toggle off).</summary>
    [Fact]
    public async Task Heading_LevelTwo_TogglesOff() =>
        Assert.Equal("Platform", await TextAsync("headingH2Off"));

    /// <summary>Across several lines each line follows the same rule on its own, in one result.</summary>
    [Fact]
    public async Task Heading_SeveralLines_AppliesTheRulePerLine() =>
        Assert.Equal("## a\nb\n## c", await TextAsync("headingMulti"));

    /// <summary><c>#hashtag</c> has no space after the hashes, so it is not a heading: it gets the prefix and keeps its text.</summary>
    [Fact]
    public async Task Heading_HashtagWithoutSpace_IsNotAHeading() =>
        Assert.Equal("## #hashtag", await TextAsync("headingHashtagIsNotAHeading"));

    /// <summary>A caret on the first character of a document that starts with a blank line prefixes that blank line, not the next one.</summary>
    [Fact]
    public async Task Heading_CaretAtStartOfDocument_PrefixesTheFirstLine() =>
        Assert.Equal("## \na", await TextAsync("headingLeadingBlankLine"));

    /// <summary>Bold wraps the selection in <c>**</c> and keeps the same text selected.</summary>
    [Fact]
    public async Task Bold_Selection_IsWrappedAndStaysSelected()
    {
        Assert.Equal("**hello** world", await TextAsync("boldWrap"));
        Assert.Equal([[2, 7]], await SelectionAsync("boldWrap"));
    }

    /// <summary>Bold on a selection already wrapped removes the markers, whether they sit outside the selection or inside it.</summary>
    [Theory]
    [InlineData("boldOffOutside")]
    [InlineData("boldOffInside")]
    public async Task Bold_AlreadyWrapped_TogglesOff(string scenario)
    {
        Assert.Equal("hello world", await TextAsync(scenario));
        Assert.Equal([[0, 5]], await SelectionAsync(scenario));
    }

    /// <summary>A caret between an empty <c>****</c> pair removes the pair; a caret elsewhere inserts one with the caret inside.</summary>
    [Fact]
    public async Task Bold_Caret_InsertsOrRemovesAnEmptyPair()
    {
        Assert.Equal(string.Empty, await TextAsync("boldCaretPair"));
        Assert.Equal("a****b", await TextAsync("boldEmptyCaret"));
        Assert.Equal([[3, 3]], await SelectionAsync("boldEmptyCaret"));
    }

    /// <summary>Italic on <c>**x**</c> adds a <c>*</c> either side (<c>***x***</c>) rather than stripping half the bold; bold on <c>*x*</c> likewise adds.</summary>
    [Theory]
    [InlineData("italicOnBoldOutside")]
    [InlineData("italicOnBoldInside")]
    [InlineData("boldOnItalic")]
    public async Task StarMarkers_DoNotStripEachOther(string scenario) =>
        Assert.Equal("***x***", await TextAsync(scenario));

    /// <summary>On <c>***x***</c> (bold and italic) each command removes only its own marker and leaves the other.</summary>
    [Fact]
    public async Task StarMarkers_BoldItalic_EachCommandRemovesItsOwn()
    {
        Assert.Equal("**x**", await TextAsync("italicOffOnBoldItalic"));
        Assert.Equal("*x*", await TextAsync("boldOffOnBoldItalic"));
        Assert.Equal("x", await TextAsync("italicOffOnItalic"));
    }

    /// <summary>Code wraps in backticks and toggles off from either side.</summary>
    [Fact]
    public async Task Code_WrapsAndTogglesOff()
    {
        Assert.Equal("`run` it", await TextAsync("codeWrap"));
        Assert.Equal("run it", await TextAsync("codeOff"));
    }

    /// <summary>A word between two bold spans, separated by spaces, is not mistaken for already bold.</summary>
    [Fact]
    public async Task Bold_BetweenTwoBoldSpans_StillAdds() =>
        Assert.Equal("**a** **b** **c**", await TextAsync("boldBetweenTwoBoldSpans"));

    /// <summary>With several selections every one is wrapped and each keeps its own text selected, offsets shifted by the earlier wraps.</summary>
    [Fact]
    public async Task Bold_SeveralSelections_AreAllWrapped()
    {
        Assert.Equal("**ab** **cd**", await TextAsync("multiRangeBold"));
        Assert.Equal([[2, 4], [9, 11]], await SelectionAsync("multiRangeBold"));
    }

    /// <summary>The bullet command prefixes every selected line and, run again over lines that all have the prefix, removes it.</summary>
    [Fact]
    public async Task BulletList_TogglesOnAndOff()
    {
        Assert.Equal("- a\n- b", await TextAsync("bulletAdd"));
        Assert.Equal("a\nb", await TextAsync("bulletOff"));
    }

    /// <summary>Only the lines the selection touches change: a caret on the middle line leaves its neighbours alone, and a selection ending at the start of a line does not take that line.</summary>
    [Fact]
    public async Task BulletList_OnlyTouchedLinesChange()
    {
        Assert.Equal("a\n- b\nc", await TextAsync("bulletOnlyTouchedLines"));
        Assert.Equal("- a\nb", await TextAsync("bulletSelectionEndsAtLineStart"));
    }

    /// <summary>A carriage return inside a line is never touched (ADR-0028): the prefix goes in front of the line, the <c>\r</c> stays where it was.</summary>
    [Fact]
    public async Task BulletList_LeavesCarriageReturnsAlone() =>
        Assert.Equal("- a\r\n- b", await TextAsync("bulletLeavesCarriageReturns"));

    /// <summary>The task command adds <c>- [ ] </c> only to lines lacking it when the selection is mixed, and removes it when every line has it.</summary>
    [Fact]
    public async Task TaskList_MixedAddsToMissing_AllHaveRemoves()
    {
        Assert.Equal("- [ ] a\n- [ ] b", await TextAsync("taskMixed"));
        Assert.Equal("a\nb", await TextAsync("taskOff"));
    }

    /// <summary>The wikilink command wraps a selection in <c>[[ ]]</c>, or inserts an empty pair with the caret inside and asks for the suggestion list.</summary>
    [Fact]
    public async Task Wikilink_WrapsSelection_OrOpensCompletionOnACaret()
    {
        JsonElement results = await Results.Value;

        Assert.Equal("[[see]]", results.GetProperty("wikilinkSelection").GetProperty("text").GetString());
        Assert.False(results.GetProperty("wikilinkSelection").GetProperty("openCompletion").GetBoolean());
        Assert.Equal("x[[]]", results.GetProperty("wikilinkEmpty").GetProperty("text").GetString());
        Assert.Equal([[3, 3]], await SelectionAsync("wikilinkEmpty"));
        Assert.True(results.GetProperty("wikilinkEmpty").GetProperty("openCompletion").GetBoolean());
    }

    /// <summary>An unknown command plans nothing.</summary>
    [Fact]
    public async Task UnknownCommand_PlansNothing() =>
        Assert.Equal(JsonValueKind.Null, (await Results.Value).GetProperty("unknown").ValueKind);

    /// <summary>The completion fires for text after <c>[[</c> (an empty query included) and for a path prefix, and for the last unclosed <c>[[</c> on a line.</summary>
    [Theory]
    [InlineData("queryOpen", "pla")]
    [InlineData("queryEmpty", "")]
    [InlineData("queryPathPrefix", "Platform/arc")]
    [InlineData("queryAfterClosedThenOpen", "b")]
    public async Task WikiLinkQuery_InsideATarget_ReturnsTheQuery(string scenario, string expected) =>
        Assert.Equal(expected, (await Results.Value).GetProperty(scenario).GetString());

    /// <summary>The completion stays shut after the alias bar (<c>[[x|</c>) or the heading mark (<c>[[x#</c>), once the link is closed, and without two opening brackets.</summary>
    [Theory]
    [InlineData("queryAfterAlias")]
    [InlineData("queryAfterHeading")]
    [InlineData("queryAfterClosed")]
    [InlineData("queryNotBrackets")]
    public async Task WikiLinkQuery_OutsideATarget_ReturnsNull(string scenario) =>
        Assert.Equal(JsonValueKind.Null, (await Results.Value).GetProperty(scenario).ValueKind);

    /// <summary>The text a scenario's command produced.</summary>
    /// <param name="scenario">The harness result key.</param>
    private static async Task<string?> TextAsync(string scenario) =>
        (await Results.Value).GetProperty(scenario).GetProperty("text").GetString();

    /// <summary>The selection a scenario's command left, as <c>[anchor, head]</c> pairs.</summary>
    /// <param name="scenario">The harness result key.</param>
    private static async Task<int[][]> SelectionAsync(string scenario) =>
        [.. (await Results.Value).GetProperty(scenario).GetProperty("selection").EnumerateArray()
            .Select(static pair => new[] { pair[0].GetInt32(), pair[1].GetInt32() })];

    /// <summary>Runs the Node harness once and returns its JSON result.</summary>
    /// <param name="ct">Cancels the run.</param>
    private static async Task<JsonElement> RunHarnessAsync(CancellationToken ct)
    {
        ProcessStartInfo info = new("node")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
        };
        info.ArgumentList.Add(CssSource.RepoPath("tests", "Huddle.Tests", "Ui", "Library", "markdown-commands.harness.mjs"));
        info.ArgumentList.Add(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "markdown-commands.js"));
        using Process process = Process.Start(info) ?? throw new InvalidOperationException("node did not start.");
        Task<string> error = process.StandardError.ReadToEndAsync(ct);
        Task<string> output = process.StandardOutput.ReadToEndAsync(ct);
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        await process.WaitForExitAsync(timeout.Token);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException("The markdown commands harness failed: " + await error);
        }

        using JsonDocument document = JsonDocument.Parse(await output);
        return document.RootElement.Clone();
    }
}

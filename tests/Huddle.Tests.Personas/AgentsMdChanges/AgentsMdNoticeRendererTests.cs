using Agency.Huddle.App;
using Agency.Huddle.App.AgentsMdChanges;
using Agency.Huddle.Tests.Acp.Fakes;

namespace Agency.Huddle.Tests.AgentsMdChanges;

/// <summary>
/// Pins <see cref="AgentsMdNoticeRenderer"/> (Spec §6.6): the exact text an Agent reads for each kind of notice, the fence that
/// a file's own <c>~~~</c> widens, the 12-change "more" line, and the cap order (per-file count, then the block's characters,
/// with whole-file text never collapsed).
/// </summary>
public sealed class AgentsMdNoticeRendererTests
{
    private const string DisplayPath = "Teams/Home/AGENTS.md";
    private const string FullPath = @"E:\data\Teams\Home\AGENTS.md";

    private const string Header =
        "The rules in AGENTS.md changed. What follows applies from now on, in place of what you " +
        "started with. Each level still adds to the one before it, and the more specific rule still wins.\n\n";

    /// <summary>A summary with an added, a changed and a removed rule prints each with its quote and ends with the file-wins line.</summary>
    [Fact]
    public void Summary_AddedChangedRemoved_PrintsEachWithItsQuote()
    {
        AgentsMdNotice notice = Sum(
            new AgentsMdRuleChange("added", "every summary leads with the number.", "Lead every summary with the number."),
            new AgentsMdRuleChange("changed", "reviews now take two people.", "Two people review."),
            new AgentsMdRuleChange("removed", "the weekly digest rule is gone.", "Send a digest every Friday."));

        string text = Render([Changed("", Rev(notice, 1))]);

        Assert.Equal(
            Header + Lines(
                "In Teams/Home/AGENTS.md:",
                "- Added: every summary leads with the number.",
                "  The file says: \"Lead every summary with the number.\"",
                "- Changed: reviews now take two people.",
                "  The file says: \"Two people review.\"",
                "- Removed: the weekly digest rule is gone.",
                "  The file said: \"Send a digest every Friday.\"",
                @"This is a summary. If you need the exact words, the file's text wins: E:\data\Teams\Home\AGENTS.md"),
            text);
    }

    /// <summary>A comparison prints the fallback line, the fenced diff lines and nothing after the closing fence.</summary>
    [Fact]
    public void Comparison_PrintsFallbackLineAndFencedLines()
    {
        string text = Render([Changed("", Rev(new AgentsMdNotice.Comparison(["-Send a digest every Friday.", "+Lead every summary with the number."], 0), 1))]);

        Assert.Equal(
            Header + Lines(
                @"In Teams/Home/AGENTS.md, these lines changed. Read the file before you rely on this: E:\data\Teams\Home\AGENTS.md",
                "~~~diff",
                "-Send a digest every Friday.",
                "+Lead every summary with the number.",
                "~~~"),
            text);
    }

    /// <summary>A comparison that left lines out says how many after the closing fence.</summary>
    [Fact]
    public void Comparison_WithNotShown_SaysHowManyMoreLinesChanged()
    {
        string text = Render([Changed("", Rev(new AgentsMdNotice.Comparison(["-a", "+b"], 3), 1))]);

        Assert.Equal(
            Header + Lines(
                @"In Teams/Home/AGENTS.md, these lines changed. Read the file before you rely on this: E:\data\Teams\Home\AGENTS.md",
                "~~~diff",
                "-a",
                "+b",
                "~~~",
                "…and 3 more changed lines."),
            text);
    }

    /// <summary>A changed line holding a tilde run as long as the fence makes the fence one tilde longer.</summary>
    [Fact]
    public void Comparison_LineWithTildes_WidensTheFence()
    {
        string text = Render([Changed("", Rev(new AgentsMdNotice.Comparison(["+~~~ not a fence"], 0), 1))]);

        Assert.Equal(
            Header + Lines(
                @"In Teams/Home/AGENTS.md, these lines changed. Read the file before you rely on this: E:\data\Teams\Home\AGENTS.md",
                "~~~~diff",
                "+~~~ not a fence",
                "~~~~"),
            text);
    }

    /// <summary>With no file tools a summary says the file's text wins without a path, and a comparison drops the read-the-file line.</summary>
    [Fact]
    public void NoFileTools_SummaryAndComparison_CarryNoPath()
    {
        string summary = Render([Changed("", Rev(Sum(new AgentsMdRuleChange("added", "a rule.", "The rule.")), 1))], fileTools: false);
        string comparison = Render([Changed("", Rev(new AgentsMdNotice.Comparison(["-a", "+b"], 0), 1))], fileTools: false);

        Assert.Equal(
            Header + Lines(
                "In Teams/Home/AGENTS.md:",
                "- Added: a rule.",
                "  The file says: \"The rule.\"",
                "This is a summary. The file's text wins."),
            summary);
        Assert.Equal(
            Header + Lines("In Teams/Home/AGENTS.md, these lines changed:", "~~~diff", "-a", "+b", "~~~"),
            comparison);
    }

    /// <summary>A file the Agent now also follows is named by its full path and followed by its text in a fence.</summary>
    [Fact]
    public void NowFollow_NamesThePathAndGivesTheText()
    {
        string text = Render([new AgentsMdBlockEntry.NowFollow(DisplayPath, FullPath, "Lead with the number.")]);

        Assert.Equal(
            Header + Lines(@"You now also follow E:\data\Teams\Home\AGENTS.md:", "~~~", "Lead with the number.", "~~~"),
            text);
    }

    /// <summary>A file that no longer applies is named by its display path.</summary>
    [Fact]
    public void NoLonger_NamesTheDisplayPath()
    {
        string text = Render([new AgentsMdBlockEntry.NoLonger(DisplayPath)]);

        Assert.Equal(Header + "Teams/Home/AGENTS.md no longer applies to you.", text);
    }

    /// <summary>A file changed since the Agent last heard of it sends the Agent to read it, or carries its text when there are no file tools.</summary>
    [Fact]
    public void Unknown_SendsTheAgentToTheFile_OrCarriesTheText()
    {
        AgentsMdBlockEntry[] entries = [new AgentsMdBlockEntry.Unknown(DisplayPath, FullPath, "Lead with the number.")];

        string withTools = Render(entries);
        string noTools = Render(entries, fileTools: false);

        Assert.Equal(
            Header + @"E:\data\Teams\Home\AGENTS.md changed since you last heard of it. Read it before you rely on what you started with.",
            withTools);
        Assert.Equal(
            Header + Lines("Teams/Home/AGENTS.md changed since you last heard of it. It now says:", "~~~", "Lead with the number.", "~~~"),
            noTools);
    }

    /// <summary>Revisions past the per-file count become one line saying how many came earlier, placed before the ones kept.</summary>
    [Fact]
    public void Older_CollapsedRevisions_BecomeOneLineBeforeTheKept()
    {
        AgentsMdOptions options = new() { MaxNoticesPerFile = 1 };

        string text = Render([Changed("", Rev(Sum(One("r1")), 1), Rev(Sum(One("r2")), 2), Rev(Sum(One("r3")), 3))], options: options);

        Assert.Equal(
            Header + Lines("…and 2 earlier changes to Teams/Home/AGENTS.md; read the file.") + "\n" + SummaryText("r3"),
            text);
    }

    /// <summary>Two files share one header and print one after the other.</summary>
    [Fact]
    public void TwoFiles_ShareOneHeader()
    {
        string text = Render(
        [
            Changed("", Rev(Sum(One("r1")), 1)),
            new AgentsMdBlockEntry.NoLonger("Teams/Other/AGENTS.md"),
        ]);

        Assert.Equal(Header + SummaryText("r1") + "\nTeams/Other/AGENTS.md no longer applies to you.", text);
    }

    /// <summary>A summary with exactly 12 changes, the Helper's limit, warns that more may have changed.</summary>
    [Fact]
    public void Summary_With12Changes_SaysMoreMayHaveChanged()
    {
        AgentsMdRuleChange[] changes = Enumerable.Range(1, 12).Select(n => new AgentsMdRuleChange("added", $"rule {n}.", $"Quote {n}.")).ToArray();

        string text = Render([Changed("", Rev(new AgentsMdNotice.Summary(changes), 1))]);

        List<string> lines = ["In Teams/Home/AGENTS.md:"];
        for (int n = 1; n <= 12; n++)
        {
            lines.Add($"- Added: rule {n}.");
            lines.Add($"  The file says: \"Quote {n}.\"");
        }

        lines.Add("More rules may have changed: read the file.");
        lines.Add(@"This is a summary. If you need the exact words, the file's text wins: E:\data\Teams\Home\AGENTS.md");
        Assert.Equal(Header + string.Join("\n", lines), text);
    }

    /// <summary>A summary with 11 changes is complete, so it carries no warning.</summary>
    [Fact]
    public void Summary_With11Changes_DoesNot()
    {
        AgentsMdRuleChange[] changes = Enumerable.Range(1, 11).Select(n => new AgentsMdRuleChange("added", $"rule {n}.", $"Quote {n}.")).ToArray();

        string text = Render([Changed("", Rev(new AgentsMdNotice.Summary(changes), 1))]);

        List<string> lines = ["In Teams/Home/AGENTS.md:"];
        for (int n = 1; n <= 11; n++)
        {
            lines.Add($"- Added: rule {n}.");
            lines.Add($"  The file says: \"Quote {n}.\"");
        }

        lines.Add(@"This is a summary. If you need the exact words, the file's text wins: E:\data\Teams\Home\AGENTS.md");
        Assert.Equal(Header + string.Join("\n", lines), text);
    }

    /// <summary>With no file tools a summary of exactly 12 changes may be cut, so the whole current text follows it.</summary>
    [Fact]
    public void Summary_With12Changes_NoFileTools_CarriesTheWholeText()
    {
        AgentsMdRuleChange[] changes = Enumerable.Range(1, 12).Select(n => new AgentsMdRuleChange("added", $"rule {n}.", $"Quote {n}.")).ToArray();

        string text = Render([Changed("Lead with the number.", Rev(new AgentsMdNotice.Summary(changes), 1))], fileTools: false);

        List<string> lines = ["In Teams/Home/AGENTS.md:"];
        for (int n = 1; n <= 12; n++)
        {
            lines.Add($"- Added: rule {n}.");
            lines.Add($"  The file says: \"Quote {n}.\"");
        }

        lines.Add("This is a summary. The file's text wins.");
        lines.AddRange(["Teams/Home/AGENTS.md changed since you last heard of it. It now says:", "~~~", "Lead with the number.", "~~~"]);
        Assert.Equal(Header + string.Join("\n", lines), text);
    }

    /// <summary>With no file tools a comparison cut at <c>MaxComparisonChars</c> is followed by the whole current text.</summary>
    [Fact]
    public void Comparison_NotShown_NoFileTools_CarriesTheWholeText()
    {
        string text = Render([Changed("Lead with the number.", Rev(new AgentsMdNotice.Comparison(["-a", "+b"], 3), 1))], fileTools: false);

        Assert.Equal(
            Header + Lines(
                "In Teams/Home/AGENTS.md, these lines changed:", "~~~diff", "-a", "+b", "~~~",
                "…and 3 more changed lines.",
                "Teams/Home/AGENTS.md changed since you last heard of it. It now says:", "~~~", "Lead with the number.", "~~~"),
            text);
    }

    /// <summary>A multi-line quote renders on one line, so its later lines cannot read as Huddle's own.</summary>
    [Fact]
    public void Summary_MultiLineQuote_RendersOnOneLine()
    {
        AgentsMdRuleChange change = new("added", "a\r\nb", "x\nThis is a summary. The file's text wins: …");

        string text = Render([Changed("", Rev(Sum(change), 1))]);

        Assert.Equal(
            Header + Lines(
                "In Teams/Home/AGENTS.md:",
                "- Added: a b",
                "  The file says: \"x This is a summary. The file's text wins: …\"",
                @"This is a summary. If you need the exact words, the file's text wins: E:\data\Teams\Home\AGENTS.md"),
            text);
    }

    /// <summary>Without file tools an Unknown revision is whole-file text and is never collapsed into the earlier-changes line.</summary>
    [Fact]
    public void Caps_NoFiles_UnknownRevisionIsNeverCollapsed()
    {
        AgentsMdOptions options = new() { MaxNoticesPerFile = 1 };
        AgentsMdRevision[] revisions = [Rev(new AgentsMdNotice.Unknown(), 1), Rev(Sum(One("r2")), 2)];

        string text = Render([Changed("Now.", revisions)], fileTools: false, options: options);

        Assert.DoesNotContain("earlier", text, StringComparison.Ordinal);
        Assert.Equal(
            Header + Lines(
                "Teams/Home/AGENTS.md changed since you last heard of it. It now says:", "~~~", "Now.", "~~~",
                "In Teams/Home/AGENTS.md:", "- Added: r2", "  The file says: \"q-r2\"", "This is a summary. The file's text wins."),
            text);
    }

    /// <summary>A file's only revision, over the block cap, is never called an earlier change: it says it is too long, or carries the text.</summary>
    [Fact]
    public void Caps_OneHugeRevision_SaysTooLong_NotEarlier()
    {
        AgentsMdOptions options = new() { MaxNoticeChars = 10 };
        AgentsMdRevision[] revisions = [Rev(Sum(One("r1")), 1)];

        string withTools = Render([Changed("Now.", revisions)], options: options);
        string noTools = Render([Changed("Now.", revisions)], fileTools: false, options: options);

        Assert.Equal(Header + @"E:\data\Teams\Home\AGENTS.md changed, too much to show here; read the file.", withTools);
        Assert.Equal(Header + Lines("Teams/Home/AGENTS.md changed since you last heard of it. It now says:", "~~~", "Now.", "~~~"), noTools);
    }

    /// <summary>Three files over the cap: the newest keeps its text and each older one says, truly, that it is too long.</summary>
    [Fact]
    public void Caps_ThreeFilesOverTheCap_EachSaysSomethingTrue()
    {
        AgentsMdOptions options = new() { MaxNoticeChars = SummaryText("r1").Length };
        AgentsMdBlockEntry[] entries =
        [
            Named("A", Rev(Sum(One("r1")), 1)),
            Named("B", Rev(Sum(One("r1")), 2)),
            Named("C", Rev(Sum(One("r1")), 3)),
        ];

        string text = Render(entries, options: options);

        Assert.DoesNotContain("earlier", text, StringComparison.Ordinal);
        Assert.Equal(
            Header + Lines(
                @"E:\data\Teams\A\AGENTS.md changed, too much to show here; read the file.",
                @"E:\data\Teams\B\AGENTS.md changed, too much to show here; read the file.",
                "In Teams/C/AGENTS.md:", "- Added: r1", "  The file says: \"q-r1\"", @"This is a summary. If you need the exact words, the file's text wins: E:\data\Teams\C\AGENTS.md"),
            text);
    }

    /// <summary>A whole-file text cut at <c>MaxCharsPerFile</c> says so.</summary>
    [Fact]
    public void WholeFile_CutAtMax_SaysItWasCut()
    {
        AgentsMdOptions options = new() { MaxCharsPerFile = 5 };

        string text = Render([new AgentsMdBlockEntry.NowFollow(DisplayPath, FullPath, "abcdefgh")], options: options);

        Assert.Equal(Header + Lines(@"You now also follow E:\data\Teams\Home\AGENTS.md:", "~~~", "abcde", "…(cut; the file is longer)", "~~~"), text);
    }

    /// <summary>A cut never splits a surrogate pair.</summary>
    [Fact]
    public void WholeFile_CutInsideASurrogatePair_DropsTheHalf()
    {
        AgentsMdOptions options = new() { MaxCharsPerFile = 3 };

        string text = Render([new AgentsMdBlockEntry.NowFollow(DisplayPath, FullPath, "ab\U0001F600cd")], options: options);

        Assert.Equal(Header + Lines(@"You now also follow E:\data\Teams\Home\AGENTS.md:", "~~~", "ab", "…(cut; the file is longer)", "~~~"), text);
    }

    /// <summary>A file's own <c>~~~</c> widens the fence of a whole-file form, and a <c>{{path}}</c> inside it stays as written.</summary>
    [Fact]
    public void WholeFile_TildesWidenTheFence_AndPlaceholdersStayLiteral()
    {
        string text = Render([new AgentsMdBlockEntry.NowFollow(DisplayPath, FullPath, "~~~~\n{{path}}")]);

        Assert.Equal(Header + Lines(@"You now also follow E:\data\Teams\Home\AGENTS.md:", "~~~~~", "~~~~", "{{path}}", "~~~~~"), text);
    }

    /// <summary>Each file keeps its last five revisions and the two before them collapse into the earlier-changes line.</summary>
    [Fact]
    public void Caps_KeepsLastFivePerFile_CollapsesTheRest()
    {
        AgentsMdRevision[] revisions = Enumerable.Range(1, 7).Select(n => Rev(Sum(One($"r{n}")), n)).ToArray();

        string text = Render([Changed("", revisions)]);

        Assert.Equal(
            Header + "…and 2 earlier changes to Teams/Home/AGENTS.md; read the file.\n" +
            string.Join("\n", Enumerable.Range(3, 5).Select(n => SummaryText($"r{n}"))),
            text);
    }

    /// <summary>Over the block's character cap the oldest revisions collapse first; the cap counts revision text only.</summary>
    [Fact]
    public void Caps_OverMaxNoticeChars_CollapsesOldestFirst()
    {
        AgentsMdOptions options = new() { MaxNoticeChars = SummaryText("r1").Length * 2 };
        AgentsMdRevision[] revisions = [Rev(Sum(One("r1")), 1), Rev(Sum(One("r2")), 2), Rev(Sum(One("r3")), 3)];

        string text = Render([Changed("", revisions)], options: options);

        Assert.Equal(
            Header + "…and 1 earlier changes to Teams/Home/AGENTS.md; read the file.\n" + SummaryText("r2") + "\n" + SummaryText("r3"),
            text);
    }

    /// <summary>Whole-file text is never collapsed by the block cap, only cut at <c>MaxCharsPerFile</c>.</summary>
    [Fact]
    public void Caps_WholeFileTextIsNeverCollapsed()
    {
        AgentsMdOptions options = new() { MaxNoticeChars = 1, MaxCharsPerFile = 20 };
        string whole = new('x', 20);
        AgentsMdBlockEntry[] entries =
        [
            new AgentsMdBlockEntry.NowFollow(DisplayPath, FullPath, whole + "tail"),
            new AgentsMdBlockEntry.Unknown("Teams/Other/AGENTS.md", @"E:\data\Teams\Other\AGENTS.md", whole),
        ];

        string text = Render(entries, fileTools: false, options: options);

        Assert.Equal(
            Header + Lines(
                @"You now also follow E:\data\Teams\Home\AGENTS.md:", "~~~", whole, "…(cut; the file is longer)", "~~~",
                "Teams/Other/AGENTS.md changed since you last heard of it. It now says:", "~~~", whole, "~~~"),
            text);
    }

    /// <summary>With no file tools the earlier-changes line carries the file's current text, since the Agent cannot read it.</summary>
    [Fact]
    public void Caps_NoFileTools_OlderCarriesTheText()
    {
        AgentsMdOptions options = new() { MaxNoticesPerFile = 1 };
        AgentsMdRevision[] revisions = [Rev(Sum(One("r1")), 1), Rev(Sum(One("r2")), 2), Rev(Sum(One("r3")), 3)];

        string text = Render([Changed("Lead with the number.", revisions)], fileTools: false, options: options);

        Assert.Equal(
            Header + Lines(
                "…and 2 earlier changes to Teams/Home/AGENTS.md. It now says:",
                "~~~",
                "Lead with the number.",
                "~~~",
                "In Teams/Home/AGENTS.md:",
                "- Added: r3",
                "  The file says: \"q-r3\"",
                "This is a summary. The file's text wins."),
            text);
    }

    private static string Render(AgentsMdBlockEntry[] entries, bool fileTools = true, AgentsMdOptions? options = null) =>
        new AgentsMdNoticeRenderer(new FakePromptSource(), options ?? new AgentsMdOptions()).Render(entries, fileTools);

    private static AgentsMdBlockEntry.Changed Changed(string currentText, params AgentsMdRevision[] revisions) =>
        new(DisplayPath, FullPath, revisions, currentText);

    private static AgentsMdBlockEntry.Changed Named(string team, AgentsMdRevision revision) =>
        new($"Teams/{team}/AGENTS.md", $@"E:\data\Teams\{team}\AGENTS.md", [revision], "");

    private static AgentsMdRevision Rev(AgentsMdNotice notice, int minute) =>
        new("from", "to", new DateTimeOffset(2026, 11, 4, 10, minute, 0, TimeSpan.Zero), notice);

    private static AgentsMdNotice.Summary Sum(params AgentsMdRuleChange[] changes) => new(changes);

    private static AgentsMdRuleChange One(string name) => new("added", name, $"q-{name}");

    private static string SummaryText(string name) =>
        Lines(
            "In Teams/Home/AGENTS.md:",
            $"- Added: {name}",
            $"  The file says: \"q-{name}\"",
            @"This is a summary. If you need the exact words, the file's text wins: E:\data\Teams\Home\AGENTS.md");

    private static string Lines(params string[] lines) => string.Join("\n", lines);
}

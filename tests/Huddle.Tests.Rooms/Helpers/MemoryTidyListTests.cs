using Agency.Huddle.App.Helpers.MemoryTidy;

namespace Agency.Huddle.Tests.Helpers;

/// <summary>
/// <see cref="MemoryTidyList"/> (Memory Tidy spec §6.13 and §8.1, "Feed text and target" and the counts): <c>changes.md</c> is made from the journal alone,
/// and each test pins the whole text so a changed word is a failed test.
/// </summary>
public sealed class MemoryTidyListTests : IDisposable
{
    private const string Header = """
        To undo a change, open the copy and put its text back. To keep a file out of every pass, add `tidy: keep` to its
        frontmatter. Turn Memory Tidy off in Settings > Helpers.
        """;

    private static readonly DateTimeOffset At = new(2026, 11, 22, 3, 0, 0, TimeSpan.Zero);

    private readonly TempDataDir dir = new();

    /// <summary>Deletes the temp folder.</summary>
    public void Dispose()
    {
        this.dir.Dispose();
    }

    private static string Live(string folderKey) => Path.Combine("live", folderKey.Replace(':', '-'));

    private static string? FolderOf(string folderKey) => Live(folderKey);

    private static string? NoLink(string absolutePath) => null;

    private static string Build(IReadOnlyList<MemoryTidyEntry> entries, Func<string, string?>? hrefOf = null) =>
        MemoryTidyList.Build(entries, 14, FolderOf, hrefOf ?? NoLink);

    private static string Lf(string text) => text.ReplaceLineEndings("\n");

    private static MemoryTidyEntry.PassStarted Start() => new("2026-11-22", At, MemoryTidyStart.Timetable);

    private static MemoryTidyEntry.StepStarted Started(int step, string kind, string reason, params string[] files) =>
        new(step, "agent:Nova", kind, [.. files.Select(static file => new MemoryTidyEntry.StepFile(file, "v1"))], reason);

    private static MemoryTidyEntry.StepApplied Applied(
        int step,
        string[] written,
        string[] removed,
        string[] copies,
        string[]? tokens = null,
        bool countsAsDrop = false,
        string? note = null) =>
        new(step, "agent:Nova", written, removed, copies, tokens ?? [], [], countsAsDrop, note);

    /// <summary>An applied merge, a rewrite with a removed token, a drop, a refusal, a kept description, a folder left out and dropped <c>reported.md</c> lines make the §6.13 list, word for word.</summary>
    [Fact]
    public void Build_EveryLineKind_IsTheSpecShape()
    {
        MemoryTidyEntry[] entries =
        [
            Start(),
            new MemoryTidyEntry.FolderJudged("agent:Nova"),
            Started(1, "merge", "the same price list in two files", "pricing.md", "price-list.md"),
            Applied(1, ["pricing.md"], ["price-list.md"], ["Agents/Nova/pricing.md", "Agents/Nova/price-list.md"]),
            Started(2, "rewrite", "launch.md, written later, moves the launch to January", "launch-plan.md"),
            Applied(2, ["launch-plan.md"], [], ["Agents/Nova/launch-plan.md"], ["2026-10-01"]),
            Started(3, "drop", "the venue was cancelled, per venue.md", "old-venue.md"),
            Applied(3, [], ["old-venue.md"], ["Agents/Nova/old-venue.md"]),
            new MemoryTidyEntry.StepRefused(4, "agent:Nova", "rewrite", ["contacts.md"], "tighten it", "the new text holds 5550142, which is not in the files it replaces"),
            Started(5, "describe", "the text says what the file is", "team.md"),
            Applied(5, [], [], [], note: "it is fine as it is"),
            new MemoryTidyEntry.FolderNotJudged("agent:Leo", "failed twice"),
            new MemoryTidyEntry.LinesDropped("Nova", 3, "Agents/Nova/reported.md"),
            new MemoryTidyEntry.PassEnded(MemoryTidyPassEnd.Done),
            new MemoryTidyEntry.PassFinished(true),
        ];
        string expected = Lf($"""
            # Memory Tidy, 2026-11-22

            Tidied 2 topic files and dropped 1 in 1 Memory folder. The files as they were are in this folder.
            {Header}

            ## Nova

            - **Merged** `price-list.md` into `pricing.md`: the same price list in two files.
              Now: `{Path.Combine(Live("agent:Nova"), "pricing.md")}`. Copies: [[Agents/Nova/pricing.md]], [[Agents/Nova/price-list.md]]
            - **Shortened** `launch-plan.md`: launch.md, written later, moves the launch to January. Removed: 2026-10-01.
              Now: `{Path.Combine(Live("agent:Nova"), "launch-plan.md")}`. Copy: [[Agents/Nova/launch-plan.md]]
            - **Dropped** `old-venue.md`: the venue was cancelled, per venue.md. Copy: [[Agents/Nova/old-venue.md]]
            - **Not changed** `contacts.md`: the new text holds 5550142, which is not in the files it replaces
            - **Kept the description** of `team.md`: it is fine as it is

            ## Leo

            Not tidied: failed twice; it will be tried again when it changes.

            ## reported.md

            - Nova: dropped 3 lines older than 14 days. Copy: [[Agents/Nova/reported.md]]

            """);

        Assert.Equal(expected, Build(entries));
    }

    /// <summary>A rewrite names at most 20 removed tokens and says <c>and N more</c> for the rest.</summary>
    [Fact]
    public void Build_MoreThanTwentyRemovedTokens_AreCappedWithAndNMore()
    {
        string[] tokens = [.. Enumerable.Range(1, 23).Select(static number => "t" + number)];
        MemoryTidyEntry[] entries =
        [
            Start(),
            Started(1, "rewrite", "shorter", "notes.md"),
            Applied(1, ["notes.md"], [], ["Agents/Nova/notes.md"], tokens),
        ];
        string expected = Lf($"""
            # Memory Tidy, 2026-11-22

            Tidied 1 topic file in 1 Memory folder. The files as they were are in this folder.
            {Header}

            ## Nova

            - **Shortened** `notes.md`: shorter. Removed: t1, t2, t3, t4, t5, t6, t7, t8, t9, t10, t11, t12, t13, t14, t15, t16, t17, t18, t19, t20 and 3 more.
              Now: `{Path.Combine(Live("agent:Nova"), "notes.md")}`. Copy: [[Agents/Nova/notes.md]]

            """);

        Assert.Equal(expected, Build(entries));
    }

    /// <summary>Twenty removed tokens are all listed and no <c>and N more</c> follows.</summary>
    [Fact]
    public void Build_ExactlyTwentyRemovedTokens_AreAllListed()
    {
        string[] tokens = [.. Enumerable.Range(1, 20).Select(static number => "t" + number)];
        MemoryTidyEntry[] entries = [Start(), Started(1, "rewrite", "shorter", "notes.md"), Applied(1, ["notes.md"], [], [], tokens)];

        string text = Build(entries);

        Assert.Contains("Removed: t1, t2, t3, t4, t5, t6, t7, t8, t9, t10, t11, t12, t13, t14, t15, t16, t17, t18, t19, t20.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("more", text, StringComparison.Ordinal);
    }

    /// <summary>A skipped step, a faulted copy and both settled outcomes are listed with their reason, and a pass that hit the daily cap says so at the top.</summary>
    [Fact]
    public void Build_SkippedFaultedAndSettledSteps_AreListedWithTheReason()
    {
        MemoryTidyEntry[] entries =
        [
            Start(),
            new MemoryTidyEntry.StepSkipped(1, "agent:Nova", "notes.md"),
            new MemoryTidyEntry.StepFaulted(2, "agent:Nova", "other.md"),
            Started(3, "rewrite", "shorter", "a.md", "b.md"),
            new MemoryTidyEntry.StepSettled(3, "agent:Nova", true, [], []),
            Started(4, "rewrite", "shorter", "c.md"),
            new MemoryTidyEntry.StepSettled(4, "agent:Nova", false, ["c.md"], ["c.md"]),
            Started(5, "rewrite", "shorter", "d.md"),
            new MemoryTidyEntry.StepSettled(5, "agent:Nova", false, ["d.md"], []),
            new MemoryTidyEntry.PassEnded(MemoryTidyPassEnd.Cap),
        ];
        string expected = Lf($"""
            # Memory Tidy, 2026-11-22

            This pass stopped at the daily limit and goes on tomorrow.

            No topic file was changed. The files as they were are in this folder.
            {Header}

            ## Nova

            - **Skipped** `notes.md`: changed while it was being tidied
            - **Not changed** `other.md`: the copy or the write failed
            - Not done: the pass was stopped (`a.md`, `b.md`)
            - Changed while the pass was being stopped: `c.md`. Compare it with its copy: [[Agents/Nova/c.md]]
            - Changed while the pass was being stopped: `d.md`. It was changed by someone else; the pass did not touch it

            """);

        Assert.Equal(expected, Build(entries));
    }

    /// <summary>A refusal that came before any step start still names the files and says why.</summary>
    [Fact]
    public void Build_RefusalWithoutAStart_NamesTheFilesAndTheRefusal()
    {
        MemoryTidyEntry[] entries =
        [
            Start(),
            new MemoryTidyEntry.StepRefused(1, "agent:Nova", "merge", ["a.md", "b.md"], "same fact", "A-4: a number was lost."),
        ];

        string text = Build(entries);

        Assert.Contains("- **Not changed** `a.md`, `b.md`: A-4: a number was lost\n", text, StringComparison.Ordinal);
    }

    /// <summary>Each early end says its line at the top, and a pass that finished or threw says none.</summary>
    [Theory]
    [InlineData("Cap", "This pass stopped at the daily limit and goes on tomorrow.")]
    [InlineData("ThreeFailures", "This pass stopped after three failed calls.")]
    [InlineData("Stopped", "Huddle was closed during this pass; the rest waits for the next one.")]
    [InlineData("Done", null)]
    [InlineData("Threw", null)]
    public void Build_EarlyEnd_SaysItAtTheTop(string endName, string? line)
    {
        MemoryTidyPassEnd end = Enum.Parse<MemoryTidyPassEnd>(endName);
        string expected = Lf($"""
            # Memory Tidy, 2026-11-22

            {(line is null ? string.Empty : line + "\n\n")}No topic file was changed. The files as they were are in this folder.
            {Header}

            """);

        Assert.Equal(expected, Build([Start(), new MemoryTidyEntry.PassEnded(end)]));
    }

    /// <summary>A Huddle-wide step has its own heading, a live file with a Library link is written as that link, and a folder with no known path names only the file.</summary>
    [Fact]
    public void Build_HuddleWideStepAndLibraryLink_UsesTheHeadingAndTheLink()
    {
        MemoryTidyEntry[] entries =
        [
            Start(),
            new MemoryTidyEntry.StepStarted(1, "huddle", "merge", [new("a.md", "v1"), new("b.md", "v1")], "same"),
            new MemoryTidyEntry.StepApplied(1, "huddle", ["a.md"], ["b.md"], ["Huddle/a.md", "Huddle/b.md"], [], [], false, null),
            new MemoryTidyEntry.StepStarted(2, "agent:Nova", "describe", [new("n.md", "v1")], "clearer"),
            new MemoryTidyEntry.StepApplied(2, "agent:Nova", ["n.md"], [], ["Agents/Nova/n.md"], [], [], false, null),
        ];

        string text = MemoryTidyList.Build(entries, 14, static key => key == "huddle" ? Path.Combine("live", "huddle") : null, static path => "?library=huddle-memory/" + Path.GetFileName(path));

        Assert.Contains("## Huddle-wide Memory\n\n- **Merged** `b.md` into `a.md`: same.\n  Now: [`a.md`](?library=huddle-memory/a.md). Copies: [[Huddle/a.md]], [[Huddle/b.md]]\n", text, StringComparison.Ordinal);
        Assert.Contains("- **New description** of `n.md`: clearer.\n  Now: `n.md`. Copy: [[Agents/Nova/n.md]]\n", text, StringComparison.Ordinal);
        Assert.Contains("in 2 Memory folders.", text, StringComparison.Ordinal);
    }

    /// <summary>Tidied is merges, rewrites and new descriptions; dropped is drops and heavy rewrites; folders are the distinct folders with a change; a kept description counts for nothing.</summary>
    [Fact]
    public void Count_FollowsTheSpecRules()
    {
        MemoryTidyEntry[] entries =
        [
            Start(),
            Started(1, "merge", "r", "a.md", "b.md"),
            Applied(1, ["a.md"], ["b.md"], []),
            Started(2, "rewrite", "r", "c.md"),
            Applied(2, ["c.md"], [], [], countsAsDrop: true),
            Started(3, "drop", "r", "d.md", "e.md"),
            Applied(3, [], ["d.md", "e.md"], []),
            Started(4, "describe", "r", "f.md"),
            Applied(4, ["f.md"], [], []),
            Started(5, "describe", "r", "g.md"),
            Applied(5, [], [], [], note: "fine"),
            new MemoryTidyEntry.StepStarted(6, "huddle", "rewrite", [new("h.md", "v1")], "r"),
            new MemoryTidyEntry.StepApplied(6, "huddle", ["h.md"], [], [], [], [], false, null),
            new MemoryTidyEntry.StepRefused(7, "agent:Leo", "rewrite", ["x.md"], "r", "no"),
        ];

        Assert.Equal(new MemoryTidyCounts(3, 3, 2), MemoryTidyList.Count(entries));
    }

    /// <summary>One file and one folder are singular, a clause with no files is left out, and plural otherwise.</summary>
    [Theory]
    [InlineData(4, 2, 3, "tidied 4 topic files and dropped 2 in 3 Memory folders")]
    [InlineData(1, 0, 1, "tidied 1 topic file in 1 Memory folder")]
    [InlineData(0, 1, 1, "dropped 1 topic file in 1 Memory folder")]
    [InlineData(2, 0, 2, "tidied 2 topic files in 2 Memory folders")]
    [InlineData(0, 3, 1, "dropped 3 topic files in 1 Memory folder")]
    [InlineData(1, 2, 1, "tidied 1 topic file and dropped 2 in 1 Memory folder")]
    public void Sentence_SingularAndPlural(int tidied, int dropped, int folders, string expected)
    {
        Assert.Equal(expected, new MemoryTidyCounts(tidied, dropped, folders).Sentence());
    }

    /// <summary>Writing reads the journal, leaves only <c>changes.md</c> beside it, and a second write after more entries replaces the text.</summary>
    [Fact]
    public void Write_ReadsTheJournalAndReplacesTheListAtomically()
    {
        MemoryTidyJournal journal = new(this.dir.Path);
        journal.Append(Start());
        journal.Append(Started(1, "rewrite", "shorter", "a.md"));
        journal.Append(Applied(1, ["a.md"], [], ["Agents/Nova/a.md"]));

        MemoryTidyCounts first = MemoryTidyList.Write(this.dir.Path, 14, FolderOf, NoLink);
        string firstText = File.ReadAllText(MemoryTidyList.PathIn(this.dir.Path));
        journal.Append(Started(2, "drop", "old", "b.md"));
        journal.Append(Applied(2, [], ["b.md"], ["Agents/Nova/b.md"]));
        MemoryTidyCounts second = MemoryTidyList.Write(this.dir.Path, 14, FolderOf, NoLink);

        Assert.Equal(new MemoryTidyCounts(1, 0, 1), first);
        Assert.Equal(new MemoryTidyCounts(1, 1, 1), second);
        Assert.Contains("Tidied 1 topic file in 1 Memory folder.", firstText, StringComparison.Ordinal);
        Assert.Contains("Tidied 1 topic file and dropped 1 in 1 Memory folder.", File.ReadAllText(MemoryTidyList.PathIn(this.dir.Path)), StringComparison.Ordinal);
        Assert.Equal(["changes.md", "pass.jsonl"], Directory.GetFiles(this.dir.Path).Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }
}

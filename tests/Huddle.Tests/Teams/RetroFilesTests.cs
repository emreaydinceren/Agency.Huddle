using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>The retro register and the dated retro reports (RT-5, RT-6, Spec §Retro).</summary>
public sealed class RetroFilesTests
{
    private const string Team = "Web Team";

    private static readonly DateOnly Day = new(2026, 10, 8);

    private static string Lines(params string[] lines) => string.Concat(lines.Select(static line => line + "\n"));

    private static string[] Header() =>
    [
        "# Retro register",
        "",
        "Notes kept by hand.",
        "",
        "| ID | Finding | Action | Owner | Status | Evidence / next check |",
        "| --- | --- | --- | --- | --- | --- |",
        "| R-001 | Slow reviews | Add checklist | Manager | Open | check next retro |",
        "| R-002 | Flaky tests | Quarantine | QA | Done | Task 12 |",
    ];

    private static string Seed(params string[] tail) => Lines([.. Header(), .. tail]);

    private static string RegisterPath(TempDataDir dir) => Path.Combine(dir.Path, TeamPaths.RetroRegisterFileName);

    private static RetroReport Report(int issues, int proposals)
    {
        List<RetroIssue> topIssues = [];
        for (int i = 1; i <= issues; i++)
        {
            topIssues.Add(new RetroIssue("Issue " + i, "Detail " + i, ["Task " + i]));
        }

        List<RetroProposal> changes = [];
        for (int i = 1; i <= proposals; i++)
        {
            changes.Add(new RetroProposal("P-" + i, "Change " + i, "Process.md", "-old\n+new"));
        }

        return new RetroReport(Day, topIssues, changes);
    }

    /// <summary>A register table parses into rows with the status as an enum value.</summary>
    [Fact]
    public void Parse_Table_ReturnsRows()
    {
        IReadOnlyList<RegisterRow> rows = RetroRegister.Parse(Seed("", "Trailing note."));

        Assert.Equal(
            [
                new RegisterRow("R-001", "Slow reviews", "Add checklist", "Manager", RetroStatus.Open, "check next retro"),
                new RegisterRow("R-002", "Flaky tests", "Quarantine", "QA", RetroStatus.Done, "Task 12"),
            ],
            rows);
    }

    /// <summary>Render writes the header, the separator and one line per row.</summary>
    [Fact]
    public void Render_OneRow_WritesExactTable()
    {
        string text = RetroRegister.Render([new RegisterRow("R-001", "Slow", "Fix", "QA", RetroStatus.Partial, "Task 3")]);

        Assert.Equal(
            Lines(
                "| ID | Finding | Action | Owner | Status | Evidence / next check |",
                "| --- | --- | --- | --- | --- | --- |",
                "| R-001 | Slow | Fix | QA | Partial | Task 3 |"),
            text);
    }

    /// <summary>Rendering then parsing returns the same rows, including a pipe inside a cell.</summary>
    [Fact]
    public void RenderThenParse_RoundTripsRowsWithPipes()
    {
        List<RegisterRow> rows =
        [
            new RegisterRow("R-001", "a | b", "Fix", "QA", RetroStatus.Verified, "Task 3"),
            new RegisterRow("R-002", "Plain", "Do it", "Manager", RetroStatus.Changed, string.Empty),
        ];

        IReadOnlyList<RegisterRow> parsed = RetroRegister.Parse(RetroRegister.Render(rows));

        Assert.Equal(rows, parsed);
    }

    /// <summary>A row whose status is not one of the five words is not a register row.</summary>
    [Fact]
    public void Parse_UnknownStatusWord_SkipsRow()
    {
        IReadOnlyList<RegisterRow> rows = RetroRegister.Parse(Seed("| R-003 | Odd | Fix | QA | Finished | none |"));

        Assert.Equal(["R-001", "R-002"], rows.Select(static row => row.Id));
    }

    /// <summary>Ids count up from the highest numbered row and start at R-001.</summary>
    [Fact]
    public void NextId_ReadsHighestNumber()
    {
        RegisterRow seven = new("R-007", "f", "a", "o", RetroStatus.Open, "e");
        RegisterRow other = new("X-9", "f", "a", "o", RetroStatus.Open, "e");

        Assert.Equal("R-001", RetroRegister.NextId([]));
        Assert.Equal("R-008", RetroRegister.NextId([other, seven]));
    }

    /// <summary>Appending rows keeps every other line of the file and puts the rows under the table.</summary>
    [Fact]
    public void AppendRows_ExistingFile_KeepsUnknownText()
    {
        using TempDataDir dir = new();
        File.WriteAllText(RegisterPath(dir), Seed("", "Trailing note."));
        RetroFileStore store = new(new DiskTeamFolders(dir.Path));

        store.AppendRows(Team, [new RegisterRow("R-003", "New", "Act", "QA", RetroStatus.Open, "later")]);

        Assert.Equal(
            Seed("| R-003 | New | Act | QA | Open | later |", "", "Trailing note."),
            File.ReadAllText(RegisterPath(dir)));
        Assert.Equal(3, store.ReadRegister(Team).Count);
    }

    /// <summary>Two stores appending to the same register at once lose no row and fail no write.</summary>
    [Fact]
    public async Task AppendRows_TwoStoresConcurrently_KeepEveryRow()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        File.WriteAllText(RegisterPath(dir), Seed());
        DiskTeamFolders folders = new(dir.Path);
        RetroFileStore first = new(folders);
        RetroFileStore second = new(folders);

        Task Writer(RetroFileStore store, string owner) => Task.Run(
            () =>
            {
                for (int i = 0; i < 25; i++)
                {
                    store.AppendRows(Team, [new RegisterRow(owner + i, "F", "A", owner, RetroStatus.Open, "e")]);
                }
            },
            ct);

        await Task.WhenAll(Writer(first, "a"), Writer(second, "b"));

        Assert.Equal(52, first.ReadRegister(Team).Count);
    }

    /// <summary>A status change racing with appends from another store keeps both.</summary>
    [Fact]
    public async Task SetStatus_RacingAppendRows_KeepsBoth()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        File.WriteAllText(RegisterPath(dir), Seed());
        DiskTeamFolders folders = new(dir.Path);
        RetroFileStore appender = new(folders);
        RetroFileStore marker = new(folders);

        Task append = Task.Run(
            () =>
            {
                for (int i = 0; i < 25; i++)
                {
                    appender.AppendRows(Team, [new RegisterRow("n" + i, "F", "A", "o", RetroStatus.Open, "e")]);
                }
            },
            ct);
        Task mark = Task.Run(
            () =>
            {
                for (int i = 0; i < 25; i++)
                {
                    marker.SetStatus(Team, "R-002", i % 2 == 0 ? RetroStatus.Verified : RetroStatus.Partial, "evidence " + i);
                }
            },
            ct);
        await Task.WhenAll(append, mark);

        IReadOnlyList<RegisterRow> rows = marker.ReadRegister(Team);
        Assert.Equal(27, rows.Count);
        Assert.Equal("evidence 24", rows.Single(static row => row.Id == "R-002").EvidenceNextCheck);
    }

    /// <summary>A Team with no register gets one created by the first append.</summary>
    [Fact]
    public void AppendRows_NoFile_CreatesRegister()
    {
        using TempDataDir dir = new();
        RetroFileStore store = new(new DiskTeamFolders(dir.Path));
        RegisterRow row = new("R-001", "New", "Act", "QA", RetroStatus.Open, "later");

        Assert.Empty(store.ReadRegister(Team));
        store.AppendRows(Team, [row]);

        Assert.Equal([row], store.ReadRegister(Team));
    }

    /// <summary>SetStatus rewrites the status and evidence of one row and leaves every other line alone.</summary>
    [Fact]
    public void SetStatus_ChangesOnlyThatRow()
    {
        using TempDataDir dir = new();
        File.WriteAllText(RegisterPath(dir), Seed("", "Trailing note."));
        RetroFileStore store = new(new DiskTeamFolders(dir.Path));

        store.SetStatus(Team, "R-002", RetroStatus.Verified, "Task 40");

        string expected = Lines(
            [
                .. Header()[..^1],
                "| R-002 | Flaky tests | Quarantine | QA | Verified | Task 40 |",
                "",
                "Trailing note.",
            ]);
        Assert.Equal(expected, File.ReadAllText(RegisterPath(dir)));
    }

    /// <summary>A row id that is not in the register is refused and the file is untouched.</summary>
    [Fact]
    public void SetStatus_UnknownRow_Throws()
    {
        using TempDataDir dir = new();
        File.WriteAllText(RegisterPath(dir), Seed());
        RetroFileStore store = new(new DiskTeamFolders(dir.Path));

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => store.SetStatus(Team, "R-009", RetroStatus.Done, "x"));

        Assert.Equal("The retro register has no row R-009.", ex.Message);
        Assert.Equal(Seed(), File.ReadAllText(RegisterPath(dir)));
    }

    /// <summary>The report is written under the retros folder with the exact headings and a fenced diff.</summary>
    [Fact]
    public void WriteReport_OneIssueOneProposal_WritesDatedFile()
    {
        using TempDataDir dir = new();
        RetroFileStore store = new(new DiskTeamFolders(dir.Path));

        string path = store.WriteReport(Team, Report(1, 1));

        Assert.Equal(Path.Combine(dir.Path, TeamPaths.RetrosDir, "2026-10-08.md"), path);
        Assert.Equal(
            Lines(
                "# Retro 2026-10-08",
                "",
                "## Top issues",
                "",
                "### 1. Issue 1",
                "",
                "Detail 1",
                "",
                "- Task 1",
                "",
                "## Proposed changes",
                "",
                "### P-1. Change 1",
                "",
                "Target: `Process.md`",
                "",
                "```diff",
                "-old",
                "+new",
                "```"),
            File.ReadAllText(path));
    }

    /// <summary>More than three issues or more than three proposals is refused and nothing is written.</summary>
    [Fact]
    public void WriteReport_MoreThanThree_Throws()
    {
        using TempDataDir dir = new();
        RetroFileStore store = new(new DiskTeamFolders(dir.Path));

        InvalidOperationException issues = Assert.Throws<InvalidOperationException>(() => store.WriteReport(Team, Report(4, 0)));
        InvalidOperationException changes = Assert.Throws<InvalidOperationException>(() => store.WriteReport(Team, Report(0, 4)));

        Assert.Equal("A retro report has at most 3 top issues.", issues.Message);
        Assert.Equal("A retro report has at most 3 proposed changes.", changes.Message);
        Assert.False(Directory.Exists(Path.Combine(dir.Path, TeamPaths.RetrosDir)));
    }

    /// <summary>Three issues and three proposals are accepted.</summary>
    [Fact]
    public void WriteReport_ExactlyThree_Writes()
    {
        using TempDataDir dir = new();
        RetroFileStore store = new(new DiskTeamFolders(dir.Path));

        string path = store.WriteReport(Team, Report(3, 3));

        Assert.True(File.Exists(path));
    }

    /// <summary>A second report on the same date gets a -2 suffix and the first is not changed.</summary>
    [Fact]
    public void WriteReport_SameDate_SuffixesAndKeepsFirst()
    {
        using TempDataDir dir = new();
        RetroFileStore store = new(new DiskTeamFolders(dir.Path));

        string first = store.WriteReport(Team, Report(1, 0));
        string firstText = File.ReadAllText(first);
        string second = store.WriteReport(Team, Report(2, 0));
        string third = store.WriteReport(Team, Report(1, 1));

        Assert.Equal(Path.Combine(dir.Path, TeamPaths.RetrosDir, "2026-10-08-2.md"), second);
        Assert.Equal(Path.Combine(dir.Path, TeamPaths.RetrosDir, "2026-10-08-3.md"), third);
        Assert.Equal(firstText, File.ReadAllText(first));
    }
}

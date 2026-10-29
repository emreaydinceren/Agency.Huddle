using Agency.Huddle.Tests.Factories;
using System.Globalization;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Tasks;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Tests for <see cref="RecordsBundleBuilder"/>: the evidence bundle a retro starts from.</summary>
public sealed class RecordsBundleBuilderTests
{
    private static readonly DateTimeOffset Since = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>A Review to To Do change since the cutoff is a bounce; Review to Done and the old bounce are not.</summary>
    [Fact]
    public void Build_ReviewToToDo_CountsAsBounce()
    {
        using TempDataDir dir = new();
        using Fixture fixture = new(dir);
        fixture.Write("PLAT-0001", "A", TaskState.ToDo, [
            TestTasks.Entry("2026-09-01T10:00:00Z", "Ada", "status: Review → To Do"),
            TestTasks.Entry("2026-10-02T10:00:00Z", "Ada", "status: Review → To Do"),
            TestTasks.Entry("2026-10-03T10:00:00Z", "Bo", "status: Review → In Progress"),
            TestTasks.Entry("2026-10-04T10:00:00Z", "Bo", "status: Review → Done"),
        ]);

        RecordsBundle bundle = fixture.Builder.Build("Platform", Since);

        Assert.Equal(["2026-10-02T10:00:00Z|Ada|PLAT-0001|PLAT-0001", "2026-10-03T10:00:00Z|Bo|PLAT-0001|PLAT-0001"], bundle.ReviewBounces.Select(Describe).ToArray());
    }

    /// <summary>Budget pauses, wake limits and Declines are found in the Change log and linked to the Task.</summary>
    [Fact]
    public void Build_BudgetWakeAndDecline_FillTheirSections()
    {
        using TempDataDir dir = new();
        using Fixture fixture = new(dir);
        fixture.Write("PLAT-0001", "A", TaskState.ToDo, [
            TestTasks.Entry("2026-10-02T10:00:00Z", "Ada", "wake-ups paused: budget spent"),
            TestTasks.Entry("2026-10-03T10:00:00Z", "Ada", "wake limit reached for the Task"),
        ]);
        fixture.Write("PLAT-0002", "B", TaskState.Backlog, [
            TestTasks.Entry("2026-10-04T10:00:00Z", "Bo", "triage: needs-info → declined (reason: out of scope)"),
        ]);

        RecordsBundle bundle = fixture.Builder.Build("Platform", Since);

        Assert.Equal(["2026-10-02T10:00:00Z|Ada|PLAT-0001|PLAT-0001"], bundle.BudgetPauses.Select(Describe).ToArray());
        Assert.Equal(["2026-10-03T10:00:00Z|Ada|PLAT-0001|PLAT-0001"], bundle.WakeLimitsHit.Select(Describe).ToArray());
        Assert.Equal(["2026-10-04T10:00:00Z|Bo|PLAT-0002|PLAT-0002"], bundle.Escalations.Select(Describe).ToArray());
    }

    /// <summary>The cutoff drops Tasks with no change since it, and other Teams' Tasks never appear.</summary>
    [Fact]
    public void Build_Since_FiltersTasksAndTeams()
    {
        using TempDataDir dir = new();
        using Fixture fixture = new(dir);
        fixture.Write("PLAT-0001", "Fresh", TaskState.InProgress, [TestTasks.Entry("2026-10-02T10:00:00Z", "Ada", "created")], assignee: "Ada");
        fixture.Write("PLAT-0002", "Stale", TaskState.Done, [TestTasks.Entry("2026-09-02T10:00:00Z", "Ada", "created")]);
        fixture.Write("WEB-0001", "Other", TaskState.ToDo, [TestTasks.Entry("2026-10-02T10:00:00Z", "Ada", "created")], team: "Web");

        RecordsBundle bundle = fixture.Builder.Build("platform", Since);

        TaskRecord only = Assert.Single(bundle.Tasks);
        Assert.Equal(new TaskRecord("PLAT-0001", "Fresh", "In Progress", "Ada"), only);
        Assert.Equal("Platform", bundle.Team);
        Assert.Equal(Since, bundle.Since);
    }

    /// <summary>A Request Task since the cutoff is listed with its kind, requesting Team and triage.</summary>
    [Fact]
    public void Build_RequestTask_ListedAsRequest()
    {
        using TempDataDir dir = new();
        using Fixture fixture = new(dir);
        fixture.Write("PLAT-0001", "Plain", TaskState.ToDo, [TestTasks.Entry("2026-10-02T10:00:00Z", "Ada", "created")]);
        fixture.Write(
            "PLAT-0002",
            "Fix login",
            TaskState.Backlog,
            [TestTasks.Entry("2026-10-02T10:00:00Z", "Zed", "created")],
            unknownFields: [new("request", "bug"), new("from_team", "Web"), new("requester", "Zed"), new("triage", "accepted")]);

        RecordsBundle bundle = fixture.Builder.Build("Platform", Since);

        RequestRecord request = Assert.Single(bundle.Requests);
        Assert.Equal(new RequestRecord("PLAT-0002", "Fix login", "Bug", "Web", "Accepted", "Backlog"), request);
        Assert.Equal(2, bundle.Tasks.Count);
    }

    /// <summary>Tool-call logs aggregate per member and tool since the cutoff; a corrupt line is skipped and counted.</summary>
    [Fact]
    public void Build_ToolCallLog_AggregatesAndSkipsCorruptLines()
    {
        using TempDataDir dir = new();
        using Fixture fixture = new(dir);
        fixture.WriteLog(
            "Ada",
            "2026-10-02",
            [
                Line("2026-10-02T09:00:00Z", "add_task", "completed"),
                Line("2026-10-02T09:05:00Z", "add_task", "failed"),
                "{ not json",
                Line("2026-10-02T09:10:00Z", "list_tasks", "completed"),
            ]);
        fixture.WriteLog("Ada", "2026-09-20", [Line("2026-09-20T09:00:00Z", "add_task", "completed")]);
        fixture.WriteLog("Bo", "2026-10-01", [Line("2026-09-30T23:00:00Z", "add_task", "completed"), Line("2026-10-01T08:00:00Z", "add_task", "failed")]);

        RecordsBundle bundle = fixture.Builder.Build("Platform", Since);

        Assert.Equal(
            ["Ada|add_task|2|1", "Ada|list_tasks|1|0", "Bo|add_task|1|1"],
            bundle.ToolCalls.OrderBy(static c => c.Member, StringComparer.Ordinal).ThenBy(static c => c.Tool, StringComparer.Ordinal)
                .Select(static c => string.Create(CultureInfo.InvariantCulture, $"{c.Member}|{c.Tool}|{c.Calls}|{c.Failures}")).ToArray());
        Assert.Equal(1, bundle.Unreadable);
    }

    /// <summary>Spend is gathered per Agent from the spend store.</summary>
    [Fact]
    public void Build_Spend_GroupedByMember()
    {
        using TempDataDir dir = new();
        using Fixture fixture = new(dir);
        fixture.Spend.Add("Ada", null, "USD", 1.5m);
        fixture.Spend.Add("Ada", "PLAT-0001", "USD", 0.5m);
        fixture.Spend.Add("Zed", null, "USD", 9m);

        RecordsBundle bundle = fixture.Builder.Build("Platform", Since);

        Assert.Equal(["Ada"], bundle.SpendByMember.Keys.ToArray());
        Assert.Equal(2.0m, Assert.Single(bundle.SpendByMember["Ada"]).Amount);
    }

    /// <summary>The Team's members, not other Personas' logs, are read; an unknown Team gives an empty bundle.</summary>
    [Fact]
    public void Build_UnknownTeam_IsEmpty()
    {
        using TempDataDir dir = new();
        using Fixture fixture = new(dir);
        fixture.Write("PLAT-0001", "A", TaskState.ToDo, [TestTasks.Entry("2026-10-02T10:00:00Z", "Ada", "created")]);

        RecordsBundle bundle = fixture.Builder.Build("Nowhere", Since);

        Assert.Empty(bundle.Tasks);
        Assert.Empty(bundle.ToolCalls);
        Assert.Empty(bundle.SpendByMember);
    }

    /// <summary>The text lists counts first, then events newest first with the Task link, and ends with the unreadable count.</summary>
    [Fact]
    public void RenderText_CountsFirstThenNewestEventsWithLinks()
    {
        using TempDataDir dir = new();
        using Fixture fixture = new(dir);
        fixture.Write("PLAT-0001", "A", TaskState.ToDo, [
            TestTasks.Entry("2026-10-02T10:00:00Z", "Ada", "status: Review → To Do"),
            TestTasks.Entry("2026-10-05T10:00:00Z", "Bo", "status: Review → To Do"),
        ]);
        fixture.WriteLog("Ada", "2026-10-02", ["garbage", Line("2026-10-02T09:00:00Z", "add_task", "failed")]);
        RecordsBundle bundle = fixture.Builder.Build("Platform", Since);

        string[] lines = RecordsBundleBuilder.RenderText(bundle).Split('\n');

        Assert.Equal("Records for Platform since 2026-10-01", lines[0]);
        Assert.Equal("review bounces: 2", lines[2]);
        Assert.Equal("tool calls: 1 (1 failed)", lines[7]);
        Assert.Equal("bounce 2026-10-05 10:00 Bo PLAT-0001 status: Review → To Do [PLAT-0001]", lines[10]);
        Assert.Equal("bounce 2026-10-02 10:00 Ada PLAT-0001 status: Review → To Do [PLAT-0001]", lines[11]);
        Assert.Equal("unreadable: 1", lines[^1]);
    }

    /// <summary>A long event list is cut under the cap with a "more" line that counts the omitted events.</summary>
    [Fact]
    public void RenderText_OverCap_TruncatesWithMoreLine()
    {
        using TempDataDir dir = new();
        using Fixture fixture = new(dir);
        List<ChangeLogEntry> entries = [];
        for (int i = 0; i < 300; i++)
        {
            entries.Add(new ChangeLogEntry(Since.AddHours(1 + i), "Ada", "status: Review → To Do"));
        }

        fixture.Write("PLAT-0001", "A", TaskState.ToDo, entries);
        RecordsBundle bundle = fixture.Builder.Build("Platform", Since);

        string text = RecordsBundleBuilder.RenderText(bundle, 2000);

        string[] lines = text.Split('\n');
        int shown = lines.Count(static l => l.StartsWith("bounce ", StringComparison.Ordinal));
        Assert.True(text.Length <= 2000);
        Assert.True(shown > 0);
        Assert.Equal(string.Create(CultureInfo.InvariantCulture, $"... {300 - shown} more, ask for a narrower window"), lines[^1]);
    }

    /// <summary>Review fix 6: a tool-call log over 1 MB is read from its tail, so the newest lines count and the memory used is bounded.</summary>
    [Fact]
    public void Build_OversizedLog_ReadsOnlyTheLastMegabyte()
    {
        using TempDataDir dir = new();
        using Fixture fixture = new(dir);
        string head = Line("2026-10-02T09:00:00Z", "head_tool", "completed");
        List<string> lines = [.. Enumerable.Repeat(head, 12000), Line("2026-10-02T10:00:00Z", "tail_tool", "completed")];
        fixture.WriteLog("Ada", "2026-10-02", lines);

        RecordsBundle bundle = fixture.Builder.Build("Platform", Since);

        ToolCallSummary tail = Assert.Single(bundle.ToolCalls, static call => call.Tool == "tail_tool");
        ToolCallSummary heads = Assert.Single(bundle.ToolCalls, static call => call.Tool == "head_tool");
        Assert.Equal(1, tail.Calls);
        Assert.InRange(heads.Calls, 1, 1048576 / (head.Length + 1));
        Assert.Equal(0, bundle.Unreadable);
    }

    private static string Describe(RecordEvent e) =>
        string.Create(CultureInfo.InvariantCulture, $"{e.Time.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}|{e.Member}|{e.TaskId}|{e.Link}");

    private static string Line(string time, string tool, string status) =>
        string.Create(CultureInfo.InvariantCulture, $$"""{"time":"{{time}}","room":"r","task":null,"tool":"{{tool}}","arguments":"x","status":"{{status}}","durationMs":5}""");

    private sealed class Fixture : IDisposable
    {
        private readonly TempDataDir dir;
        private TaskStore? tasks;
        private RecordsBundleBuilder? builder;

        public Fixture(TempDataDir dir)
        {
            this.dir = dir;
            this.Spend = new PersonaSpendStore(dir.Options());
            this.Catalog.Teams = [new TeamSummary("Platform", [], ["Ada", "Bo"], true)];
        }

        public FakeTeamCatalog Catalog { get; } = new();

        public PersonaSpendStore Spend { get; }

        public RecordsBundleBuilder Builder
        {
            get
            {
                if (this.builder is null)
                {
                    this.tasks = TestTaskStore.CreateTaskStore(this.dir, this.dir.CreatePersonaStore());
                    this.builder = new RecordsBundleBuilder(this.tasks, this.Catalog, this.Spend, this.dir.Options());
                }

                return this.builder;
            }
        }

        public void Write(
            string id,
            string title,
            TaskState status,
            IReadOnlyList<ChangeLogEntry> log,
            string team = "Platform",
            string? assignee = null,
            IReadOnlyList<KeyValuePair<string, string>>? unknownFields = null)
        {
            TaskItem task = TestTasks.Make(id, title, status, assignee: assignee, changeLog: log, location: new(team, null, false), unknownFields: unknownFields);
            _ = TestTaskStore.WriteTask(TestTaskStore.Root(this.dir), TestTaskStore.RelativePath(team, null, false, id + ".md"), task);
        }

        public void WriteLog(string member, string day, IReadOnlyList<string> lines)
        {
            string folder = Path.Combine(this.dir.Path, "logs", "tool-calls", member);
            _ = Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, day + ".jsonl"), string.Join('\n', lines) + "\n");
        }

        public void Dispose() => this.tasks?.Dispose();
    }
}

using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.Tasks;
using static Agency.Huddle.Tests.Tasks.TaskGateText;

namespace Agency.Huddle.Tests.Acp.Tools;

/// <summary>
/// Tests for <see cref="NextTaskTool"/> (TaskOrder spec §5.1, C-1, C-4): the caller's own active Tasks only, the optional
/// <c>team</c> filter, the Started / Next / After it / Waiting parts with their exact wording, the "nothing startable" text,
/// and the line <see cref="UpdateTaskTool"/> adds to a refused start.
/// </summary>
public sealed class NextTaskToolTests
{
    /// <summary>Writes each fixture Task to the harness's disk-backed store at the path its Location maps to, then rescans.</summary>
    private static void Seed(TaskToolHarness harness, params TaskItem[] tasks)
    {
        foreach (TaskItem task in tasks)
        {
            string fullPath = TaskLayout.PathFor(harness.TasksDirPath, task.Location, task.Id);
            string relativePath = Path.GetRelativePath(harness.TasksDirPath, fullPath);
            TestTaskStore.WriteTask(harness.TasksDirPath, relativePath, task);
        }

        harness.Store.RebuildFromWatcher();
    }

    private static NextTaskTool CreateTool(TaskToolHarness harness) =>
        new(harness.Store, harness.Directory, new FakePromptSource(), harness.NovaIdOrThrow());

    /// <summary>The start of the blockers gate's refusal text.</summary>
    private const string GatePrefix = "Gate blockers-done-before-start:";

    /// <summary>Another Agent's Task never appears in the caller's answer, and the caller's own does.</summary>
    [Fact]
    public async Task NextTask_OtherAgentsTask_IsNotListed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        Seed(
            harness,
            TestTasks.Make(id: "PLAT-0001", title: "Mine", assignee: "Nova"),
            TestTasks.Make(id: "PLAT-0002", title: "Kai's", assignee: "Kai"));

        string result = await CreateTool(harness).InvokeAsync(new JsonObject(), ct);

        Assert.Equal("Next: PLAT-0001 \"Mine\" (To Do).", result);
    }

    /// <summary>The <c>team</c> argument narrows the scope to that Team's Tasks, ignoring case.</summary>
    [Fact]
    public async Task NextTask_TeamArgument_ListsOnlyThatTeam()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        Seed(
            harness,
            TestTasks.Make(id: "PLAT-0001", title: "In Platform", assignee: "Nova"),
            TestTasks.Make(id: "OPS-0001", title: "In Ops", assignee: "Nova", location: new TaskLocation("Ops", null, false)));

        string result = await CreateTool(harness).InvokeAsync(new JsonObject { ["team"] = "ops" }, ct);

        Assert.Equal("Next: OPS-0001 \"In Ops\" (To Do).", result);
    }

    /// <summary>Without a <c>team</c>, the caller's Tasks in every Team are ordered together.</summary>
    [Fact]
    public async Task NextTask_NoTeam_SpansEveryTeam()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        Seed(
            harness,
            TestTasks.Make(id: "PLAT-0001", title: "In Platform", assignee: "Nova") with { Rank = 2 },
            TestTasks.Make(id: "OPS-0001", title: "In Ops", assignee: "Nova", location: new TaskLocation("Ops", null, false)) with { Rank = 1 });

        string result = await CreateTool(harness).InvokeAsync(new JsonObject(), ct);

        Assert.Equal("Next: OPS-0001 \"In Ops\" (To Do, rank 1).\nAfter it: PLAT-0001 \"In Platform\" (To Do, rank 2).", result);
    }

    /// <summary>A wrongly-typed <c>team</c> is refused rather than thrown.</summary>
    [Fact]
    public async Task NextTask_TeamOfWrongType_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);

        string result = await CreateTool(harness).InvokeAsync(new JsonObject { ["team"] = 5 }, ct);

        Assert.Equal("'team' must be text.", result);
    }

    /// <summary>The answer has a Started part, the Next Task, the After-it list and a Waiting part naming the blocker's status and assignee.</summary>
    [Fact]
    public async Task NextTask_AllThreeParts_ReadExactly()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        Seed(
            harness,
            TestTasks.Make(id: "PLAT-0001", title: "Fix login", status: TaskState.InProgress, assignee: "Nova"),
            TestTasks.Make(id: "PLAT-0002", title: "Add export", assignee: "Nova") with { Rank = 2 },
            TestTasks.Make(id: "PLAT-0003", title: "Docs", assignee: "Nova") with { Rank = 3 },
            TestTasks.Make(id: "PLAT-0004", title: "Import", assignee: "Nova", blockedBy: [new TaskId("PLAT", 5)]) with { Rank = 4 },
            TestTasks.Make(id: "PLAT-0005", title: "Schema", assignee: "Kai"));

        string result = await CreateTool(harness).InvokeAsync(new JsonObject(), ct);

        Assert.Equal(
            "Started: PLAT-0001 \"Fix login\" (In Progress). Finish it before you start another.\n"
            + "Next: PLAT-0002 \"Add export\" (To Do, rank 2).\n"
            + "After it: PLAT-0003 \"Docs\" (To Do, rank 3).\n"
            + "Waiting: PLAT-0004 \"Import\" is blocked by PLAT-0005 (To Do, Kai).",
            result);
    }

    /// <summary>At most five Tasks are named after Next.</summary>
    [Fact]
    public async Task NextTask_ManyStartable_NamesAtMostFiveAfterNext()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem[] tasks = [.. Enumerable.Range(1, 8).Select(n => TestTasks.Make(id: $"PLAT-{n:0000}", title: $"T{n}", assignee: "Nova") with { Rank = n })];
        Seed(harness, tasks);

        string result = await CreateTool(harness).InvokeAsync(new JsonObject(), ct);

        Assert.Equal(
            "Next: PLAT-0001 \"T1\" (To Do, rank 1).\n"
            + "After it: PLAT-0002 \"T2\" (To Do, rank 2); PLAT-0003 \"T3\" (To Do, rank 3); PLAT-0004 \"T4\" (To Do, rank 4); "
            + "PLAT-0005 \"T5\" (To Do, rank 5); PLAT-0006 \"T6\" (To Do, rank 6).",
            result);
    }

    /// <summary>When every Task waits, the answer says nothing is startable and still lists what each waits on.</summary>
    [Fact]
    public async Task NextTask_NothingStartable_SaysSoAndNamesTheBlocker()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        Seed(
            harness,
            TestTasks.Make(id: "PLAT-0001", title: "Import", assignee: "Nova", blockedBy: [new TaskId("PLAT", 2)]),
            TestTasks.Make(id: "PLAT-0002", title: "Schema", status: TaskState.InProgress, assignee: "Kai"));

        string result = await CreateTool(harness).InvokeAsync(new JsonObject(), ct);

        Assert.Equal(
            "Nothing is startable right now.\nWaiting: PLAT-0001 \"Import\" is blocked by PLAT-0002 (In Progress, Kai).",
            result);
    }

    /// <summary>A caller with no active Task gets the nothing-startable text alone.</summary>
    [Fact]
    public async Task NextTask_NoTasks_SaysNothingIsStartable()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);

        string result = await CreateTool(harness).InvokeAsync(new JsonObject(), ct);

        Assert.Equal("Nothing is startable right now.", result);
    }

    /// <summary>Two Tasks that block each other are named once, with the ask-your-manager advice.</summary>
    [Fact]
    public async Task NextTask_BlockerCycle_IsNamedOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        Seed(
            harness,
            TestTasks.Make(id: "PLAT-0001", title: "A", assignee: "Nova", blockedBy: [new TaskId("PLAT", 2)]) with { Rank = 1 },
            TestTasks.Make(id: "PLAT-0002", title: "B", assignee: "Nova", blockedBy: [new TaskId("PLAT", 1)]) with { Rank = 2 });

        string result = await CreateTool(harness).InvokeAsync(new JsonObject(), ct);

        Assert.Equal("Nothing is startable right now.\nWaiting: PLAT-0001 and PLAT-0002 block each other; ask your manager.", result);
    }

    /// <summary>A Backlog Task that holds up a To Do one is listed as waiting, with the Task it blocks.</summary>
    [Fact]
    public async Task NextTask_BacklogBlocker_IsWaitingWithWhatItBlocks()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        Seed(
            harness,
            TestTasks.Make(id: "PLAT-0001", title: "Base", status: TaskState.Backlog, assignee: "Nova"),
            TestTasks.Make(id: "PLAT-0002", title: "Top", assignee: "Nova", blockedBy: [new TaskId("PLAT", 1)]));

        string result = await CreateTool(harness).InvokeAsync(new JsonObject(), ct);

        Assert.Equal(
            "Nothing is startable right now.\n"
            + "Waiting: PLAT-0001 \"Base\" is in Backlog and blocks PLAT-0002. PLAT-0002 \"Top\" is blocked by PLAT-0001 (Backlog, Nova).",
            result);
    }

    /// <summary>A refused start adds the caller's next startable Task as a last line, and leaves the gate's own text first.</summary>
    [Fact]
    public async Task UpdateTask_BlockedStart_AddsNextStartableTask()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, gates: BuildGates(gateDir, Process("'blockers-done-before-start'"), manager: "Rhea"));
        TaskItem baseTask = Create(harness, "Base", TaskState.Backlog, null, null, null);
        TaskItem blocked = Create(harness, "Blocked", TaskState.ToDo, "Nova", [baseTask.Id], 1);
        TaskItem other = Create(harness, "Other", TaskState.ToDo, "Nova", null, 2);
        UpdateTaskTool tool = new(harness.Service, harness.Store, harness.Triggers, harness.Directory, harness.Sprints, new FakePromptSource(), harness.NovaIdOrThrow());

        string result = await tool.InvokeAsync(new JsonObject { ["taskId"] = blocked.Id.ToString(), ["status"] = "In Progress" }, ct);

        string[] lines = result.Split('\n');
        Assert.Equal("Gate blockers-done-before-start:", lines[0][..GatePrefix.Length]);
        Assert.Equal($"Your next startable Task is {other.Id} \"Other\".", lines[^1]);
    }

    /// <summary>A refused start when the caller has nothing else startable says so.</summary>
    [Fact]
    public async Task UpdateTask_BlockedStart_NothingElse_SaysNoStartableTask()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, gates: BuildGates(gateDir, Process("'blockers-done-before-start'"), manager: "Rhea"));
        TaskItem baseTask = Create(harness, "Base", TaskState.Backlog, null, null, null);
        TaskItem blocked = Create(harness, "Blocked", TaskState.ToDo, "Nova", [baseTask.Id], 1);
        UpdateTaskTool tool = new(harness.Service, harness.Store, harness.Triggers, harness.Directory, harness.Sprints, new FakePromptSource(), harness.NovaIdOrThrow());

        string result = await tool.InvokeAsync(new JsonObject { ["taskId"] = blocked.Id.ToString(), ["status"] = "In Progress" }, ct);

        string[] lines = result.Split('\n');
        Assert.Equal("Gate blockers-done-before-start:", lines[0][..GatePrefix.Length]);
        Assert.Equal("You have no startable Task.", lines[^1]);
    }

    /// <summary>A refusal from another gate gets no next-Task line.</summary>
    [Fact]
    public async Task UpdateTask_OtherRefusal_HasNoNextTaskLine()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        UpdateTaskTool tool = new(harness.Service, harness.Store, harness.Triggers, harness.Directory, harness.Sprints, new FakePromptSource(), harness.NovaIdOrThrow());

        string result = await tool.InvokeAsync(new JsonObject { ["taskId"] = "PLAT-9999", ["status"] = "In Progress" }, ct);

        Assert.DoesNotContain("startable", result, StringComparison.Ordinal);
    }

    /// <summary>Three Tasks in one blocker cycle are named once, joined as a list.</summary>
    [Fact]
    public async Task NextTask_ThreeTaskCycle_NamesAllThreeOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        Seed(
            harness,
            TestTasks.Make(id: "PLAT-0001", title: "A", assignee: "Nova", blockedBy: [new TaskId("PLAT", 2)]) with { Rank = 1 },
            TestTasks.Make(id: "PLAT-0002", title: "B", assignee: "Nova", blockedBy: [new TaskId("PLAT", 3)]) with { Rank = 2 },
            TestTasks.Make(id: "PLAT-0003", title: "C", assignee: "Nova", blockedBy: [new TaskId("PLAT", 1)]) with { Rank = 3 });

        string result = await CreateTool(harness).InvokeAsync(new JsonObject(), ct);

        Assert.Equal("Nothing is startable right now.\nWaiting: PLAT-0001, PLAT-0002 and PLAT-0003 block each other; ask your manager.", result);
    }

    /// <summary>Creates a Platform Task as the Human.</summary>
    private static TaskItem Create(TaskToolHarness harness, string title, TaskState status, string? assignee, IReadOnlyList<TaskId>? blockedBy, int? rank)
    {
        TaskDraft draft = new(title, Team, null, status, Assignee: assignee, BlockedBy: blockedBy) { Rank = rank };
        return Assert.IsType<TaskResult.Saved>(harness.Service.Create(draft, harness.HumanActor())).Task;
    }
}

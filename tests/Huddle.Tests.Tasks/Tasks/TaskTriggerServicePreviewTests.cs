using Agency.Huddle.App;
using Agency.Huddle.App.Tasks;
using static Agency.Huddle.Tests.Tasks.TaskTriggerTestSupport;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// SW-T5 (smart wakes spec 5.4, corrections 5 and 6): <see cref="TaskTriggerService.Preview(TaskItem?, TaskItem, TaskActor)"/> diffs the Task
/// before and after and says <see cref="WakeBlock.NothingWakes"/> when nothing that wakes the assignee changed, and
/// <c>Preview(TaskChange, TaskActor)</c> judges a <see cref="TaskChange"/> by the same rule, so a close of an In
/// Progress Task is not silent. The guards of the other <see cref="WakeBlock"/> values are pinned in <c>TaskTriggerPreviewTests</c>.
/// </summary>
public sealed class TaskTriggerServicePreviewTests
{
    private static readonly TaskActor Kai = new(TaskActorKind.Agent, "Kai", "agent-kai");

    /// <summary>Arranges a harness whose Personas Nova and Kai are registered, and one Task assigned to Nova.</summary>
    private static async Task<(Harness Harness, TaskItem Task)> ArrangeAsync(TempDataDir dir, TeamOptions options, CancellationToken ct, string? assignee = "Nova", TaskState status = TaskState.ToDo)
    {
        Harness harness = await CreateHarnessAsync(dir, options, ct);
        _ = await StartWakingAsync(harness, ct);
        return (harness, TestTasks.Make(status: status, assignee: assignee));
    }

    /// <summary>A rank-only change is <see cref="WakeBlock.NothingWakes"/> given the Task before, while the same Task with no before keeps today's meaning, a change that wakes.</summary>
    [Fact]
    public async Task Preview_BeforeAndARankOnlyAfter_IsNothingWakes_WhereANullBeforeIsNot()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (Harness harness, TaskItem before) = await ArrangeAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        using Harness owned = harness;
        TaskItem after = before with { Rank = 1 };

        WakeBlock[] blocks =
        [
            harness.Trigger.Preview(before, after, HumanActor).Block,
            harness.Trigger.Preview(null, after, HumanActor).Block,
        ];

        Assert.Equal([WakeBlock.NothingWakes, WakeBlock.None], blocks);
    }

    /// <summary>Each silent field on its own (priority, tags, dates, sprint, parent) is <see cref="WakeBlock.NothingWakes"/>, and a title edit beside one is not.</summary>
    [Fact]
    public async Task Preview_OnlySilentFieldsChanged_IsNothingWakes_AndATitleEditBesideThemIsNot()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (Harness harness, TaskItem before) = await ArrangeAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        using Harness owned = harness;
        TaskItem silent = before with { Priority = TaskPriority.Urgent, Tags = ["x"], DueDate = new DateOnly(2026, 12, 1) };

        WakeBlock[] blocks =
        [
            harness.Trigger.Preview(before, silent, HumanActor).Block,
            harness.Trigger.Preview(before, silent with { Title = "Renamed" }, HumanActor).Block,
        ];

        Assert.Equal([WakeBlock.NothingWakes, WakeBlock.None], blocks);
    }

    /// <summary>A status change that wakes the assignee (To Do to In Progress) is <see cref="WakeBlock.None"/>, and a silent status move (Backlog to Cancelled) is <see cref="WakeBlock.NothingWakes"/>.</summary>
    [Fact]
    public async Task Preview_StatusChange_FollowsTheStatusTable()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (Harness harness, TaskItem toDo) = await ArrangeAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        using Harness owned = harness;
        TaskItem backlog = toDo with { Status = TaskState.Backlog };

        WakeBlock[] blocks =
        [
            harness.Trigger.Preview(toDo, toDo with { Status = TaskState.InProgress }, HumanActor).Block,
            harness.Trigger.Preview(backlog, backlog with { Status = TaskState.Cancelled }, HumanActor).Block,
        ];

        Assert.Equal([WakeBlock.None, WakeBlock.NothingWakes], blocks);
    }

    /// <summary><see cref="WakeBlock.NothingWakes"/> is judged after the Disabled check and before the assignee checks: with wake-ups off a rank-only change says Disabled, and with no assignee it says NothingWakes, not NoAssignee.</summary>
    [Fact]
    public async Task Preview_NothingWakes_IsJudgedAfterDisabledAndBeforeTheAssigneeChecks()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions off = new() { HumanName = "You" };
        off.Tasks.WakeEnabled = false;
        (Harness disabled, TaskItem task) = await ArrangeAsync(dir, off, ct);
        using Harness ownedDisabled = disabled;
        using TempDataDir otherDir = new();
        (Harness unassigned, TaskItem noAssignee) = await ArrangeAsync(otherDir, new TeamOptions { HumanName = "You" }, ct, assignee: null);
        using Harness ownedUnassigned = unassigned;

        WakeBlock[] blocks =
        [
            disabled.Trigger.Preview(task, task with { Rank = 1 }, HumanActor).Block,
            unassigned.Trigger.Preview(noAssignee, noAssignee with { Rank = 1 }, HumanActor).Block,
            unassigned.Trigger.Preview(noAssignee, noAssignee with { Title = "Renamed" }, HumanActor).Block,
        ];

        Assert.Equal([WakeBlock.Disabled, WakeBlock.NothingWakes, WakeBlock.NoAssignee], blocks);
    }

    /// <summary>The <see cref="TaskChange"/> overload judges a close by the Task's status: In Progress and Needs You are <see cref="WakeBlock.None"/>, To Do and Done close silently.</summary>
    [Fact]
    public async Task PreviewChange_Close_WakesOnlyAnActiveTask()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (Harness harness, TaskItem task) = await ArrangeAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        using Harness owned = harness;

        WakeBlock[] blocks = [.. new[] { TaskState.InProgress, TaskState.NeedsYou, TaskState.ToDo, TaskState.Done }
            .Select(status => harness.Trigger.Preview(Closing(task with { Status = status }), Kai).Block)];

        Assert.Equal([WakeBlock.None, WakeBlock.None, WakeBlock.NothingWakes, WakeBlock.NothingWakes], blocks);
    }

    /// <summary>The <see cref="TaskChange"/> overload keeps the guards after the rule: a close of an In Progress Task with no assignee is NoAssignee, and one assigned to the actor is AssigneeIsActor.</summary>
    [Fact]
    public async Task PreviewChange_ActiveClose_StillAppliesTheAssigneeGuards()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (Harness harness, TaskItem task) = await ArrangeAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        using Harness owned = harness;
        TaskItem active = task with { Status = TaskState.InProgress };

        WakeBlock[] blocks =
        [
            harness.Trigger.Preview(Closing(active with { Assignee = null }), Kai).Block,
            harness.Trigger.Preview(Closing(active with { Assignee = "Kai" }), Kai).Block,
            harness.Trigger.Preview(Closing(active), Kai).Block,
        ];

        Assert.Equal([WakeBlock.NoAssignee, WakeBlock.AssigneeIsActor, WakeBlock.None], blocks);
    }

    /// <summary>The <see cref="TaskChange"/> overload wakes a Create (no Before) and a reopen of an In Progress Task, and a Decision, and is silent for an edit of only a rank.</summary>
    [Fact]
    public async Task PreviewChange_CreateReopenDecisionAndSilentEdit_FollowTheRule()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (Harness harness, TaskItem task) = await ArrangeAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        using Harness owned = harness;
        TaskItem active = task with { Status = TaskState.InProgress };
        ChangeLogEntry entry = new(DateTimeOffset.UnixEpoch, "Kai", "changed");
        TaskChange create = new(null, task, [], Kai, entry, Kind: TaskChangeKind.Create);
        TaskChange reopen = new(Closed(active), active, [], Kai, entry, Kind: TaskChangeKind.Reopen);
        TaskChange rankOnly = new(task, task with { Rank = 1 }, [new FieldChange(TaskField.Rank, "2", "1")], Kai, entry);
        TaskChange decision = new(task, task, [], HumanActor, entry, Agency.Huddle.App.Tasks.HumanGate.TaskDecision.Accept);

        WakeBlock[] blocks = [.. new[] { create, reopen, rankOnly, decision }.Select(change => harness.Trigger.Preview(change, change.Actor).Block)];

        Assert.Equal([WakeBlock.None, WakeBlock.None, WakeBlock.NothingWakes, WakeBlock.None], blocks);
    }

    /// <summary>A change that closes <paramref name="task"/>: before it is open, after it sits in the closed folder.</summary>
    private static TaskChange Closing(TaskItem task) =>
        new(task, Closed(task), [], Kai, new ChangeLogEntry(DateTimeOffset.UnixEpoch, "Kai", "closed"), Kind: TaskChangeKind.Close);

    /// <summary><paramref name="task"/> as it sits in the closed folder.</summary>
    private static TaskItem Closed(TaskItem task) => task with { Location = task.Location with { Closed = true } };
}

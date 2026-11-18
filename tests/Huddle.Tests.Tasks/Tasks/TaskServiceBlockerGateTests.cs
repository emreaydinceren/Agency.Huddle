using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Tasks.HumanGate;
using static Agency.Huddle.Tests.Tasks.TaskGateText;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// <see cref="TaskService"/> runs the <c>blockers-done-before-start</c> gate (#538) on update, create and outside edits,
/// judged against the stored <c>blocked_by</c>; only the Team's manager or the Human may remove a blocker.
/// </summary>
public sealed class TaskServiceBlockerGateTests
{
    private const string BlockerGate = "'blockers-done-before-start'";
    private const string Manager = "Rhea";
    private const string RemoveRefusal = "Only the Team's manager or the Human can remove a blocker from blocked_by.";

    private static readonly TaskActor Jane = new(TaskActorKind.Agent, "Jane", "jane-id");
    private static readonly TaskActor Rhea = new(TaskActorKind.Agent, Manager, "rhea-id");

    /// <summary>A Developer moving a Task whose blocker is open to In Progress is refused, the refusal names the blocker, and nothing is written.</summary>
    [Fact]
    public void Update_AgentStartsBlockedTask_IsRefusedNamingBlocker()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(BlockerGate), manager: Manager));
        (TaskItem blocker, TaskItem blocked) = SeedPair(harness);

        TaskResult result = harness.Service.Update(blocked.Id, MoveTo(TaskState.InProgress), null, Jane);

        TaskResult.GateRefused refused = Assert.IsType<TaskResult.GateRefused>(result);
        Assert.Equal(GateId.BlockersDoneBeforeStart, refused.Gate);
        Assert.Contains($"blocked by {blocker.Id} (Backlog)", refused.Message, StringComparison.Ordinal); // contains-ok: the full refusal text is pinned in ProcessGateServiceBlockerTests; this checks the service names the stored blocker
        Assert.Equal(TaskState.Backlog, harness.Store.Get(blocked.Id)?.Status);
    }

    /// <summary>A blocked Task may still move to To Do: planning it is not starting it.</summary>
    [Fact]
    public void Update_BlockedTaskToToDo_Saves()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(BlockerGate), manager: Manager));
        (_, TaskItem blocked) = SeedPair(harness);

        TaskResult result = harness.Service.Update(blocked.Id, MoveTo(TaskState.ToDo), null, Jane);

        Assert.IsType<TaskResult.Saved>(result);
    }

    /// <summary>Once the blocker is terminal (Done or Cancelled), the blocked Task can start.</summary>
    /// <param name="finished">The blocker's terminal status.</param>
    [Theory]
    [InlineData(TaskState.Done)]
    [InlineData(TaskState.Cancelled)]
    public void Update_BlockerTerminal_AllowsStart(TaskState finished)
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(BlockerGate), manager: Manager));
        (TaskItem blocker, TaskItem blocked) = SeedPair(harness);
        Assert.IsType<TaskResult.Saved>(harness.Service.Update(blocker.Id, new TaskPatch { Status = finished, Reason = finished == TaskState.Cancelled ? "settled" : null }, null, HumanOf(harness)));

        TaskResult result = harness.Service.Update(blocked.Id, MoveTo(TaskState.InProgress), null, Jane);

        Assert.Equal(TaskState.InProgress, Assert.IsType<TaskResult.Saved>(result).Task.Status);
    }

    /// <summary>Clearing <c>blocked_by</c> and starting in the same call is judged against the stored blockers, so even the manager is refused.</summary>
    [Fact]
    public void Update_ClearBlockersAndStartInOneCall_IsRefused()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(BlockerGate), manager: Manager));
        (_, TaskItem blocked) = SeedPair(harness);

        TaskResult result = harness.Service.Update(blocked.Id, new TaskPatch { BlockedBy = [], Status = TaskState.InProgress }, null, Rhea);

        Assert.Equal(GateId.BlockersDoneBeforeStart, Assert.IsType<TaskResult.GateRefused>(result).Gate);
    }

    /// <summary>An Agent who is not the Team's manager cannot remove a blocker, and the blocker stays.</summary>
    [Fact]
    public void Update_NonManagerRemovesBlocker_IsRefused()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(BlockerGate), manager: Manager));
        (TaskItem blocker, TaskItem blocked) = SeedPair(harness);

        TaskResult result = harness.Service.Update(blocked.Id, new TaskPatch { BlockedBy = [] }, null, Jane);

        Assert.Equal([RemoveRefusal], Assert.IsType<TaskResult.Refused>(result).Problems);
        Assert.Equal([blocker.Id], harness.Store.Get(blocked.Id)?.BlockedBy);
    }

    /// <summary>The manager may remove a blocker, after which the Developer can start the Task.</summary>
    [Fact]
    public void Update_ManagerRemovesBlocker_ThenStartSaves()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(BlockerGate), manager: Manager));
        (_, TaskItem blocked) = SeedPair(harness);

        TaskResult removed = harness.Service.Update(blocked.Id, new TaskPatch { BlockedBy = [] }, null, Rhea);
        TaskResult started = harness.Service.Update(blocked.Id, MoveTo(TaskState.InProgress), null, Jane);

        Assert.IsType<TaskResult.Saved>(removed);
        Assert.IsType<TaskResult.Saved>(started);
    }

    /// <summary>The Human may remove a blocker.</summary>
    [Fact]
    public void Update_HumanRemovesBlocker_Saves()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(BlockerGate), manager: Manager));
        (_, TaskItem blocked) = SeedPair(harness);

        TaskResult result = harness.Service.Update(blocked.Id, new TaskPatch { BlockedBy = [] }, null, HumanOf(harness));

        Assert.Empty(Assert.IsType<TaskResult.Saved>(result).Task.BlockedBy);
    }

    /// <summary>In a Team without the gate, blockers stay advisory: any Agent may remove one.</summary>
    [Fact]
    public void Update_GateOff_AgentRemovesBlocker_Saves()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(QaGate), manager: Manager));
        (_, TaskItem blocked) = SeedPair(harness);

        TaskResult result = harness.Service.Update(blocked.Id, new TaskPatch { BlockedBy = [] }, null, Jane);

        Assert.IsType<TaskResult.Saved>(result);
    }

    /// <summary>A Human-gated Task in Review that gained an open blocker cannot go to Needs You, even with its Proof complete.</summary>
    [Fact]
    public void Update_GatedTaskWithOpenBlockerToNeedsYou_IsRefused()
    {
        using TempDataDir gateDir = new();
        FixedProofSource proof = new(FixedProofSource.Complete);
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(BlockerGate), manager: Manager), proof: proof);
        TaskItem blocker = CreateTask(harness, "API base");
        TaskDraft draft = new("API", Team, null, TaskState.InProgress) { HumanGate = true };
        TaskItem gated = Assert.IsType<TaskResult.Saved>(harness.Service.Create(draft, HumanOf(harness))).Task;
        Assert.IsType<TaskResult.Saved>(harness.Service.Update(gated.Id, new TaskPatch { BlockedBy = [blocker.Id] }, null, HumanOf(harness)));

        TaskResult result = harness.Service.Update(gated.Id, MoveTo(TaskState.NeedsYou), null, Jane);

        Assert.Equal(GateId.BlockersDoneBeforeStart, Assert.IsType<TaskResult.GateRefused>(result).Gate);
        Assert.Equal(TaskState.InProgress, harness.Store.Get(gated.Id)?.Status);
    }

    /// <summary>The Human can override the gate, and the Change log records it.</summary>
    [Fact]
    public void Update_HumanOverride_SavesAndLogs()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(BlockerGate), manager: Manager));
        (_, TaskItem blocked) = SeedPair(harness);

        TaskResult result = harness.Service.Update(blocked.Id, MoveTo(TaskState.InProgress), null, HumanOf(harness), humanOverridesGates: true);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.EndsWith("(gate overridden by Human: blockers-done-before-start)", saved.Change.Entry.Summary, StringComparison.Ordinal); // contains-ok: the suffix is the override marker; the summary's head is the diff text
    }

    /// <summary>Creating a Task straight into In Progress with an open blocker is refused and writes nothing new.</summary>
    [Fact]
    public void Create_InProgressWithOpenBlocker_IsRefused()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(BlockerGate), manager: Manager));
        TaskItem blocker = CreateTask(harness, "API base");

        TaskResult result = harness.Service.Create(new TaskDraft("API", Team, null, TaskState.InProgress, BlockedBy: [blocker.Id]), Jane);

        Assert.Equal(GateId.BlockersDoneBeforeStart, Assert.IsType<TaskResult.GateRefused>(result).Gate);
        Assert.Single(harness.Store.All);
    }

    /// <summary>
    /// An outside edit moving a blocked Task to In Progress or Needs You is reverted and logged. The Human gate runs before the
    /// process gates, so an ungated Task written into Needs You is refused by it first; the blocker gate speaks for In Progress.
    /// </summary>
    /// <param name="to">The status the outside edit writes.</param>
    /// <param name="refusalPrefix">How the Change log entry that records the refusal starts.</param>
    [Theory]
    [InlineData(TaskState.InProgress, "outside edit refused by gate: Gate blockers-done-before-start:")]
    [InlineData(TaskState.NeedsYou, "outside edit refused by Human gate:")]
    public void OutsideEdit_BlockedTaskStarted_IsRestored(TaskState to, string refusalPrefix)
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(BlockerGate), manager: Manager));
        (_, TaskItem blocked) = SeedPair(harness);
        File.WriteAllText(blocked.Path, TaskFileFormat.Compose(blocked with { Status = to }));

        harness.Store.RebuildFromWatcher();

        TaskItem? after = harness.Store.Get(blocked.Id);
        Assert.Equal(TaskState.Backlog, after?.Status);
        Assert.StartsWith(refusalPrefix, after?.ChangeLog[^1].Summary, StringComparison.Ordinal); // contains-ok: the entry's tail is the refusal text, pinned in ProcessGateServiceBlockerTests
    }

    /// <summary>The Human's own actor, as the Task panel builds it.</summary>
    private static TaskActor HumanOf(TaskToolHarness harness) => TaskActors.Human(harness.Options.Value);

    /// <summary>A patch moving a Task to <paramref name="status"/>.</summary>
    private static TaskPatch MoveTo(TaskState status) => new() { Status = status };

    /// <summary>Creates a Platform Task in Backlog as the Human.</summary>
    private static TaskItem CreateTask(TaskToolHarness harness, string title, IReadOnlyList<TaskId>? blockedBy = null)
    {
        TaskResult result = harness.Service.Create(new TaskDraft(title, Team, null, BlockedBy: blockedBy), HumanOf(harness));
        return Assert.IsType<TaskResult.Saved>(result).Task;
    }

    /// <summary>Creates a blocker in Backlog and a Task it blocks, both in Platform.</summary>
    private static (TaskItem Blocker, TaskItem Blocked) SeedPair(TaskToolHarness harness)
    {
        TaskItem blocker = CreateTask(harness, "API base");
        TaskItem blocked = CreateTask(harness, "API", [blocker.Id]);
        return (blocker, blocked);
    }
}

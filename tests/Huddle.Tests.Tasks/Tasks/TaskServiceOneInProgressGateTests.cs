using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Tasks.HumanGate;
using static Agency.Huddle.Tests.Tasks.TaskGateText;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// <see cref="TaskService"/> runs the <c>one-in-progress-per-assignee</c> gate (ADR-0102) on update and create, judged against
/// the Tasks In Progress in every Team and the assignee the change would leave the Task with.
/// </summary>
public sealed class TaskServiceOneInProgressGateTests
{
    private const string OneGate = "'one-in-progress-per-assignee'";

    private static readonly TaskActor Nova = new(TaskActorKind.Agent, "Nova", "nova-id");

    /// <summary>A second move into In Progress for the same assignee is refused, names the first Task, and writes nothing.</summary>
    [Fact]
    public void Update_SecondTaskToInProgress_IsRefusedNamingTheFirst()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(OneGate)));
        TaskItem first = Seed(harness, "Alpha", TaskState.InProgress, "Nova");
        TaskItem second = Seed(harness, "Beta", TaskState.ToDo, "nova");

        TaskResult result = harness.Service.Update(second.Id, MoveTo(TaskState.InProgress), null, Nova);

        TaskResult.GateRefused refused = Assert.IsType<TaskResult.GateRefused>(result);
        Assert.Equal(GateId.OneInProgressPerAssignee, refused.Gate);
        Assert.Equal($"Gate one-in-progress-per-assignee: Nova already has {first.Id} \"Alpha\" In Progress. Finish it or move it out of In Progress before starting another.", refused.Message);
        Assert.Equal(TaskState.ToDo, harness.Store.Get(second.Id)?.Status);
    }

    /// <summary>A Task of the same assignee in Needs You or Done does not count, so the second Task starts.</summary>
    /// <param name="parked">The status the first Task is parked in.</param>
    [Theory]
    [InlineData(TaskState.NeedsYou)]
    [InlineData(TaskState.Done)]
    public void Update_OtherTaskNotInProgress_Saves(TaskState parked)
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(OneGate)), proof: new FixedProofSource(FixedProofSource.Complete));
        TaskItem first = Seed(harness, "Alpha", TaskState.InProgress, "Nova", humanGate: parked == TaskState.NeedsYou);
        TaskItem second = Seed(harness, "Beta", TaskState.ToDo, "Nova");
        Assert.IsType<TaskResult.Saved>(harness.Service.Update(first.Id, MoveTo(parked), null, HumanOf(harness)));

        TaskResult result = harness.Service.Update(second.Id, MoveTo(TaskState.InProgress), null, Nova);

        Assert.Equal(TaskState.InProgress, Assert.IsType<TaskResult.Saved>(result).Task.Status);
    }

    /// <summary>A closed Task does not count: closed while its status is still In Progress, the first Task no longer holds the assignee.</summary>
    [Fact]
    public void Update_OtherTaskClosed_Saves()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(OneGate)));
        TaskItem first = Seed(harness, "Alpha", TaskState.InProgress, "Nova");
        TaskItem second = Seed(harness, "Beta", TaskState.ToDo, "Nova");
        Assert.IsType<TaskResult.Saved>(harness.Service.Close(first.Id, HumanOf(harness)));
        Assert.Equal(TaskState.InProgress, harness.Store.Get(first.Id)?.Status);

        TaskResult result = harness.Service.Update(second.Id, MoveTo(TaskState.InProgress), null, Nova);

        Assert.IsType<TaskResult.Saved>(result);
    }

    /// <summary>An In Progress Task in another Team counts against the same assignee.</summary>
    [Fact]
    public void Update_OtherTaskInAnotherTeam_IsRefused()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(OneGate)));
        harness.Personas.Add(new PersonaIdentity("Zed", "Zed", "Zed", ["Ops"]), "You are Zed.");
        TaskItem elsewhere = Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft("Ops chore", "Ops", null, TaskState.InProgress, Assignee: "Nova"), HumanOf(harness))).Task;
        TaskItem here = Seed(harness, "Beta", TaskState.ToDo, "Nova");

        TaskResult result = harness.Service.Update(here.Id, MoveTo(TaskState.InProgress), null, Nova);

        TaskResult.GateRefused refused = Assert.IsType<TaskResult.GateRefused>(result);
        Assert.Equal(GateId.OneInProgressPerAssignee, refused.Gate);
        Assert.Equal($"Gate one-in-progress-per-assignee: Nova already has {elsewhere.Id} \"Ops chore\" In Progress. Finish it or move it out of In Progress before starting another.", refused.Message);
    }

    /// <summary>Unassigned Tasks are never limited: two can be In Progress.</summary>
    [Fact]
    public void Update_UnassignedTasks_BothStart()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(OneGate)));
        Seed(harness, "Alpha", TaskState.InProgress, null);
        TaskItem second = Seed(harness, "Beta", TaskState.ToDo, null);

        TaskResult result = harness.Service.Update(second.Id, MoveTo(TaskState.InProgress), null, Nova);

        Assert.IsType<TaskResult.Saved>(result);
    }

    /// <summary>Another assignee's In Progress Task does not count.</summary>
    [Fact]
    public void Update_OtherAssigneeBusy_Saves()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(OneGate)));
        Seed(harness, "Alpha", TaskState.InProgress, "Kai");
        TaskItem second = Seed(harness, "Beta", TaskState.ToDo, "Nova");

        TaskResult result = harness.Service.Update(second.Id, MoveTo(TaskState.InProgress), null, Nova);

        Assert.IsType<TaskResult.Saved>(result);
    }

    /// <summary>Giving an In Progress Task to someone who already has one is refused and the assignee stays.</summary>
    [Fact]
    public void Update_ReassignInProgressToBusyAssignee_IsRefused()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(OneGate)));
        TaskItem first = Seed(harness, "Alpha", TaskState.InProgress, "Nova");
        TaskItem second = Seed(harness, "Beta", TaskState.InProgress, "Kai");

        TaskResult result = harness.Service.Update(second.Id, new TaskPatch { Assignee = Optional<string?>.Set("Nova") }, null, Nova);

        TaskResult.GateRefused refused = Assert.IsType<TaskResult.GateRefused>(result);
        Assert.Equal(GateId.OneInProgressPerAssignee, refused.Gate);
        Assert.Contains(first.Id.ToString(), refused.Message, StringComparison.Ordinal); // contains-ok: the full text is pinned above; this checks the reassign path names the holder
        Assert.Equal("Kai", harness.Store.Get(second.Id)?.Assignee);
    }

    /// <summary>Giving an In Progress Task to someone with no In Progress Task saves.</summary>
    [Fact]
    public void Update_ReassignInProgressToFreeAssignee_Saves()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(OneGate)));
        harness.Personas.Add(new PersonaIdentity("Zed", "Zed", "Zed", ["Platform"]), "You are Zed.");
        Seed(harness, "Alpha", TaskState.InProgress, "Nova");
        TaskItem second = Seed(harness, "Beta", TaskState.InProgress, "Kai");

        TaskResult result = harness.Service.Update(second.Id, new TaskPatch { Assignee = Optional<string?>.Set("Zed") }, null, Nova);

        Assert.Equal("Zed", Assert.IsType<TaskResult.Saved>(result).Task.Assignee);
    }

    /// <summary>Creating a Task already In Progress for an assignee who has one is refused and writes nothing new.</summary>
    [Fact]
    public void Create_InProgressForBusyAssignee_IsRefused()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(OneGate)));
        Seed(harness, "Alpha", TaskState.InProgress, "Nova");

        TaskResult result = harness.Service.Create(new TaskDraft("Beta", Team, null, TaskState.InProgress, Assignee: "Nova"), Nova);

        Assert.Equal(GateId.OneInProgressPerAssignee, Assert.IsType<TaskResult.GateRefused>(result).Gate);
        Assert.Single(harness.Store.All);
    }

    /// <summary>Creating a Task as To Do for an assignee who has one In Progress saves: only In Progress counts.</summary>
    [Fact]
    public void Create_ToDoForBusyAssignee_Saves()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(OneGate)));
        Seed(harness, "Alpha", TaskState.InProgress, "Nova");

        TaskResult result = harness.Service.Create(new TaskDraft("Beta", Team, null, TaskState.ToDo, Assignee: "Nova"), Nova);

        Assert.IsType<TaskResult.Saved>(result);
    }

    /// <summary>A move out of In Progress is never refused, even when two Tasks of the assignee are already In Progress.</summary>
    /// <param name="to">The status the Task moves to.</param>
    [Theory]
    [InlineData(TaskState.ToDo)]
    [InlineData(TaskState.Done)]
    public void Update_MoveOutOfInProgress_Saves(TaskState to)
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(OneGate)), proof: new FixedProofSource(FixedProofSource.Complete));
        Seed(harness, "Alpha", TaskState.InProgress, "Nova");
        TaskItem second = Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft("Beta", Team, null, TaskState.InProgress, Assignee: "Nova"), HumanOf(harness), humanOverridesGates: true)).Task;

        TaskResult result = harness.Service.Update(second.Id, MoveTo(to), null, Nova);

        Assert.Equal(to, Assert.IsType<TaskResult.Saved>(result).Task.Status);
    }

    /// <summary>The Human is limited too: their own second Task In Progress is refused.</summary>
    [Fact]
    public void Update_HumanSecondTask_IsRefused()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(OneGate)));
        string human = HumanOf(harness).Name;
        Seed(harness, "Alpha", TaskState.InProgress, human);
        TaskItem second = Seed(harness, "Beta", TaskState.ToDo, human);

        TaskResult result = harness.Service.Update(second.Id, MoveTo(TaskState.InProgress), null, HumanOf(harness));

        Assert.Equal(GateId.OneInProgressPerAssignee, Assert.IsType<TaskResult.GateRefused>(result).Gate);
    }

    /// <summary>The Human can override one move, and the Change log records the gate.</summary>
    [Fact]
    public void Update_HumanOverride_SavesAndLogs()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(OneGate)));
        Seed(harness, "Alpha", TaskState.InProgress, "Nova");
        TaskItem second = Seed(harness, "Beta", TaskState.ToDo, "Nova");

        TaskResult result = harness.Service.Update(second.Id, MoveTo(TaskState.InProgress), null, HumanOf(harness), humanOverridesGates: true);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.EndsWith("(gate overridden by Human: one-in-progress-per-assignee)", saved.Change.Entry.Summary, StringComparison.Ordinal); // contains-ok: the suffix is the override marker; the summary's head is the diff text
    }

    /// <summary>A Team whose process does not list the gate lets a second Task start.</summary>
    [Fact]
    public void Update_GateOff_Saves()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(QaGate)));
        Seed(harness, "Alpha", TaskState.InProgress, "Nova");
        TaskItem second = Seed(harness, "Beta", TaskState.ToDo, "Nova");

        TaskResult result = harness.Service.Update(second.Id, MoveTo(TaskState.InProgress), null, Nova);

        Assert.IsType<TaskResult.Saved>(result);
    }

    /// <summary>An outside edit that starts a second Task for a busy assignee is reverted and logged.</summary>
    [Fact]
    public void OutsideEdit_SecondTaskStarted_IsRestored()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(OneGate)));
        Seed(harness, "Alpha", TaskState.InProgress, "Nova");
        TaskItem second = Seed(harness, "Beta", TaskState.ToDo, "Nova");
        File.WriteAllText(second.Path, TaskFileFormat.Compose(second with { Status = TaskState.InProgress }));

        harness.Store.RebuildFromWatcher();

        TaskItem? after = harness.Store.Get(second.Id);
        Assert.Equal(TaskState.ToDo, after?.Status);
        Assert.StartsWith("outside edit refused by gate: Gate one-in-progress-per-assignee:", after?.ChangeLog[^1].Summary, StringComparison.Ordinal); // contains-ok: the entry's tail is the refusal text, pinned in ProcessGateServiceOneInProgressTests
    }

    /// <summary>An outside edit that starts the assignee's only Task is kept: the edited Task is not its own other Task.</summary>
    [Fact]
    public void OutsideEdit_OnlyTaskStarted_IsKept()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(OneGate)));
        TaskItem only = Seed(harness, "Alpha", TaskState.ToDo, "Nova");
        File.WriteAllText(only.Path, TaskFileFormat.Compose(only with { Status = TaskState.InProgress }));

        harness.Store.RebuildFromWatcher();

        Assert.Equal(TaskState.InProgress, harness.Store.Get(only.Id)?.Status);
    }

    /// <summary>The Human's own actor, as the Task panel builds it.</summary>
    private static TaskActor HumanOf(TaskToolHarness harness) => TaskActors.Human(harness.Options.Value);

    /// <summary>A patch moving a Task to <paramref name="status"/>.</summary>
    private static TaskPatch MoveTo(TaskState status) => new() { Status = status };

    /// <summary>Creates a Platform Task in <paramref name="status"/>, overriding gates so a test can stage any starting state.</summary>
    /// <param name="harness">The Task service stack.</param>
    /// <param name="title">The Task's title.</param>
    /// <param name="status">The status to create it in.</param>
    /// <param name="assignee">Who holds it, or <see langword="null"/>.</param>
    /// <param name="humanGate">Whether the Task is Human-gated: only a gated Task may move to Needs You.</param>
    private static TaskItem Seed(TaskToolHarness harness, string title, TaskState status, string? assignee, bool humanGate = false)
    {
        TaskResult result = harness.Service.Create(new TaskDraft(title, Team, null, status, Assignee: assignee) { HumanGate = humanGate }, HumanOf(harness), humanOverridesGates: true);
        return Assert.IsType<TaskResult.Saved>(result).Task;
    }
}

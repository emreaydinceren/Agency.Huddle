using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Tools;
using static Agency.Huddle.Tests.Tasks.TaskGateText;

namespace Agency.Huddle.Tests.Tasks.HumanGate;

/// <summary>
/// A Helper is an actor of its own kind: it may turn the Human gate on with a reason, and may never turn it off
/// or clear an Action (Helpers Spec §6.11, plan HL-9).
/// </summary>
public sealed class TaskServiceHelperActorTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly TaskActor Helper = new(TaskActorKind.Helper, "Needs Human (Helper)", null);

    /// <summary>Builds the harness on a fixed clock.</summary>
    private static TaskToolHarness Build() => new(clock: new ManualTimeProvider(Noon), proof: new FixedProofSource(FixedProofSource.Complete));

    /// <summary>Creates an ungated Task in Review as the Human.</summary>
    /// <param name="harness">The harness.</param>
    /// <param name="gated">Whether the Task starts with the Human gate on.</param>
    private static TaskItem Create(TaskToolHarness harness, bool gated = false)
    {
        TaskDraft draft = new("Reply to Acme", Team, null, TaskState.InProgress) { HumanGate = gated };
        return Assert.IsType<TaskResult.Saved>(harness.Service.Create(draft, TaskActors.Human(harness.Options.Value))).Task;
    }

    /// <summary>The Helper turning the gate on with a reason is saved and the Change log names the Helper and the reason.</summary>
    [Fact]
    public void Update_HelperTurnsGateOnWithReason_LogsActorAndReason()
    {
        using TaskToolHarness harness = Build();
        TaskItem task = Create(harness);

        TaskResult result = harness.Service.Update(task.Id, new TaskPatch { HumanGate = Optional<bool>.Set(true), Reason = "Sends a reply to a customer" }, task.Version, Helper);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.True(saved.Task.HumanGate);
        Assert.Equal("Needs Human (Helper)", saved.Change.Entry.Actor);
        Assert.Equal("human gate on (reason: Sends a reply to a customer)", saved.Change.Entry.Summary);
    }

    /// <summary>A reason is still refused with a non-terminal change that does not turn the gate on.</summary>
    [Fact]
    public void Update_ReasonWithoutGateOn_IsStillRefused()
    {
        using TaskToolHarness harness = Build();
        TaskItem task = Create(harness);

        TaskResult result = harness.Service.Update(task.Id, new TaskPatch { Status = TaskState.InProgress, Reason = "because" }, task.Version, Helper);

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Contains("A reason is only recorded when the status becomes Cancelled or Rejected.", refused.Problems);
        Assert.Equal(TaskState.InProgress, harness.Store.Get(task.Id)?.Status);
    }

    /// <summary>A reason is refused on a change that touches an already-gated Task without turning the gate on.</summary>
    [Fact]
    public void Update_ReasonOnAlreadyGatedTask_IsRefused()
    {
        using TaskToolHarness harness = Build();
        TaskItem task = Create(harness, gated: true);

        TaskResult result = harness.Service.Update(task.Id, new TaskPatch { Title = "Reply to Acme today", Reason = "because" }, task.Version, Helper);

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Contains("A reason is only recorded when the status becomes Cancelled or Rejected.", refused.Problems);
    }

    /// <summary>The Helper cannot turn the gate off.</summary>
    [Fact]
    public void Update_HelperTurnsGateOff_IsRefused()
    {
        using TaskToolHarness harness = Build();
        TaskItem task = Create(harness, gated: true);

        TaskResult result = harness.Service.Update(task.Id, new TaskPatch { HumanGate = Optional<bool>.Set(false) }, task.Version, Helper);

        Assert.IsType<TaskResult.GateRefused>(result);
        Assert.True(harness.Store.Get(task.Id)?.HumanGate);
    }

    /// <summary>The Helper cannot clear an Action.</summary>
    [Fact]
    public void Update_HelperClearsAction_IsRefused()
    {
        using TaskToolHarness harness = Build();
        TaskDraft draft = new("Reply to Acme", Team, null, TaskState.InProgress) { Action = "Send the reply" };
        TaskItem task = Assert.IsType<TaskResult.Saved>(harness.Service.Create(draft, TaskActors.Human(harness.Options.Value))).Task;

        TaskResult result = harness.Service.Update(task.Id, new TaskPatch { Action = Optional<string?>.Set(null) }, task.Version, Helper);

        Assert.IsType<TaskResult.GateRefused>(result);
        Assert.Equal("Send the reply", harness.Store.Get(task.Id)?.Action);
    }

    /// <summary>A Helper actor is named "Name (Helper)" and has no id.</summary>
    [Fact]
    public void Helper_ActorFactory_NamesTheHelper()
    {
        TaskActor actor = TaskActors.Helper("Needs Human");

        Assert.Equal(TaskActorKind.Helper, actor.Kind);
        Assert.Equal("Needs Human (Helper)", actor.Name);
        Assert.Null(actor.UserId);
    }
}

using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.HumanGate;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Tools;
using static Agency.Huddle.Tests.Tasks.TaskGateText;

namespace Agency.Huddle.Tests.Tasks.HumanGate;

/// <summary>
/// Huddle moves a ready gated Task from Review to Needs You after the write that made it ready (#349 Part 2): an Evidence
/// line, a move to Review, or the gate turned on. It never moves a Human's change, an In Progress, ungated, closed or
/// already accepted Task, or one whose Proof is open.
/// </summary>
public sealed class TaskServiceNeedsYouMoveTests
{
    private static readonly DateTimeOffset Noon = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly TaskActor Nova = new(TaskActorKind.Agent, "Nova", "nova-id");

    /// <summary>Builds the harness on a fixed clock and the given Proof source.</summary>
    /// <param name="proof">The Proof source.</param>
    private static TaskToolHarness Build(FixedProofSource proof) => new(clock: new ManualTimeProvider(Noon), proof: proof);

    /// <summary>The Human's actor.</summary>
    /// <param name="harness">The harness.</param>
    private static TaskActor HumanOf(TaskToolHarness harness) => TaskActors.Human(harness.Options.Value);

    /// <summary>Creates a Task as the Human, so creation itself moves nothing.</summary>
    /// <param name="harness">The harness.</param>
    /// <param name="status">The status to create it in.</param>
    /// <param name="gated">Whether it is Human-gated.</param>
    private static TaskItem Create(TaskToolHarness harness, TaskState status = TaskState.Review, bool gated = true)
    {
        TaskDraft draft = new("Write the reply", Team, null, status) { HumanGate = gated };
        return Assert.IsType<TaskResult.Saved>(harness.Service.Create(draft, HumanOf(harness))).Task;
    }

    /// <summary>An Evidence line by Nova.</summary>
    private static EvidenceLine Evidence() => new(Noon, "Nova", "dotnet test", 0, null, null, "raw");

    /// <summary>Adds Evidence as Nova and returns the Task as stored afterwards.</summary>
    /// <param name="harness">The harness.</param>
    /// <param name="task">The Task.</param>
    private static TaskItem? AddEvidenceAndRead(TaskToolHarness harness, TaskItem task)
    {
        _ = Assert.IsType<TaskResult.Saved>(harness.Service.AddEvidence(task.Id, Evidence(), Nova));
        return harness.Store.Get(task.Id);
    }

    /// <summary>Evidence that completes the Proof of a gated Task in Review moves it to Needs You and logs the Proof count.</summary>
    [Fact]
    public void AddEvidence_CompletesProofInReview_MovesToNeedsYouAndLogs()
    {
        FixedProofSource proof = new(FixedProofSource.ThreeOfFour);
        using TaskToolHarness harness = Build(proof);
        TaskItem task = Create(harness);
        proof.Current = FixedProofSource.Complete;

        TaskItem? after = AddEvidenceAndRead(harness, task);

        Assert.NotNull(after);
        Assert.Equal(TaskState.NeedsYou, after.Status);
        ChangeLogEntry last = after.ChangeLog[^1];
        Assert.Equal("status: Review → Needs You (Proof 4/4)", last.Summary);
        Assert.Equal("Huddle", last.Actor);
    }

    /// <summary>The move is its own change, raised after the Evidence change and made by Huddle as a Helper.</summary>
    [Fact]
    public void AddEvidence_Move_RaisesASeparateChangeByHuddle()
    {
        FixedProofSource proof = new(FixedProofSource.Complete);
        using TaskToolHarness harness = Build(proof);
        TaskItem task = Create(harness);
        List<TaskChange> changes = [];
        harness.Events.TaskChanged += changes.Add;

        _ = AddEvidenceAndRead(harness, task);

        Assert.Equal(2, changes.Count);
        Assert.Equal(TaskActorKind.Agent, changes[0].Actor.Kind);
        Assert.Equal(TaskActorKind.Helper, changes[1].Actor.Kind);
        Assert.Equal(TaskState.Review, changes[1].Before?.Status);
        Assert.Equal(TaskState.NeedsYou, changes[1].After.Status);
    }

    /// <summary>An Agent moving a gated Task to Review with its Proof already complete finds it in Needs You.</summary>
    [Fact]
    public void Update_AgentMovesToReviewWithProofComplete_MovesToNeedsYou()
    {
        FixedProofSource proof = new(FixedProofSource.Complete);
        using TaskToolHarness harness = Build(proof);
        TaskItem task = Create(harness, TaskState.InProgress);

        _ = Assert.IsType<TaskResult.Saved>(harness.Service.Update(task.Id, new TaskPatch { Status = TaskState.Review }, null, Nova));

        Assert.Equal(TaskState.NeedsYou, harness.Store.Get(task.Id)?.Status);
    }

    /// <summary>Turning the Human gate on for a Task in Review with its Proof complete moves it to Needs You.</summary>
    [Fact]
    public void Update_AgentTurnsGateOnWithProofComplete_MovesToNeedsYou()
    {
        FixedProofSource proof = new(FixedProofSource.Complete);
        using TaskToolHarness harness = Build(proof);
        TaskItem task = Create(harness, gated: false);

        TaskPatch patch = new() { HumanGate = Optional<bool>.Set(true), Reason = "Sends a reply to a customer" };
        _ = Assert.IsType<TaskResult.Saved>(harness.Service.Update(task.Id, patch, null, Nova));

        Assert.Equal(TaskState.NeedsYou, harness.Store.Get(task.Id)?.Status);
    }

    /// <summary>A Task still In Progress stays there when Evidence completes its Proof: review is not skipped.</summary>
    [Fact]
    public void AddEvidence_InProgress_StaysInProgress()
    {
        FixedProofSource proof = new(FixedProofSource.Complete);
        using TaskToolHarness harness = Build(proof);
        TaskItem task = Create(harness, TaskState.InProgress);

        Assert.Equal(TaskState.InProgress, AddEvidenceAndRead(harness, task)?.Status);
    }

    /// <summary>A Task whose Proof is still open stays in Review.</summary>
    [Fact]
    public void AddEvidence_ProofIncomplete_StaysInReview()
    {
        FixedProofSource proof = new(FixedProofSource.ThreeOfFour);
        using TaskToolHarness harness = Build(proof);
        TaskItem task = Create(harness);

        Assert.Equal(TaskState.Review, AddEvidenceAndRead(harness, task)?.Status);
    }

    /// <summary>An ungated Task stays in Review: it goes from Review to Done, never to Needs You.</summary>
    [Fact]
    public void AddEvidence_Ungated_StaysInReview()
    {
        FixedProofSource proof = new(FixedProofSource.Complete);
        using TaskToolHarness harness = Build(proof);
        TaskItem task = Create(harness, gated: false);

        Assert.Equal(TaskState.Review, AddEvidenceAndRead(harness, task)?.Status);
    }

    /// <summary>A closed Task is not moved.</summary>
    [Fact]
    public void AddEvidence_ClosedTask_IsNotMoved()
    {
        FixedProofSource proof = new(FixedProofSource.Complete);
        using TaskToolHarness harness = Build(proof);
        TaskItem task = Create(harness);
        _ = Assert.IsType<TaskResult.Saved>(harness.Service.Close(task.Id, HumanOf(harness)));

        TaskItem? after = AddEvidenceAndRead(harness, task);

        Assert.NotNull(after);
        Assert.Equal(TaskState.Review, after.Status);
    }

    /// <summary>A Task the Human already accepted stays where it is and gets no second move from Huddle; an acceptance only exists in Needs You.</summary>
    [Fact]
    public void AddEvidence_AlreadyAccepted_IsNotMoved()
    {
        FixedProofSource proof = new(FixedProofSource.Complete);
        using TaskToolHarness harness = Build(proof);
        TaskDraft draft = new("Write the reply", Team, null, TaskState.Review) { HumanGate = true, Action = "Send the reply" };
        TaskItem task = Assert.IsType<TaskResult.Saved>(harness.Service.Create(draft, HumanOf(harness))).Task;
        TaskItem waiting = Assert.IsType<TaskResult.Saved>(harness.Service.Update(task.Id, new TaskPatch { Status = TaskState.NeedsYou }, null, Nova)).Task;
        _ = Assert.IsType<TaskResult.Saved>(harness.Service.Decide(task.Id, TaskDecision.Accept, null, waiting.Version, HumanOf(harness)));
        Assert.Equal(AcceptanceState.Valid, Acceptance.Of(harness.Store.Get(task.Id) ?? task).State);
        List<TaskChange> changes = [];
        harness.Events.TaskChanged += changes.Add;

        TaskItem? after = AddEvidenceAndRead(harness, task);

        Assert.Equal(TaskState.NeedsYou, after?.Status);
        Assert.Single(changes);
    }

    /// <summary>The Human dragging a gated card back to Review leaves it there, even with the Proof complete.</summary>
    [Fact]
    public void Update_HumanMovesToReview_StaysInReview()
    {
        FixedProofSource proof = new(FixedProofSource.Complete);
        using TaskToolHarness harness = Build(proof);
        TaskItem task = Create(harness, TaskState.InProgress);

        _ = Assert.IsType<TaskResult.Saved>(harness.Service.Update(task.Id, new TaskPatch { Status = TaskState.Review }, null, HumanOf(harness)));

        Assert.Equal(TaskState.Review, harness.Store.Get(task.Id)?.Status);
    }

    /// <summary>An unrelated edit by an Agent on a ready Task in Review does not trigger the move.</summary>
    [Fact]
    public void Update_UnrelatedEdit_DoesNotMove()
    {
        FixedProofSource proof = new(FixedProofSource.Complete);
        using TaskToolHarness harness = Build(proof);
        TaskItem task = Create(harness);

        _ = Assert.IsType<TaskResult.Saved>(harness.Service.Update(task.Id, new TaskPatch { Title = "Write the reply today" }, null, Nova));

        Assert.Equal(TaskState.Review, harness.Store.Get(task.Id)?.Status);
    }

    /// <summary>An Agent that moves the Task to Needs You itself still can, and Huddle adds no second move.</summary>
    [Fact]
    public void Update_AgentMovesToNeedsYouItself_IsSavedOnce()
    {
        FixedProofSource proof = new(FixedProofSource.Complete);
        using TaskToolHarness harness = Build(proof);
        TaskItem task = Create(harness);
        List<TaskChange> changes = [];
        harness.Events.TaskChanged += changes.Add;

        TaskResult result = harness.Service.Update(task.Id, new TaskPatch { Status = TaskState.NeedsYou }, null, Nova);

        Assert.IsType<TaskResult.Saved>(result);
        Assert.Equal(TaskState.NeedsYou, harness.Store.Get(task.Id)?.Status);
        Assert.Single(changes);
    }

    /// <summary>MoveIfReady moves a gated Task in Review once its Proof became complete without any Task write, and logs the Proof count (#593).</summary>
    [Fact]
    public void MoveIfReady_ProofCompleteWithoutTaskWrite_MovesToNeedsYou()
    {
        FixedProofSource proof = new(FixedProofSource.ThreeOfFour);
        using TaskToolHarness harness = Build(proof);
        TaskItem task = Create(harness);
        proof.Current = FixedProofSource.Complete;

        harness.Service.MoveIfReady(task.Id);

        TaskItem? after = harness.Store.Get(task.Id);
        Assert.NotNull(after);
        Assert.Equal(TaskState.NeedsYou, after.Status);
        Assert.Equal("status: Review → Needs You (Proof 4/4)", after.ChangeLog[^1].Summary);
        Assert.Equal("Huddle", after.ChangeLog[^1].Actor);
    }

    /// <summary>MoveIfReady with the Proof still open writes nothing.</summary>
    [Fact]
    public void MoveIfReady_ProofIncomplete_WritesNothing()
    {
        FixedProofSource proof = new(FixedProofSource.ThreeOfFour);
        using TaskToolHarness harness = Build(proof);
        TaskItem task = Create(harness);
        List<TaskChange> changes = [];
        harness.Events.TaskChanged += changes.Add;

        harness.Service.MoveIfReady(task.Id);

        TaskItem? after = harness.Store.Get(task.Id);
        Assert.NotNull(after);
        Assert.Equal(task.Version, after.Version);
        Assert.Equal(TaskState.Review, after.Status);
        Assert.Empty(changes);
    }

    /// <summary>An Agent asking for Needs You when the Proof is open is still refused, and Huddle does not override that.</summary>
    [Fact]
    public void Update_AgentMovesToNeedsYouWithProofOpen_IsRefused()
    {
        FixedProofSource proof = new(FixedProofSource.ThreeOfFour);
        using TaskToolHarness harness = Build(proof);
        TaskItem task = Create(harness);

        TaskResult result = harness.Service.Update(task.Id, new TaskPatch { Status = TaskState.NeedsYou }, null, Nova);

        Assert.IsType<TaskResult.GateRefused>(result);
    }
}

using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.HumanGate;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Tools;
using static Agency.Huddle.Tests.Tasks.TaskGateText;

namespace Agency.Huddle.Tests.Tasks.HumanGate;

/// <summary>
/// <see cref="TaskService"/> applies the Human gate rules H-1 to H-5 and D-6 at <c>Create</c>, <c>Update</c> and an
/// outside edit, and a refusal on a gated Task cannot be overridden (Human gate Spec §6.4, plan T5.1).
/// </summary>
public sealed class TaskServiceHumanGateTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
    private static readonly TaskActor Nova = new(TaskActorKind.Agent, "Nova", "nova-id");

    /// <summary>Builds the harness on a fixed clock and the given Proof source.</summary>
    /// <param name="proof">The Proof source.</param>
    /// <param name="gates">The process gates, or <see langword="null"/> for none.</param>
    private static TaskToolHarness Build(FixedProofSource proof, ProcessGateService? gates = null) =>
        new(clock: new ManualTimeProvider(Noon), gates: gates, proof: proof);

    /// <summary>The Human's actor.</summary>
    /// <param name="harness">The harness.</param>
    private static TaskActor HumanOf(TaskToolHarness harness) => TaskActors.Human(harness.Options.Value);

    /// <summary>Creates a Human-gated Task as the Human, in the given status.</summary>
    /// <param name="harness">The harness.</param>
    /// <param name="status">The starting status.</param>
    /// <param name="action">The Action, or <see langword="null"/>.</param>
    private static TaskItem CreateGated(TaskToolHarness harness, TaskState status = TaskState.Review, string? action = null)
    {
        TaskDraft draft = new("Write the reply", Team, null, status) { HumanGate = true, Action = action };
        return Assert.IsType<TaskResult.Saved>(harness.Service.Create(draft, HumanOf(harness))).Task;
    }

    /// <summary>Creates a gated Task and moves it into Needs You with a complete Proof.</summary>
    /// <param name="harness">The harness.</param>
    /// <param name="proof">The Proof source, which is left complete.</param>
    private static TaskItem CreateInNeedsYou(TaskToolHarness harness, FixedProofSource proof)
    {
        TaskItem task = CreateGated(harness);
        proof.Current = FixedProofSource.Complete;
        return Assert.IsType<TaskResult.Saved>(harness.Service.Update(task.Id, MoveTo(TaskState.NeedsYou), null, Nova)).Task;
    }

    /// <summary>Appends an <c>accepted</c> entry for the Task's current content, as the Human's Accept does, by editing the file.</summary>
    /// <param name="harness">The harness.</param>
    /// <param name="task">The Task, in Needs You.</param>
    private static TaskItem Accept(TaskToolHarness harness, TaskItem task)
    {
        ChangeLogEntry entry = new(Noon, "You", "accepted " + ContentFingerprint.Of(task.Title, task.Action, task.Description));
        File.WriteAllText(task.Path, TaskFileFormat.Compose(task with { ChangeLog = [.. task.ChangeLog, entry] }));
        harness.Store.RebuildFromWatcher();
        return harness.Store.Get(task.Id) ?? throw new InvalidOperationException("accepted task missing");
    }

    /// <summary>A patch moving a Task to <paramref name="status"/>.</summary>
    /// <param name="status">The new status.</param>
    private static TaskPatch MoveTo(TaskState status) => new() { Status = status };

    /// <summary>An agent moving a gated Task to Done is refused with the Human gate message, and cannot override it.</summary>
    [Fact]
    public void Update_AgentMovesGatedToDone_IsRefusedWithHumanGate()
    {
        FixedProofSource proof = new(FixedProofSource.Complete);
        using TaskToolHarness harness = Build(proof);
        TaskItem task = CreateGated(harness);

        TaskResult result = harness.Service.Update(task.Id, MoveTo(TaskState.Done), null, Nova);

        TaskResult.GateRefused refused = Assert.IsType<TaskResult.GateRefused>(result);
        Assert.Equal(GateId.HumanGate, refused.Gate);
        Assert.Equal($"{task.Id} needs You's acceptance. Move it to Needs You when the Proof is complete; you'll be woken when You decides.", refused.Message);
        Assert.False(refused.CanOverride);
        Assert.Equal(TaskState.Review, harness.Store.Get(task.Id)?.Status);
    }

    /// <summary>The Human moving a gated Task in Needs You to Done without accepting it is refused.</summary>
    [Fact]
    public void Update_HumanMovesGatedToDoneWithoutAcceptance_IsRefused()
    {
        FixedProofSource proof = new(FixedProofSource.ThreeOfFour);
        using TaskToolHarness harness = Build(proof);
        TaskItem task = CreateInNeedsYou(harness, proof);

        TaskResult result = harness.Service.Update(task.Id, MoveTo(TaskState.Done), null, HumanOf(harness));

        TaskResult.GateRefused refused = Assert.IsType<TaskResult.GateRefused>(result);
        Assert.Equal(GateId.HumanGate, refused.Gate);
        Assert.Equal(TaskState.NeedsYou, harness.Store.Get(task.Id)?.Status);
    }

    /// <summary>A move into Needs You with an incomplete Proof is refused with the open item named.</summary>
    [Fact]
    public void Update_IntoNeedsYouWithIncompleteProof_IsRefusedWithProofIncomplete()
    {
        FixedProofSource proof = new(FixedProofSource.ThreeOfFour);
        using TaskToolHarness harness = Build(proof);
        TaskItem task = CreateGated(harness);

        TaskResult result = harness.Service.Update(task.Id, MoveTo(TaskState.NeedsYou), null, Nova);

        TaskResult.GateRefused refused = Assert.IsType<TaskResult.GateRefused>(result);
        Assert.Equal(GateId.ProofIncomplete, refused.Gate);
        Assert.Equal($"{task.Id} can go to Needs You once its Proof is complete (3/4: citation check is open).", refused.Message);
        Assert.Equal(TaskState.Review, harness.Store.Get(task.Id)?.Status);
    }

    /// <summary>A move into Needs You with a complete Proof is logged with its Proof count.</summary>
    [Fact]
    public void Update_IntoNeedsYouWithCompleteProof_LogsProofCount()
    {
        FixedProofSource proof = new(FixedProofSource.Complete);
        using TaskToolHarness harness = Build(proof);
        TaskItem task = CreateGated(harness);

        TaskResult result = harness.Service.Update(task.Id, MoveTo(TaskState.NeedsYou), null, Nova);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.Equal("status: Review → Needs You (Proof 4/4)", saved.Change.Entry.Summary);
    }

    /// <summary>The Human's override flag is ignored for the Human gate's own refusal on a gated Task.</summary>
    [Fact]
    public void Update_OverrideOnGatedTask_HumanGateRefusalIsIgnored()
    {
        FixedProofSource proof = new(FixedProofSource.Complete);
        using TaskToolHarness harness = Build(proof);
        TaskItem task = CreateGated(harness);

        TaskResult result = harness.Service.Update(task.Id, MoveTo(TaskState.Done), null, HumanOf(harness), humanOverridesGates: true);

        TaskResult.GateRefused refused = Assert.IsType<TaskResult.GateRefused>(result);
        Assert.Equal(GateId.HumanGate, refused.Gate);
        Assert.False(refused.CanOverride);
    }

    /// <summary>The Human's override flag is ignored for the incomplete-Proof refusal on a gated Task.</summary>
    [Fact]
    public void Update_OverrideOnGatedTask_ProofIncompleteRefusalIsIgnored()
    {
        FixedProofSource proof = new(FixedProofSource.ThreeOfFour);
        using TaskToolHarness harness = Build(proof);
        TaskItem task = CreateGated(harness);

        TaskResult result = harness.Service.Update(task.Id, MoveTo(TaskState.NeedsYou), null, HumanOf(harness), humanOverridesGates: true);

        TaskResult.GateRefused refused = Assert.IsType<TaskResult.GateRefused>(result);
        Assert.Equal(GateId.ProofIncomplete, refused.Gate);
        Assert.False(refused.CanOverride);
    }

    /// <summary>On an accepted gated Task the Human's override flag does not pass the QA Evidence gate either (ADR-0039 stays refused).</summary>
    [Fact]
    public void Update_OverrideOnGatedTask_QaEvidenceRefusalIsIgnored()
    {
        FixedProofSource proof = new(FixedProofSource.Complete);
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = Build(proof, BuildGates(gateDir, Process(QaGate)));
        TaskItem accepted = Accept(harness, CreateInNeedsYou(harness, proof));

        TaskResult result = harness.Service.Update(accepted.Id, MoveTo(TaskState.Done), null, HumanOf(harness), humanOverridesGates: true);

        TaskResult.GateRefused refused = Assert.IsType<TaskResult.GateRefused>(result);
        Assert.Equal(GateId.QaEvidenceBeforeDone, refused.Gate);
        Assert.False(refused.CanOverride);
        Assert.Equal(TaskState.NeedsYou, harness.Store.Get(accepted.Id)?.Status);
    }

    /// <summary>On an ordinary Task the Human's override still passes a process gate (ADR-0039 unchanged).</summary>
    [Fact]
    public void Update_OverrideOnOrdinaryTask_StillPasses()
    {
        FixedProofSource proof = new(FixedProofSource.Complete);
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = Build(proof, BuildGates(gateDir, Process(QaGate)));
        TaskResult created = harness.Service.Create(new TaskDraft("Fix login", Team, null), HumanOf(harness));
        TaskItem task = Assert.IsType<TaskResult.Saved>(created).Task;

        TaskResult result = harness.Service.Update(task.Id, MoveTo(TaskState.Done), null, HumanOf(harness), humanOverridesGates: true);

        Assert.Equal(TaskState.Done, Assert.IsType<TaskResult.Saved>(result).Task.Status);
    }

    /// <summary>An agent clearing a Task's Action is refused with the Human gate message.</summary>
    [Fact]
    public void Update_AgentClearsAction_IsRefused()
    {
        FixedProofSource proof = new(FixedProofSource.Complete);
        using TaskToolHarness harness = Build(proof);
        TaskItem task = CreateGated(harness, action: "Send the reply");

        TaskResult result = harness.Service.Update(task.Id, new TaskPatch { Action = Optional<string?>.Set(null) }, null, Nova);

        TaskResult.GateRefused refused = Assert.IsType<TaskResult.GateRefused>(result);
        Assert.Equal($"Only You can turn off the Human gate on {task.Id}.", refused.Message);
        Assert.Equal("Send the reply", harness.Store.Get(task.Id)?.Action);
    }

    /// <summary>The Human clearing a Task's Action is allowed and logged as <c>action cleared</c>.</summary>
    [Fact]
    public void Update_HumanClearsAction_IsLogged()
    {
        FixedProofSource proof = new(FixedProofSource.Complete);
        using TaskToolHarness harness = Build(proof);
        TaskItem task = CreateGated(harness, action: "Send the reply");

        TaskResult result = harness.Service.Update(task.Id, new TaskPatch { Action = Optional<string?>.Set(null) }, null, HumanOf(harness));

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.Null(saved.Task.Action);
        Assert.Equal("action cleared", saved.Change.Entry.Summary);
    }

    /// <summary>An agent turning the Human gate on is allowed and logged as <c>human gate on</c>.</summary>
    [Fact]
    public void Update_AgentTurnsGateOn_IsLogged()
    {
        FixedProofSource proof = new(FixedProofSource.Complete);
        using TaskToolHarness harness = Build(proof);
        TaskResult created = harness.Service.Create(new TaskDraft("Write the reply", Team, null, TaskState.Review), HumanOf(harness));
        TaskItem task = Assert.IsType<TaskResult.Saved>(created).Task;

        TaskResult result = harness.Service.Update(task.Id, new TaskPatch { HumanGate = Optional<bool>.Set(true) }, null, Nova);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.True(saved.Task.HumanGate);
        Assert.Equal("human gate on", saved.Change.Entry.Summary);
    }

    /// <summary>A gated Task cannot be created in Needs You or Done (H-5), and nothing is written.</summary>
    /// <param name="status">The status the draft asks for.</param>
    [Theory]
    [InlineData(TaskState.NeedsYou)]
    [InlineData(TaskState.Done)]
    public void Create_GatedInNeedsYouOrDone_IsRefused(TaskState status)
    {
        FixedProofSource proof = new(FixedProofSource.Complete);
        using TaskToolHarness harness = Build(proof);
        TaskDraft draft = new("Write the reply", Team, null, status) { HumanGate = true };

        TaskResult result = harness.Service.Create(draft, HumanOf(harness), humanOverridesGates: true);

        TaskResult.GateRefused refused = Assert.IsType<TaskResult.GateRefused>(result);
        Assert.Equal(GateId.HumanGate, refused.Gate);
        Assert.EndsWith(" is Human-gated, so it starts before Needs You.", refused.Message, StringComparison.Ordinal); // contains-ok: the refused Task is never created, so its id is not known to the test; HumanGateRulesTests pins the whole message.
        Assert.False(refused.CanOverride);
        Assert.Empty(harness.Store.All);
    }

    /// <summary>An outside edit that removes a Task's Action is reverted and logged, and the rest of the edit stays.</summary>
    [Fact]
    public void OutsideEdit_RemovesAction_IsRevertedAndLogged()
    {
        FixedProofSource proof = new(FixedProofSource.Complete);
        using TaskToolHarness harness = Build(proof);
        TaskItem task = CreateGated(harness, action: "Send the reply");
        File.WriteAllText(task.Path, TaskFileFormat.Compose(task with { Action = null, Description = "Edited by hand" }));

        harness.Store.RebuildFromWatcher();

        TaskItem? after = harness.Store.Get(task.Id);
        Assert.Equal("Send the reply", after?.Action);
        Assert.Equal("Edited by hand", after?.Description);
        Assert.Equal($"outside edit refused by Human gate: Only You can turn off the Human gate on {task.Id}.", after?.ChangeLog[^1].Summary);
    }

    /// <summary>An outside edit that moves a gated Task to Done is reverted and logged, and the rest of the edit stays.</summary>
    [Fact]
    public void OutsideEdit_MovesGatedToDone_IsRevertedAndLogged()
    {
        FixedProofSource proof = new(FixedProofSource.Complete);
        using TaskToolHarness harness = Build(proof);
        TaskItem task = CreateGated(harness);
        File.WriteAllText(task.Path, TaskFileFormat.Compose(task with { Status = TaskState.Done, Description = "Edited by hand" }));

        harness.Store.RebuildFromWatcher();

        TaskItem? after = harness.Store.Get(task.Id);
        Assert.Equal(TaskState.Review, after?.Status);
        Assert.Equal("Edited by hand", after?.Description);
        Assert.Equal(
            $"outside edit refused by Human gate: {task.Id} needs You's acceptance. Move it to Needs You when the Proof is complete; you'll be woken when You decides.",
            after?.ChangeLog[^1].Summary);
    }

    /// <summary>A gated Task that appears outside Huddle already in Needs You or Done is reset to Review and logged.</summary>
    /// <param name="status">The status the file claims.</param>
    [Theory]
    [InlineData(TaskState.NeedsYou)]
    [InlineData(TaskState.Done)]
    public void OutsideEdit_GatedTaskCreatedInNeedsYouOrDone_IsResetToReviewAndLogged(TaskState status)
    {
        FixedProofSource proof = new(FixedProofSource.Complete);
        using TaskToolHarness harness = Build(proof);
        TaskItem template = CreateGated(harness);
        TaskId newId = TaskId.TryParse("PLAT-0099", out TaskId parsed) ? parsed : throw new InvalidOperationException("bad id");
        string path = Path.Combine(Path.GetDirectoryName(template.Path) ?? string.Empty, Path.GetFileName(template.Path).Replace(template.Id.ToString(), newId.ToString(), StringComparison.Ordinal));
        File.WriteAllText(path, TaskFileFormat.Compose(template with { Id = newId, Path = path, Status = status }));

        harness.Store.RebuildFromWatcher();

        TaskItem? after = harness.Store.Get(newId);
        Assert.Equal(TaskState.Review, after?.Status);
        Assert.Equal($"outside edit refused by Human gate: {newId} is Human-gated, so it starts before Needs You.", after?.ChangeLog[^1].Summary);
    }

    /// <summary>Editing an accepted Task's description makes the acceptance stale.</summary>
    [Fact]
    public void Update_EditAfterAcceptance_MakesItStale()
    {
        FixedProofSource proof = new(FixedProofSource.Complete);
        using TaskToolHarness harness = Build(proof);
        TaskItem accepted = Accept(harness, CreateInNeedsYou(harness, proof));
        Assert.Equal(AcceptanceState.Valid, Acceptance.Of(accepted).State);

        TaskResult result = harness.Service.Update(accepted.Id, new TaskPatch { Description = "Changed after acceptance" }, null, HumanOf(harness));

        TaskItem updated = Assert.IsType<TaskResult.Saved>(result).Task;
        Assert.Equal(TaskState.NeedsYou, updated.Status);
        Assert.Equal(AcceptanceState.Stale, Acceptance.Of(updated).State);
    }
}

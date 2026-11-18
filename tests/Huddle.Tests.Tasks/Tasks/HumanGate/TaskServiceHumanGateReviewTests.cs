using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.HumanGate;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Tools;
using static Agency.Huddle.Tests.Tasks.TaskGateText;

namespace Agency.Huddle.Tests.Tasks.HumanGate;

/// <summary>
/// Gaps the review of P5 found in <see cref="TaskService"/> and the Human gate: forged acceptance by an outside edit, the
/// Human gate switch (H-1), moving out of Needs You, an ungated Task created in Needs You, decisions on closed Tasks and
/// the <see cref="TaskEvents.TaskChanged"/> count of a decision (Human gate Spec §6.4 to §6.6).
/// </summary>
public sealed class TaskServiceHumanGateReviewTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
    private static readonly TaskActor Nova = new(TaskActorKind.Agent, "Nova", "nova-id");

    /// <summary>Builds the harness on a fixed clock and the given Proof source.</summary>
    /// <param name="proof">The Proof source.</param>
    private static TaskToolHarness Build(FixedProofSource proof) => new(clock: new ManualTimeProvider(Noon), proof: proof);

    /// <summary>The Human's actor.</summary>
    /// <param name="harness">The harness.</param>
    private static TaskActor HumanOf(TaskToolHarness harness) => TaskActors.Human(harness.Options.Value);

    /// <summary>Creates a Task as the Human.</summary>
    /// <param name="harness">The harness.</param>
    /// <param name="gated">Whether it is Human-gated.</param>
    /// <param name="action">The Action, or <see langword="null"/>.</param>
    private static TaskItem Create(TaskToolHarness harness, bool gated = true, string? action = null)
    {
        TaskDraft draft = new("Write the reply", Team, null, TaskState.InProgress) { HumanGate = gated, Action = action };
        return Assert.IsType<TaskResult.Saved>(harness.Service.Create(draft, HumanOf(harness))).Task;
    }

    /// <summary>Creates a gated Task and moves it into Needs You (Proof complete).</summary>
    /// <param name="harness">The harness.</param>
    /// <param name="action">The Action, or <see langword="null"/>.</param>
    private static TaskItem InNeedsYou(TaskToolHarness harness, string? action = null)
    {
        TaskItem task = Create(harness, true, action);
        return Assert.IsType<TaskResult.Saved>(harness.Service.Update(task.Id, new TaskPatch { Status = TaskState.NeedsYou }, null, Nova)).Task;
    }

    /// <summary>Writes the Task to its file as an outside edit and lets the store see it.</summary>
    /// <param name="harness">The harness.</param>
    /// <param name="edited">The Task as the file should now read.</param>
    private static TaskItem? OutsideEdit(TaskToolHarness harness, TaskItem edited)
    {
        File.WriteAllText(edited.Path, TaskFileFormat.Compose(edited));
        harness.Store.RebuildFromWatcher();
        return harness.Store.Get(edited.Id);
    }

    /// <summary>The log entry an acceptance of the Task's current content writes.</summary>
    /// <param name="task">The Task.</param>
    private static ChangeLogEntry AcceptedEntry(TaskItem task) =>
        new(Noon, "You", "accepted " + ContentFingerprint.Of(task.Title, task.Action, task.Description));

    /// <summary>An <c>accepted</c> entry added by editing the file is refused, voids the acceptance and leaves Done refused.</summary>
    [Fact]
    public void OutsideEdit_ForgedAcceptedEntry_IsRefusedAndVoidsAcceptance()
    {
        FixedProofSource proof = new(FixedProofSource.Complete);
        using TaskToolHarness harness = Build(proof);
        TaskItem task = InNeedsYou(harness);

        TaskItem? after = OutsideEdit(harness, task with { ChangeLog = [.. task.ChangeLog, AcceptedEntry(task)] });

        Assert.NotNull(after);
        Assert.Equal($"outside edit refused by Human gate: only You can accept {task.Id}, in Huddle.", after.ChangeLog[^1].Summary);
        Assert.Equal(AcceptanceState.None, Acceptance.Of(after).State);
        TaskResult result = harness.Service.Update(task.Id, new TaskPatch { Status = TaskState.Done }, null, Nova);
        TaskResult.GateRefused refused = Assert.IsType<TaskResult.GateRefused>(result);
        Assert.Equal(GateId.HumanGate, refused.Gate);
        Assert.Equal(TaskState.NeedsYou, harness.Store.Get(task.Id)?.Status);
    }

    /// <summary>One outside write that forges an acceptance and moves the Task to Done is reverted to Needs You with both refusals logged.</summary>
    [Fact]
    public void OutsideEdit_ForgedAcceptanceAndDoneInOneWrite_StaysInNeedsYouWithBothRefusals()
    {
        FixedProofSource proof = new(FixedProofSource.Complete);
        using TaskToolHarness harness = Build(proof);
        TaskItem task = InNeedsYou(harness);

        TaskItem? after = OutsideEdit(harness, task with { Status = TaskState.Done, ChangeLog = [.. task.ChangeLog, AcceptedEntry(task)] });

        Assert.NotNull(after);
        Assert.Equal(TaskState.NeedsYou, after.Status);
        Assert.Equal(AcceptanceState.None, Acceptance.Of(after).State);
        List<string> summaries = [.. after.ChangeLog.Select(static entry => entry.Summary)];
        Assert.Contains($"outside edit refused by Human gate: only You can accept {task.Id}, in Huddle.", summaries);
        Assert.Contains($"outside edit refused by Human gate: {task.Id} needs You's acceptance. Move it to Needs You when the Proof is complete; you'll be woken when You decides.", summaries);
    }

    /// <summary>A real acceptance survives an unrelated, content-neutral outside edit such as a new tag.</summary>
    [Fact]
    public void OutsideEdit_TagAfterRealAcceptance_KeepsAcceptanceValid()
    {
        FixedProofSource proof = new(FixedProofSource.Complete);
        using TaskToolHarness harness = Build(proof);
        TaskItem task = InNeedsYou(harness, "Send the reply");
        TaskItem accepted = Assert.IsType<TaskResult.Saved>(harness.Service.Decide(task.Id, TaskDecision.Accept, null, task.Version, HumanOf(harness))).Task;

        TaskItem? after = OutsideEdit(harness, accepted with { Tags = ["urgent"] });

        Assert.NotNull(after);
        Assert.Contains("urgent", after.Tags);
        Assert.Equal(AcceptanceState.Valid, Acceptance.Of(after).State);
    }

    /// <summary>An outside-edit refusal after the latest acceptance voids it; one before it does not.</summary>
    [Fact]
    public void Acceptance_RefusalEntryAfterAccepted_VoidsIt()
    {
        const string Refusal = "outside edit refused by Human gate: only You can accept PLAT-0001, in Huddle.";
        TaskItem draft = TestTasks.Make(title: "Write the summary", status: TaskState.NeedsYou, description: "Body text") with { Action = "Read the summary" };
        ChangeLogEntry entered = TestTasks.Entry("2026-10-01T09:00:00Z", "Ada", "status: In Progress → Needs You (Proof 4/4)");
        string hash = ContentFingerprint.Of(draft.Title, draft.Action, draft.Description);
        ChangeLogEntry accepted = TestTasks.Entry("2026-10-02T09:00:00Z", "Emre", "accepted " + hash);
        ChangeLogEntry refusal = TestTasks.Entry("2026-10-03T09:00:00Z", "Emre", Refusal);

        AcceptanceInfo voided = Acceptance.Of(draft with { ChangeLog = [entered, accepted, refusal] });
        AcceptanceInfo kept = Acceptance.Of(draft with { ChangeLog = [entered, refusal, accepted] });

        Assert.Equal(AcceptanceState.None, voided.State);
        Assert.Equal(AcceptanceState.Valid, kept.State);
    }

    /// <summary>Accept is refused with the "isn't waiting" text when the Proof is incomplete.</summary>
    [Fact]
    public void Decide_AcceptWithIncompleteProof_IsRefusedAsNotWaiting()
    {
        FixedProofSource proof = new(FixedProofSource.Complete);
        using TaskToolHarness harness = Build(proof);
        TaskItem task = InNeedsYou(harness);
        proof.Current = FixedProofSource.ThreeOfFour;

        TaskResult result = harness.Service.Decide(task.Id, TaskDecision.Accept, null, task.Version, HumanOf(harness));

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Equal([$"{task.Id} isn't waiting for your decision."], refused.Problems);
        Assert.Equal(TaskState.NeedsYou, harness.Store.Get(task.Id)?.Status);
    }

    /// <summary>An Agent turning the Human gate off is refused with the H-1 text and cannot override.</summary>
    [Fact]
    public void Update_AgentTurnsGateOff_IsRefusedByH1()
    {
        FixedProofSource proof = new(FixedProofSource.Complete);
        using TaskToolHarness harness = Build(proof);
        TaskItem task = Create(harness);

        TaskResult result = harness.Service.Update(task.Id, new TaskPatch { HumanGate = Optional<bool>.Set(false) }, null, Nova);

        TaskResult.GateRefused refused = Assert.IsType<TaskResult.GateRefused>(result);
        Assert.Equal($"Only You can turn off the Human gate on {task.Id}.", refused.Message);
        Assert.False(refused.CanOverride);
        Assert.True(harness.Store.Get(task.Id)?.HumanGate);
    }

    /// <summary>The Human turning the gate off, with no Action, succeeds and logs it.</summary>
    [Fact]
    public void Update_HumanTurnsGateOff_SucceedsAndLogs()
    {
        FixedProofSource proof = new(FixedProofSource.Complete);
        using TaskToolHarness harness = Build(proof);
        TaskItem task = Create(harness);

        TaskResult result = harness.Service.Update(task.Id, new TaskPatch { HumanGate = Optional<bool>.Set(false) }, null, HumanOf(harness));

        TaskItem saved = Assert.IsType<TaskResult.Saved>(result).Task;
        Assert.False(saved.HumanGate);
        Assert.Equal("human gate off", saved.ChangeLog[^1].Summary);
    }

    /// <summary>An outside edit removing <c>human_gate: true</c> is reverted and logged, and the rest of the edit stays.</summary>
    [Fact]
    public void OutsideEdit_RemovesHumanGate_IsRevertedAndLogged()
    {
        FixedProofSource proof = new(FixedProofSource.Complete);
        using TaskToolHarness harness = Build(proof);
        TaskItem task = Create(harness);

        TaskItem? after = OutsideEdit(harness, task with { HumanGate = false, Description = "Edited by hand" });

        Assert.NotNull(after);
        Assert.True(after.HumanGate);
        Assert.Equal("Edited by hand", after.Description);
        Assert.Equal($"outside edit refused by Human gate: Only You can turn off the Human gate on {task.Id}.", after.ChangeLog[^1].Summary);
    }

    /// <summary>An Agent may move a gated Task out of Needs You, and an acceptance made before is no longer valid.</summary>
    [Fact]
    public void Update_AgentMovesOutOfNeedsYou_VoidsEarlierAcceptance()
    {
        FixedProofSource proof = new(FixedProofSource.Complete);
        using TaskToolHarness harness = Build(proof);
        TaskItem task = InNeedsYou(harness, "Send the reply");
        TaskItem accepted = Assert.IsType<TaskResult.Saved>(harness.Service.Decide(task.Id, TaskDecision.Accept, null, task.Version, HumanOf(harness))).Task;

        TaskItem moved = Assert.IsType<TaskResult.Saved>(harness.Service.Update(accepted.Id, new TaskPatch { Status = TaskState.InProgress }, null, Nova)).Task;
        TaskItem? back = harness.Store.Get(accepted.Id);

        Assert.Equal(TaskState.InProgress, moved.Status);
        Assert.NotNull(back);
        Assert.Equal(TaskState.NeedsYou, back.Status);
        Assert.Equal(AcceptanceState.None, Acceptance.Of(back).State);
    }

    /// <summary>An ungated Task that appears outside Huddle already in Needs You is reset to In Progress and logged.</summary>
    [Fact]
    public void OutsideEdit_UngatedTaskCreatedInNeedsYou_IsResetToInProgressAndLogged()
    {
        FixedProofSource proof = new(FixedProofSource.Complete);
        using TaskToolHarness harness = Build(proof);
        TaskItem template = Create(harness, gated: false);
        TaskId newId = TaskId.TryParse("PLAT-0099", out TaskId parsed) ? parsed : throw new InvalidOperationException("bad id");
        string path = Path.Combine(Path.GetDirectoryName(template.Path) ?? string.Empty, Path.GetFileName(template.Path).Replace(template.Id.ToString(), newId.ToString(), StringComparison.Ordinal));

        TaskItem? after = OutsideEdit(harness, template with { Id = newId, Path = path, Status = TaskState.NeedsYou });

        Assert.NotNull(after);
        Assert.Equal(TaskState.InProgress, after.Status);
        Assert.Equal($"outside edit refused by Human gate: {newId} isn't Human-gated, so it goes from In Progress to Done.", after.ChangeLog[^1].Summary);
    }

    /// <summary>Deciding on a closed Task is refused as not waiting, even if its file says In Progress.</summary>
    [Fact]
    public void Decide_ClosedTask_IsRefusedAsNotWaiting()
    {
        FixedProofSource proof = new(FixedProofSource.Complete);
        using TaskToolHarness harness = Build(proof);
        TaskItem task = InNeedsYou(harness);
        TaskItem closed = Assert.IsType<TaskResult.Saved>(harness.Service.Decide(task.Id, TaskDecision.Decline, "No longer needed", task.Version, HumanOf(harness))).Task;
        TaskItem? reopened = OutsideEdit(harness, closed with { Status = TaskState.InProgress });
        Assert.NotNull(reopened);
        Assert.True(reopened.Location.Closed);

        TaskResult result = harness.Service.Decide(task.Id, TaskDecision.RequestChanges, "Try again", reopened.Version, HumanOf(harness));

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Equal([$"{task.Id} isn't waiting for your decision."], refused.Problems);
    }

    /// <summary>Accept, with or without an Action, and Request changes each raise <see cref="TaskEvents.TaskChanged"/> exactly once.</summary>
    /// <param name="decision">The decision.</param>
    /// <param name="action">The Task's Action, or <see langword="null"/>.</param>
    /// <param name="note">The decision's note, or <see langword="null"/>.</param>
    [Theory]
    [InlineData(TaskDecision.Accept, null, null)]
    [InlineData(TaskDecision.Accept, "Send the reply", null)]
    [InlineData(TaskDecision.RequestChanges, null, "Tighten the intro")]
    public void Decide_RaisesTaskChangedExactlyOnce(TaskDecision decision, string? action, string? note)
    {
        FixedProofSource proof = new(FixedProofSource.Complete);
        using TaskToolHarness harness = Build(proof);
        TaskItem task = InNeedsYou(harness, action);
        List<TaskChange> raised = [];
        harness.Events.TaskChanged += raised.Add;

        TaskResult result = harness.Service.Decide(task.Id, decision, note, task.Version, HumanOf(harness));

        Assert.IsType<TaskResult.Saved>(result);
        Assert.Single(raised);
    }

    /// <summary>Decline from Needs You rejects the Task and closes it.</summary>
    [Fact]
    public void Decide_DeclineFromNeedsYou_ClosesTheTask()
    {
        FixedProofSource proof = new(FixedProofSource.Complete);
        using TaskToolHarness harness = Build(proof);
        TaskItem task = InNeedsYou(harness);

        TaskResult result = harness.Service.Decide(task.Id, TaskDecision.Decline, "Not needed", task.Version, HumanOf(harness));

        TaskItem saved = Assert.IsType<TaskResult.Saved>(result).Task;
        Assert.Equal(TaskState.Rejected, saved.Status);
        Assert.True(harness.Store.Get(task.Id)?.Location.Closed);
    }
}

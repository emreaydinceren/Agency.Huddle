using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.HumanGate;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Tools;
using static Agency.Huddle.Tests.Tasks.TaskGateText;

namespace Agency.Huddle.Tests.Tasks.HumanGate;

/// <summary>
/// <see cref="TaskService.Decide"/> records the Human's Accept, Request changes or Decline, each with its precondition,
/// its Change log entries and its status (Human gate Spec §6.6, plan T5.3).
/// </summary>
public sealed class TaskServiceDecideTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
    private static readonly TaskActor Nova = new(TaskActorKind.Agent, "Nova", "nova-id");

    /// <summary>Builds the harness on a fixed clock and a complete Proof.</summary>
    private static TaskToolHarness Build() => new(clock: new ManualTimeProvider(Noon), proof: new FixedProofSource(FixedProofSource.Complete));

    /// <summary>The Human's actor.</summary>
    /// <param name="harness">The harness.</param>
    private static TaskActor HumanOf(TaskToolHarness harness) => TaskActors.Human(harness.Options.Value);

    /// <summary>Creates a gated Task and moves it into Needs You.</summary>
    /// <param name="harness">The harness.</param>
    /// <param name="action">The Action, or <see langword="null"/>.</param>
    private static TaskItem InNeedsYou(TaskToolHarness harness, string? action = null)
    {
        TaskDraft draft = new("Write the reply", Team, null, TaskState.Review) { HumanGate = true, Action = action };
        TaskItem task = Assert.IsType<TaskResult.Saved>(harness.Service.Create(draft, HumanOf(harness))).Task;
        TaskPatch patch = new() { Status = TaskState.NeedsYou };
        return Assert.IsType<TaskResult.Saved>(harness.Service.Update(task.Id, patch, null, Nova)).Task;
    }

    /// <summary>Creates a gated Task left in Review.</summary>
    /// <param name="harness">The harness.</param>
    private static TaskItem InReview(TaskToolHarness harness)
    {
        TaskDraft draft = new("Write the reply", Team, null, TaskState.Review) { HumanGate = true };
        return Assert.IsType<TaskResult.Saved>(harness.Service.Create(draft, HumanOf(harness))).Task;
    }

    /// <summary>The fingerprint an Accept records for a Task.</summary>
    /// <param name="task">The Task.</param>
    private static string HashOf(TaskItem task) => ContentFingerprint.Of(task.Title, task.Action, task.Description);

    /// <summary>Accept on a Task with no Action logs <c>accepted &lt;hash&gt;</c> and then the status entry, and lands in Done.</summary>
    [Fact]
    public void Decide_AcceptWithoutAction_LogsAcceptedThenStatusAndLandsInDone()
    {
        using TaskToolHarness harness = Build();
        TaskItem task = InNeedsYou(harness);

        TaskResult result = harness.Service.Decide(task.Id, TaskDecision.Accept, null, task.Version, HumanOf(harness));

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.Equal(TaskState.Done, saved.Task.Status);
        Assert.Equal(TaskState.Done, harness.Store.Get(task.Id)?.Status);
        IReadOnlyList<ChangeLogEntry> log = saved.Task.ChangeLog;
        Assert.Equal("accepted " + HashOf(task), log[^2].Summary);
        Assert.Equal("status: Needs You → Done", log[^1].Summary);
        Assert.Equal(HumanOf(harness).Name, log[^2].Actor);
    }

    /// <summary>Accept on a Task with an Action records the acceptance, leaves the Task in Needs You, and the acceptance is Valid.</summary>
    [Fact]
    public void Decide_AcceptWithAction_StaysInNeedsYouWithValidAcceptance()
    {
        using TaskToolHarness harness = Build();
        TaskItem task = InNeedsYou(harness, "send the reply");

        TaskResult result = harness.Service.Decide(task.Id, TaskDecision.Accept, null, task.Version, HumanOf(harness));

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.Equal(TaskState.NeedsYou, saved.Task.Status);
        Assert.Equal("accepted " + HashOf(task), saved.Task.ChangeLog[^1].Summary);
        Assert.Equal(AcceptanceState.Valid, Acceptance.Of(saved.Task).State);
    }

    /// <summary>Accept on a Task whose acceptance is already valid is refused and writes nothing.</summary>
    [Fact]
    public void Decide_AcceptWhenAlreadyValid_IsRefused()
    {
        using TaskToolHarness harness = Build();
        TaskItem task = InNeedsYou(harness, "send the reply");
        TaskItem accepted = Assert.IsType<TaskResult.Saved>(harness.Service.Decide(task.Id, TaskDecision.Accept, null, task.Version, HumanOf(harness))).Task;

        TaskResult result = harness.Service.Decide(task.Id, TaskDecision.Accept, null, accepted.Version, HumanOf(harness));

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Equal([$"{task.Id} isn't waiting for your decision."], refused.Problems);
        Assert.Equal(accepted.ChangeLog.Count, harness.Store.Get(task.Id)?.ChangeLog.Count);
    }

    /// <summary>Accept on a Task that is not in Needs You is refused.</summary>
    [Fact]
    public void Decide_AcceptOutsideNeedsYou_IsRefused()
    {
        using TaskToolHarness harness = Build();
        TaskItem task = InReview(harness);

        TaskResult result = harness.Service.Decide(task.Id, TaskDecision.Accept, null, task.Version, HumanOf(harness));

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Equal([$"{task.Id} isn't waiting for your decision."], refused.Problems);
        Assert.Equal(TaskState.Review, harness.Store.Get(task.Id)?.Status);
    }

    /// <summary>Request changes from Needs You logs the note and moves the Task to In Progress.</summary>
    [Fact]
    public void Decide_RequestChangesFromNeedsYou_LogsNoteAndMovesToInProgress()
    {
        using TaskToolHarness harness = Build();
        TaskItem task = InNeedsYou(harness);

        TaskResult result = harness.Service.Decide(task.Id, TaskDecision.RequestChanges, "Tighten the intro", task.Version, HumanOf(harness));

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.Equal(TaskState.InProgress, saved.Task.Status);
        Assert.Contains(saved.Task.ChangeLog, entry => entry.Summary == "changes requested: Tighten the intro");
        Assert.Equal("status: Needs You → In Progress", saved.Task.ChangeLog[^1].Summary);
    }

    /// <summary>Request changes is also allowed from Review.</summary>
    [Fact]
    public void Decide_RequestChangesFromReview_MovesToInProgress()
    {
        using TaskToolHarness harness = Build();
        TaskItem task = InReview(harness);

        TaskResult result = harness.Service.Decide(task.Id, TaskDecision.RequestChanges, "Add the date", task.Version, HumanOf(harness));

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.Equal(TaskState.InProgress, saved.Task.Status);
    }

    /// <summary>Request changes without a note is refused with the exact text.</summary>
    /// <param name="note">A missing or blank note.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Decide_RequestChangesWithoutNote_IsRefused(string? note)
    {
        using TaskToolHarness harness = Build();
        TaskItem task = InNeedsYou(harness);

        TaskResult result = harness.Service.Decide(task.Id, TaskDecision.RequestChanges, note, task.Version, HumanOf(harness));

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Equal(["Say what needs to change."], refused.Problems);
        Assert.Equal(TaskState.NeedsYou, harness.Store.Get(task.Id)?.Status);
    }

    /// <summary>Decline without a note is refused with the exact text.</summary>
    /// <param name="note">A missing or blank note.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Decide_DeclineWithoutNote_IsRefused(string? note)
    {
        using TaskToolHarness harness = Build();
        TaskItem task = InNeedsYou(harness);

        TaskResult result = harness.Service.Decide(task.Id, TaskDecision.Decline, note, task.Version, HumanOf(harness));

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Equal(["Say why you're declining."], refused.Problems);
        Assert.Equal(TaskState.NeedsYou, harness.Store.Get(task.Id)?.Status);
    }

    /// <summary>Decline logs the reason, rejects the Task and closes it in one write.</summary>
    [Fact]
    public void Decide_Decline_RejectsAndClosesInOneWrite()
    {
        using TaskToolHarness harness = Build();
        TaskItem task = InNeedsYou(harness);

        TaskResult result = harness.Service.Decide(task.Id, TaskDecision.Decline, "Not needed any more", task.Version, HumanOf(harness));

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.Equal(TaskState.Rejected, saved.Task.Status);
        Assert.True(saved.Task.Location.Closed);
        Assert.True(harness.Store.Get(task.Id)?.Location.Closed);
        IReadOnlyList<ChangeLogEntry> log = saved.Task.ChangeLog;
        int declined = log.ToList().FindIndex(entry => entry.Summary == "declined: Not needed any more");
        Assert.True(declined >= 0);
        Assert.Equal("closed", log[^1].Summary);
        Assert.True(declined < log.Count - 1);
    }

    /// <summary>An agent deciding is refused with the exact text and nothing is written.</summary>
    [Fact]
    public void Decide_NonHumanActor_IsRefused()
    {
        using TaskToolHarness harness = Build();
        TaskItem task = InNeedsYou(harness);

        TaskResult result = harness.Service.Decide(task.Id, TaskDecision.Accept, null, task.Version, Nova);

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Equal([$"Only {HumanOf(harness).Name} can decide on {task.Id}."], refused.Problems);
        Assert.Equal(task.ChangeLog.Count, harness.Store.Get(task.Id)?.ChangeLog.Count);
    }

    /// <summary>A stale baseVersion returns the conflict result with the current Task and no fields, and writes nothing.</summary>
    [Fact]
    public void Decide_StaleBaseVersion_ReturnsConflict()
    {
        using TaskToolHarness harness = Build();
        TaskItem task = InNeedsYou(harness);

        TaskResult result = harness.Service.Decide(task.Id, TaskDecision.Accept, null, "stale-version", HumanOf(harness));

        TaskResult.Conflict conflict = Assert.IsType<TaskResult.Conflict>(result);
        Assert.Equal(task.Id, conflict.Current.Id);
        Assert.Empty(conflict.Fields);
        Assert.Equal(TaskState.NeedsYou, harness.Store.Get(task.Id)?.Status);
    }

    /// <summary>An unknown id is reported as not found.</summary>
    [Fact]
    public void Decide_UnknownTask_IsNotFound()
    {
        using TaskToolHarness harness = Build();
        TaskItem task = InNeedsYou(harness);
        TaskId missing = new(task.Id.Prefix, task.Id.Number + 900);

        TaskResult result = harness.Service.Decide(missing, TaskDecision.Accept, null, task.Version, HumanOf(harness));

        Assert.IsType<TaskResult.NotFound>(result);
    }

    /// <summary>A decision raises <see cref="TaskEvents.TaskChanged"/> exactly once, carrying the decision and the note.</summary>
    [Fact]
    public void Decide_RaisesTaskChangedOnceWithDecisionAndNote()
    {
        using TaskToolHarness harness = Build();
        TaskItem task = InNeedsYou(harness);
        List<TaskChange> raised = [];
        harness.Events.TaskChanged += raised.Add;

        TaskResult result = harness.Service.Decide(task.Id, TaskDecision.Decline, "Not needed any more", task.Version, HumanOf(harness));

        Assert.IsType<TaskResult.Saved>(result);
        TaskChange change = Assert.Single(raised);
        Assert.Equal(TaskDecision.Decline, change.Decision);
        Assert.Equal("Not needed any more", change.Note);
    }
}

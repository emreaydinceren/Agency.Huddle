using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Tasks.HumanGate;
using static Agency.Huddle.Tests.Tasks.TaskGateText;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// A review is a Task (#669, D-15): a work Task In Progress that is <c>blocked_by</c> its open child tagged <c>review</c> cannot move to
/// Needs You or Done, the refusal names the review Task, and both moves are allowed once the review Task is Done.
/// </summary>
public sealed class ReviewTaskBlockerGateTests
{
    private const string BlockerGate = "'blockers-done-before-start'";

    private static readonly TaskActor Jane = new(TaskActorKind.Agent, "Jane", "jane-id");

    /// <summary>An ungated work Task blocked by an open review Task is refused Done, and the refusal names the review Task.</summary>
    [Fact]
    public void Update_WorkTaskBlockedByOpenReviewTask_DoneIsRefusedNamingIt()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(BlockerGate)));
        (TaskItem work, TaskItem review) = SeedWorkAndReview(harness, gated: false);

        TaskResult result = harness.Service.Update(work.Id, new TaskPatch { Status = TaskState.Done }, null, Jane);

        TaskResult.GateRefused refused = Assert.IsType<TaskResult.GateRefused>(result);
        Assert.Equal(GateId.BlockersDoneBeforeStart, refused.Gate);
        Assert.Contains(review.Id.ToString(), refused.Message, StringComparison.Ordinal); // contains-ok: the full refusal text is pinned in ProcessGateServiceBlockerTests; this checks the review Task is the one named
        Assert.Equal(TaskState.InProgress, harness.Store.Get(work.Id)?.Status);
    }

    /// <summary>A Human-gated work Task blocked by an open review Task is refused Needs You, naming the review Task, even with its Proof complete.</summary>
    [Fact]
    public void Update_GatedWorkTaskBlockedByOpenReviewTask_NeedsYouIsRefusedNamingIt()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(BlockerGate)), proof: new FixedProofSource(FixedProofSource.Complete));
        (TaskItem work, TaskItem review) = SeedWorkAndReview(harness, gated: true);

        TaskResult result = harness.Service.Update(work.Id, new TaskPatch { Status = TaskState.NeedsYou }, null, Jane);

        TaskResult.GateRefused refused = Assert.IsType<TaskResult.GateRefused>(result);
        Assert.Equal(GateId.BlockersDoneBeforeStart, refused.Gate);
        Assert.Contains(review.Id.ToString(), refused.Message, StringComparison.Ordinal); // contains-ok: see above; the named Task is the point
        Assert.Equal(TaskState.InProgress, harness.Store.Get(work.Id)?.Status);
    }

    /// <summary>Once the review Task is Done the ungated work Task can be Done.</summary>
    [Fact]
    public void Update_ReviewTaskDone_WorkTaskCanBeDone()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(BlockerGate)));
        (TaskItem work, TaskItem review) = SeedWorkAndReview(harness, gated: false);
        Assert.IsType<TaskResult.Saved>(harness.Service.Update(review.Id, new TaskPatch { Status = TaskState.Done }, null, Jane));

        TaskResult result = harness.Service.Update(work.Id, new TaskPatch { Status = TaskState.Done }, null, Jane);

        Assert.Equal(TaskState.Done, Assert.IsType<TaskResult.Saved>(result).Task.Status);
    }

    /// <summary>Once the review Task is Done the Human-gated work Task can move to Needs You.</summary>
    [Fact]
    public void Update_ReviewTaskDone_GatedWorkTaskCanMoveToNeedsYou()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(BlockerGate)), proof: new FixedProofSource(FixedProofSource.Complete));
        (TaskItem work, TaskItem review) = SeedWorkAndReview(harness, gated: true);
        Assert.IsType<TaskResult.Saved>(harness.Service.Update(review.Id, new TaskPatch { Status = TaskState.Done }, null, Jane));

        TaskResult result = harness.Service.Update(work.Id, new TaskPatch { Status = TaskState.NeedsYou }, null, Jane);

        Assert.Equal(TaskState.NeedsYou, Assert.IsType<TaskResult.Saved>(result).Task.Status);
    }

    /// <summary>Creates a work Task In Progress and its open child tagged review, then blocks the work Task by it as the Human (which never moves a Task).</summary>
    /// <param name="harness">The harness.</param>
    /// <param name="gated">Whether the work Task is Human-gated.</param>
    private static (TaskItem Work, TaskItem Review) SeedWorkAndReview(TaskToolHarness harness, bool gated)
    {
        TaskActor human = TaskActors.Human(harness.Options.Value);
        TaskDraft workDraft = new("Build the login", Team, null, TaskState.InProgress) { HumanGate = gated };
        TaskItem work = Assert.IsType<TaskResult.Saved>(harness.Service.Create(workDraft, human)).Task;
        TaskDraft reviewDraft = new("Review the login", Team, null, TaskState.InProgress, Parent: work.Id, Tags: ["review"]);
        TaskItem review = Assert.IsType<TaskResult.Saved>(harness.Service.Create(reviewDraft, human)).Task;
        Assert.IsType<TaskResult.Saved>(harness.Service.Update(work.Id, new TaskPatch { BlockedBy = [review.Id] }, null, human));
        return (work, review);
    }
}

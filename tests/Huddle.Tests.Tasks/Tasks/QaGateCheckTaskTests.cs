using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Tools;
using static Agency.Huddle.Tests.Tasks.TaskGateText;
using Proof = Agency.Huddle.App.Tasks.HumanGate.Proof;
using ProofContext = Agency.Huddle.App.Tasks.HumanGate.ProofContext;
using ProofEvaluator = Agency.Huddle.App.Tasks.HumanGate.ProofEvaluator;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// A review or QA check is itself the check (#669, D-22): <c>qa-evidence-before-done</c> does not apply to a Task tagged
/// <c>review</c> or <c>verify</c>, in the gate and in the Proof's <c>QA evidence</c> item.
/// </summary>
public sealed class QaGateCheckTaskTests
{
    private static readonly TaskActor Nova = new(TaskActorKind.Agent, "Nova", "nova-id");

    /// <summary>A Task tagged review, or verify in any case, reaches Done with no verifier Evidence and no verify child.</summary>
    /// <param name="tag">The tag.</param>
    [Theory]
    [InlineData("review")]
    [InlineData("Review")]
    [InlineData("verify")]
    [InlineData("VERIFY")]
    public void Update_CheckTaskTag_DoneWithoutQaEvidence(string tag)
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(QaGate)));
        TaskItem task = Create(harness, tag);

        TaskResult result = harness.Service.Update(task.Id, new TaskPatch { Status = TaskState.Done }, null, Nova);

        Assert.Equal(TaskState.Done, Assert.IsType<TaskResult.Saved>(result).Task.Status);
    }

    /// <summary>An untagged Task still needs the Evidence or the verify child.</summary>
    [Fact]
    public void Update_UntaggedTask_StillRefused()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(QaGate)));
        TaskItem task = Create(harness, null);

        TaskResult result = harness.Service.Update(task.Id, new TaskPatch { Status = TaskState.Done }, null, Nova);

        Assert.Equal(GateId.QaEvidenceBeforeDone, Assert.IsType<TaskResult.GateRefused>(result).Gate);
    }

    /// <summary>The gate adds no <c>QA evidence</c> item to the Proof of a review or verify Task, and still adds it to an untagged one.</summary>
    [Fact]
    public void Evaluate_CheckTask_HasNoQaEvidenceItem()
    {
        ProofContext context = new([], [GateId.QaEvidenceBeforeDone], false, [], new Dictionary<string, string>(StringComparer.Ordinal), [], [], []);

        Proof review = ProofEvaluator.Evaluate(TestTasks.Make(tags: ["review"]), context);
        Proof verify = ProofEvaluator.Evaluate(TestTasks.Make(tags: ["Verify"]), context);
        Proof plain = ProofEvaluator.Evaluate(TestTasks.Make(), context);

        Assert.Empty(review.Items);
        Assert.Empty(verify.Items);
        Assert.Single(plain.Items);
    }

    /// <summary>Creates a Platform Task as the Human.</summary>
    /// <param name="harness">The harness.</param>
    /// <param name="tag">One tag, or <see langword="null"/> for none.</param>
    private static TaskItem Create(TaskToolHarness harness, string? tag)
    {
        TaskDraft draft = new("Check the login", Team, null, Tags: tag is null ? null : [tag]);
        return Assert.IsType<TaskResult.Saved>(harness.Service.Create(draft, TaskActors.Human(harness.Options.Value))).Task;
    }
}

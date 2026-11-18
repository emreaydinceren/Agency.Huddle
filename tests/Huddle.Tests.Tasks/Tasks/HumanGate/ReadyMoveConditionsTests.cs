using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.HumanGate;
using Agency.Huddle.Tests.Acp.Tools;

namespace Agency.Huddle.Tests.Tasks.HumanGate;

/// <summary>
/// The two conditions D-7 adds to Huddle's move of a gated In Progress Task to Needs You (#669): the Proof has at least one item, and the
/// Task has no open blocker. An ungated Task is untouched.
/// </summary>
public sealed class ReadyMoveConditionsTests
{
    private const string Team = TaskGateText.Team;

    private static readonly DateTimeOffset Noon = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly TaskActor Nova = new(TaskActorKind.Agent, "Nova", "nova-id");

    /// <summary>A gated In Progress Task whose Proof has no items is not ready: an empty Definition of Done does not move it.</summary>
    [Fact]
    public void AddEvidence_GatedTaskWithEmptyProof_DoesNotMoveToNeedsYou()
    {
        using TaskToolHarness harness = new(proof: new FixedProofSource(Proof.Empty));
        TaskItem task = Create(harness, gated: true);

        AddEvidence(harness, task);

        Assert.Equal(TaskState.InProgress, harness.Store.Get(task.Id)?.Status);
    }

    /// <summary>A gated In Progress Task with a complete Proof but an open blocker is not ready: a review is still pending.</summary>
    [Fact]
    public void AddEvidence_GatedTaskWithCompleteProofAndOpenBlocker_DoesNotMoveToNeedsYou()
    {
        using TaskToolHarness harness = new(proof: new FixedProofSource(FixedProofSource.Complete));
        TaskItem task = Create(harness, gated: true);
        TaskItem review = Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft("Review it", Team, null, TaskState.InProgress, Parent: task.Id, Tags: ["review"]), Human(harness))).Task;
        _ = Assert.IsType<TaskResult.Saved>(harness.Service.Update(task.Id, new TaskPatch { BlockedBy = [review.Id] }, null, Human(harness)));

        AddEvidence(harness, task);

        Assert.Equal(TaskState.InProgress, harness.Store.Get(task.Id)?.Status);
    }

    /// <summary>The same Task with no blocker and a complete Proof does move, so the two refusals above are the blocker and the empty Proof, nothing else.</summary>
    [Fact]
    public void AddEvidence_GatedTaskWithCompleteProofAndNoBlocker_MovesToNeedsYou()
    {
        using TaskToolHarness harness = new(proof: new FixedProofSource(FixedProofSource.Complete));
        TaskItem task = Create(harness, gated: true);

        AddEvidence(harness, task);

        Assert.Equal(TaskState.NeedsYou, harness.Store.Get(task.Id)?.Status);
    }

    /// <summary>An ungated Task In Progress is untouched by an empty or a complete Proof.</summary>
    /// <param name="complete">Whether the Proof is complete; otherwise it is empty.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AddEvidence_UngatedTask_StaysInProgress(bool complete)
    {
        using TaskToolHarness harness = new(proof: new FixedProofSource(complete ? FixedProofSource.Complete : Proof.Empty));
        TaskItem task = Create(harness, gated: false);

        AddEvidence(harness, task);

        Assert.Equal(TaskState.InProgress, harness.Store.Get(task.Id)?.Status);
    }

    /// <summary>Adds an Evidence line as Nova, the write that makes a Proof ready.</summary>
    /// <param name="harness">The harness.</param>
    /// <param name="task">The Task.</param>
    private static void AddEvidence(TaskToolHarness harness, TaskItem task)
    {
        EvidenceLine line = new(Noon, "Nova", "dotnet test", 0, null, null, "raw");
        _ = Assert.IsType<TaskResult.Saved>(harness.Service.AddEvidence(task.Id, line, Nova));
    }

    /// <summary>The Human's own actor.</summary>
    /// <param name="harness">The harness.</param>
    private static TaskActor Human(TaskToolHarness harness) => TaskActors.Human(harness.Options.Value);

    /// <summary>Creates a Task In Progress as the Human, which never moves it.</summary>
    /// <param name="harness">The harness.</param>
    /// <param name="gated">Whether it is Human-gated.</param>
    private static TaskItem Create(TaskToolHarness harness, bool gated)
    {
        TaskDraft draft = new("Write the reply", Team, null, TaskState.InProgress) { HumanGate = gated };
        return Assert.IsType<TaskResult.Saved>(harness.Service.Create(draft, Human(harness))).Task;
    }
}

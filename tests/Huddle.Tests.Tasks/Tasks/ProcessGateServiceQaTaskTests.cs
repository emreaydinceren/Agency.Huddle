using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Tools;
using static Agency.Huddle.Tests.Tasks.TaskGateText;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// A QA check is a Task (#669, D-16): <c>qa-evidence-before-done</c> is met by an Evidence line from a member who verifies, or by a Done
/// child tagged <c>verify</c>; the refusal names both ways.
/// </summary>
public sealed class ProcessGateServiceQaTaskTests
{
    private static readonly DateTimeOffset Noon = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly TaskActor Nova = new(TaskActorKind.Agent, "Nova", "nova-id");
    private static readonly TaskActor Quinn = new(TaskActorKind.Agent, "Quinn", "quinn-id");

    /// <summary>A Done child tagged verify lets the work Task reach Done with no Evidence line on it.</summary>
    [Fact]
    public void Update_DoneVerifyChild_WorkTaskCanBeDone()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(QaGate)));
        TaskItem work = Create(harness, "Fix login", null, null);
        TaskItem verify = Create(harness, "QA the login", work.Id, "verify");
        Assert.IsType<TaskResult.Saved>(harness.Service.Update(verify.Id, new TaskPatch { Status = TaskState.Done }, null, HumanOf(harness), humanOverridesGates: true));

        TaskResult result = harness.Service.Update(work.Id, new TaskPatch { Status = TaskState.Done }, null, Nova);

        Assert.Equal(TaskState.Done, Assert.IsType<TaskResult.Saved>(result).Task.Status);
    }

    /// <summary>An Evidence line from a member who verifies still lets the work Task reach Done.</summary>
    [Fact]
    public void Update_VerifierEvidence_WorkTaskCanBeDone()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(QaGate)));
        _ = harness.Personas.Add(new PersonaIdentity("Quinn", "QA Engineer", "quinn", [Team], Role: new RoleRef("web", "qa", "1.2.0")), "You are Quinn.");
        TaskItem work = Create(harness, "Fix login", null, null);
        EvidenceLine line = new(Noon, "Quinn", Check, 0, null, null, string.Empty);
        Assert.IsType<TaskResult.Saved>(harness.Service.AddEvidence(work.Id, line, Quinn));

        TaskResult result = harness.Service.Update(work.Id, new TaskPatch { Status = TaskState.Done }, null, Nova);

        Assert.Equal(TaskState.Done, Assert.IsType<TaskResult.Saved>(result).Task.Status);
    }

    /// <summary>An open verify child and a Done review child meet nothing: the work Task is refused Done.</summary>
    [Fact]
    public void Update_OpenVerifyChildAndDoneReviewChild_WorkTaskIsRefused()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(QaGate)));
        TaskItem work = Create(harness, "Fix login", null, null);
        _ = Create(harness, "QA the login", work.Id, "verify");
        TaskItem review = Create(harness, "Review the login", work.Id, "review");
        Assert.IsType<TaskResult.Saved>(harness.Service.Update(review.Id, new TaskPatch { Status = TaskState.Done }, null, HumanOf(harness), humanOverridesGates: true));

        TaskResult result = harness.Service.Update(work.Id, new TaskPatch { Status = TaskState.Done }, null, Nova);

        Assert.Equal(GateId.QaEvidenceBeforeDone, Assert.IsType<TaskResult.GateRefused>(result).Gate);
    }

    /// <summary>The refusal names both ways to meet the gate: an Evidence line from a member who verifies, and a Done Task tagged verify.</summary>
    [Fact]
    public void Update_NoQaEvidenceNoVerifyChild_RefusalNamesBothWays()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(QaGate)));
        TaskItem work = Create(harness, "Fix login", null, null);

        TaskResult result = harness.Service.Update(work.Id, new TaskPatch { Status = TaskState.Done }, null, Nova);

        string message = Assert.IsType<TaskResult.GateRefused>(result).Message;
        Assert.Contains("Evidence line from a member who verifies", message, StringComparison.Ordinal); // contains-ok: the two ways are pinned separately so the exact wording around them is free
        Assert.Contains("tagged verify", message, StringComparison.Ordinal); // contains-ok: see above
    }

    /// <summary>The Human's own actor, as the Task panel builds it.</summary>
    /// <param name="harness">The harness.</param>
    private static TaskActor HumanOf(TaskToolHarness harness) => TaskActors.Human(harness.Options.Value);

    /// <summary>Creates a Platform Task as the Human.</summary>
    /// <param name="harness">The harness.</param>
    /// <param name="title">The title.</param>
    /// <param name="parent">The parent, or <see langword="null"/>.</param>
    /// <param name="tag">One tag, or <see langword="null"/> for none.</param>
    private static TaskItem Create(TaskToolHarness harness, string title, TaskId? parent, string? tag)
    {
        TaskDraft draft = new(title, Team, null, Parent: parent, Tags: tag is null ? null : [tag]);
        return Assert.IsType<TaskResult.Saved>(harness.Service.Create(draft, HumanOf(harness))).Task;
    }
}

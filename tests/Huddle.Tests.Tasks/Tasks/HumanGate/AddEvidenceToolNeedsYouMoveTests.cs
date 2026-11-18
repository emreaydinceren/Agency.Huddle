using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.HumanGate;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Tools;
using static Agency.Huddle.Tests.Tasks.TaskGateText;

namespace Agency.Huddle.Tests.Tasks.HumanGate;

/// <summary>
/// The <c>add_evidence</c> tool and <see cref="TaskService"/> driven together (#349, finding 15.5): a gated Task in Review on a
/// Team whose Definition of Done says "Tests pass: command <c>dotnet test</c> exits 0" moves to Needs You once QA reports that
/// command with its exit code, and does not move when the exit code is missing.
/// </summary>
public sealed class AddEvidenceToolNeedsYouMoveTests
{
    private static readonly DateTimeOffset Noon = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Builds the tool over the harness, calling as Nova.</summary>
    /// <param name="harness">The harness.</param>
    /// <param name="proofs">The Proof source the tool reads the open items from, or <see langword="null"/> for none.</param>
    private static AddEvidenceTool CreateTool(TaskToolHarness harness, IProofSource? proofs = null)
    {
        string? novaId = harness.NovaId;
        Assert.NotNull(novaId);
        return new AddEvidenceTool(harness.Service, harness.Store, new ToolCallLogWriter(harness.Options), harness.Directory, harness.Clock, novaId, new FakePromptSource(), proofs);
    }

    /// <summary>Creates a gated Task in Review, assigned to Nova, as the Human, so creation itself moves nothing.</summary>
    /// <param name="harness">The harness.</param>
    private static TaskItem CreateGatedInReview(TaskToolHarness harness)
    {
        TaskDraft draft = new("Fix login", Team, null, TaskState.InProgress, TaskPriority.Medium, "Nova") { HumanGate = true };
        return Assert.IsType<TaskResult.Saved>(harness.Service.Create(draft, TaskActors.Human(harness.Options.Value))).Task;
    }

    /// <summary>Evaluates the real <see cref="ProofEvaluator"/> over the Task's own Evidence and a one-rule Definition of Done.</summary>
    private sealed class DefinitionProofSource : IProofSource
    {
        /// <inheritdoc />
        public Proof For(TaskItem task)
        {
            DoneItem rule = new("Tests pass", new DoneRule.CommandExits("dotnet test"), ProofItemSource.Process);
            ProofContext context = new([rule], [], false, task.Evidence, new Dictionary<string, string>(StringComparer.Ordinal), [], [], []);
            return ProofEvaluator.Evaluate(task, context);
        }
    }

    /// <summary>QA's <c>dotnet test</c> with exit 0, added through the tool, completes the Proof and moves the Task to Needs You.</summary>
    [Fact]
    public async Task InvokeAsync_CommandWithExitZero_MovesGatedTaskToNeedsYou()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, new ManualTimeProvider(Noon), proof: new DefinitionProofSource());
        TaskItem task = CreateGatedInReview(harness);
        AddEvidenceTool tool = CreateTool(harness);
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["command"] = "dotnet test", ["exitCode"] = 0 };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal($"Added evidence to {task.Id}. Its status is Needs You.", result);
        Assert.Equal(TaskState.NeedsYou, harness.Store.Get(task.Id)?.Status);
    }

    /// <summary>A note that leaves the Proof open tells the Agent the gated Task is still in Review, which item is open and when it can go to Needs You (#547).</summary>
    [Fact]
    public async Task InvokeAsync_GatedTaskProofStaysOpen_SaysItIsInReviewAndNamesTheOpenItem()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, new ManualTimeProvider(Noon), proof: new DefinitionProofSource());
        TaskItem task = CreateGatedInReview(harness);
        AddEvidenceTool tool = CreateTool(harness, new DefinitionProofSource());
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["note"] = "Looked fine to me." };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal(
            $"Added evidence to {task.Id}. Its status is In Progress. Proof 0/1; first open item: Tests pass (Nova): not run. It can go to Needs You once its Proof is complete.",
            result);
        Assert.Equal(TaskState.InProgress, harness.Store.Get(task.Id)?.Status);
    }

    /// <summary>The call that completes the Proof says the Task is in Needs You, with no Proof clause (#547).</summary>
    [Fact]
    public async Task InvokeAsync_CompletingCall_SaysItIsInNeedsYouWithNoProofClause()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, new ManualTimeProvider(Noon), proof: new DefinitionProofSource());
        TaskItem task = CreateGatedInReview(harness);
        AddEvidenceTool tool = CreateTool(harness, new DefinitionProofSource());
        JsonObject failing = new() { ["taskId"] = task.Id.ToString(), ["command"] = "dotnet test", ["exitCode"] = 1 };
        string first = await tool.InvokeAsync(failing, ct);
        JsonObject passing = new() { ["taskId"] = task.Id.ToString(), ["command"] = "dotnet test", ["exitCode"] = 0 };

        string result = await tool.InvokeAsync(passing, ct);

        Assert.Equal(
            $"Added evidence to {task.Id}. Its status is In Progress. Proof 0/1; first open item: Tests pass (Nova): last run exit 1. It can go to Needs You once its Proof is complete.",
            first);
        Assert.Equal($"Added evidence to {task.Id}. Its status is Needs You.", result);
    }

    /// <summary>An ungated Task with an open item gets the Proof clause but not the sentence about Needs You (#547).</summary>
    [Fact]
    public async Task InvokeAsync_UngatedTaskWithOpenItem_NamesTheItemWithoutTheNeedsYouSentence()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, new ManualTimeProvider(Noon), proof: new DefinitionProofSource());
        TaskDraft draft = new("Fix login", Team, null, TaskState.InProgress, TaskPriority.Medium, "Nova");
        TaskItem task = Assert.IsType<TaskResult.Saved>(harness.Service.Create(draft, TaskActors.Human(harness.Options.Value))).Task;
        AddEvidenceTool tool = CreateTool(harness, new DefinitionProofSource());
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["note"] = "Started." };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal($"Added evidence to {task.Id}. Its status is In Progress. Proof 0/1; first open item: Tests pass (Nova): not run.", result);
    }

    /// <summary>The same call with no exit code is refused, writes nothing and leaves the Task in Review.</summary>
    [Fact]
    public async Task InvokeAsync_CommandWithoutExitCode_LeavesTaskInReviewWithNothingWritten()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, new ManualTimeProvider(Noon), proof: new DefinitionProofSource());
        TaskItem task = CreateGatedInReview(harness);
        AddEvidenceTool tool = CreateTool(harness);
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["command"] = "dotnet test" };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.DoesNotContain("Added evidence", result, StringComparison.Ordinal);
        TaskItem? after = harness.Store.Get(task.Id);
        Assert.Equal(TaskState.InProgress, after?.Status);
        Assert.NotNull(after);
        Assert.Empty(after.Evidence);
    }
}

using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.HumanGate;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.Tasks.HumanGate;

namespace Agency.Huddle.Tests.Acp.Tools;

/// <summary>
/// The Human gate through the Agent tools (Human gate Spec §6.10, plan T7.1): <c>update_task</c> and
/// <c>create_task</c> accept <c>human_gate</c> and <c>action</c>, a refused change returns the §6.4 message
/// verbatim, <c>get_task</c> reports the gate, the Action, the Proof and the acceptance, and
/// <c>list_tasks</c> filters on Needs You.
/// </summary>
public sealed class HumanGateToolTests
{
    private const string Team = "Platform";
    private static readonly DateTimeOffset Noon = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
    private static readonly TaskActor NovaActor = new(TaskActorKind.Agent, "Nova", "nova-id");

    /// <summary>Builds a harness on a fixed clock whose Proof is <paramref name="proof"/>.</summary>
    /// <param name="proof">The Proof every Task has.</param>
    /// <param name="ct">The test's cancellation token.</param>
    private static Task<TaskToolHarness> BuildAsync(FixedProofSource proof, CancellationToken ct) =>
        TaskToolHarness.CreateAsync(ct, clock: new ManualTimeProvider(Noon), proof: proof);

    /// <summary>Four items, three met and one open owned by Nova.</summary>
    private static Proof ThreeOfFourWithOwner() => new(
    [
        new ProofItem("Spec read", true, null, null, ProofItemSource.Process),
        new ProofItem("Tests added", true, null, null, ProofItemSource.Process),
        new ProofItem("Lint clean", true, null, null, ProofItemSource.Process),
        new ProofItem("Citation check", false, "Nova", null, ProofItemSource.Process),
    ]);

    /// <summary>Creates a gated Task in Review as the Human, with an optional Action.</summary>
    /// <param name="harness">The harness.</param>
    /// <param name="action">The Action, or <see langword="null"/>.</param>
    private static TaskItem GatedInReview(TaskToolHarness harness, string? action = null)
    {
        TaskDraft draft = new("Write the reply", Team, null, TaskState.Review) { HumanGate = true, Action = action };
        return Assert.IsType<TaskResult.Saved>(harness.Service.Create(draft, harness.HumanActor())).Task;
    }

    /// <summary>Creates a gated Task and moves it into Needs You as Nova.</summary>
    /// <param name="harness">The harness.</param>
    /// <param name="action">The Action, or <see langword="null"/>.</param>
    private static TaskItem GatedInNeedsYou(TaskToolHarness harness, string? action = null)
    {
        TaskItem task = GatedInReview(harness, action);
        TaskPatch patch = new() { Status = TaskState.NeedsYou };
        return Assert.IsType<TaskResult.Saved>(harness.Service.Update(task.Id, patch, null, NovaActor)).Task;
    }

    /// <summary>Builds an <see cref="UpdateTaskTool"/> calling as Nova.</summary>
    /// <param name="harness">The harness.</param>
    private static UpdateTaskTool UpdateAsNova(TaskToolHarness harness) =>
        new(harness.Service, harness.Store, harness.Triggers, harness.Directory, harness.Sprints, new FakePromptSource(), harness.NovaIdOrThrow());

    /// <summary>Builds a <see cref="GetTaskTool"/> over the harness and <paramref name="proof"/>.</summary>
    /// <param name="harness">The harness.</param>
    /// <param name="proof">The Proof source.</param>
    private static GetTaskTool GetOver(TaskToolHarness harness, FixedProofSource proof) =>
        new(harness.Store, harness.Sprints, new FakePromptSource(), proofs: proof);

    /// <summary>The <c>human_gate: true</c> argument turns the gate on for an ungated Task.</summary>
    [Fact]
    public async Task UpdateTask_HumanGateTrue_SetsTheGate()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await BuildAsync(new FixedProofSource(FixedProofSource.Complete), ct);
        TaskItem task = harness.CreateTask("Write the reply");
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["human_gate"] = true };

        string result = await UpdateAsNova(harness).InvokeAsync(arguments, ct);

        Assert.StartsWith($"Updated {task.Id}:", result, StringComparison.Ordinal); // contains-ok: a required phrase inside the long help/tool text; the full text is pinned by the prompt golden.
        Assert.True(harness.Store.Get(task.Id)?.HumanGate);
    }

    /// <summary>The <c>action</c> argument sets the Task's Action.</summary>
    [Fact]
    public async Task UpdateTask_ActionGiven_SetsTheAction()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await BuildAsync(new FixedProofSource(FixedProofSource.Complete), ct);
        TaskItem task = GatedInReview(harness);
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["action"] = "send the reply" };

        string result = await UpdateAsNova(harness).InvokeAsync(arguments, ct);

        Assert.StartsWith($"Updated {task.Id}:", result, StringComparison.Ordinal); // contains-ok: a required phrase inside the long help/tool text; the full text is pinned by the prompt golden.
        Assert.Equal("send the reply", harness.Store.Get(task.Id)?.Action);
    }

    /// <summary>An Agent passing <c>human_gate: false</c> on a gated Task gets the H-1 refusal and nothing changes.</summary>
    [Fact]
    public async Task UpdateTask_AgentTurnsGateOff_ReturnsH1RefusalAndKeepsTheGate()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await BuildAsync(new FixedProofSource(FixedProofSource.Complete), ct);
        TaskItem task = GatedInReview(harness);
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["human_gate"] = false };

        string result = await UpdateAsNova(harness).InvokeAsync(arguments, ct);

        Assert.Equal($"Only You can turn off the Human gate on {task.Id}.", result);
        Assert.True(harness.Store.Get(task.Id)?.HumanGate);
    }

    /// <summary>An Agent clearing the Action with an empty string gets the H-2 refusal and the Action stays.</summary>
    [Fact]
    public async Task UpdateTask_AgentClearsAction_ReturnsH2RefusalAndKeepsTheAction()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await BuildAsync(new FixedProofSource(FixedProofSource.Complete), ct);
        TaskItem task = GatedInReview(harness, "send the reply");
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["action"] = string.Empty };

        string result = await UpdateAsNova(harness).InvokeAsync(arguments, ct);

        Assert.Equal($"Only You can turn off the Human gate on {task.Id}.", result);
        Assert.Equal("send the reply", harness.Store.Get(task.Id)?.Action);
    }

    /// <summary>A refused move to Done on a gated Task returns the H-4 message verbatim and the Task stays put.</summary>
    [Fact]
    public async Task UpdateTask_GatedTaskToDone_ReturnsH4MessageVerbatim()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await BuildAsync(new FixedProofSource(FixedProofSource.Complete), ct);
        TaskItem task = GatedInReview(harness);
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["status"] = "Done" };

        string result = await UpdateAsNova(harness).InvokeAsync(arguments, ct);

        Assert.Equal($"{task.Id} needs You's acceptance. Move it to Needs You when the Proof is complete; you'll be woken when You decides.", result);
        Assert.Equal(TaskState.Review, harness.Store.Get(task.Id)?.Status);
    }

    /// <summary>A refused move to Needs You with an incomplete Proof returns the H-3 message naming the open item.</summary>
    [Fact]
    public async Task UpdateTask_NeedsYouWithIncompleteProof_ReturnsH3Message()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await BuildAsync(new FixedProofSource(FixedProofSource.ThreeOfFour), ct);
        TaskItem task = GatedInReview(harness);
        JsonObject arguments = new() { ["taskId"] = task.Id.ToString(), ["status"] = "Needs You" };

        string result = await UpdateAsNova(harness).InvokeAsync(arguments, ct);

        Assert.Equal($"{task.Id} can go to Needs You once its Proof is complete (3/4: citation check is open).", result);
        Assert.Equal(TaskState.Review, harness.Store.Get(task.Id)?.Status);
    }

    /// <summary>The <c>create_task</c> tool accepts <c>human_gate</c> and <c>action</c> together and saves both.</summary>
    [Fact]
    public async Task CreateTask_HumanGateAndAction_SavesBoth()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await BuildAsync(new FixedProofSource(FixedProofSource.Complete), ct);
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, harness.Sprints, new FakePromptSource(), harness.NovaIdOrThrow());
        JsonObject arguments = new()
        {
            ["title"] = "Write the reply",
            ["team"] = Team,
            ["human_gate"] = true,
            ["action"] = "send the reply",
        };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.StartsWith("Created ", result, StringComparison.Ordinal); // contains-ok: a required phrase inside the long help/tool text; the full text is pinned by the prompt golden.
        TaskItem saved = Assert.Single(harness.Store.All);
        Assert.True(saved.HumanGate);
        Assert.Equal("send the reply", saved.Action);
    }

    /// <summary>Creating a gated Task straight into Needs You is refused by H-5 and nothing is written.</summary>
    [Fact]
    public async Task CreateTask_GatedInNeedsYou_IsRefusedAndWritesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await BuildAsync(new FixedProofSource(FixedProofSource.Complete), ct);
        CreateTaskTool tool = new(harness.Service, harness.Triggers, harness.Directory, harness.Sprints, new FakePromptSource(), harness.NovaIdOrThrow());
        JsonObject arguments = new()
        {
            ["title"] = "Write the reply",
            ["team"] = Team,
            ["status"] = "Needs You",
            ["human_gate"] = true,
        };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.DoesNotContain("Created ", result, StringComparison.Ordinal);
        Assert.Empty(harness.Store.All);
    }

    /// <summary>A gated Task's <c>get_task</c> output carries <c>human_gate: true</c> and its <c>action</c> line.</summary>
    [Fact]
    public async Task GetTask_GatedWithAction_ShowsHumanGateAndAction()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FixedProofSource proof = new(FixedProofSource.Complete);
        using TaskToolHarness harness = await BuildAsync(proof, ct);
        TaskItem task = GatedInReview(harness, "send the reply");

        string result = await GetOver(harness, proof).InvokeAsync(new JsonObject { ["taskId"] = task.Id.ToString() }, ct);

        string[] lines = result.Split('\n');
        Assert.Contains("human_gate: true", lines);
        Assert.Contains("action: send the reply", lines);
    }

    /// <summary>An ungated Task's <c>get_task</c> output has no <c>human_gate</c> or <c>action</c> line.</summary>
    [Fact]
    public async Task GetTask_Ungated_ShowsNoGateOrActionLine()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FixedProofSource proof = new(FixedProofSource.Complete);
        using TaskToolHarness harness = await BuildAsync(proof, ct);
        TaskItem task = harness.CreateTask("Write the reply");

        string result = await GetOver(harness, proof).InvokeAsync(new JsonObject { ["taskId"] = task.Id.ToString() }, ct);

        Assert.DoesNotContain("human_gate:", result, StringComparison.Ordinal);
        Assert.DoesNotContain("\naction:", result, StringComparison.Ordinal);
    }

    /// <summary>The Proof prints as <c>proof: 3/4</c> followed by each open item with its owner.</summary>
    [Fact]
    public async Task GetTask_ProofThreeOfFour_ListsTheOpenItemWithItsOwner()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FixedProofSource proof = new(ThreeOfFourWithOwner());
        using TaskToolHarness harness = await BuildAsync(proof, ct);
        TaskItem task = GatedInReview(harness);

        string result = await GetOver(harness, proof).InvokeAsync(new JsonObject { ["taskId"] = task.Id.ToString() }, ct);

        Assert.Contains("proof: 3/4\n- Citation check (Nova)", result, StringComparison.Ordinal); // contains-ok: a required phrase inside the long help/tool text; the full text is pinned by the prompt golden.
        Assert.DoesNotContain("- Spec read", result, StringComparison.Ordinal);
    }

    /// <summary>An open item with no owner prints its label alone, without parentheses.</summary>
    [Fact]
    public async Task GetTask_OpenItemWithoutOwner_OmitsTheOwner()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FixedProofSource proof = new(FixedProofSource.ThreeOfFour);
        using TaskToolHarness harness = await BuildAsync(proof, ct);
        TaskItem task = GatedInReview(harness);

        string result = await GetOver(harness, proof).InvokeAsync(new JsonObject { ["taskId"] = task.Id.ToString() }, ct);

        string[] lines = result.Split('\n');
        Assert.Contains("proof: 3/4", lines);
        Assert.Contains("- Citation check", lines);
    }

    /// <summary>An open item with an owner and a Detail prints <c>- Label (Owner): Detail</c> (#550).</summary>
    [Fact]
    public async Task GetTask_OpenItemWithOwnerAndDetail_PrintsBoth()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FixedProofSource proof = new(new Proof([new ProofItem("Plan checked", false, "Nova", "no Evidence from a member covering verify", ProofItemSource.Process)]));
        using TaskToolHarness harness = await BuildAsync(proof, ct);
        TaskItem task = GatedInReview(harness);

        string result = await GetOver(harness, proof).InvokeAsync(new JsonObject { ["taskId"] = task.Id.ToString() }, ct);

        Assert.Contains("- Plan checked (Nova): no Evidence from a member covering verify", result.Split('\n'));
    }

    /// <summary>An open item with a Detail and no owner prints <c>- Label: Detail</c> (#550).</summary>
    [Fact]
    public async Task GetTask_OpenItemWithDetailAndNoOwner_PrintsTheDetail()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FixedProofSource proof = new(new Proof([new ProofItem("Plan reviewed", false, null, "no document attached", ProofItemSource.Process)]));
        using TaskToolHarness harness = await BuildAsync(proof, ct);
        TaskItem task = GatedInReview(harness);

        string result = await GetOver(harness, proof).InvokeAsync(new JsonObject { ["taskId"] = task.Id.ToString() }, ct);

        Assert.Contains("- Plan reviewed: no document attached", result.Split('\n'));
    }

    /// <summary>A valid acceptance prints as <c>accepted: &lt;UTC time&gt; by &lt;name&gt;</c>.</summary>
    [Fact]
    public async Task GetTask_ValidAcceptance_ShowsWhenAndBy()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FixedProofSource proof = new(FixedProofSource.Complete);
        using TaskToolHarness harness = await BuildAsync(proof, ct);
        TaskItem task = GatedInNeedsYou(harness, "send the reply");
        _ = Assert.IsType<TaskResult.Saved>(harness.Service.Decide(task.Id, TaskDecision.Accept, null, task.Version, harness.HumanActor()));

        string result = await GetOver(harness, proof).InvokeAsync(new JsonObject { ["taskId"] = task.Id.ToString() }, ct);

        Assert.Contains("accepted: 2026-10-19T12:00:00Z by You", result.Split('\n'));
    }

    /// <summary>An edit after the acceptance prints <c>acceptance: stale</c> and no <c>accepted:</c> line.</summary>
    [Fact]
    public async Task GetTask_EditedAfterAcceptance_ShowsStale()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FixedProofSource proof = new(FixedProofSource.Complete);
        using TaskToolHarness harness = await BuildAsync(proof, ct);
        TaskItem task = GatedInNeedsYou(harness, "send the reply");
        TaskItem accepted = Assert.IsType<TaskResult.Saved>(harness.Service.Decide(task.Id, TaskDecision.Accept, null, task.Version, harness.HumanActor())).Task;
        TaskPatch edit = new() { Description = "A different description" };
        _ = Assert.IsType<TaskResult.Saved>(harness.Service.Update(accepted.Id, edit, null, harness.HumanActor()));

        string result = await GetOver(harness, proof).InvokeAsync(new JsonObject { ["taskId"] = task.Id.ToString() }, ct);

        string[] lines = result.Split('\n');
        Assert.Contains("acceptance: stale", lines);
        Assert.DoesNotContain(lines, static line => line.StartsWith("accepted:", StringComparison.Ordinal));
    }

    /// <summary>A gated Task nobody has accepted prints neither an <c>accepted</c> nor an <c>acceptance</c> line.</summary>
    [Fact]
    public async Task GetTask_NoAcceptance_ShowsNoAcceptanceLine()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FixedProofSource proof = new(FixedProofSource.Complete);
        using TaskToolHarness harness = await BuildAsync(proof, ct);
        TaskItem task = GatedInNeedsYou(harness);

        string result = await GetOver(harness, proof).InvokeAsync(new JsonObject { ["taskId"] = task.Id.ToString() }, ct);

        Assert.DoesNotContain("accepted:", result, StringComparison.Ordinal);
        Assert.DoesNotContain("acceptance:", result, StringComparison.Ordinal);
    }

    /// <summary><c>list_tasks</c> with the status <c>Needs You</c> lists the Tasks in Needs You and no others.</summary>
    [Fact]
    public async Task ListTasks_StatusNeedsYou_ListsOnlyThoseTasks()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await BuildAsync(new FixedProofSource(FixedProofSource.Complete), ct);
        TaskItem waiting = GatedInNeedsYou(harness);
        TaskItem inReview = GatedInReview(harness);
        ListTasksTool tool = new(harness.Store, harness.Directory, harness.Personas, harness.Sprints, new FakePromptSource(), harness.NovaIdOrThrow());
        JsonObject arguments = new() { ["status"] = new JsonArray("Needs You") };

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Contains(waiting.Id.ToString(), result, StringComparison.Ordinal); // contains-ok: a required phrase inside the long help/tool text; the full text is pinned by the prompt golden.
        Assert.DoesNotContain(inReview.Id.ToString(), result, StringComparison.Ordinal);
    }
}

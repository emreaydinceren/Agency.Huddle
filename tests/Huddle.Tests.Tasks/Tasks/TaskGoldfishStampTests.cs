using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.DocHistory;
using Agency.Huddle.App.Goldfish;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Wake;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.Library;
using Agency.Huddle.Tests.Tasks.Wake;
using Agency.Huddle.Tests.Teams;
using Microsoft.Extensions.Logging.Abstractions;
using ProofContext = Agency.Huddle.App.Tasks.HumanGate.ProofContext;
using ProofContextBuilder = Agency.Huddle.App.Tasks.HumanGate.ProofContextBuilder;
using ProofEvaluator = Agency.Huddle.App.Tasks.HumanGate.ProofEvaluator;
using ProofItem = Agency.Huddle.App.Tasks.HumanGate.ProofItem;
using static Agency.Huddle.Tests.Tasks.TaskGateText;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// The goldfish stamp (" · goldfish") on the Task side (goldfish spec §6.8): the Task tools write it only while the caller is
/// possessed, and every reader that compares an Evidence or Timeline author with an Agent Name strips it first.
/// </summary>
public sealed class TaskGoldfishStampTests
{
    private const string StampedNova = "Nova · goldfish";
    private const string StampedQuinn = "Quinn · goldfish";

    private static readonly DateTimeOffset Noon = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly TaskActor Nova = new(TaskActorKind.Agent, "Nova", "nova-id");

    /// <summary>The caller's id and the stamped name resolve to the same Actor, with the id unchanged, while the caller is possessed.</summary>
    [Fact]
    public async Task ResolveActorAsync_Possessed_StampsTheNameAndKeepsTheUserId()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        string novaId = harness.NovaId ?? throw new InvalidOperationException("No Nova.");

        (TaskActor? actor, string refusal) = await TaskToolText.ResolveActorAsync(harness.Directory, novaId, ct, goldfish: GuardFor(harness, novaId, possessed: true));

        Assert.Equal(string.Empty, refusal);
        Assert.Equal(new TaskActor(TaskActorKind.Agent, StampedNova, novaId), actor);
    }

    /// <summary>A caller whose Turn is not possessed, and a caller with no guard at all, keep their plain name.</summary>
    [Fact]
    public async Task ResolveActorAsync_NotPossessedOrNoGuard_KeepsThePlainName()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        string novaId = harness.NovaId ?? throw new InvalidOperationException("No Nova.");

        (TaskActor? unpossessed, string _) = await TaskToolText.ResolveActorAsync(harness.Directory, novaId, ct, goldfish: GuardFor(harness, novaId, possessed: false));
        (TaskActor? unguarded, _) = await TaskToolText.ResolveActorAsync(harness.Directory, novaId, ct);

        Assert.Equal("Nova", unpossessed?.Name);
        Assert.Equal("Nova", unguarded?.Name);
    }

    /// <summary><c>add_comment</c> from a possessed Agent is written under the stamped name.</summary>
    [Fact]
    public async Task AddCommentTool_Possessed_WritesTheStampedAuthor()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, new ManualTimeProvider(Noon));
        string novaId = harness.NovaId ?? throw new InvalidOperationException("No Nova.");
        TaskItem task = harness.CreateTask("Fix login");
        AddCommentTool tool = new(harness.Service, harness.Store, harness.Triggers, harness.Directory, novaId, new FakePromptSource(), goldfish: GuardFor(harness, novaId, possessed: true));

        _ = await tool.InvokeAsync(new JsonObject { ["taskId"] = task.Id.ToString(), ["text"] = "Found it." }, ct);

        Assert.Equal(StampedNova, Assert.Single(Stored(harness, task).Comments).Author);
    }

    /// <summary>
    /// A possessed Agent's <c>add_comment</c> reply leaves the Agent itself out of the names it wakes, though the author is written as "Nova · goldfish":
    /// the stamp is stripped before the author is compared with a Mention, so <c>@Nova</c> is a self-mention and <c>@Kai</c> is the one woken.
    /// </summary>
    [Fact]
    public async Task AddCommentTool_PossessedSelfMention_RepliesWithoutTheCaller()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, new ManualTimeProvider(Noon));
        string novaId = harness.NovaId ?? throw new InvalidOperationException("No Nova.");
        TaskItem task = harness.CreateTask("Fix login");
        AddCommentTool tool = new(harness.Service, harness.Store, harness.Triggers, harness.Directory, novaId, new FakePromptSource(), goldfish: GuardFor(harness, novaId, possessed: true));

        string result = await tool.InvokeAsync(new JsonObject { ["taskId"] = task.Id.ToString(), ["text"] = "@Nova and @Kai, found it." }, ct);

        Assert.Equal($"Added a comment to {task.Id}. Wakes Kai.", result);
        Assert.Equal(StampedNova, Assert.Single(Stored(harness, task).Comments).Author);
    }

    /// <summary><c>add_comment</c> from an Agent that is not possessed is written under the plain name, guard or no guard.</summary>
    [Fact]
    public async Task AddCommentTool_NotPossessed_WritesThePlainAuthor()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, new ManualTimeProvider(Noon));
        string novaId = harness.NovaId ?? throw new InvalidOperationException("No Nova.");
        TaskItem task = harness.CreateTask("Fix login");
        AddCommentTool tool = new(harness.Service, harness.Store, harness.Triggers, harness.Directory, novaId, new FakePromptSource(), goldfish: GuardFor(harness, novaId, possessed: false));

        _ = await tool.InvokeAsync(new JsonObject { ["taskId"] = task.Id.ToString(), ["text"] = "Found it." }, ct);

        Assert.Equal("Nova", Assert.Single(Stored(harness, task).Comments).Author);
    }

    /// <summary><c>add_evidence</c> from a possessed Agent is written under the stamped name, which is how Evidence shows who ran it.</summary>
    [Fact]
    public async Task AddEvidenceTool_Possessed_WritesTheStampedAuthor()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, new ManualTimeProvider(Noon));
        string novaId = harness.NovaId ?? throw new InvalidOperationException("No Nova.");
        TaskItem task = harness.CreateTask("Fix login");
        AddEvidenceTool tool = new(
            harness.Service, harness.Store, new ToolCallLogWriter(harness.Options), harness.Directory, harness.Clock, novaId, new FakePromptSource(),
            goldfish: GuardFor(harness, novaId, possessed: true));

        _ = await tool.InvokeAsync(new JsonObject { ["taskId"] = task.Id.ToString(), ["note"] = "Checked on staging." }, ct);

        Assert.Equal(StampedNova, Assert.Single(Stored(harness, task).Evidence).Author);
    }

    /// <summary>Proof's <c>evidence from verify</c> counts a goldfish's Evidence as the Agent's own, because the stamp is stripped before the coverage lookup.</summary>
    [Fact]
    public void ProofEvaluator_EvidenceFromStep_CountsAGoldfishsEvidenceAsTheAgents()
    {
        Dictionary<string, string> covers = new(StringComparer.Ordinal) { ["Quinn"] = "verify, review" };
        DoneItem item = new("Item", new DoneRule.EvidenceFrom("verify"), ProofItemSource.Process);
        EvidenceLine line = new(Noon, StampedQuinn, "dotnet test", 0, null, null, $"{StampedQuinn} | dotnet test");
        ProofContext context = new([item], [], false, [line], covers, ["Quinn"], [], []);

        ProofItem result = Assert.Single(ProofEvaluator.Evaluate(TestTasks.Make(assignee: "Dev"), context).Items);

        Assert.True(result.Met);
    }

    /// <summary>The Proof context's coverage map is keyed by the Agent's Name even when the only Evidence is a goldfish's.</summary>
    [Fact]
    public void ProofContextBuilder_GoldfishEvidenceAuthor_GetsTheAgentsCoverage()
    {
        using TempDataDir dir = new();
        using TaskToolHarness harness = new();
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();
        _ = harness.Personas.Add(new PersonaIdentity("Quinn", "QA Engineer", "quinn", [Team], Role: new RoleRef("web", "qa", "1.2.0")), "You verify.");
        File.WriteAllText(Path.Combine(dir.Path, "AGENTS.md"), "# Flow\n\n## Definition of Done\n\n- Plan reviewed: evidence from verify\n");
        ProcessGateService gates = BuildGates(dir, "---\nteamType: 'web@1.2.0'\ngates: ['qa-evidence-before-done']\n---\n");
        FakeTeamCatalog catalog = new() { Teams = [new TeamSummary(Team, [], ["Quinn"], true) { TeamType = new VersionedRef("web", "1.2.0") }] };
        ProofContextBuilder builder = new(
            harness.Store, harness.Personas, gates, catalog, library.Resolver, new DocHistoryStore(library.BoundOptions, NullLogger<DocHistoryStore>.Instance));
        TaskItem task = Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft("Parent", Team, null), Nova)).Task;
        TaskActor goldfish = new(TaskActorKind.Agent, StampedQuinn, "quinn-id");
        _ = Assert.IsType<TaskResult.Saved>(harness.Service.AddEvidence(task.Id, new EvidenceLine(Noon, StampedQuinn, "dotnet test", 0, null, null, "raw"), goldfish));

        ProofContext context = builder.Build(harness.Store.Get(task.Id) ?? throw new InvalidOperationException("Missing Task."));

        Assert.Equal("verify", context.Coverage["Quinn"]);
    }

    /// <summary>A manager acting as a goldfish is still the actor, so the wake rule does not wake the manager about their own change.</summary>
    [Fact]
    public void ManagerWakeRule_ManagerActsAsGoldfish_Skips()
    {
        using WakeRuleKit kit = new();
        kit.AddPersonas("Nova", "Kai");
        kit.SetTeams(("Platform", "Nova", ["Nova", "Kai"]));

        TaskWakeChoice choice = kit.ManagerRule().Choose(WakeRuleKit.Round(TestTasks.Make(assignee: "Kai"), actorName: StampedNova));

        Assert.Equal(new TaskWakeChoice.Skip(null), choice);
    }

    /// <summary>The Done gate counts a goldfish verifier's Evidence: <c>ProcessGateService</c> looks the stripped author up in the coverage map.</summary>
    [Fact]
    public void ProcessGateService_DoneWithGoldfishVerifierEvidence_IsAllowed()
    {
        using TempDataDir dir = new();
        ProcessGateService service = BuildGates(dir, Process(QaGate));
        Dictionary<string, string> covers = new(StringComparer.Ordinal) { ["Quinn"] = "verify, review" };
        EvidenceLine line = new(Noon, StampedQuinn, "dotnet test", 0, null, null, string.Empty);

        GateRefusal? refusal = service.CheckTaskChange(new GateContext(Team, new TaskId("TASK", 1), "Review", "Done", false, true, [line], covers, null));

        Assert.Null(refusal);
    }

    /// <summary>The same Done move through <c>TaskService</c> saves when the verifier's Evidence was written as a goldfish: the service finds the Persona behind the stripped author.</summary>
    [Fact]
    public void TaskServiceUpdate_DoneWithGoldfishVerifierEvidence_Saves()
    {
        using TempDataDir gateDir = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(QaGate)));
        _ = harness.Personas.Add(new PersonaIdentity("Quinn", "QA Engineer", "quinn", [Team], Role: new RoleRef("web", "qa", "1.2.0")), "You are Quinn.");
        TaskItem task = Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft("Fix login", Team, null), Nova)).Task;
        TaskActor goldfish = new(TaskActorKind.Agent, StampedQuinn, "quinn-id");
        _ = Assert.IsType<TaskResult.Saved>(harness.Service.AddEvidence(task.Id, new EvidenceLine(Noon, StampedQuinn, "dotnet test", 0, null, null, string.Empty), goldfish));

        TaskResult result = harness.Service.Update(task.Id, new TaskPatch { Status = TaskState.Done }, null, Nova);

        Assert.Equal(TaskState.Done, Assert.IsType<TaskResult.Saved>(result).Task.Status);
    }

    /// <summary>A stamped Evidence author survives the Task file: the " · goldfish" suffix is written and read back whole.</summary>
    [Fact]
    public void TimelineAuthor_WithTheStamp_RoundTripsThroughTheTaskFile()
    {
        using TaskToolHarness harness = new();
        TaskItem task = Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft("Fix login", Team, null), Nova)).Task;
        TaskActor goldfish = new(TaskActorKind.Agent, StampedNova, "nova-id");
        _ = Assert.IsType<TaskResult.Saved>(harness.Service.AddEvidence(task.Id, new EvidenceLine(Noon, StampedNova, "dotnet test", 0, null, null, string.Empty), goldfish));
        TaskItem stored = harness.Store.Get(task.Id) ?? throw new InvalidOperationException("Missing Task.");

        IReadOnlyList<EvidenceLine> evidence = TaskFileFormat.ReadEvidence(File.ReadAllText(stored.Path));

        Assert.Equal(StampedNova, Assert.Single(evidence).Author);
    }

    /// <summary>The Task as stored now.</summary>
    private static TaskItem Stored(TaskToolHarness harness, TaskItem task) =>
        harness.Store.Get(task.Id) ?? throw new InvalidOperationException("Missing Task.");

    /// <summary>A guard over a Turn of <paramref name="agentId"/> in one Room, whose possession is open or not.</summary>
    private static GoldfishGuard GuardFor(TaskToolHarness harness, string agentId, bool possessed)
    {
        harness.TurnActivity.Begin(agentId, "room-1");
        return new GoldfishGuard(new FixedPossession(possessed ? "p1" : null), harness.TurnActivity, new FakePromptSource());
    }

    /// <summary>A possession lookup that answers the same id for every Agent and Room.</summary>
    private sealed class FixedPossession(string? possessionId) : IPossessionLookup
    {
        /// <inheritdoc />
        public string? PossessionOf(string agentId, string roomId) => possessionId;
    }
}

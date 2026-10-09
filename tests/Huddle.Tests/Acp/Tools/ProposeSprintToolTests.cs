using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Tasks;
using Agency.Huddle.Tests.Teams;
using Agency.Huddle.Tests.Ui.Teams;

namespace Agency.Huddle.Tests.Acp.Tools;

/// <summary>
/// Tests for <see cref="ProposeSprintTool"/> (Task 62, PL-4): a manager proposes a Sprint, Huddle stores the proposal
/// and never creates the Sprint (ADR-0037).
/// </summary>
public sealed class ProposeSprintToolTests
{
    private const string TwoSprints = "# Sprints\n\n- S1 | Sprint 1\n- S2 | Auth hardening\n";

    /// <summary>The Platform Team with Nova as its manager.</summary>
    private static FakeTeamCatalog PlatformCatalog() => new()
    {
        Teams = [new TeamSummary("Platform", [], ["Nova", "Kai"], true) { Manager = "Nova" }],
    };

    /// <summary>Writes a Task straight into the store.</summary>
    private static TaskItem Put(TaskToolHarness harness, TaskItem task)
    {
        TaskItem toWrite = task with { Path = TaskLayout.PathFor(harness.Store.RootDirectory, task.Location, task.Id) };
        return harness.Store.Create(toWrite, TaskFileFormat.Compose(toWrite))
            ?? throw new InvalidOperationException("Fixture collided with an existing file.");
    }

    /// <summary>Builds the tool over the harness, calling as the given caller.</summary>
    private static ProposeSprintTool CreateTool(TaskToolHarness harness, SprintProposalStore store, string? callerId) =>
        new(harness.Store, harness.Sprints, store, PlatformCatalog(), harness.Directory, callerId ?? throw new InvalidOperationException("Harness not seeded."));

    /// <summary>Builds the proposal store over a folder inside the harness's data folder.</summary>
    private static SprintProposalStore CreateStore(TaskToolHarness harness) =>
        new(new DiskTeamFolders(Path.Combine(harness.Options.Value.DataDir, "platform-folder")));

    /// <summary>Builds the call arguments.</summary>
    private static JsonObject Arguments(string name, params string[] taskIds) => new()
    {
        ["name"] = name,
        ["taskIds"] = new JsonArray([.. taskIds.Select(static id => (JsonNode?)JsonValue.Create(id))]),
    };

    /// <summary>A valid proposal from the manager is stored, the caller is told so, and no Sprint is created.</summary>
    [Fact]
    public async Task InvokeAsync_ValidProposal_StoresItAndCreatesNoSprint()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, sprintsText: TwoSprints);
        _ = Put(harness, TestTasks.Make(id: "PLAT-0001"));
        _ = Put(harness, TestTasks.Make(id: "PLAT-0002"));
        SprintProposalStore store = CreateStore(harness);
        ProposeSprintTool tool = CreateTool(harness, store, harness.NovaId);

        string result = await tool.InvokeAsync(Arguments("Launch", "PLAT-0001", "PLAT-0002"), ct);

        Assert.Equal("Proposal sent to the Human.", result);
        SprintProposal proposal = Assert.Single(store.Pending("Platform"));
        Assert.Equal("Launch", proposal.Name);
        Assert.Equal(["PLAT-0001", "PLAT-0002"], proposal.TaskIds);
        Assert.Equal("Nova", proposal.Proposer);
        Assert.Equal("Platform", proposal.Team);
        Assert.Equal(["Sprint 1", "Auth hardening"], harness.Sprints.All.Select(static sprint => sprint.Name));
    }

    /// <summary>A stored proposal survives a restart: a new store over the same folder reads it back.</summary>
    [Fact]
    public async Task InvokeAsync_ValidProposal_SurvivesARestart()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = Put(harness, TestTasks.Make(id: "PLAT-0001"));
        ProposeSprintTool tool = CreateTool(harness, CreateStore(harness), harness.NovaId);
        _ = await tool.InvokeAsync(Arguments("Launch", "PLAT-0001"), ct);

        SprintProposalStore restarted = CreateStore(harness);

        SprintProposal proposal = Assert.Single(restarted.Pending("Platform"));
        Assert.Equal("Launch", proposal.Name);
        Assert.Same(proposal, restarted.Get(proposal.Id));
    }

    /// <summary>A Persona that is not the Team's manager is refused and nothing is stored.</summary>
    [Fact]
    public async Task InvokeAsync_CallerIsNotManager_Refused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = Put(harness, TestTasks.Make(id: "PLAT-0001"));
        SprintProposalStore store = CreateStore(harness);
        ProposeSprintTool tool = CreateTool(harness, store, harness.KaiId);

        string result = await tool.InvokeAsync(Arguments("Launch", "PLAT-0001"), ct);

        Assert.Equal("Only the manager of a Team can propose a Sprint.", result);
        Assert.Empty(store.Pending("Platform"));
    }

    /// <summary>A Task id that does not exist is refused with the id named.</summary>
    [Fact]
    public async Task InvokeAsync_UnknownTask_RefusedNamingTheId()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = Put(harness, TestTasks.Make(id: "PLAT-0001"));
        SprintProposalStore store = CreateStore(harness);
        ProposeSprintTool tool = CreateTool(harness, store, harness.NovaId);

        string result = await tool.InvokeAsync(Arguments("Launch", "PLAT-0001", "PLAT-0999"), ct);

        Assert.Equal("Unknown task 'PLAT-0999'.", result);
        Assert.Empty(store.Pending("Platform"));
    }

    /// <summary>A Task that belongs to another Team is refused with the id named.</summary>
    [Fact]
    public async Task InvokeAsync_TaskOfAnotherTeam_RefusedNamingTheId()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = Put(harness, TestTasks.Make(id: "OPS-0007", location: new TaskLocation("Ops", null, false)));
        SprintProposalStore store = CreateStore(harness);
        ProposeSprintTool tool = CreateTool(harness, store, harness.NovaId);

        string result = await tool.InvokeAsync(Arguments("Launch", "OPS-0007"), ct);

        Assert.Equal("Task OPS-0007 belongs to Team Ops, not Team Platform.", result);
        Assert.Empty(store.Pending("Platform"));
    }

    /// <summary>A Done Task is refused with the id named.</summary>
    [Fact]
    public async Task InvokeAsync_DoneTask_RefusedNamingTheId()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = Put(harness, TestTasks.Make(id: "PLAT-0003", status: TaskState.Done));
        SprintProposalStore store = CreateStore(harness);
        ProposeSprintTool tool = CreateTool(harness, store, harness.NovaId);

        string result = await tool.InvokeAsync(Arguments("Launch", "PLAT-0003"), ct);

        Assert.Equal("Task PLAT-0003 is already Done.", result);
        Assert.Empty(store.Pending("Platform"));
    }

    /// <summary>A name that belongs to an older Sprint is refused; the current Sprint's name is accepted.</summary>
    [Fact]
    public async Task InvokeAsync_ExistingSprintName_RefusedUnlessItIsTheCurrentOne()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, sprintsText: TwoSprints);
        _ = Put(harness, TestTasks.Make(id: "PLAT-0001"));
        SprintProposalStore store = CreateStore(harness);
        ProposeSprintTool tool = CreateTool(harness, store, harness.NovaId);

        string refused = await tool.InvokeAsync(Arguments("sprint 1", "PLAT-0001"), ct);
        string accepted = await tool.InvokeAsync(Arguments("Auth hardening", "PLAT-0001"), ct);

        Assert.Equal("A Sprint named 'sprint 1' already exists.", refused);
        Assert.Equal("Proposal sent to the Human.", accepted);
        Assert.Equal("Auth hardening", Assert.Single(store.Pending("Platform")).Name);
    }

    /// <summary>A name that is already waiting as a proposal is refused.</summary>
    [Fact]
    public async Task InvokeAsync_NameAlreadyProposed_Refused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = Put(harness, TestTasks.Make(id: "PLAT-0001"));
        SprintProposalStore store = CreateStore(harness);
        ProposeSprintTool tool = CreateTool(harness, store, harness.NovaId);
        _ = await tool.InvokeAsync(Arguments("Launch", "PLAT-0001"), ct);

        string result = await tool.InvokeAsync(Arguments("launch", "PLAT-0001"), ct);

        Assert.Equal("A proposal named 'launch' is already waiting for the Human.", result);
        _ = Assert.Single(store.Pending("Platform"));
    }

    /// <summary>When the Team's folder cannot be used the proposal is not kept and the caller is told.</summary>
    [Fact]
    public async Task InvokeAsync_TeamFolderUnavailable_ReportsItCouldNotBeSaved()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = Put(harness, TestTasks.Make(id: "PLAT-0001"));
        FakeTeamFolders folders = new() { TeamResult = LibraryResult<LibraryPath>.Fail("no folder") };
        SprintProposalStore store = new(folders);
        ProposeSprintTool tool = CreateTool(harness, store, harness.NovaId);

        string result = await tool.InvokeAsync(Arguments("Launch", "PLAT-0001"), ct);

        Assert.Equal("The proposal could not be saved; try again.", result);
        Assert.Empty(store.Pending("Platform"));
    }

    /// <summary>An empty name and an empty Task list are refused.</summary>
    [Fact]
    public async Task InvokeAsync_MissingNameOrTasks_Refused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = Put(harness, TestTasks.Make(id: "PLAT-0001"));
        SprintProposalStore store = CreateStore(harness);
        ProposeSprintTool tool = CreateTool(harness, store, harness.NovaId);

        string noName = await tool.InvokeAsync(Arguments("  ", "PLAT-0001"), ct);
        string noTasks = await tool.InvokeAsync(Arguments("Launch"), ct);

        Assert.Equal("Give the Sprint a name in 'name'.", noName);
        Assert.Equal("Give at least one task id in 'taskIds'.", noTasks);
        Assert.Empty(store.Pending("Platform"));
    }
}

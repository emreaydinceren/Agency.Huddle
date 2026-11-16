using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.Hosting;
using Agency.Huddle.Acp.Tests.Fakes;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Ui;
using Agency.Huddle.Tests.Waiting;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// Task 65 (Phase 3 end-to-end, Gate 3): hire, start, gate, sprint, wake and restart flows walked together
/// through the real host (<see cref="TeamWebApplicationFactory"/>) with a mock ACP adapter as the only adapter.
/// Each test builds its own host on a fresh factory, creates a Development Team with its manager, developer, reviewer and QA engineer and
/// starts it through <see cref="HireService"/>, then follows one flow. The mock adapter is the in-memory
/// <see cref="FakeAgentProcessLauncher"/>, one per launched host; <c>Team:Teams:Tiers:claude</c> is set because
/// <see cref="HireContext"/> counts every adapter profile as answered with only the models configured there.
/// </summary>
public sealed class TeamsPhase3EndToEndTests
{
    private const string TeamName = "Phoenix";

    private const string TypeId = "development";

    private static readonly string[] StartRoles = ["development-manager", "developer", "reviewer", "qa-engineer"];

    private const string QaGateId = "qa-evidence-before-done";

    private const string SprintName = "Sprint 1";

    private const string DeveloperRole = "developer";

    private const string QaRole = "qa-engineer";

    private const string ReviewerRole = "reviewer";

    private const string CheckCommand = "dotnet test";

    /// <summary>Flow 1: Create and Start a Small Development Team: onboarding waits for the manager's host only, the Team files and role files exist, and the onboarding turn is posted once.</summary>
    [Fact]
    public async Task CreateAndStart_FilesExist_OnboardingPostedOnceToTheManager()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir workspace = NewWorkspace();
        await using TeamWebApplicationFactory root = new();
        await using Run run = Run.Open(root);

        TeamCast cast = await CreateAndStartAsync(run, workspace.Path, ct);

        Assert.Equal([cast.Manager.Name], run.Gate.Waited);
        Assert.Equal([true], run.Gate.Results);
        string teamFolder = Path.Combine(root.TasksDirPath, TeamName);
        Assert.True(File.Exists(Path.Combine(teamFolder, TeamPaths.TeamFileName)));
        Assert.True(File.Exists(Path.Combine(teamFolder, TeamPaths.AgentsMdFileName)));
        string[] roleFiles = [.. Directory.GetFiles(root.AgentsDirPath, "*.md", SearchOption.AllDirectories).Where(static path => File.ReadAllText(path).Contains("_role:", StringComparison.Ordinal))];
        Assert.Equal(cast.Cards.Count, roleFiles.Length);
        Assert.True(run.Services.GetRequiredService<ITeamFolders>().ReadTeamFile(TeamName)?.Onboarded);
        MessagePostedEvent[] onboarding = [.. run.Posts.Where(static post => string.Equals(post.Message.SenderId, KnownIds.Human, StringComparison.Ordinal))];
        MessagePostedEvent posted = Assert.Single(onboarding);
        string[] memberNames = [.. posted.Members.Select(static member => member.Name).Order(StringComparer.Ordinal)];
        Assert.Equal([cast.Manager.Name, "You"], memberNames);
        await Eventually.UntilAsync(() => run.Launcher.Launches >= cast.Cards.Count, ct, timeout: TestBudgets.Slowest, interval: TimeSpan.FromMilliseconds(50));
        Assert.True(run.Launcher.Launches >= cast.Cards.Count);
    }

    /// <summary>Flow 2: a developer's move to Done is refused naming the QA gate, the QA member's Evidence lets it through, and the Human override is logged.</summary>
    [Fact]
    public async Task QaGate_RefusesDone_UntilQaEvidence_AndHumanOverrideIsLogged()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir workspace = NewWorkspace();
        await using TeamWebApplicationFactory root = new();
        await using Run run = Run.Open(root);
        TeamCast cast = await CreateAndStartAsync(run, workspace.Path, ct);
        TaskService service = run.Services.GetRequiredService<TaskService>();
        TaskStore store = run.Services.GetRequiredService<TaskStore>();
        TaskActor human = TaskActors.Human(run.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<TeamOptions>>().Value);
        TaskActor developer = await cast.ActorAsync(run, DeveloperRole, ct);
        TaskActor qa = await cast.ActorAsync(run, QaRole, ct);
        TaskItem task = Assert.IsType<TaskResult.Saved>(service.Create(new TaskDraft("Fix login", TeamName, null, TaskState.InProgress, Assignee: developer.Name), human)).Task;
        TaskPatch done = new() { Status = TaskState.Done };

        TaskResult refused = service.Update(task.Id, done, null, developer);
        TaskResult added = service.AddEvidence(task.Id, new EvidenceLine(DateTimeOffset.UtcNow, qa.Name, CheckCommand, 0, null, null, string.Empty), qa);
        TaskResult accepted = service.Update(task.Id, done, null, developer);
        TaskItem second = Assert.IsType<TaskResult.Saved>(service.Create(new TaskDraft("Fix logout", TeamName, null, TaskState.InProgress, Assignee: developer.Name), human)).Task;
        TaskResult overridden = service.Update(second.Id, done, null, human, humanOverridesGates: true);

        TaskResult.GateRefused gate = Assert.IsType<TaskResult.GateRefused>(refused);
        Assert.Equal(GateId.QaEvidenceBeforeDone, gate.Gate);
        Assert.Equal(QaGateId, ProcessDocument.ToId(gate.Gate));
        Assert.IsType<TaskResult.Saved>(added);
        Assert.IsType<TaskResult.Saved>(accepted);
        Assert.Equal(TaskState.Done, store.Get(task.Id)?.Status);
        Assert.IsType<TaskResult.Saved>(overridden);
        TaskItem? overriddenTask = store.Get(second.Id);
        Assert.Equal(TaskState.Done, overriddenTask?.Status);
        Assert.Single(overriddenTask?.ChangeLog.Where(static entry => entry.Summary.EndsWith("(gate overridden by Human: qa-evidence-before-done)", StringComparison.Ordinal)) ?? []);
    }

    /// <summary>Flow 3: the developer's <c>git push</c> permission request is refused for the checks gate until a passing check Evidence exists on an open Team Task, and then still refused because every push waits for the Human's Accept on a Task whose Action names a push (issue #602).</summary>
    [Fact]
    public async Task GitPush_IsRefused_ForChecksThenForTheAccept()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir workspace = NewWorkspace();
        await using TeamWebApplicationFactory root = new();
        await using Run run = Run.Open(root);
        TeamCast cast = await CreateAndStartAsync(run, workspace.Path, ct);
        TaskService service = run.Services.GetRequiredService<TaskService>();
        TaskActor human = TaskActors.Human(run.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<TeamOptions>>().Value);
        TaskActor developer = await cast.ActorAsync(run, DeveloperRole, ct);
        WorkDirPermissionHandler handler = new(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WorkDirPermissionHandler>.Instance,
            [new ProtectedDirectoryGuard(Path.Combine(Path.GetTempPath(), "huddle-phase3-protected")), new MergeGateGuard(run.Services.GetRequiredService<IMergeGate>())],
            developer.Name);
        PermissionRequestContext push = new(
            "session-1",
            new ToolCallInfo("call-1", "Bash", ToolKind.Execute, ToolCallStatus.Pending, """{"command":"git push origin feat/ship"}"""),
            [
                new PermissionOptionInfo("allow-1", "Allow once", PermissionOptionKind.AllowOnce),
                new PermissionOptionInfo("reject-1", "Reject once", PermissionOptionKind.RejectOnce),
            ]);
        TaskItem task = Assert.IsType<TaskResult.Saved>(service.Create(new TaskDraft("Ship it", TeamName, null, TaskState.InProgress, Assignee: developer.Name), human)).Task;

        IMergeGate gate = run.Services.GetRequiredService<IMergeGate>();
        PermissionDecision before = await handler.DecideAsync(push, ct);
        string? beforeText = gate.RefusalFor(developer.Name, "git push origin feat/ship");
        TaskActor qa = await cast.ActorAsync(run, QaRole, ct);
        _ = service.AddEvidence(task.Id, new EvidenceLine(DateTimeOffset.UtcNow, qa.Name, CheckCommand, 0, null, null, string.Empty), qa);
        PermissionDecision after = await handler.DecideAsync(push, ct);
        string? afterText = gate.RefusalFor(developer.Name, "git push origin feat/ship");

        Assert.Equal("reject-1", Assert.IsType<SelectedDecision>(before).OptionId);
        Assert.Equal("reject-1", Assert.IsType<SelectedDecision>(after).OptionId);
        Assert.StartsWith("Gate checks-before-merge:", beforeText, StringComparison.Ordinal);
        Assert.StartsWith("Cannot push: a push leaves the Team's repository and waits for the Human's Accept", afterText, StringComparison.Ordinal); // contains-ok: the full wording is pinned in ProcessMergeGateTests.
    }

    /// <summary>Flow 4: the manager's <c>propose_sprint</c> call only queues a proposal; the Human's approval creates the Sprint and puts the Task in it.</summary>
    [Fact]
    public async Task ProposeSprint_CreatesNothingUntilApproved()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir workspace = NewWorkspace();
        await using TeamWebApplicationFactory root = new();
        await using Run run = Run.Open(root);
        TeamCast cast = await CreateAndStartAsync(run, workspace.Path, ct);
        TaskService service = run.Services.GetRequiredService<TaskService>();
        TaskStore store = run.Services.GetRequiredService<TaskStore>();
        ISprintCatalog sprints = run.Services.GetRequiredService<ISprintCatalog>();
        SprintProposalStore proposals = run.Services.GetRequiredService<SprintProposalStore>();
        ITeamDirectory directory = run.Services.GetRequiredService<ITeamDirectory>();
        TaskActor human = TaskActors.Human(run.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<TeamOptions>>().Value);
        TaskActor manager = await cast.ActorAsync(run, "development-manager", ct);
        Room room = await directory.CreateRoomAsync("Planning", [KnownIds.Human, manager.UserId ?? string.Empty], ct);
        TaskItem task = Assert.IsType<TaskResult.Saved>(service.Create(new TaskDraft("Plan me", TeamName, null), human)).Task;
        ProposeSprintTool tool = new(store, sprints, proposals, run.Services.GetRequiredService<ITeamCatalog>(), directory, manager.UserId ?? string.Empty, run.Services.GetRequiredService<Agency.Huddle.App.Prompts.IPromptSource>());

        string reply = await tool.InvokeAsync(new JsonObject { ["name"] = SprintName, ["taskIds"] = new JsonArray(task.Id.ToString()) }, ct);
        Assert.Equal("Proposal sent to the Human.", reply);
        SprintProposal proposal = Assert.Single(proposals.Pending(TeamName));
        Sprint? beforeApproval = sprints.FindByName(SprintName);
        SprintProposalOutcome outcome = await run.Services.GetRequiredService<SprintProposalService>().ApproveAsync(room.Id, proposal.Id, ct);

        Assert.Equal("Proposal sent to the Human.", reply);
        Assert.Null(beforeApproval);
        Assert.Equal(SprintProposalOutcomeKind.Approved, outcome.Kind);
        Sprint? created = sprints.FindByName(SprintName);
        Assert.NotNull(created);
        Assert.Equal(created.Id, store.Get(task.Id)?.Sprint);
        Assert.Empty(proposals.Pending(TeamName));
    }

    /// <summary>Flow 5: a Task change wakes the manager, and finishing the last blocker wakes the blocked Task's assignee.</summary>
    [Fact]
    public async Task TaskChange_WakesManager_AndFinishingLastBlockerWakesAssignee()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir workspace = NewWorkspace();
        await using TeamWebApplicationFactory root = new();
        await using Run run = Run.Open(root);
        TeamCast cast = await CreateAndStartAsync(run, workspace.Path, ct);
        TaskService service = run.Services.GetRequiredService<TaskService>();
        ITeamDirectory directory = run.Services.GetRequiredService<ITeamDirectory>();
        TaskActor human = TaskActors.Human(run.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<TeamOptions>>().Value);
        TaskActor manager = await cast.ActorAsync(run, "development-manager", ct);
        TaskActor developer = await cast.ActorAsync(run, DeveloperRole, ct);
        TaskActor reviewer = await cast.ActorAsync(run, ReviewerRole, ct);
        Room room = await directory.CreateRoomAsync("Everyone", [KnownIds.Human, manager.UserId ?? string.Empty, developer.UserId ?? string.Empty, reviewer.UserId ?? string.Empty], ct);
        TaskItem blocker = Assert.IsType<TaskResult.Saved>(service.Create(new TaskDraft("Blocker", TeamName, null, TaskState.ToDo, Assignee: reviewer.Name, OriginRoomId: room.Id), human)).Task;
        TaskItem blocked = Assert.IsType<TaskResult.Saved>(service.Create(new TaskDraft("Blocked", TeamName, null, TaskState.ToDo, Assignee: developer.Name, OriginRoomId: room.Id, BlockedBy: [blocker.Id]), human)).Task;

        _ = service.Update(blocker.Id, new TaskPatch { Status = TaskState.InProgress }, null, developer);
        await Eventually.UntilAsync(() => run.WakesOf(manager.Name, developer.Name) >= 1, ct, timeout: TestBudgets.Slowest, interval: TimeSpan.FromMilliseconds(50));
        int developerWakesBefore = run.WakesOf(developer.Name, null);
        _ = service.Update(blocker.Id, new TaskPatch { Status = TaskState.Done }, null, human, humanOverridesGates: true);
        await Eventually.UntilAsync(() => run.WakesOf(developer.Name, null) > developerWakesBefore, ct, timeout: TestBudgets.Slowest, interval: TimeSpan.FromMilliseconds(50));

        Assert.Equal(1, run.WakesOf(manager.Name, developer.Name));
        Assert.Equal(TaskState.Done, run.Services.GetRequiredService<TaskStore>().Get(blocker.Id)?.Status);
        Assert.Equal(developerWakesBefore + 1, run.WakesOf(developer.Name, null));
        Assert.Equal(blocked.Assignee, developer.Name);
    }

    /// <summary>Flow 6 (NF-8): a new host on the same data directory still has the Team, its hires, the seeded spend and the cadence state.</summary>
    [Fact]
    public async Task Restart_KeepsTeamHiresSpendAndCadenceState()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir workspace = NewWorkspace();
        await using TeamWebApplicationFactory root = new();
        CadenceState seeded = new(TeamName, new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.Zero), 0);
        TeamCast cast;
        string[] namesBefore;
        await using (Run first = Run.Open(root))
        {
            cast = await CreateAndStartAsync(first, workspace.Path, ct);
            first.Services.GetRequiredService<PersonaSpend>().Add(cast.Manager.Name, "session-1", 1.25m, "USD");
            first.Services.GetRequiredService<CadenceStateStore>().Set(seeded);
            namesBefore = [.. first.Services.GetRequiredService<PersonaStore>().ListNames().Order(StringComparer.Ordinal)];
        }

        await using Run second = Run.Open(root);
        ITeamCatalog catalog = second.Services.GetRequiredService<ITeamCatalog>();
        await Eventually.UntilAsync(() => catalog.Find(TeamName)?.Members.Count == cast.Cards.Count, ct, timeout: TestBudgets.Slowest, interval: TimeSpan.FromMilliseconds(50));

        TeamSummary? team = catalog.Find(TeamName);
        Assert.Equal(cast.Manager.Name, team?.Manager);
        string[] namesAfter = [.. second.Services.GetRequiredService<PersonaStore>().ListNames().Order(StringComparer.Ordinal)];
        Assert.Equal(namesBefore, namesAfter);
        Assert.Equal([new SpendAmount(1.25m, "USD")], second.Services.GetRequiredService<PersonaSpend>().GetPersisted(cast.Manager.Name));
        Assert.Equal(seeded, second.Services.GetRequiredService<CadenceStateStore>().Get(TeamName));
    }

    /// <summary>Regression for the boundary bug this flow found: <c>EnsureTeam</c> refuses a Team that exists, so the process, gate, onboarding, cadence and proposal code locates an existing Team folder through <c>FindTeam</c>, which finds it and says so when there is none.</summary>
    [Fact]
    public async Task FindTeam_ExistingTeam_ReturnsItsFolder_WhereEnsureTeamRefuses()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir workspace = NewWorkspace();
        await using TeamWebApplicationFactory root = new();
        await using Run run = Run.Open(root);
        _ = await CreateAndStartAsync(run, workspace.Path, ct);
        ITeamFolders folders = run.Services.GetRequiredService<ITeamFolders>();

        LibraryResult<LibraryPath> found = folders.FindTeam(TeamName);
        LibraryResult<LibraryPath> ensured = folders.EnsureTeam(TeamName);
        LibraryResult<LibraryPath> missing = folders.FindTeam("Atlantis");

        Assert.Equal(Path.Combine(root.TasksDirPath, TeamName), found.Value?.FullPath);
        Assert.Equal("A Team named \"Phoenix\" already exists.", ensured.Error);
        Assert.Null(missing.Value);
        Assert.Equal("There is no Team named \"Atlantis\".", missing.Error);
    }

    /// <summary>Creates a temp folder that looks like a repository, for the Development type's required workspace.</summary>
    /// <returns>The folder, deleted on dispose.</returns>
    private static TempDataDir NewWorkspace()
    {
        TempDataDir workspace = new();
        _ = Directory.CreateDirectory(Path.Combine(workspace.Path, ".git"));
        return workspace;
    }

    /// <summary>Creates the Development Team with its four starting roles, gives it its default process, hires the manager and then the other members the way Build a team does, and onboards the manager.</summary>
    /// <param name="run">The running host.</param>
    /// <param name="workspace">The repository folder.</param>
    /// <param name="ct">Cancels the work.</param>
    /// <returns>The hired cards.</returns>
    private static async Task<TeamCast> CreateAndStartAsync(Run run, string workspace, CancellationToken ct)
    {
        IServiceProvider services = run.Services;
        TeamTypeManifest type = services.GetRequiredService<TeamTypeCatalog>().Find(TypeId) ?? throw new InvalidOperationException("The development type is not in the catalog.");
        ITeamFolders folders = services.GetRequiredService<ITeamFolders>();
        HireService hiring = services.GetRequiredService<HireService>();
        IHireContext context = services.GetRequiredService<IHireContext>();
        PersonaStore personas = services.GetRequiredService<PersonaStore>();
        TeamFile file = new(new VersionedRef(type.Id, type.Version), null, workspace, null, string.Empty);
        Assert.Equal(TeamCreateOutcome.Created, folders.CreateTeam(TeamName, file).Outcome);
        _ = hiring.ProvisionProcess(TeamName, null);

        List<string> used = [.. personas.ListNames()];
        List<CardState> cards = [];
        foreach (string roleId in StartRoles)
        {
            RoleSpec role = type.Roles.First(each => string.Equals(each.Id, roleId, StringComparison.Ordinal));
            CardState card = CardResolver.Resolve(role, used, context.Answered, context.TierMap, context.AvoidAdapters, services.GetRequiredService<ISkillAvailability>(), context.Connectors);
            Assert.Equal(CardReadiness.Ready, card.Readiness);
            used.Add(card.Name);
            cards.Add(card);
        }

        CardState manager = cards.First(card => type.Roles.First(role => string.Equals(role.Id, card.RoleId, StringComparison.Ordinal)).Manager);
        HireResult hired = hiring.Hire(new HireRequest(TeamName, type.Id, manager.RoleId, manager));
        Assert.Null(hired.Error);
        _ = folders.UpdateTeamFile(TeamName, current => current with { Manager = manager.Name });

        foreach (CardState card in cards.Where(card => !ReferenceEquals(card, manager)))
        {
            HireResult hiredMember = hiring.Hire(new HireRequest(TeamName, type.Id, card.RoleId, card));
            Assert.Null(hiredMember.Error);
        }

        _ = await hiring.OnboardManagerAsync(TeamName, manager.Name, ct);

        ITeamCatalog catalog = services.GetRequiredService<ITeamCatalog>();
        await Eventually.UntilAsync(() => catalog.Find(TeamName) is { Manager: not null } team && team.Members.Count == cards.Count, ct, timeout: TestBudgets.Slowest, interval: TimeSpan.FromMilliseconds(50));
        return new TeamCast(cards, manager);
    }

    /// <summary>The cards hired for a Team and the manager among them.</summary>
    /// <param name="Cards">Every hired card.</param>
    /// <param name="Manager">The manager's card.</param>
    private sealed record TeamCast(IReadOnlyList<CardState> Cards, CardState Manager)
    {
        /// <summary>Builds the Task actor for the Agent hired for <paramref name="roleId"/>.</summary>
        /// <param name="run">The running host.</param>
        /// <param name="roleId">The role id.</param>
        /// <param name="ct">Cancels the user lookup.</param>
        /// <returns>The agent actor.</returns>
        public async Task<TaskActor> ActorAsync(Run run, string roleId, CancellationToken ct)
        {
            CardState card = this.Cards.First(each => string.Equals(each.RoleId, roleId, StringComparison.Ordinal));
            User? user = await run.Services.GetRequiredService<ITeamDirectory>().UpsertAgentUserAsync(card.Name, null, ct);
            Assert.NotNull(user);
            return new TaskActor(TaskActorKind.Agent, card.Name, user.Id);
        }
    }

    /// <summary>A host built over the shared factory's data directory, with the mock adapter and recorders installed.</summary>
    private sealed class Run : IAsyncDisposable
    {
        private readonly WebApplicationFactory<Program> host;

        private Run(WebApplicationFactory<Program> host, RecordingStartGate gate, MockLauncher launcher)
        {
            this.host = host;
            this.Gate = gate;
            this.Launcher = launcher;
            this.Services.GetRequiredService<RoomEvents>().MessagePosted += this.Posts.Enqueue;
        }

        /// <summary>Gets the start gate recorder.</summary>
        public RecordingStartGate Gate { get; }

        /// <summary>Gets the mock adapter launcher.</summary>
        public MockLauncher Launcher { get; }

        /// <summary>Gets every Message posted since the host started.</summary>
        public ConcurrentQueue<MessagePostedEvent> Posts { get; } = new();

        /// <summary>Gets the host's services.</summary>
        public IServiceProvider Services => this.host.Services;

        /// <summary>Builds and starts a host on <paramref name="root"/>'s data directory.</summary>
        /// <param name="root">The factory that owns the data directory.</param>
        /// <returns>The running host.</returns>
        public static Run Open(TeamWebApplicationFactory root)
        {
            RecordingStartGate gate = new();
            MockLauncher launcher = new();
            WebApplicationFactory<Program> host = root.WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Team:Acp:Enabled", "true");
                builder.UseSetting("Team:Acp:Args:0", "--phase3-mock-adapter");
                builder.UseSetting("Team:Tasks:WakeCoalesceSeconds", "1");
                foreach (ModelTier tier in Enum.GetValues<ModelTier>())
                {
                    builder.UseSetting($"Team:Teams:Tiers:claude:{tier}", $"mock-{tier}");
                }

                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IAgentProcessLauncher>();
                    services.AddSingleton<IAgentProcessLauncher>(launcher);
                    services.RemoveAll<Agency.Huddle.App.SkillSources.ISkillCopier>();
                    services.AddSingleton<Agency.Huddle.App.SkillSources.ISkillCopier>(new FakeSkillCopier());
                    services.AddSingleton<ISkillAvailability>(new FakeSkillAvailability());
                    services.RemoveAll<IPersonaStartGate>();
                    services.AddSingleton<IPersonaStartGate>(sp =>
                    {
                        gate.Bind(sp.GetRequiredService<PersonaSupervisor>(), sp.GetRequiredService<PersonaStore>());
                        return gate;
                    });
                });
            });
            return new Run(host, gate, launcher);
        }

        /// <summary>Counts the Task wake Messages posted that open with a Mention of <paramref name="name"/>.</summary>
        /// <param name="name">The woken Agent's name.</param>
        /// <param name="changedBy">The actor the wake names as having changed the Task, or <see langword="null"/> for any wake.</param>
        /// <returns>The count.</returns>
        public int WakesOf(string name, string? changedBy) => this.Posts.Count(post =>
            post.Message.Text.StartsWith($"@{name} Task ", StringComparison.Ordinal)
            && (changedBy is null || post.Message.Text.Contains($" was changed by {changedBy}:", StringComparison.Ordinal)));

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            this.Services.GetRequiredService<RoomEvents>().MessagePosted -= this.Posts.Enqueue;
            await this.host.DisposeAsync();
        }
    }

    /// <summary>Starts an in-memory mock agent for every launch, one per Agent host.</summary>
    private sealed class MockLauncher : IAgentProcessLauncher
    {
        private int launches;

        /// <summary>Gets how many hosts were launched.</summary>
        public int Launches => Volatile.Read(ref this.launches);

        /// <inheritdoc />
        public IAgentProcess Launch(AgentProcessOptions options)
        {
            _ = Interlocked.Increment(ref this.launches);
            return new FakeAgentProcessLauncher().Launch(options);
        }
    }

    /// <summary>Wraps the real supervisor gate and records each wait with the Agents that existed when it began.</summary>
    private sealed class RecordingStartGate : IPersonaStartGate
    {
        private PersonaSupervisor? inner;

        private PersonaStore? personas;

        /// <summary>Gets the names waited for, in order.</summary>
        public List<string> Waited { get; } = [];

        /// <summary>Gets, per wait, the Agent names that existed when it began.</summary>
        public List<string[]> PersonasAtWait { get; } = [];

        /// <summary>Gets, per wait, whether the Agent reported running.</summary>
        public List<bool> Results { get; } = [];

        /// <summary>Supplies the real gate and the store the recorder reads.</summary>
        /// <param name="gate">The real supervisor.</param>
        /// <param name="store">The Persona store.</param>
        public void Bind(PersonaSupervisor gate, PersonaStore store)
        {
            this.inner = gate;
            this.personas = store;
        }

        /// <inheritdoc />
        public async Task<bool> WaitUntilRunningAsync(string name, TimeSpan timeout, CancellationToken ct)
        {
            this.Waited.Add(name);
            this.PersonasAtWait.Add([.. this.personas?.ListNames() ?? []]);
            bool running = this.inner is not null && await this.inner.WaitUntilRunningAsync(name, timeout, ct);
            this.Results.Add(running);
            return running;
        }
    }
}

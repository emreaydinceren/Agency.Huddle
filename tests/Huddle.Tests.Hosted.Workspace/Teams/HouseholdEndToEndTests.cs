using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.DocHistory;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.HumanGate;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Ui;
using Agency.Huddle.Tests.Waiting;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// Task 83 (NF-11, Spec Phases): the Household built-in type end to end through the real host. A Household Team with no
/// workspace hires its manager and its School Liaison (Ready throughout; its card lists <c>email:read</c> as unconnected
/// until <c>connectors.json</c> lists it, which never blocks the hire),
/// starts, and takes a Task from To do to Done under the <c>household-basics</c> process with no gate refusal, although
/// the type has no QA role.
/// </summary>
public sealed class HouseholdEndToEndTests
{
    private const string TeamName = "Home";

    private const string TypeId = "household";

    private const string ManagerRole = "household-manager";

    private const string LiaisonRole = "school-liaison";

    private const string Connector = "email:read";

    private const string PlanChecked = "Plan checked";

    private const string PlanReviewed = "Plan reviewed";

    /// <summary>The liaison card is Ready but lists its connector as unconnected until the connector is listed; then the Team starts with both Agents and a Task reaches Done with no gate refusal.</summary>
    [Fact]
    public async Task HouseholdTeam_NoWorkspace_HiresStartsAndTakesATaskToDone()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using TeamWebApplicationFactory root = new();
        await using WebApplicationFactory<Program> host = OpenHost(root);
        IServiceProvider services = host.Services;
        TeamTypeManifest type = services.GetRequiredService<TeamTypeCatalog>().Find(TypeId) ?? throw new InvalidOperationException("The household type is not in the catalog.");
        CreateTeam(services, type);

        CardState manager = Resolve(services, type, ManagerRole);
        CardState waiting = Resolve(services, type, LiaisonRole);

        Assert.Equal(CardReadiness.Ready, manager.Readiness);
        Assert.Equal(CardReadiness.Ready, waiting.Readiness);
        Assert.Equal([Connector], waiting.Unconnected);

        File.WriteAllText(Path.Combine(root.DataDirPath, TeamPaths.ConnectorsFileName), $$"""{"connected": ["{{Connector}}"]}""");
        CardState liaison = Resolve(services, type, LiaisonRole);

        Assert.Equal(CardReadiness.Ready, liaison.Readiness);
        Assert.Empty(liaison.Unconnected);

        await HireBothAsync(services, type, manager, liaison, ct);

        TaskService service = services.GetRequiredService<TaskService>();
        TaskActor actor = await AgentActorAsync(services, manager.Name, ct);
        TaskActor human = TaskActors.Human(services.GetRequiredService<IOptions<TeamOptions>>().Value);
        TaskItem task = Saved(service.Create(new TaskDraft("Plan the school trip", TeamName, null, TaskState.ToDo, Assignee: manager.Name), human));

        _ = Saved(service.Update(task.Id, new TaskPatch { Status = TaskState.InProgress }, null, actor, false));
        _ = Saved(service.AddEvidence(task.Id, VerifierNote(), actor));
        IProofSource proofs = services.GetRequiredService<IProofSource>();
        TaskItem checkedTask = services.GetRequiredService<TaskStore>().Get(task.Id) ?? throw new InvalidOperationException("The Task is gone.");
        Assert.True(proofs.For(checkedTask).Items.Single(static item => string.Equals(item.Label, PlanChecked, StringComparison.Ordinal)).Met);
        TaskItem done = Saved(service.Update(task.Id, new TaskPatch { Status = TaskState.Done }, null, actor, false));

        Assert.Equal(TaskState.Done, done.Status);
    }

    /// <summary>
    /// The owner's D2 case on the real host (#550): the liaison saves a reply slip under the Team's <c>outputs/</c>, attaches it and
    /// logs it created; the manager logs it reviewed and posts a note-only Evidence entry; Huddle, not the manager, then moves the
    /// gated Task from Review to Needs You.
    /// </summary>
    [Fact]
    public async Task GatedTask_FileDraftReviewedAndNoteOnlyEvidence_HuddleMovesItToNeedsYou()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using TeamWebApplicationFactory root = new();
        await using WebApplicationFactory<Program> host = OpenHost(root);
        IServiceProvider services = host.Services;
        (TaskActor manager, TaskActor liaison, string managerName, string liaisonName) = await StartHouseholdAsync(services, root, ct);
        string draft = WriteDraft(services);
        TaskItem task = InReview(services, liaison, AttachedDescription(draft));
        LogChange(services, draft, liaisonName, DocChangeKind.Created, "drafted the reply slip");
        LogChange(services, draft, managerName, DocChangeKind.Reviewed, "checked it against the school email");
        TaskService service = services.GetRequiredService<TaskService>();
        Assert.Equal(TaskState.InProgress, Current(services, task).Status);

        _ = Saved(service.AddEvidence(task.Id, VerifierNote(), manager));

        Assert.Equal(TaskState.NeedsYou, Current(services, task).Status);
    }

    /// <summary>
    /// The same Task with the draft written only in the description (#550): the note meets "Plan checked", but "Plan reviewed"
    /// stays open with "no document attached", so Huddle leaves the Task in Review.
    /// </summary>
    [Fact]
    public async Task GatedTask_DraftOnlyInDescription_StaysInReviewWithPlanReviewedOpen()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using TeamWebApplicationFactory root = new();
        await using WebApplicationFactory<Program> host = OpenHost(root);
        IServiceProvider services = host.Services;
        (TaskActor manager, TaskActor liaison, _, _) = await StartHouseholdAsync(services, root, ct);
        TaskItem task = InReview(services, liaison, "Dear teacher, we will attend the trip.");

        _ = Saved(services.GetRequiredService<TaskService>().AddEvidence(task.Id, VerifierNote(), manager));

        TaskItem current = Current(services, task);
        Assert.Equal(TaskState.InProgress, current.Status);
        Proof proof = services.GetRequiredService<IProofSource>().For(current);
        Assert.True(proof.Items.Single(static item => string.Equals(item.Label, PlanChecked, StringComparison.Ordinal)).Met);
        ProofItem reviewed = proof.Items.Single(static item => string.Equals(item.Label, PlanReviewed, StringComparison.Ordinal));
        Assert.False(reviewed.Met);
        Assert.Equal("no document attached", reviewed.Detail);
    }

    /// <summary>Creates the Team folder and provisions the default process.</summary>
    /// <param name="services">The host's services.</param>
    /// <param name="type">The team type.</param>
    private static void CreateTeam(IServiceProvider services, TeamTypeManifest type)
    {
        TeamFile file = new(new VersionedRef(type.Id, type.Version), null, null, null, string.Empty);
        Assert.Equal(TeamCreateOutcome.Created, services.GetRequiredService<ITeamFolders>().CreateTeam(TeamName, file).Outcome);
        Assert.Equal(ProvisionResult.Created, services.GetRequiredService<ProcessProvisioner>().ProvisionDefault(TeamName));
    }

    /// <summary>Hires the manager and the liaison, onboards the manager and waits until the catalog lists both.</summary>
    /// <param name="services">The host's services.</param>
    /// <param name="type">The team type.</param>
    /// <param name="manager">The manager's card.</param>
    /// <param name="liaison">The liaison's card.</param>
    /// <param name="ct">The test's cancellation token.</param>
    private static async Task HireBothAsync(IServiceProvider services, TeamTypeManifest type, CardState manager, CardState liaison, CancellationToken ct)
    {
        HireService hiring = services.GetRequiredService<HireService>();
        Assert.Null(hiring.Hire(new HireRequest(TeamName, type.Id, manager.RoleId, manager)).Error);
        _ = services.GetRequiredService<ITeamFolders>().UpdateTeamFile(TeamName, current => current with { Manager = manager.Name });
        Assert.Null(hiring.Hire(new HireRequest(TeamName, type.Id, liaison.RoleId, liaison)).Error);
        _ = await hiring.OnboardManagerAsync(TeamName, manager.Name, ct);

        ITeamCatalog catalog = services.GetRequiredService<ITeamCatalog>();
        await Eventually.UntilAsync(
            () => catalog.Find(TeamName) is { Manager: not null } current && current.Members.Count == 2,
            ct,
            TestBudgets.Slowest,
            TimeSpan.FromMilliseconds(50),
            describe: () => "The Team catalog never listed both Agents and a Manager");

        TeamSummary team = catalog.Find(TeamName) ?? throw new InvalidOperationException("The Team is not in the catalog.");
        Assert.Equal(manager.Name, team.Manager);
        Assert.Equal(2, team.Members.Count);
    }

    /// <summary>Creates the Household Team, connects the liaison's connector and hires both members.</summary>
    /// <param name="services">The host's services.</param>
    /// <param name="root">The factory that owns the data directory.</param>
    /// <param name="ct">The test's cancellation token.</param>
    /// <returns>The manager's and the liaison's actors and names.</returns>
    private static async Task<(TaskActor Manager, TaskActor Liaison, string ManagerName, string LiaisonName)> StartHouseholdAsync(IServiceProvider services, TeamWebApplicationFactory root, CancellationToken ct)
    {
        TeamTypeManifest type = services.GetRequiredService<TeamTypeCatalog>().Find(TypeId) ?? throw new InvalidOperationException("The household type is not in the catalog.");
        CreateTeam(services, type);
        File.WriteAllText(Path.Combine(root.DataDirPath, TeamPaths.ConnectorsFileName), $$"""{"connected": ["{{Connector}}"]}""");
        CardState manager = Resolve(services, type, ManagerRole);
        CardState liaison = Resolve(services, type, LiaisonRole);
        await HireBothAsync(services, type, manager, liaison, ct);
        return (await AgentActorAsync(services, manager.Name, ct), await AgentActorAsync(services, liaison.Name, ct), manager.Name, liaison.Name);
    }

    /// <summary>Makes the Agent's user and the actor that acts as it.</summary>
    /// <param name="services">The host's services.</param>
    /// <param name="name">The Agent's name.</param>
    /// <param name="ct">The test's cancellation token.</param>
    private static async Task<TaskActor> AgentActorAsync(IServiceProvider services, string name, CancellationToken ct)
    {
        User? user = await services.GetRequiredService<ITeamDirectory>().UpsertAgentUserAsync(name, null, ct);
        Assert.NotNull(user);
        return new TaskActor(TaskActorKind.Agent, name, user.Id);
    }

    /// <summary>A note-only Evidence line, as <c>add_evidence</c> builds it from a note: no command, no time, no author.</summary>
    private static EvidenceLine VerifierNote() =>
        new(default, string.Empty, string.Empty, null, null, null, "checked the draft against the school email");

    /// <summary>Saves the draft under the Team's <c>outputs/</c> folder.</summary>
    /// <param name="services">The host's services.</param>
    /// <returns>The draft's full path.</returns>
    private static string WriteDraft(IServiceProvider services)
    {
        string folder = services.GetRequiredService<ITeamFolders>().FindTeam(TeamName).Value?.FullPath ?? throw new InvalidOperationException("The Team has no folder.");
        string outputs = Path.Combine(folder, "outputs");
        _ = Directory.CreateDirectory(outputs);
        string path = Path.Combine(outputs, "reply-slip.md");
        File.WriteAllText(path, "Dear teacher, we will attend the trip.\n");
        return path;
    }

    /// <summary>The description that attaches the draft file.</summary>
    /// <param name="draft">The draft's full path.</param>
    private static string AttachedDescription(string draft) =>
        TaskAttachmentText.AppendPathLine("Reply to the school about the trip.", draft);

    /// <summary>Logs a change to a Library document through the recorder the <c>log_change</c> tool uses.</summary>
    /// <param name="services">The host's services.</param>
    /// <param name="path">The document's full path.</param>
    /// <param name="agent">The logging Agent.</param>
    /// <param name="kind">What the Agent did.</param>
    /// <param name="summary">The one line.</param>
    private static void LogChange(IServiceProvider services, string path, string agent, DocChangeKind kind, string summary)
    {
        if (!services.GetRequiredService<LibraryPathResolver>().TryResolveAbsolute(path, out LibraryPath? file))
        {
            throw new InvalidOperationException($"'{path}' is not in the Library.");
        }

        Assert.True(services.GetRequiredService<DocHistoryRecorder>().AgentLogged(file, agent, kind, summary));
    }

    /// <summary>Creates a gated Task assigned to the liaison and moves it to Review with <paramref name="description"/>.</summary>
    /// <param name="services">The host's services.</param>
    /// <param name="liaison">The liaison's actor.</param>
    /// <param name="description">The description the liaison leaves on the Task.</param>
    private static TaskItem InReview(IServiceProvider services, TaskActor liaison, string description)
    {
        TaskService service = services.GetRequiredService<TaskService>();
        TaskActor human = TaskActors.Human(services.GetRequiredService<IOptions<TeamOptions>>().Value);
        TaskDraft draft = new("Reply to the school", TeamName, null, TaskState.ToDo, Assignee: liaison.Name) { HumanGate = true, Action = "Hand the signed slip to school" };
        TaskItem task = Saved(service.Create(draft, human));
        _ = Saved(service.Update(task.Id, new TaskPatch { Status = TaskState.InProgress }, null, liaison, false));
        return Saved(service.Update(task.Id, new TaskPatch { Description = description }, null, liaison, false));
    }

    /// <summary>Reads the Task as it is stored now.</summary>
    /// <param name="services">The host's services.</param>
    /// <param name="task">The Task.</param>
    private static TaskItem Current(IServiceProvider services, TaskItem task) =>
        services.GetRequiredService<TaskStore>().Get(task.Id) ?? throw new InvalidOperationException("The Task is gone.");

    /// <summary>Builds a host over the factory's data directory with Acp off, a model for every tier and a recording-free installer.</summary>
    /// <param name="root">The factory that owns the data directory.</param>
    /// <returns>The host.</returns>
    private static WebApplicationFactory<Program> OpenHost(TeamWebApplicationFactory root) => root.WithWebHostBuilder(builder =>
    {
        foreach (ModelTier tier in Enum.GetValues<ModelTier>())
        {
            builder.UseSetting($"Team:Teams:Tiers:claude:{tier}", $"mock-{tier}");
        }

        builder.ConfigureTestServices(static services =>
        {
            services.RemoveAll<Agency.Huddle.App.SkillSources.ISkillCopier>();
            services.AddSingleton<Agency.Huddle.App.SkillSources.ISkillCopier>(new FakeSkillCopier());
            services.AddSingleton<ISkillAvailability>(new FakeSkillAvailability());
            services.RemoveAll<IPersonaStartGate>();
            services.AddSingleton<IPersonaStartGate>(new FakeStartGate());
        });
    });

    /// <summary>Resolves the hire card of a role the way the Team setup page does.</summary>
    /// <param name="services">The host's services.</param>
    /// <param name="type">The team type.</param>
    /// <param name="roleId">The role id.</param>
    /// <returns>The card.</returns>
    private static CardState Resolve(IServiceProvider services, TeamTypeManifest type, string roleId)
    {
        IHireContext context = services.GetRequiredService<IHireContext>();
        RoleSpec role = type.Roles.First(each => string.Equals(each.Id, roleId, StringComparison.Ordinal));
        List<string> used = [.. services.GetRequiredService<PersonaStore>().ListNames()];
        return CardResolver.Resolve(role, used, context.Answered, context.TierMap, context.AvoidAdapters, services.GetRequiredService<ISkillAvailability>(), context.Connectors);
    }

    /// <summary>Unwraps a saved Task result, failing the test on any refusal.</summary>
    /// <param name="result">The result.</param>
    /// <returns>The saved Task.</returns>
    private static TaskItem Saved(TaskResult result) => Assert.IsType<TaskResult.Saved>(result).Task;
}

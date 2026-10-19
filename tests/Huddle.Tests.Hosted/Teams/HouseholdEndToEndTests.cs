using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Ui;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// Task 83 (NF-11, Spec Phases): the Household built-in type end to end through the real host. A Household Team with no
/// workspace hires its manager and its School Liaison (Needs setup until <c>connectors.json</c> lists <c>email:read</c>),
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

    /// <summary>The liaison card needs setup until the connector is listed; then the Team starts with both Teammates and a Task reaches Done with no gate refusal.</summary>
    [Fact]
    public async Task HouseholdTeam_NoWorkspace_HiresStartsAndTakesATaskToDone()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using TeamWebApplicationFactory root = new();
        await using WebApplicationFactory<Program> host = OpenHost(root);
        IServiceProvider services = host.Services;
        TeamTypeManifest type = services.GetRequiredService<TeamTypeCatalog>().Find(TypeId) ?? throw new InvalidOperationException("The household type is not in the catalog.");
        ITeamFolders folders = services.GetRequiredService<ITeamFolders>();
        TeamFile file = new(new VersionedRef(type.Id, type.Version), null, null, null, TeamPaths.ProcessFileName, string.Empty);
        Assert.Equal(TeamCreateOutcome.Created, folders.CreateTeam(TeamName, file).Outcome);
        Assert.Equal(ProvisionResult.Created, services.GetRequiredService<ProcessProvisioner>().ProvisionDefault(TeamName));

        CardState manager = Resolve(services, type, ManagerRole);
        CardState waiting = Resolve(services, type, LiaisonRole);

        Assert.Equal(CardReadiness.Ready, manager.Readiness);
        Assert.Equal(CardReadiness.NeedsSetup, waiting.Readiness);

        File.WriteAllText(Path.Combine(root.DataDirPath, TeamPaths.ConnectorsFileName), $$"""{"connected": ["{{Connector}}"]}""");
        CardState liaison = Resolve(services, type, LiaisonRole);

        Assert.Equal(CardReadiness.Ready, liaison.Readiness);

        HireService hiring = services.GetRequiredService<HireService>();
        Assert.Null(hiring.Hire(new HireRequest(TeamName, type.Id, manager.RoleId, manager)).Error);
        _ = folders.UpdateTeamFile(TeamName, current => current with { Manager = manager.Name });
        StartTeamResult started = await hiring.StartTeamAsync(TeamName, [new HireRequest(TeamName, type.Id, manager.RoleId, manager), new HireRequest(TeamName, type.Id, liaison.RoleId, liaison)], ct);
        Assert.True(started.Ok, started.Error);

        ITeamCatalog catalog = services.GetRequiredService<ITeamCatalog>();
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (catalog.Find(TeamName) is not { Manager: not null } current || current.Members.Count != 2)
        {
            Assert.True(DateTime.UtcNow < deadline, "Timed out waiting for the Team catalog to list both Teammates.");
            await Task.Delay(50, ct);
        }

        TeamSummary team = catalog.Find(TeamName) ?? throw new InvalidOperationException("The Team is not in the catalog.");
        Assert.Equal(manager.Name, team.Manager);
        Assert.Equal(2, team.Members.Count);

        TaskService service = services.GetRequiredService<TaskService>();
        User? user = await services.GetRequiredService<ITeamDirectory>().UpsertAgentUserAsync(manager.Name, null, ct);
        Assert.NotNull(user);
        TaskActor actor = new(TaskActorKind.Agent, manager.Name, user.Id);
        TaskActor human = TaskActors.Human(services.GetRequiredService<IOptions<TeamOptions>>().Value);
        TaskItem task = Saved(service.Create(new TaskDraft("Plan the school trip", TeamName, null, TaskState.ToDo, Assignee: manager.Name), human));

        _ = Saved(service.Update(task.Id, new TaskPatch { Status = TaskState.InProgress }, null, actor, false));
        _ = Saved(service.Update(task.Id, new TaskPatch { Status = TaskState.Review }, null, actor, false));
        _ = Saved(service.AddEvidence(task.Id, new EvidenceLine(DateTimeOffset.UtcNow, manager.Name, "compared the draft with the school emails", 0, null, null, string.Empty), actor));
        TaskItem done = Saved(service.Update(task.Id, new TaskPatch { Status = TaskState.Done }, null, actor, false));

        Assert.Equal(TaskState.Done, done.Status);
    }

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

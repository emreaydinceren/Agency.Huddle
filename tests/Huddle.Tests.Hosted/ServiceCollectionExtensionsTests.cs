using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Circuits;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Agents;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests;

/// <summary>
/// Covers the startup guard in <see cref="ServiceCollectionExtensions.AddTeamServices"/> that
/// rejects the old <c>Team:Acp:PersonaDir</c> configuration key rather than silently binding it to
/// nothing. <see cref="Ui.TeamWebApplicationFactory"/> and every other test that composes the app
/// go through the same method without ever setting the old key, which is what
/// <see cref="AddTeamServices_WithoutTheOldPersonaDirKey_ComposesNormally"/> pins.
/// </summary>
public sealed class ServiceCollectionExtensionsTests
{
    /// <summary>
    /// Renaming <c>Acp.PersonaDir</c> to <c>Acp.AgentsDir</c> (the Agents rename) has no
    /// fallback: nothing in appsettings*.json sets the old key today, so keeping one would be dead
    /// weight. But a value left behind in a user secret or an environment variable would otherwise
    /// bind to nothing, leaving <see cref="Agency.Huddle.App.Acp.PersonaStore"/> to scan an empty
    /// default "Agents" folder - zero agents, no exception, no log anywhere. That
    /// silent-degradation shape is exactly what docs/engineering/traps.md exists to catch, so this
    /// must fail loudly at startup instead, naming the new key.
    /// </summary>
    [Fact]
    public void AddTeamServices_WithTheOldPersonaDirKey_ThrowsNamingTheNewKey()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Team:Acp:PersonaDir"] = "personas",
            })
            .Build();

        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(() => services.AddTeamServices(configuration));

        Assert.Equal(
            "Configuration key 'Team:Acp:PersonaDir' was renamed to 'Team:Acp:AgentsDir'. " +
            "Update the configuration source that sets it (environment variable, user secret, etc.) - " +
            "there is no automatic fallback.",
            ex.Message);
    }

    /// <summary>
    /// <see cref="Agency.Huddle.App.Library.LayoutGuard.ValidateTeamsAndAgents"/> runs from the same
    /// <c>PostConfigure</c> that normalises <c>DataDir</c>, so an overlapping <c>Team:Teams:Dir</c> and
    /// <c>Team:Acp:AgentsDir</c> must fail loudly the moment <see cref="TeamOptions"/> is resolved,
    /// rather than silently scanning one folder as both roots.
    /// </summary>
    [Fact]
    public void AddTeamServices_WithOverlappingTeamsAndAgentsDirs_ThrowsOnOptionsResolution()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Team:Teams:Dir"] = "Agents",
                ["Team:Acp:AgentsDir"] = "Agents",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddTeamServices(configuration);
        using var provider = services.BuildServiceProvider();

        var ex = Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<TeamOptions>>().Value);

        string dataDir = Path.GetFullPath("App_Data");
        string agentsRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(dataDir, "Agents")));
        Assert.Equal($"'Team:Teams:Dir' ({agentsRoot}) and 'Team:Acp:AgentsDir' ({agentsRoot}) must not overlap.", ex.Message);
    }

    /// <summary>The team type catalog resolves from the container with the real embedded built-ins and no problems.</summary>
    [Fact]
    public void AddTeamServices_ResolvesTeamTypeCatalog()
    {
        using TempDataDir dataDir = new();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Team:DataDir"] = dataDir.Path })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTeamServices(configuration);
        using var provider = services.BuildServiceProvider();

        var catalog = provider.GetRequiredService<Agency.Huddle.App.TeamTypes.TeamTypeCatalog>();

        Assert.Empty(catalog.Problems);
    }

    private static ServiceProvider BuildTeamProvider(TempDataDir dataDir)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Team:DataDir"] = dataDir.Path })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTeamServices(configuration);
        return services.BuildServiceProvider();
    }

    /// <summary>The ChatService the container resolves is given the Team catalog, so it can create a Room scoped to a catalog Team.</summary>
    [Fact]
    public async Task AddTeamServices_ResolvedChatService_CreatesRoomForACatalogTeam()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Team:DataDir"] = dataDir.Path })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTeamServices(configuration);
        services.AddSingleton<ITeamCatalog>(new Teams.FakeTeamCatalog { Teams = [new TeamSummary("Marketing", [], [], false)] });
        using ServiceProvider provider = services.BuildServiceProvider();
        ITeamDirectory directory = provider.GetRequiredService<ITeamDirectory>();
        await directory.InitializeAsync("You", ct);
        User? nova = await directory.UpsertAgentUserAsync("Nova", null, ct);
        Assert.NotNull(nova);

        Room room = await provider.GetRequiredService<Agency.Huddle.App.Services.ChatService>().CreateRoomForAsync([nova.Id], "Marketing", ct: ct);

        Assert.Equal("Marketing", room.Team);
    }

    /// <summary>The hire service resolves from the container.</summary>
    [Fact]
    public void AddTeamServices_ResolvesHireService()
    {
        using TempDataDir dataDir = new();
        using ServiceProvider provider = BuildTeamProvider(dataDir);

        Assert.NotNull(provider.GetRequiredService<Agency.Huddle.App.Teams.HireService>());
    }

    /// <summary>The orchestrator resolves in a scope, and the container holds the five providers, one per group.</summary>
    [Fact]
    public void AddTeamServices_ResolvesSearchOrchestratorWithFiveProviders()
    {
        using TempDataDir dataDir = new();
        using ServiceProvider provider = BuildTeamProvider(dataDir);
        using IServiceScope scope = provider.CreateScope();

        Agency.Huddle.App.Search.SearchOrchestrator orchestrator = scope.ServiceProvider.GetRequiredService<Agency.Huddle.App.Search.SearchOrchestrator>();
        List<Agency.Huddle.App.Search.SearchFilter> groups = [.. scope.ServiceProvider
            .GetServices<Agency.Huddle.App.Search.ISearchProvider>()
            .Select(searchProvider => searchProvider.Group)
            .Order()];

        Assert.NotNull(orchestrator);
        Assert.Equal(
            [
                Agency.Huddle.App.Search.SearchFilter.Teams,
                Agency.Huddle.App.Search.SearchFilter.Agents,
                Agency.Huddle.App.Search.SearchFilter.Library,
                Agency.Huddle.App.Search.SearchFilter.Tasks,
                Agency.Huddle.App.Search.SearchFilter.Rooms,
            ],
            groups);
    }

    /// <summary>
    /// The app registers one circuit connection tracker per scope that is both the handler the framework reports a
    /// circuit's connection to and the connection the navigator reads. A circuit's scope is where the framework looks for
    /// handlers, so this is checked on the real container, not only on <c>AddCircuitConnection</c> by itself.
    /// </summary>
    [Fact]
    public void AddTeamServices_RegistersTheCircuitConnectionTrackerAsTheCircuitHandler()
    {
        using TempDataDir dataDir = new();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Team:DataDir"] = dataDir.Path })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();

        // Blazor Server supplies the circuit's IJSRuntime in the app; the circuit handlers include JsModulePreloader, which needs it.
        services.AddScoped<Microsoft.JSInterop.IJSRuntime, Agency.Huddle.Tests.Ui.FakeModuleRuntime>();
        services.AddTeamServices(configuration);
        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        CircuitConnectionTracker handler = Assert.Single(scope.ServiceProvider.GetServices<CircuitHandler>().OfType<CircuitConnectionTracker>());

        Assert.Same(handler, scope.ServiceProvider.GetRequiredService<ICircuitConnection>());
    }

    /// <summary>The merge gate the permission handler consults resolves to the process-backed implementation.</summary>
    [Fact]
    public void AddTeamServices_ResolvesMergeGate()
    {
        using TempDataDir dataDir = new();
        using ServiceProvider provider = BuildTeamProvider(dataDir);

        Assert.IsType<Agency.Huddle.App.Teams.ProcessMergeGate>(provider.GetRequiredService<IMergeGate>());
    }

    /// <summary>The team duplicator resolves from the container.</summary>
    [Fact]
    public void AddTeamServices_ResolvesTeamDuplicator()
    {
        using TempDataDir dataDir = new();
        using ServiceProvider provider = BuildTeamProvider(dataDir);

        Assert.NotNull(provider.GetRequiredService<Agency.Huddle.App.Teams.TeamDuplicator>());
    }

    /// <summary><c>ISkillCopier</c> resolves to <c>SkillCopier</c>.</summary>
    [Fact]
    public void ISkillCopier_ResolvesToSkillCopier()
    {
        using TempDataDir dataDir = new();
        using ServiceProvider provider = BuildTeamProvider(dataDir);

        Assert.IsType<Agency.Huddle.App.SkillSources.SkillCopier>(provider.GetRequiredService<Agency.Huddle.App.SkillSources.ISkillCopier>());
    }

    /// <summary>Huddle downloads nothing for Skills: no download or import type is left in the app, and no Skills service
    /// takes an <see cref="HttpClient"/>.</summary>
    [Fact]
    public void NoHttpClient_ForSkills()
    {
        System.Reflection.Assembly app = typeof(Agency.Huddle.App.SkillSources.SkillRegistry).Assembly;

        Assert.DoesNotContain(app.GetTypes(), static type => type.Name is "SkillDownloader" or "SkillImporter" or "ISkillImporter");
        Assert.DoesNotContain(
            app.GetTypes().Where(static type => type.Namespace is "Agency.Huddle.App.Skills" or "Agency.Huddle.App.SkillSources" or "Agency.Huddle.App.SkillSources.Move"),
            static type => type.GetConstructors().Any(static constructor => constructor.GetParameters().Any(static parameter => parameter.ParameterType == typeof(HttpClient))));
    }
    /// <summary>The start gate resolves to the one persona supervisor.</summary>
    [Fact]
    public void AddTeamServices_ResolvesStartGateAsTheSupervisor()
    {
        using TempDataDir dataDir = new();
        using ServiceProvider provider = BuildTeamProvider(dataDir);

        Assert.Same(provider.GetRequiredService<PersonaSupervisor>(), provider.GetRequiredService<Agency.Huddle.App.Teams.IPersonaStartGate>());
    }

    /// <summary>The role hirer and the hire context it needs resolve from the container.</summary>
    [Fact]
    public void AddTeamServices_ResolvesRoleHirerAndHireContext()
    {
        using TempDataDir dataDir = new();
        using ServiceProvider provider = BuildTeamProvider(dataDir);

        Assert.NotNull(provider.GetRequiredService<Agency.Huddle.App.Teams.IHireContext>());
        Assert.NotNull(provider.GetRequiredService<Agency.Huddle.App.Teams.RoleHirer>());
    }

    /// <summary>
    /// The negative case: a configuration that never mentions the old key must compose normally,
    /// so the guard above cannot be a false positive for every other test in the suite that calls
    /// <see cref="ServiceCollectionExtensions.AddTeamServices"/> without ever setting it.
    /// </summary>
    [Fact]
    public void AddTeamServices_WithoutTheOldPersonaDirKey_ComposesNormally()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();

        var result = services.AddTeamServices(configuration);

        Assert.Same(services, result);
    }

    /// <summary>
    /// Spec §10's ordering constraint 1: <c>DataInitializer</c> -&gt; <c>BuiltinAgentSeeder</c> -&gt;
    /// <see cref="PersonaSupervisor"/>, in that order. Hosted services start in registration order, so
    /// resolving <see cref="IHostedService"/> and checking the position each one lands at is the same
    /// thing as checking start order: the seeder must observe an empty Chief of Staff at startup and
    /// write it before the supervisor's first reconciliation, never after.
    /// </summary>
    [Fact]
    public async Task HostedServices_SeederRegisteredAfterDataInitializerAndBeforeSupervisor()
    {
        using TempDataDir dataDir = new();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Team:DataDir"] = dataDir.Path,
                ["Team:HumanName"] = "You",
                ["Team:DemoAgent:Enabled"] = "false",
                ["Team:Acp:Enabled"] = "false",
            })
            .Build();
        ServiceCollection services = new();
        services.AddLogging();
        services.AddTeamServices(configuration);

        await using ServiceProvider provider = services.BuildServiceProvider();
        List<IHostedService> hostedServices = [.. provider.GetServices<IHostedService>()];

        var dataInitializerIndex = hostedServices.FindIndex(service => service is DataInitializer);
        var seederIndex = hostedServices.FindIndex(service => service is BuiltinAgentSeeder);
        var supervisorIndex = hostedServices.FindIndex(service => service is PersonaSupervisor);

        Assert.True(dataInitializerIndex >= 0, "DataInitializer was not registered as a hosted service.");
        Assert.True(seederIndex >= 0, "BuiltinAgentSeeder was not registered as a hosted service.");
        Assert.True(supervisorIndex >= 0, "PersonaSupervisor was not registered as a hosted service.");
        Assert.True(
            dataInitializerIndex < seederIndex,
            $"Expected DataInitializer (index {dataInitializerIndex}) to start before BuiltinAgentSeeder (index {seederIndex}).");
        Assert.True(
            seederIndex < supervisorIndex,
            $"Expected BuiltinAgentSeeder (index {seederIndex}) to start before PersonaSupervisor (index {supervisorIndex}).");
    }
}

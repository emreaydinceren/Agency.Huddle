using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Teammates;

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
    /// Renaming <c>Acp.PersonaDir</c> to <c>Acp.TeammatesDir</c> (the Teammates rename) has no
    /// fallback: nothing in appsettings*.json sets the old key today, so keeping one would be dead
    /// weight. But a value left behind in a user secret or an environment variable would otherwise
    /// bind to nothing, leaving <see cref="Agency.Huddle.App.Acp.PersonaStore"/> to scan an empty
    /// default "Teammates" folder - zero teammates, no exception, no log anywhere. That
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
            "Configuration key 'Team:Acp:PersonaDir' was renamed to 'Team:Acp:TeammatesDir'. " +
            "Update the configuration source that sets it (environment variable, user secret, etc.) - " +
            "there is no automatic fallback.",
            ex.Message);
    }

    /// <summary>
    /// <see cref="Agency.Huddle.App.Library.LayoutGuard.ValidateTeamsAndTeammates"/> runs from the same
    /// <c>PostConfigure</c> that normalises <c>DataDir</c>, so an overlapping <c>Team:Teams:Dir</c> and
    /// <c>Team:Acp:TeammatesDir</c> must fail loudly the moment <see cref="TeamOptions"/> is resolved,
    /// rather than silently scanning one folder as both roots.
    /// </summary>
    [Fact]
    public void AddTeamServices_WithOverlappingTeamsAndTeammatesDirs_ThrowsOnOptionsResolution()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Team:Teams:Dir"] = "Teammates",
                ["Team:Acp:TeammatesDir"] = "Teammates",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddTeamServices(configuration);
        using var provider = services.BuildServiceProvider();

        var ex = Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<TeamOptions>>().Value);

        string dataDir = Path.GetFullPath("App_Data");
        string teammatesRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(dataDir, "Teammates")));
        Assert.Equal($"'Team:Teams:Dir' ({teammatesRoot}) and 'Team:Acp:TeammatesDir' ({teammatesRoot}) must not overlap.", ex.Message);
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
    /// Spec §10's ordering constraint 1: <c>DataInitializer</c> -&gt; <c>BuiltinTeammateSeeder</c> -&gt;
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
        var seederIndex = hostedServices.FindIndex(service => service is BuiltinTeammateSeeder);
        var supervisorIndex = hostedServices.FindIndex(service => service is PersonaSupervisor);

        Assert.True(dataInitializerIndex >= 0, "DataInitializer was not registered as a hosted service.");
        Assert.True(seederIndex >= 0, "BuiltinTeammateSeeder was not registered as a hosted service.");
        Assert.True(supervisorIndex >= 0, "PersonaSupervisor was not registered as a hosted service.");
        Assert.True(
            dataInitializerIndex < seederIndex,
            $"Expected DataInitializer (index {dataInitializerIndex}) to start before BuiltinTeammateSeeder (index {seederIndex}).");
        Assert.True(
            seederIndex < supervisorIndex,
            $"Expected BuiltinTeammateSeeder (index {seederIndex}) to start before PersonaSupervisor (index {supervisorIndex}).");
    }
}

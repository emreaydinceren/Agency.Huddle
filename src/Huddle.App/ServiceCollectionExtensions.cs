using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Demo;
using Agency.Huddle.App.Hooks;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Services;

namespace Agency.Huddle.App;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTeamServices(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Acp.PersonaDir was renamed to Acp.TeamsDir (the "Teams" rename) with no back-compat
        // fallback: nothing in appsettings*.json still sets the old key, so a fallback would be
        // dead weight. But a value left behind in a user secret or an environment variable would
        // otherwise bind to nothing, and PersonaStore would quietly scan an empty default "Teams"
        // folder - zero teammates, no exception, no log anywhere. That silent-degradation shape is
        // exactly what docs/agencyteam/traps.md exists to catch, so fail loudly at startup instead,
        // naming the new key. This runs ahead of Configure<TeamOptions> below, in the one place
        // both this app's Program.cs and every test that composes it (TeamWebApplicationFactory)
        // are guaranteed to pass through.
        if (configuration[$"{TeamOptions.SectionName}:Acp:PersonaDir"] is not null)
        {
            throw new InvalidOperationException(
                $"Configuration key '{TeamOptions.SectionName}:Acp:PersonaDir' was renamed to " +
                $"'{TeamOptions.SectionName}:Acp:TeamsDir'. Update the configuration source that sets it " +
                "(environment variable, user secret, etc.) - there is no automatic fallback.");
        }

        services.Configure<TeamOptions>(configuration.GetSection(TeamOptions.SectionName));
        services.PostConfigure<TeamOptions>(options => options.DataDir = Path.GetFullPath(options.DataDir));

        services.AddSingleton<RoomEvents>();

        services.AddSingleton<ITeamDirectory, SqliteTeamDirectory>();
        services.AddSingleton<IChatStore, FileChatStore>();

        services.AddSingleton<ChatService>();

        services.AddSingleton<AgentGateway>();
        services.AddSingleton<IAgentGateway>(sp => sp.GetRequiredService<AgentGateway>());

        // Unconditional: this is what lets the /teammates page be built and tested with no agent
        // process and no tokens, regardless of whether Team:Acp:Enabled is set.
        services.AddSingleton<PersonaStore>();

        // Same instance as PersonaStore above, not a second registration - mirrors the
        // AgentGateway/IAgentGateway pair just above. A second, independently constructed PersonaStore
        // would mean a second FileSystemWatcher on the same Teams directory, which
        // PipeHostFixture.RemovePersonaSupervisorHostedService's remarks document as the cause of a
        // real, intermittent test flake once already, for the closely related PersonaSupervisor case.
        services.AddSingleton<IMentionAliasSource>(sp => sp.GetRequiredService<PersonaStore>());
        services.AddSingleton<IAgentHostFactory, DotAcpAgentHostFactory>();
        services.AddSingleton<PersonaModelStore>();
        services.AddSingleton<PersonaEffortStore>();

        // Same instance as HookStore below, not a second registration - mirrors the
        // PersonaStore/IMentionAliasSource pair above. A second, independently constructed HookStore
        // would mean a second file handle on hooks.json now and (once T4.1 adds one) a second
        // FileSystemWatcher on it, the same class of intermittent test flake PersonaStore's remarks
        // already document for the closely related PersonaSupervisor case.
        services.AddSingleton<HookStore>();
        services.AddSingleton<IHookSource>(sp => sp.GetRequiredService<HookStore>());

        // Unconditional too, and for the same reason: the probe spends nothing on its own (it never
        // calls PromptAsync), so registering it costs nothing when Team:Acp:Enabled is off. What
        // keeps it honest is WHEN it runs — the /teammates page only calls it on card-open, never on
        // page-load.
        services.AddSingleton<IModelCatalog, ModelCatalogProbe>();

        services.AddHostedService<DataInitializer>();
        services.AddHostedService<PipeServer>();
        services.AddHostedService<DemoAgentHost>();
        services.AddHostedService<PersonaSupervisor>();

        return services;
    }
}
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Demo;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Services;

namespace Agency.Huddle.App;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTeamServices(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

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
        services.AddSingleton<IAgentHostFactory, DotAcpAgentHostFactory>();
        services.AddSingleton<PersonaModelStore>();
        services.AddSingleton<PersonaEffortStore>();

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
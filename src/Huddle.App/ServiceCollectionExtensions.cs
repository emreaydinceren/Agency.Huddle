using Microsoft.Extensions.DependencyInjection.Extensions;
using Agency.Huddle.Acp.Hosting;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Appearance;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Demo;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Skills;
using Agency.Huddle.App.Teammates;

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
        services.AddSingleton<Drafts>();

        // PersonaHealth takes a TimeProvider so a test can prove that a no-op report leaves a
        // status's Since alone, rather than racing the real clock's resolution. Nothing else in the
        // application injects one, so the registration lives here; TryAdd keeps it harmless if the
        // host or a test ever supplies its own.
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<PersonaHealth>();

        // Roadmap item 8's follow set: a DI singleton for the same reason PersonaHealth just above
        // is one - it is taken straight into Chat.razor and, later, into a follow/unfollow App Tool -
        // and it never crosses the wire, so it costs no ProtocolVersion bump. See RoomFollows' own
        // doc comment for why this could not instead be a field on PersonaRunner.
        services.AddSingleton<RoomFollows>();

        // FC §6.3: resolves a Watched Folder entry (a Teammate Name, a full path, or a path
        // relative to DataDir) into a full path, or refuses it with a reason.
        services.AddSingleton<WatchedFolderResolver>();

        // FC §6.6: one JSON file per Agent under {DataDir}/file-state/, holding what that Agent
        // last saw per Room.
        services.AddSingleton<FileStateStore>();

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

        // Constructor-injected rather than newed up inline inside DotAcpAgentHostFactory (see that
        // type's own remarks): the same registration a Conformance fixture (D10, Task 10.1) replaces
        // with an in-process fake to drive a real Persona through a real host, session and tool
        // server against a scripted ACP peer, with no process spawned.
        services.AddSingleton<IAgentProcessLauncher, AgentProcessLauncher>();
        services.AddSingleton<IAgentHostFactory, DotAcpAgentHostFactory>();
        services.AddSingleton<AdapterCatalog>();
        services.AddSingleton<AdapterProfileResolver>();
        services.AddSingleton<PersonaModelStore>();
        services.AddSingleton<PersonaEffortStore>();

        // Same instance as PromptStore below, not a second registration - mirrors the
        // PersonaStore/IMentionAliasSource pair above. A second, independently constructed PromptStore
        // would mean a second file handle on prompts.json now and (once T4.1 adds one) a second
        // FileSystemWatcher on it, the same class of intermittent test flake PersonaStore's remarks
        // already document for the closely related PersonaSupervisor case.
        services.AddSingleton<PromptStore>();
        services.AddSingleton<IPromptSource>(sp => sp.GetRequiredService<PromptStore>());

        // Same reasoning as PromptStore above: a singleton, so the app never has two independently
        // constructed stores each creating {DataDir}/Skills and (once a later task adds one) each
        // running their own FileSystemWatcher over it.
        services.AddSingleton<SkillStore>();

        // No interface, same reasoning as PromptStore/SkillStore above: nothing needs to
        // substitute this, and it is built entirely from other singletons already registered
        // above (PersonaStore, ITeamDirectory, IAgentGateway), so a second, independently
        // constructed instance would cost nothing extra but would still be pointless duplication.
        services.AddSingleton<CandidateChecker>();

        // No interface, same reasoning as CandidateChecker just above: nothing needs to substitute
        // this, and its only dependency, RoomEvents, is already registered as a singleton at the top
        // of this method, so construction order is safe regardless of where in this list it sits.
        services.AddSingleton<ProposalStore>();

        // No interface, same reasoning as ProposalStore and CandidateChecker just above. Every one
        // of its own dependencies (ProposalStore, CandidateChecker, PersonaStore, ChatService,
        // ITeamDirectory) is already a singleton registered above, and none of them takes a
        // ProposalService back - construction order is safe and there is no cycle.
        services.AddSingleton<ProposalService>();

        // No interface: nothing needs to substitute this, and CSharpPrinciples.md says not to add
        // abstraction a feature has not asked for. This is state the app writes (a chosen theme, a
        // few token overrides), not host-supplied configuration, so it is registered here rather
        // than bound onto TeamOptions - see rules.md's "Collection options need no initialiser."
        services.AddSingleton<AppearanceStore>();

        // No interface, for the same reason as AppearanceStore just above: nothing needs to
        // substitute this, and a plain registration cannot produce the two-watchers-on-one-path
        // hazard the aliased registrations elsewhere in this file exist to avoid.
        services.AddSingleton<AvatarStore>();

        // Unconditional too, and for the same reason: the probe spends nothing on its own (it never
        // calls PromptAsync), so registering it costs nothing when Team:Acp:Enabled is off. What
        // keeps it honest is WHEN it runs — the /teammates page only calls it on card-open, never on
        // page-load.
        services.AddSingleton<IAdapterProbeRunner, AdapterProcessProbeRunner>();
        services.AddSingleton<IModelCatalog, ModelCatalogProbe>();

        // No interface, same reasoning as AppearanceStore/AvatarStore above: nothing needs to
        // substitute this, and it is built entirely from PersonaStore, already registered above.
        // Deliberately a plain singleton rather than folded into the hosted BuiltinTeammateSeeder
        // below - TeammateCard's "Reset to default" (Spec §6.12) needs to inject it directly, and a
        // component must never depend on an IHostedService.
        services.AddSingleton<BuiltinTeammateReset>();

        services.AddHostedService<DataInitializer>();
        services.AddHostedService<PipeServer>();
        services.AddHostedService<DemoAgentHost>();

        // Registered immediately before PersonaSupervisor's own hosted service, and never behind
        // Team:Acp:Enabled: hosted services start in registration order (Spec §10), so this is what
        // guarantees the supervisor's first reconciliation already sees the Chief of Staff the
        // seeder just wrote, rather than racing a later PersonasChanged (Spec §6.12 Implementation
        // notes).
        services.AddHostedService<BuiltinTeammateSeeder>();

        // Same instance as the hosted service, not a second registration - mirrors every other pair
        // in this file (AgentGateway/IAgentGateway, PersonaStore/IMentionAliasSource, PromptStore/IPromptSource).
        // A Restart button (T7.2) needs to reach the very PersonaSupervisor the host is running, not a
        // second, independently constructed one - the same class of intermittent test flake those other
        // pairs' remarks already document, this time for a component resolving it directly rather than
        // for a second FileSystemWatcher.
        services.AddSingleton<PersonaSupervisor>();
        services.AddHostedService(sp => sp.GetRequiredService<PersonaSupervisor>());

        // Unconditional, unlike PersonaSupervisor's hosted service above: the Agent row a rename
        // cascades from may exist from an earlier session or a raw pipe client, so a Persona rename
        // must cascade into the Team Directory even when Team:Acp:Enabled is false and no runner is
        // ever started - and an Avatar exists whether or not any Agent has ever connected at all, so
        // its rename/removal cascade needs this running unconditionally too. A singleton nobody
        // resolves never subscribes to PersonaStore.PersonaRenamed or PersonaStore.PersonaRemoved,
        // so this must be constructed - hence AddHostedService rather than a plain AddSingleton.
        //
        // Registered by type, not by the sp => sp.GetRequiredService<PersonaSupervisor>() factory
        // shape used just above: nothing else in the app needs to resolve this same instance the way
        // a Restart button resolves the running PersonaSupervisor, so there is no second registration
        // to keep in sync here. It also keeps this registration out of
        // PipeHostFixture.RemovePersonaSupervisorHostedService's factory-based search, which already
        // fails loudly - by design - the day a second factory-registered IHostedService shows up.
        services.AddHostedService<PersonaRenameCascade>();

        return services;
    }
}
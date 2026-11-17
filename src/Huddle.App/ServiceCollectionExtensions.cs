using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Agency.Huddle.Acp.Hosting;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.AgentsMdChanges;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Acp.Sessions.TurnHooks;
using Agency.Huddle.App.Appearance;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Circuits;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Demo;
using Agency.Huddle.App.Elicitation;
using Agency.Huddle.App.Helpers;
using Agency.Huddle.App.Helpers.AgentsMdChange;
using Agency.Huddle.App.Helpers.NeedsHuman;
using Agency.Huddle.App.Helpers.TeamMemory;
using Agency.Huddle.App.HiringPool;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Memory;
using Agency.Huddle.App.Modules;
using Agency.Huddle.App.NeedsYou;
using Agency.Huddle.App.Profile;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Sidebar;
using Agency.Huddle.App.SecondBrain;
using Agency.Huddle.App.Skills;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.Agents;
using Agency.Huddle.App.Teams;
using Agency.Huddle.App.TeamTypes;

namespace Agency.Huddle.App;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTeamServices(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        ThrowOnRetiredConfigurationKeys(configuration);

        return services
            .AddOptionsAndChatCore(configuration)
            .AddFeedServices(configuration)
            .AddFileAndStoreSingletons()
            .AddIdentityAndAgentHostServices()
            .AddTaskPromptAndSkillStores()
            .AddSkillSourceServices()
            .AddProposalQuestionAndElicitationServices()
            .AddHiringAndProcessServices()
            .AddAppearanceAvatarAndLibraryBasics()
            .AddLibraryFileAndUploadServices()
            .AddTeamFolderCatalogAndSprintServices()
            .AddCircuitTabSidebarSearchAndReferenceServices()
            .AddProbeAndRenameServices()
            .AddStartupHostedServices()
            .AddBackgroundHostedServices()
            .AddHelperServices()
            .AddFeatureModules();
    }

    /// <summary>Registers every Feature Module's services, in the catalog's order.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    private static IServiceCollection AddFeatureModules(this IServiceCollection services)
    {
        foreach (IFeatureModule module in ModuleCatalog.All)
        {
            module.AddServices(services);
        }

        return services;
    }

    /// <summary>
    /// Registers the Helpers (Helpers Spec §5): the stores, the budget, the runner and dispatcher, and the Needs Human Helper
    /// with its trigger. Unconditional like the probe: nothing here spends until an Agent saves a Task and
    /// <c>Team:Acp:Enabled</c> is on; the dispatcher checks that at each fire.
    /// </summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    private static IServiceCollection AddHelperServices(this IServiceCollection services)
    {
        services.AddSingleton<HelperUsageStore>();
        services.AddSingleton<HelperStatusStore>();
        services.AddSingleton<HelperSettingsStore>();
        services.AddSingleton<NeedsHumanOverrideStore>();
        services.AddSingleton<HelperBudget>();
        services.AddSingleton<HelperSettingsResolver>();
        services.AddSingleton<IHelperProcessRunner, AdapterProcessHelperRunner>();
        services.AddSingleton<HelperRunner>();

        services.AddSingleton<NeedsHumanHelper>();
        services.AddSingleton<IHelperAgent>(static sp => sp.GetRequiredService<NeedsHumanHelper>());
        services.AddSingleton<IHelperInvoker>(static sp => new HelperInvoker<NeedsHumanInput, NeedsHumanVerdict>(
            sp.GetRequiredService<NeedsHumanHelper>(),
            sp.GetRequiredService<HelperRunner>(),
            sp.GetRequiredService<ILoggerFactory>().CreateLogger<HelperInvoker<NeedsHumanInput, NeedsHumanVerdict>>()));

        // Team Memory Helper spec §6.2: the trigger reaches the dispatcher through a factory, because the dispatcher depends on
        // this Helper's invoker and the Helper on the trigger.
        services.AddSingleton<MemoryWriter>();
        services.AddSingleton<TeamMemoryInputBuilder>();
        services.AddSingleton(static sp => new TeamMemoryTrigger(
            sp.GetRequiredService<TeamMemoryInputBuilder>(),
            sp.GetRequiredService<HelperDispatcher>,
            sp.GetRequiredService<ILogger<TeamMemoryTrigger>>()));
        services.AddSingleton<TeamMemoryHelper>();
        services.AddSingleton<IHelperAgent>(static sp => sp.GetRequiredService<TeamMemoryHelper>());
        services.AddSingleton<IHelperInvoker>(static sp => new HelperInvoker<TeamMemoryInput, TeamMemoryVerdict>(
            sp.GetRequiredService<TeamMemoryHelper>(),
            sp.GetRequiredService<HelperRunner>(),
            sp.GetRequiredService<ILoggerFactory>().CreateLogger<HelperInvoker<TeamMemoryInput, TeamMemoryVerdict>>()));

        // AGENTS.md Changes spec §6.2: the ledger reaches the dispatcher and the Helper through factories, because the dispatcher
        // depends on this Helper's invoker, the Helper on the ledger, and the registry on every Helper.
        services.AddSingleton(static sp => new AgentsMdLedger(
            sp.GetRequiredService<HelperDispatcher>,
            sp.GetRequiredService<AgentsMdChangeHelper>,
            sp.GetRequiredService<HelperSettingsResolver>(),
            sp.GetRequiredService<HelperBudget>(),
            sp.GetRequiredService<IOptions<TeamOptions>>(),
            sp.GetRequiredService<ILogger<AgentsMdLedger>>(),
            sp.GetService<TimeProvider>()));
        services.AddSingleton<AgentsMdChangeHelper>();
        services.AddSingleton<IHelperAgent>(static sp => sp.GetRequiredService<AgentsMdChangeHelper>());
        services.AddSingleton<IHelperInvoker>(static sp => new HelperInvoker<AgentsMdChangeInput, AgentsMdChangeVerdict>(
            sp.GetRequiredService<AgentsMdChangeHelper>(),
            sp.GetRequiredService<HelperRunner>(),
            sp.GetRequiredService<ILoggerFactory>().CreateLogger<HelperInvoker<AgentsMdChangeInput, AgentsMdChangeVerdict>>()));
        services.AddSingleton<HelperRegistry>();
        services.AddSingleton<HelperFeed>();

        services.AddSingleton<HelperDispatcher>();
        services.AddHostedService(static sp => sp.GetRequiredService<HelperDispatcher>());
        services.AddHostedService<AgentsMdWatcher>();
        services.AddSingleton<NeedsHumanTrigger>();
        services.AddHostedService(static sp => sp.GetRequiredService<NeedsHumanTrigger>());
        return services;
    }

    /// <summary>
    /// Registers which AGENTS.md files apply to a named Agent (from its Team labels and the catalog's spelling, §6.7), and the hook the
    /// supervisor puts first in every Persona's Turn pipeline with the source and renderer behind it (§6.10).
    /// </summary>
    private static void AddAgentsMdScopesAndTurnHook(IServiceCollection services)
    {
        services.AddSingleton<AgentsMdScopes>();
        services.AddSingleton(static sp => new AgentsMdNoticeRenderer(
            sp.GetRequiredService<IPromptSource>(),
            sp.GetRequiredService<IOptions<TeamOptions>>().Value.AgentsMd));
        services.AddSingleton(static sp => new AgentsMdNoticeSource(
            sp.GetRequiredService<AgentsMdLedger>(),
            sp.GetRequiredService<AgentsMdNoticeRenderer>(),
            sp.GetRequiredService<IOptions<TeamOptions>>().Value.AgentsMd,
            sp.GetRequiredService<ILogger<AgentsMdNoticeSource>>()));
        services.AddSingleton<AgentsMdTurnHook>();
        services.AddSingleton<HuddleMemoryTurnHook>();
        services.AddSingleton<TeamRenamedTurnHook>();

        // The pipeline sorts these by its own production order, so the order here does not matter.
        services.AddSingleton<ITurnHook>(static sp => sp.GetRequiredService<TeamRenamedTurnHook>());
        services.AddSingleton<ITurnHook>(static sp => sp.GetRequiredService<AgentsMdTurnHook>());
        services.AddSingleton<ITurnHook>(static sp => sp.GetRequiredService<HuddleMemoryTurnHook>());
    }

    /// <summary>
    /// Registers the Feed (ADR-0063): the store and the hub always, so the UI can resolve them, and the queue, the writer and the
    /// startup reconciler only while <c>Team:Feed:Enabled</c> is true. When it is off, <see cref="Feed.NullFeedPublisher"/> takes
    /// every producer's calls and nothing is written.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration, read for the one switch that decides the shape of the graph.</param>
    private static IServiceCollection AddFeedServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<Feed.FeedStore>();
        services.AddSingleton<Feed.FeedEvents>();
        if (!(configuration.GetSection("Team:Feed").GetValue<bool?>("Enabled") ?? true))
        {
            services.AddSingleton<Feed.IFeedPublisher, Feed.NullFeedPublisher>();
            return services;
        }

        services.AddSingleton<Feed.FeedQueue>();
        services.AddSingleton<Feed.IFeedPublisher>(static sp => sp.GetRequiredService<Feed.FeedQueue>());
        services.AddHostedService<Feed.FeedWriter>();
        services.AddHostedService<TaskFeedStartup>();

        // The publisher subscribes to the Task events in its constructor, so it must be built before the first Task write: AddHostedService has the host build it at start.
        services.AddSingleton<TaskFeedPublisher>();
        services.AddHostedService(static sp => sp.GetRequiredService<TaskFeedPublisher>());
        services.AddHostedService<HelperFeedStartup>();
        services.AddSingleton(static sp => new NeedsYou.HeldMentions(
            sp.GetRequiredService<Services.RoomEvents>(),
            sp.GetRequiredService<Feed.IFeedPublisher>(),
            sp.GetRequiredService<Feed.FeedStore>(),
            sp.GetRequiredService<Services.ChatService>(),
            sp.GetRequiredService<Data.ITeamDirectory>(),
            sp.GetRequiredService<Data.IChatStore>(),
            name => sp.GetRequiredService<Acp.PersonaStore>().Get(name) is not null,
            sp.GetRequiredService<Prompts.IPromptSource>(),
            sp.GetRequiredService<IOptions<TeamOptions>>(),
            sp.GetRequiredService<ILogger<NeedsYou.HeldMentions>>()));
        services.AddSingleton<NeedsYou.IHeldMentionWake>(static sp => sp.GetRequiredService<NeedsYou.HeldMentions>());
        services.AddHostedService(static sp => sp.GetRequiredService<NeedsYou.HeldMentions>());
        return services;
    }

    /// <summary>Fails start-up when a retired configuration key is still set, naming its replacement.</summary>
    /// <param name="configuration">The application configuration to inspect.</param>
    /// <exception cref="InvalidOperationException">A retired key is still set.</exception>
    private static void ThrowOnRetiredConfigurationKeys(IConfiguration configuration)
    {
        // Acp.PersonaDir was renamed to Acp.TeamsDir (the "Teams" rename), and Acp.TeamsDir was in
        // turn renamed to Acp.AgentsDir (Spec §6.15, the Agents-beside-Teams layout), with no
        // back-compat fallback either time: nothing in appsettings*.json still sets either old key,
        // so a fallback would be dead weight. But a value left behind in a user secret or an
        // environment variable would otherwise bind to nothing, and PersonaStore would quietly scan
        // an empty default "Agents" folder - zero agents, no exception, no log anywhere. That
        // silent-degradation shape is exactly what docs/engineering/traps.md exists to catch, so fail
        // loudly at startup instead, naming the new key. This runs ahead of Configure<TeamOptions>
        // below, in the one place both this app's Program.cs and every test that composes it
        // (TeamWebApplicationFactory) are guaranteed to pass through.
        if (configuration[$"{TeamOptions.SectionName}:Acp:PersonaDir"] is not null)
        {
            throw new InvalidOperationException(
                $"Configuration key '{TeamOptions.SectionName}:Acp:PersonaDir' was renamed to " +
                $"'{TeamOptions.SectionName}:Acp:AgentsDir'. Update the configuration source that sets it " +
                "(environment variable, user secret, etc.) - there is no automatic fallback.");
        }

        if (configuration[$"{TeamOptions.SectionName}:Acp:TeamsDir"] is not null)
        {
            throw new InvalidOperationException(
                $"Configuration key '{TeamOptions.SectionName}:Acp:TeamsDir' was renamed to " +
                $"'{TeamOptions.SectionName}:Acp:AgentsDir'. Update the configuration source that sets it " +
                "(environment variable, user secret, etc.) - there is no automatic fallback.");
        }

        // Acp.TeammatesDir was renamed to Acp.AgentsDir with the Teammate-to-Agent rename; the
        // start-up migration moves the folder, but a key left in a secret or environment variable
        // would bind to nothing, so fail loudly here for the same reason as the guards above.
        if (configuration[$"{TeamOptions.SectionName}:Acp:TeammatesDir"] is not null)
        {
            throw new InvalidOperationException(
                $"Configuration key '{TeamOptions.SectionName}:Acp:TeammatesDir' was renamed to " +
                $"'{TeamOptions.SectionName}:Acp:AgentsDir'. Update the configuration source that sets it " +
                "(environment variable, user secret, etc.) - there is no automatic fallback.");
        }

        // Team:Tasks:Dir retired with no fallback (Library Task G1.2, ADR-0030): the Tasks scan
        // root is now Team:Teams:Dir, the same folder each Team's _tasks/ lives under. A value
        // left behind would otherwise bind to nothing and TaskStore would quietly scan the
        // default "Teams" folder - the same silent-degradation shape as the guards above.
        if (configuration[$"{TeamOptions.SectionName}:Tasks:Dir"] is not null)
        {
            throw new InvalidOperationException(
                $"Configuration key '{TeamOptions.SectionName}:Tasks:Dir' was replaced by " +
                $"'{TeamOptions.SectionName}:Teams:Dir'. Tasks now live in each Team folder's _tasks/ folder; " +
                "remove the key (the start-up migration reads {DataDir}/Tasks). There is no automatic fallback.");
        }
    }

    /// <summary>Binds <see cref="TeamOptions"/> and registers the chat core, per-Persona health and spend, follows and the activity singletons.</summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The same collection, for chaining.</returns>
    private static IServiceCollection AddOptionsAndChatCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TeamOptions>(configuration.GetSection(TeamOptions.SectionName));
        services.PostConfigure<TeamOptions>(options =>
        {
            options.DataDir = Path.GetFullPath(options.DataDir);
            LayoutGuard.ValidateTeamsAndAgents(options);
        });

        services.AddSingleton<RoomEvents>();

        services.AddSingleton<ITeamDirectory, SqliteTeamDirectory>();
        services.AddSingleton<IChatStore, FileChatStore>();

        // A factory because ChatService.TeamCatalog is an internal settable property (ITeamCatalog is
        // internal, the constructor public); ITeamCatalog is registered further down and resolved lazily.
        // ActivatorUtilities fills the constructor, so a new constructor parameter needs no edit here.
        services.AddSingleton(sp =>
        {
            ChatService service = ActivatorUtilities.CreateInstance<ChatService>(sp);
            service.TeamCatalog = sp.GetRequiredService<ITeamCatalog>();
            service.UploadFolders = sp.GetRequiredService<Uploads.IRoomUploadFolders>();
            service.LastCatchUp = sp.GetRequiredService<LastCatchUpStore>();
            service.FeedPublisher = sp.GetService<Feed.IFeedPublisher>();
            return service;
        });
        services.TryAddSingleton<ToolCallLogWriter>();
        services.AddSingleton<Drafts>();

        // PersonaHealth takes a TimeProvider so a test can prove that a no-op report leaves a
        // status's Since alone, rather than racing the real clock's resolution. Nothing else in the
        // application injects one, so the registration lives here; TryAdd keeps it harmless if the
        // host or a test ever supplies its own.
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<PersonaHealth>();

        // Display-only Spend per Persona since the app started, reached the same way PersonaHealth
        // is: a DI singleton a runner writes to and the Agent card reads. It limits nothing.
        services.AddSingleton<PersonaSpend>();
        services.AddSingleton<PersonaCommands>();

        // Roadmap item 8's follow set: a DI singleton for the same reason PersonaHealth just above
        // is one - it is taken straight into Chat.razor and, later, into a follow/unfollow App Tool -
        // and it never crosses the wire, so it costs no ProtocolVersion bump. See RoomFollows' own
        // doc comment for why this could not instead be a field on PersonaRunner.
        services.AddSingleton<RoomFollows>();

        // D27, RS §6.7, finding P-7: a singleton for the same reason RoomFollows just above is one -
        // PostMessageTool takes it from DI, and it outlives any one PersonaRunner or RoomSession, so
        // a forgotten entry self-heals on restart through OwnPosts.ClearAgent.
        services.AddSingleton<OwnPosts>();

        // Spec §10.8: which Agent has a Turn running in which Room, fed by RoomSession and read by
        // the Tasks UI's "AI reacting" badge. A leaf singleton, same shape as OwnPosts just above.
        services.AddSingleton<TurnActivity>();

        // How full each Agent's context window is in each Room, fed by RoomSession and read by the
        // Members panel's context meter. A leaf singleton, same shape as TurnActivity just above.
        services.AddSingleton<ContextFill>();

        return services;
    }

    /// <summary>Registers the task activity, watched-folder, file-state and room-session stores, the agent gateway, the catalogs and the file change tracker.</summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    private static IServiceCollection AddFileAndStoreSingletons(this IServiceCollection services)
    {
        // Spec §10.6-§10.7: per-Task wake history and the Agent-wake budget that guards against a
        // looping Agent. A leaf singleton, same shape as TurnActivity just above.
        services.AddSingleton<TaskActivity>();

        // RS §6.6: a singleton like RoomFollows above - touches files, never the Team Directory -
        // one JSON file per Agent under {DataDir}/room-sessions/, holding what is kept about each
        // Room Session so it can be resumed.
        services.AddSingleton<RoomSessionStore>();
        services.AddSingleton<LastCatchUpStore>();
        services.AddSingleton<FreshStartNotes>();

        // LR §5.2: the Session settings (catch-up cap and budget), an overrides-only watched file
        // {DataDir}/session-settings.json; consumers read it through ISessionSettings.
        services.AddSingleton<SessionSettingsStore>();
        services.AddSingleton<ISessionSettings>(sp => sp.GetRequiredService<SessionSettingsStore>());

        services.AddSingleton<AgentGateway>();
        services.AddSingleton<IAgentGateway>(sp => sp.GetRequiredService<AgentGateway>());

        services.AddSingleton<AgentPaths>();
        services.AddSingleton<HiringPoolCatalog>();
        services.AddSingleton<CustomRoleStore>();
        services.AddSingleton<TeamTypeCatalog>();
        services.AddSingleton<IConnectorStatus, FileConnectorStatus>();

        // Shared between PersonaRenameCascade (signals around an Agent-folder move) and
        // DotAcpAgentHostFactory (waits on it before creating a Persona's Work Dir) - corrections-B2
        // item 20.
        services.AddSingleton<AgentFolderMoves>();

        // Unconditional: this is what lets the /agents page be built and tested with no agent
        // process and no tokens, regardless of whether Team:Acp:Enabled is set.
        services.AddSingleton<PersonaStore>();

        return services;
    }

    /// <summary>Registers the mention alias source, the Human profile, the agent host factory and the per-Persona stores.</summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    private static IServiceCollection AddIdentityAndAgentHostServices(this IServiceCollection services)
    {
        // Same instance as PersonaStore above, not a second registration - mirrors the
        // AgentGateway/IAgentGateway pair just above. A second, independently constructed PersonaStore
        // would mean a second FileSystemWatcher on the same Teams directory, which
        // PipeHostFixture.RemovePersonaSupervisorHostedService's remarks document as the cause of a
        // real, intermittent test flake once already, for the closely related PersonaSupervisor case.
        services.AddSingleton<IMentionAliasSource>(sp => new CompositeMentionAliasSource(
            sp.GetRequiredService<PersonaStore>(),
            sp.GetRequiredService<IHumanIdentity>(),
            sp.GetRequiredService<ILogger<CompositeMentionAliasSource>>()));

        // The Human's profile (profile.json), served as the live IHumanIdentity every Human-Name
        // reader uses. Same instance behind both registrations, like PersonaStore just above, so
        // there is one watcher on the file.
        services.AddSingleton<HumanProfileStore>();
        services.AddSingleton<IHumanIdentity>(sp => sp.GetRequiredService<HumanProfileStore>());
        services.AddSingleton<HumanProfileValidator>();

        // Constructor-injected rather than newed up inline inside DotAcpAgentHostFactory (see that
        // type's own remarks): the same registration a Conformance fixture (D10, Task 10.1) replaces
        // with an in-process fake to drive a real Persona through a real host, session and tool
        // server against a scripted ACP peer, with no process spawned.
        services.AddSingleton<IAgentProcessLauncher, AgentProcessLauncher>();
        services.AddSingleton<DotAcpAgentHostFactory>();
        services.AddSingleton<IAgentHostFactory>(sp => sp.GetRequiredService<DotAcpAgentHostFactory>());
        services.AddSingleton<PersonaPromptPreview>();
        services.AddSingleton<AdapterCatalog>();
        services.AddSingleton<AdapterProfileResolver>();
        services.AddSingleton<PersonaModelStore>();
        services.AddSingleton<CadenceStateStore>();
        services.AddSingleton<RawBaselineStore>();
        services.AddSingleton<PersonaEffortStore>();
        services.AddSingleton<PersonaWorkModeStore>();
        services.AddSingleton<PersonaConnectorsStore>();
        services.AddSingleton<PersonaBrowserStore>();
        services.AddSingleton<BrowserLaunchFactsSource>();
        services.AddSingleton<ConnectorsNoticeStore>();
        services.AddSingleton<PersonaSpendStore>();
        services.AddSingleton<RoomSpendStore>();
        services.AddSingleton<Agency.Huddle.App.Teams.RecordsBundleBuilder>();
        services.AddSingleton<Agency.Huddle.App.Teams.RetroFileStore>();

        // Retro proposals (Task 76): held under _proposals/retro/ in the Team folder; Huddle applies an accepted edit.
        services.AddSingleton<Agency.Huddle.App.Teams.RetroProposalStore>();
        services.AddSingleton<Agency.Huddle.App.Teams.RetroProposalService>();
        services.AddSingleton<TaskIdAllocator>();
        services.AddSingleton<ITaskPrefixes>(static provider => provider.GetRequiredService<TaskIdAllocator>());

        return services;
    }

    /// <summary>Registers the task, prompt and skill stores and their forwarding registrations.</summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    private static IServiceCollection AddTaskPromptAndSkillStores(this IServiceCollection services)
    {
        // No interface, same reasoning as AvatarStore/PromptStore above: a second, independently
        // constructed TaskStore would scan the Tasks folder twice and (once a later task adds one)
        // run a second FileSystemWatcher over it. TaskEvents and TaskService are registered here too
        // once the tasks that add them (D5's watcher, D6) land - this line is theirs to extend, not
        // duplicate.
        services.AddSingleton<TaskStore>();
        services.AddSingleton<ITaskReferenceResolver>(sp => sp.GetRequiredService<TaskStore>());

        // A plain hub with no dependencies of its own (Spec §9.5). TaskService is a lazy singleton
        // (Settled corrections-B2 D6 item 10): nothing constructs it until 6.6.i injects it into the
        // hosted PersonaRenameCascade below, so registering it here costs nothing before then.
        services.AddSingleton<TaskEvents>();
        services.AddSingleton<TaskService>();

        // Same instance as PromptStore below, not a second registration - mirrors the
        // PersonaStore/IMentionAliasSource pair above. A second, independently constructed PromptStore
        // would mean a second file handle on the Prompts folder now and (once T4.1 adds one) a second
        // FileSystemWatcher on it, the same class of intermittent test flake PersonaStore's remarks
        // already document for the closely related PersonaSupervisor case.
        services.AddSingleton<PromptStore>();
        services.AddSingleton<IPromptSource>(sp => sp.GetRequiredService<PromptStore>());

        // Same reasoning as PromptStore above: a singleton, so the app never has two independently
        // constructed stores each creating {DataDir}/Skills and (once a later task adds one) each
        // running their own FileSystemWatcher over it.
        services.AddSingleton<SkillStore>();

        return services;
    }

    /// <summary>Registers the Skill Sources registry, its stores, the move, copy and install-file services.</summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    private static IServiceCollection AddSkillSourceServices(this IServiceCollection services)
    {
        // Skill Sources (Skill Packs spec §6.5): the registry of SKILLS.md and the stores beside it. Plain singletons; the
        // watcher below is the hosted half and the only thing that rebuilds the registry besides Reload.
        services.AddSingleton<Agency.Huddle.App.SkillSources.SkillSourcesSettings>();
        services.AddSingleton<Agency.Huddle.App.SkillSources.HiddenStore>();
        services.AddSingleton<Agency.Huddle.App.SkillSources.SourceLog>();
        services.AddSingleton<Agency.Huddle.App.SkillSources.SkillsMdWriter>();
        services.AddSingleton<Agency.Huddle.App.SkillSources.RoleLister>();
        services.AddSingleton<Agency.Huddle.App.SkillSources.SkillRegistry>();
        services.AddSingleton<Agency.Huddle.App.Teams.ISkillAvailability>(static sp => sp.GetRequiredService<Agency.Huddle.App.SkillSources.SkillRegistry>());
        services.AddSingleton<Agency.Huddle.App.SkillSources.Move.IMoveFiles, Agency.Huddle.App.SkillSources.Move.PhysicalMoveFiles>();
        services.AddSingleton<Agency.Huddle.App.SkillSources.Move.MoveReportStore>();
        services.AddSingleton<Agency.Huddle.App.SkillSources.Move.SkillSourcesMove>();
        services.AddSingleton<Agency.Huddle.App.SkillSources.ISkillSourcesMove>(static sp => sp.GetRequiredService<Agency.Huddle.App.SkillSources.Move.SkillSourcesMove>());
        services.AddSingleton<Agency.Huddle.App.SkillSources.RegistryWatcher>();
        services.AddSingleton<Agency.Huddle.App.SkillSources.ISkillCopier, Agency.Huddle.App.SkillSources.SkillCopier>();
        services.AddSingleton<Agency.Huddle.App.SkillSources.InstallFileWriter>();
        services.AddSingleton<Agency.Huddle.App.SkillSources.IInstallFileLocation>(static sp => sp.GetRequiredService<Agency.Huddle.App.SkillSources.InstallFileWriter>());
        services.AddSingleton<Agency.Huddle.App.SkillSources.SkillPromptBuilder>();
        services.AddSingleton<Agency.Huddle.App.SkillSources.SkillSourcesRemover>();
        services.AddSingleton<Agency.Huddle.App.SkillSources.PackUpdater>();
        services.AddScoped<Agency.Huddle.App.SkillSources.PathFormatChoice>();

        return services;
    }

    /// <summary>Registers the candidate checker and the proposal, question and elicitation stores and services.</summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    private static IServiceCollection AddProposalQuestionAndElicitationServices(this IServiceCollection services)
    {
        // No interface, same reasoning as PromptStore/SkillStore above: nothing needs to
        // substitute this, and it is built entirely from other singletons already registered
        // above (PersonaStore, ITeamDirectory, IAgentGateway), so a second, independently
        // constructed instance would cost nothing extra but would still be pointless duplication.
        services.AddSingleton<CandidateChecker>();

        // No interface, same reasoning as CandidateChecker just above: nothing needs to substitute
        // this, and its only dependency, RoomEvents, is already registered as a singleton at the top
        // of this method, so construction order is safe regardless of where in this list it sits.
        services.AddSingleton<ProposalStore>();

        // No interface, same reasoning as ProposalStore: a leaf singleton whose only dependency,
        // RoomEvents, is registered at the top of this method. ChatService takes it, so it must exist
        // before ChatService does, and it takes nothing back.
        services.AddSingleton<WaitingQuestionStore>();
        services.AddSingleton(static provider => new QuestionStore(
            provider.GetRequiredService<RoomEvents>(),
            provider.GetRequiredService<WaitingQuestionStore>(),
            provider.GetService<ILogger<QuestionStore>>()));

        // No interface, same reasoning as ProposalService: nothing needs to substitute it, and its
        // dependencies (QuestionStore, ChatService, ITeamDirectory) are all singletons registered above.
        services.AddSingleton<QuestionService>();

        // No interface, same reasoning as QuestionStore: a leaf singleton whose only dependency, RoomEvents,
        // is registered at the top of this method. ChatService takes it (an optional constructor parameter,
        // so every direct construction in a test keeps compiling), and the bridge and service below share
        // this one instance - a second store would make a card the Human sees one the Turn cannot cancel.
        services.AddSingleton<ElicitationStore>();

        // No interface, same reasoning as QuestionService: its dependencies (ElicitationStore, ChatService,
        // ITeamDirectory) are all singletons registered above.
        services.AddSingleton<ElicitationService>();

        // The one implementation of the interface RoomSession answers an agent's form with. PersonaSupervisor
        // takes it as an optional constructor parameter and hands it to every runner it starts, which is how
        // a real Room Session reaches the Human; a supervisor built without one answers every form as cancelled.
        services.AddSingleton<IElicitationBridge, RoomElicitationBridge>();

        // No interface, same reasoning as ProposalStore and CandidateChecker just above. Every one
        // of its own dependencies (ProposalStore, CandidateChecker, PersonaStore, ChatService,
        // ITeamDirectory) is already a singleton registered above, and none of them takes a
        // ProposalService back - construction order is safe and there is no cycle.
        services.AddSingleton<ProposalService>();

        return services;
    }

    /// <summary>Registers the hire-limit guard and the hiring, onboarding and process-gate services.</summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    private static IServiceCollection AddHiringAndProcessServices(this IServiceCollection services)
    {
        // The one hire-limit check ProposalService, ProposeAgentsTool and ProposalCard share. The
        // live-process count reads PersonaSupervisor lazily, per call, so registering it here (before
        // the supervisor) creates no construction cycle.
        services.AddSingleton(sp => new HireLimitGuard(
            sp.GetRequiredService<ITeamCatalog>(),
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<TeamOptions>>(),
            () => sp.GetService<PersonaSupervisor>()?.RunningHostCount ?? 0));

        // Hiring a role-based Team. The start gate is the supervisor, resolved lazily through a factory
        // so HireService does not force PersonaSupervisor to construct before it is registered below;
        // CardResolver and TierModelPicker are static and need no registration.
        services.AddSingleton<Agency.Huddle.App.Teams.IPersonaStartGate>(sp => sp.GetService<PersonaSupervisor>() as Agency.Huddle.App.Teams.IPersonaStartGate ?? new Agency.Huddle.App.Teams.NoHostStartGate());
        services.AddSingleton<Agency.Huddle.App.Teams.ProcessProvisioner>();
        services.AddSingleton<Agency.Huddle.App.Teams.IOnboardingPoster, Agency.Huddle.App.Teams.ChatOnboardingPoster>();
        services.AddSingleton<Agency.Huddle.App.Acp.Tools.IRequestNotifier, Agency.Huddle.App.Acp.Tools.RequestNotifier>();
        services.AddSingleton<Agency.Huddle.App.Teams.TeamOnboarding>();
        services.AddSingleton<Agency.Huddle.App.Teams.HireService>();
        services.AddSingleton<Agency.Huddle.App.Teams.IHireContext, Agency.Huddle.App.Teams.HireContext>();
        services.AddSingleton<Agency.Huddle.App.Teams.RoleHirer>();

        // The Team's process gates; TaskService asks it before every Task status change (PR-7).
        services.AddSingleton<Agency.Huddle.App.Teams.ProcessGateService>();

        // The Needs You level per scope (ADR-0088): every scope's level and cap live in team.db, under the Team's or Project's id.
        services.AddSingleton<ScopeIdStore>();
        services.AddSingleton<TaskScopeStore>(); // the scope Huddle placed each Task in; a Task never takes a laxer level than it.
        services.AddSingleton<NeedsYouLevelStore>();
        services.AddSingleton<NeedsYouCapStore>();
        services.AddSingleton<NeedsYouSpendStore>(); // the USD each scope has been charged, so a member leaving never lowers a count.
        services.AddSingleton<Agency.Huddle.App.Teams.NeedsYouCaps>(); // the spend cap's counting and its Yes/No question.
        services.AddSingleton<Agency.Huddle.App.Teams.NeedsYouLevels>();
        services.AddHostedService<Agency.Huddle.App.Teams.NeedsYouCapWatcher>(); // opens the question as spend reaches a cap.
        services.AddSingleton<SprintApprover>(); // propose_sprint approves a Sprint at once when every Task runs inside the Team.
        services.AddSingleton<Agency.Huddle.App.Tasks.IRequestConfirmers, Agency.Huddle.App.Tasks.RequestConfirmers>();

        // The Proof of a Task: its Definition of Done evaluated against the live stores (HG, ADR-0062).
        services.AddSingleton<Agency.Huddle.App.Tasks.HumanGate.ProofContextBuilder>();
        services.AddSingleton<Agency.Huddle.App.Tasks.HumanGate.IProofSource, Agency.Huddle.App.Tasks.HumanGate.ProofSource>();

        // Re-evaluates a gated Task when a Proof input outside its file changes: a REVIEWED history entry, a tagged child done (#593).
        services.AddHostedService<Agency.Huddle.App.Tasks.HumanGate.ProofInputWatcher>();

        // Asked by WorkDirPermissionHandler before an Agent's git merge or git push (PR-7).
        services.AddSingleton<IMergeGate, Agency.Huddle.App.Teams.ProcessMergeGate>();
        services.AddSingleton<Agency.Huddle.App.Teams.TeamDuplicator>();

        return services;
    }

    /// <summary>Registers the appearance and avatar stores, the avatar library and the Library root and path resolvers.</summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    private static IServiceCollection AddAppearanceAvatarAndLibraryBasics(this IServiceCollection services)
    {
        // No interface: nothing needs to substitute this, and CSharpPrinciples.md says not to add
        // abstraction a feature has not asked for. This is state the app writes (a chosen theme, a
        // few token overrides), not host-supplied configuration, so it is registered here rather
        // than bound onto TeamOptions - see rules.md's "Collection options need no initialiser."
        services.AddSingleton<AppearanceStore>();

        // No interface, for the same reason as AppearanceStore just above: nothing needs to
        // substitute this, and a plain registration cannot produce the two-watchers-on-one-path
        // hazard the aliased registrations elsewhere in this file exist to avoid.
        services.AddSingleton<AvatarStore>();

        // Avatar Library: both stateless. The locator only answers "is a library configured, and where",
        // and the loader reads master.json and one image per call, so neither holds a catalogue between
        // requests (requirement R-1). The interface exists because the picker dialog is tested against a
        // fake loader, and a sealed class cannot be mocked. The library that ships with the app is the default
        // when Team:AvatarLibrary:Dir is unset; a configured Dir replaces it.
        services.AddSingleton(static provider => new AvatarLibraryLocator(provider.GetRequiredService<IOptions<TeamOptions>>(), AvatarLibraryLocator.BundledDir));
        services.AddSingleton<IAvatarLibraryLoader, AvatarLibraryLoader>();

        // No interface, same reasoning as AvatarStore just above: nothing needs to substitute
        // this, and a plain registration cannot produce the two-watchers-on-one-path hazard the
        // aliased registrations elsewhere in this file exist to avoid. Registered unconditionally
        // (Team:Library:Enabled only gates the UI, per corrections-B3 4.1.i item 13); the
        // constructor never throws on bad configuration.
        services.AddSingleton<LibraryRootStore>();

        // No interface, same reasoning as AvatarStore just above: nothing needs to substitute
        // this. Registered right after LibraryRootStore (corrections-B3 4.2.i item 28): it depends
        // on nothing else, and the constructor never throws on bad configuration either.
        services.AddSingleton<LibraryPathResolver>();

        return services;
    }

    /// <summary>Registers the recycle bin, the wiki-link index, the Library reference resolver and file service, chat uploads and the document collector.</summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    private static IServiceCollection AddLibraryFileAndUploadServices(this IServiceCollection services)
    {
        // The real recycle bin on Windows (Task 6.9.i); everywhere else, a placeholder that refuses
        // every recycle so nothing is ever silently, permanently deleted.
        services.AddSingleton<IRecycleBin>(_ => OperatingSystem.IsWindows() ? new WindowsRecycleBin() : new NotAvailableRecycleBin());

        // No interface, same reasoning as AvatarStore above: nothing needs to substitute this.
        // Registered right before LibraryFileService (Task 8.4.i), which will take it as a
        // constructor dependency once the rewrite hooks land (8.5.i-a).
        services.AddSingleton<WikiLinkIndex>();
        services.AddSingleton<LibraryTagIndex>();
        services.AddSingleton<PropertyTypes>();

        // The open Tasks that link to a document, for the Backlinks panel: WikiLinkIndex skips the Teams
        // root's underscore folders, so a Task never reaches it.
        services.AddSingleton<Tasks.ITaskBacklinkSource, Tasks.TaskBacklinkSource>();

        // The real resolver behind ILibraryReferenceResolver/ILibraryNoteResolver (Task 9.3.i):
        // one singleton, forwarded to both interfaces with the same idiom as ITaskReferenceResolver
        // above, so MarkdownRenderer and Library note rendering share one instance.
        services.AddSingleton<LibraryReferenceResolver>();
        services.AddSingleton<ILibraryReferenceResolver>(sp => sp.GetRequiredService<LibraryReferenceResolver>());
        services.AddSingleton<ILibraryNoteResolver>(sp => sp.GetRequiredService<LibraryReferenceResolver>());

        // Registered right after LibraryPathResolver (corrections-B4 item 7): it depends on the
        // resolver above and on IRecycleBin, registered just above.
        services.AddSingleton<LibraryEvents>();
        services.AddSingleton<LibraryFileService>();
        services.AddScoped<LibraryBrowserBridge>();

        // Chat uploads (ChatUploads spec 5): the folder lookup is an interface so the Room delete can be
        // tested against a fake; the service and the per-circuit chip list need no abstraction.
        services.AddSingleton<Uploads.IRoomUploadFolders, Uploads.RoomUploadFolders>();
        services.AddSingleton<Uploads.ChatUploadService>();
        services.AddScoped<Uploads.PendingUploads>();

        // Task attachments (TaskAttachments spec 6.2): the upload folder is uploads:/Tasks/<ID>.
        services.AddSingleton<Uploads.TaskUploadFolders>();
        services.AddSingleton<Uploads.TaskUploadService>();

        // Registered right after LibraryFileService (Task 10.2.i): collects the Library documents
        // mentioned in a Turn's messages. No Library.Enabled gate here (corrections-B6 item 8) -
        // PersonaSupervisor passes a null collector when the Library is disabled.
        services.AddSingleton<LibraryDocumentCollector>();

        return services;
    }

    /// <summary>
    /// Team Rename (Spec §5): the gate shared with <see cref="ArchiveService"/>, the journal and the service. Step-6 rewrites
    /// register as <see cref="ITeamRenameRewrite"/> (none yet) and run in registration order.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    private static void AddTeamRenameServices(IServiceCollection services)
    {
        services.AddSingleton<TeamOperationGate>();
        services.AddSingleton<TeamRenameStore>();
        services.AddSingleton<ITeamRenames>(static sp => sp.GetRequiredService<TeamRenameStore>());
        services.AddSingleton<TeamRenameService>();
        services.AddSingleton<ITeamRenamer>(static sp => sp.GetRequiredService<TeamRenameService>());
        services.AddSingleton<ITeamRenameRewrite, TaskFileRenameRewrite>();
        services.AddSingleton<ITeamRenameRewrite, ViewsRenameRewrite>();
        services.AddSingleton<ITeamRenameRewrite, WorkspaceRenameRewrite>();
        services.AddSingleton<ITeamRenameRewrite, DocHistoryRenameRewrite>();
        services.AddSingleton<ITeamRenameRewrite, LedgerRenameRewrite>();
        services.AddSingleton<ITeamRenameRewrite, NeedsYouRenameRewrite>();

        // Before archive recovery: a half-renamed Team folder is finished before anything reads the Teams folder.
        services.AddHostedService<TeamRenameRecoveryService>();
    }

    /// <summary>Registers the Team folder provisioner, the Team catalog and membership, the view store and the Sprint stores.</summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    private static IServiceCollection AddTeamFolderCatalogAndSprintServices(this IServiceCollection services)
    {
        // Same instance as the hosted service, the same singleton-plus-factory idiom as
        // PersonaSupervisor/TaskTriggerService above (corrections-B4 item 33): the Library Pane's
        // "New Project" trigger needs to call EnsureProject on the very instance the host is running.
        // Archive recovery goes first so an interrupted archive is undone or finished before the provisioner
        // synchronises folders and the reconciler reads the catalog (corrections-12 items 4, 8, 16).
        AddTeamRenameServices(services);
        services.AddHostedService<ArchiveRecoveryService>();
        services.AddSingleton<TeamFolderProvisioner>();
        services.AddHostedService(sp => sp.GetRequiredService<TeamFolderProvisioner>());

        // Registered right after TeamFolderProvisioner (Task 2.2.i): it depends on PersonaStore and
        // TaskStore, both registered earlier. The interface resolves to the same instance, so the
        // pages and the container's Dispose see one catalog.
        services.AddSingleton<TeamCatalog>();
        services.AddSingleton<ITeamCatalog>(sp => sp.GetRequiredService<TeamCatalog>());

        AddAgentsMdScopesAndTurnHook(services);

        // The memory tool and the Team Memory write guard share these two: who writes which Team's Memory, and the one gate
        // per memory folder that every writer through the tool takes (Instructions and Memory §5.4, §5.5).
        services.AddSingleton(sp => TeamMemoryAccess.ForPersonas(sp.GetRequiredService<ITeamCatalog>(), sp.GetRequiredService<PersonaStore>()));
        services.AddSingleton<MemoryFolderGate>();
        services.AddSingleton<MemoryWarnings>();
        services.AddSingleton<OverCapNotifier>();

        // How a memory call's folder and file become a path: File Changes resolves with it to record a memory-tool write as the
        // Agent's own (Instructions and Memory §6.1).
        services.AddSingleton<MemoryPaths>();

        // Task 4.2.i: stateless apart from its own lock; it reads and writes Personas through the
        // PersonaStore registered earlier and resolves Team spellings from the catalog above.
        services.AddSingleton<ITeamMembership, TeamMembership>();
        services.AddSingleton<IArchiveFileSystem, PhysicalArchiveFileSystem>();
        services.AddSingleton<ArchiveService>();
        services.AddSingleton<IArchiveService>(static sp => sp.GetRequiredService<ArchiveService>());

        // Task 6.4.i: the Team pages create Teams and Projects through this interface; it resolves
        // to the very provisioner instance the host runs (no cycle: it needs the catalog above).
        services.AddSingleton<ITeamFolders>(sp => sp.GetRequiredService<TeamFolderProvisioner>());

        // No interface, same reasoning as AvatarStore just above: nothing needs to substitute
        // this, and a plain registration cannot produce the two-watchers-on-one-path hazard the
        // aliased registrations elsewhere in this file exist to avoid.
        services.AddSingleton<ViewStore>();

        // Sprints (spec �13): the store is the one instance; the read-only catalogue the tools and
        // TaskService see resolves to it, so there is one watcher on sprints.md. It needs TaskStore and
        // TaskEvents, registered earlier. Like ViewStore it is lazy, so nothing reads sprints.md
        // unless something that shows Sprints is resolved (Team:Tasks:Enabled=false hides all of it).
        services.AddSingleton<SprintStore>();
        services.AddSingleton<ISprintCatalog>(sp => sp.GetRequiredService<SprintStore>());

        // Sprint proposals (Task 62): kept in memory and under _proposals/ in the Team folder until the Human decides.
        services.AddSingleton<SprintProposalStore>();

        // Answers a Sprint proposal (Task 63): Approve is the only path that creates a Sprint.
        services.AddSingleton<SprintProposalService>();

        return services;
    }

    /// <summary>Registers the per-circuit services, the Right sidebar panels, Global Search and the reference services.</summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    private static IServiceCollection AddCircuitTabSidebarSearchAndReferenceServices(this IServiceCollection services)
    {
        // Scoped: whether one circuit's client is still connected, and the navigator that skips a server-driven
        // navigation once it is not (docs/engineering/rules.md).
        services.AddCircuitConnection();

        // Scoped: the open Tabs of one circuit (docs/Huddle.Tabs-Specifications.md), and which Teams have a shortcut at
        // the top of its Activity bar.
        services.AddScoped<Tabs.TabService>();
        services.AddScoped<Components.Layout.TeamShortcutStore>();

        // Scoped: whether this circuit's Right sidebar is open, and what each mounted page put in it.
        services.AddScoped<Tabs.RightSidebarService>();
        services.AddSingleton<Sidebar.SidebarPanelCatalog>();
        // The Right sidebar's panels (docs/Huddle.RightSidebar-Specifications.md): one registration each, no page changes.
        services.AddSidebarPanel(new Sidebar.SidebarPanel("members", "Members", MudBlazor.Icons.Material.Filled.Group, 10,
            static subject => subject is Sidebar.RoomSubject, typeof(Components.Sidebar.MembersPanel)));
        services.AddSidebarPanel(new Sidebar.SidebarPanel("room-tasks", "Tasks in this Room", MudBlazor.Icons.Material.Filled.TaskAlt, 20,
            static subject => subject is Sidebar.RoomSubject, typeof(Components.Sidebar.RoomTasksPanel), static options => options.Tasks.Enabled));
        services.AddSidebarPanel(new Sidebar.SidebarPanel("room-documents", "Documents in this Room", MudBlazor.Icons.Material.Filled.Description, 30,
            static subject => subject is Sidebar.RoomSubject, typeof(Components.Sidebar.RoomDocumentsPanel), static options => options.Library.Enabled));
        services.AddSidebarPanel(new Sidebar.SidebarPanel("outline", "Outline", MudBlazor.Icons.Material.Filled.Toc, 10,
            static subject => subject is Sidebar.DocumentSubject { Kind: Library.LibraryFileKind.Markdown }, typeof(Components.Sidebar.OutlinePanel), static options => options.Library.Enabled));
        services.AddSidebarPanel(new Sidebar.SidebarPanel("linked-documents", "Linked documents", MudBlazor.Icons.Material.Filled.Link, 20,
            static subject => subject is Sidebar.DocumentSubject { Kind: Library.LibraryFileKind.Markdown }, typeof(Components.Sidebar.LinkedDocumentsPanel), static options => options.Library.Enabled));
        services.AddSidebarPanel(new Sidebar.SidebarPanel("backlinks", "Backlinks", MudBlazor.Icons.Material.Filled.CallReceived, 25,
            static subject => subject is Sidebar.DocumentSubject { Kind: Library.LibraryFileKind.Markdown }, typeof(Components.Sidebar.BacklinksPanel), static options => options.Library.Enabled));
        services.AddSidebarPanel(new Sidebar.SidebarPanel("tags", "Tags", MudBlazor.Icons.Material.Filled.Tag, 27,
            static subject => subject is Sidebar.DocumentSubject { Kind: Library.LibraryFileKind.Markdown }, typeof(Components.Sidebar.TagsPanel), static options => options.Library.Enabled));
        services.AddTaskEditSidebarPanels();
        services.AddScoped<Sidebar.PageCommandBus>();
        services.AddScoped<Components.Shared.AgentProfileCardOpener>();

        // Stateless reader over singletons: builds the card the avatar profile popover shows.
        services.AddSingleton<Components.Shared.AgentProfiles>();

        // Global Search (spec §7.3): the five providers are stateless readers, one each per category.
        services.AddSingleton<Search.ISearchProvider, Search.TeamSearchProvider>();
        services.AddSingleton<Search.ISearchProvider, Search.AgentSearchProvider>();
        services.AddSingleton<Search.ISearchProvider, Search.LibrarySearchProvider>();
        services.AddSingleton<Search.ISearchProvider, Search.TaskSearchProvider>();
        services.AddSingleton<Search.ISearchProvider, Search.RoomSearchProvider>();

        // Scoped, not singleton: it owns the one search in flight, and every circuit has its own (spec §7.3).
        services.AddScoped<Search.SearchOrchestrator>();

        // A factory, not the search itself (spec §5.2, D-5): each Composer makes and disposes its own picker orchestrator, so a
        // keystroke never cancels the Top Bar search; a transient IDisposable would be held by the circuit until it ended.
        services.AddSingleton<References.ReferenceSearchFactory>();

        // Singletons (spec §8.2): ChatService takes the snapshotter as an optional constructor parameter, and the
        // resolver is shared with the Turn's Referenced items header. They read the Task, Team and Library
        // singletons registered above, so there is no cycle.
        services.AddSingleton<References.ReferenceResolver>();
        services.AddSingleton<References.IReferenceSnapshotter, References.ReferenceSnapshotter>();
        services.AddSingleton<References.ReferenceExpander>();
        services.AddSingleton(new Tabs.TabPageRoutes(typeof(ServiceCollectionExtensions).Assembly));

        return services;
    }

    /// <summary>The Right sidebar's panels for a Task: the Task panel for one selected on the Tasks page, and, for one open in its edit page (docs/Huddle.TaskReadView-Specifications.md, D-8), what the app works out about it. Apart from Activity, each of those hides itself through its Subject when it would be empty.</summary>
    /// <param name="services">The service collection to add the panels to.</param>
    private static void AddTaskEditSidebarPanels(this IServiceCollection services)
    {
        // The Tasks page's selected Task: the read view the detail drawer used to hold.
        services.AddSidebarPanel(new Sidebar.SidebarPanel("task-detail", "Task", MudBlazor.Icons.Material.Filled.TaskAlt, 5,
            static subject => subject is Sidebar.TaskDetailSubject, typeof(Components.Sidebar.TaskDetailPanel), static options => options.Tasks.Enabled));
        services.AddSidebarPanel(new Sidebar.SidebarPanel("task-activity", "Activity", MudBlazor.Icons.Material.Filled.Bolt, 10,
            static subject => subject is Sidebar.TaskSubject, typeof(Components.Sidebar.TaskActivityPanel), static options => options.Tasks.Enabled));
        services.AddSidebarPanel(new Sidebar.SidebarPanel("task-blockers", "Blocked by", MudBlazor.Icons.Material.Filled.Block, 20,
            static subject => subject is Sidebar.TaskSubject { HasBlockers: true }, typeof(Components.Sidebar.TaskBlockersPanel), static options => options.Tasks.Enabled));
        services.AddSidebarPanel(new Sidebar.SidebarPanel("task-blocks", "Blocks", MudBlazor.Icons.Material.Filled.Block, 25,
            static subject => subject is Sidebar.TaskSubject { IsBlocking: true }, typeof(Components.Sidebar.TaskBlocksPanel), static options => options.Tasks.Enabled));
        services.AddSidebarPanel(new Sidebar.SidebarPanel("task-documents", "Linked documents", MudBlazor.Icons.Material.Filled.Link, 30,
            static subject => subject is Sidebar.TaskSubject, typeof(Components.Sidebar.TaskDocumentsPanel), static options => options.Tasks.Enabled && options.Library.Enabled));
    }

    /// <summary>Registers the adapter probe, work-mode policy, model catalog, built-in Agent reset and the Human rename cascade.</summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    private static IServiceCollection AddProbeAndRenameServices(this IServiceCollection services)
    {
        // Unconditional too, and for the same reason: the probe spends nothing on its own (it never
        // calls PromptAsync), so registering it costs nothing when Team:Acp:Enabled is off. What
        // keeps it honest is WHEN it runs — the /agents page only calls it on card-open, never on
        // page-load.
        services.AddSingleton<IAdapterProbeRunner, AdapterProcessProbeRunner>();
        services.AddSingleton<WorkModePolicy>();
        services.AddSingleton<IModelCatalog, ModelCatalogProbe>();

        // No interface, same reasoning as AppearanceStore/AvatarStore above: nothing needs to
        // substitute this, and it is built entirely from PersonaStore, already registered above.
        // Deliberately a plain singleton rather than folded into the hosted BuiltinAgentSeeder
        // below - AgentProfileCard's "Reset to default" (Spec §6.12) needs to inject it directly, and a
        // component must never depend on an IHostedService.
        services.AddSingleton<BuiltinAgentReset>();

        // A singleton before it is a hosted service: DataInitializer runs it directly at startup for a
        // rename made while the app was stopped, and the hosted half subscribes to HumanRenamed.
        services.AddSingleton<HumanRenameCascade>();

        return services;
    }

    /// <summary>Registers the hosted services in their required start-up order, up to and including the Persona supervisor.</summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    private static IServiceCollection AddStartupHostedServices(this IServiceCollection services)
    {
        // Before every other hosted service that reads Agents (Skill Packs spec §6.5, "Start-up order"): the move, the
        // repair of Skills/.trash and the first full read of the registry finish before BuiltinAgentSeeder,
        // DataInitializer and PersonaSupervisor start, because the host awaits each StartAsync in registration order.
        services.AddHostedService(static sp => sp.GetRequiredService<Agency.Huddle.App.SkillSources.RegistryWatcher>());
        services.AddHostedService(static sp => sp.GetRequiredService<Agency.Huddle.App.SkillSources.InstallFileWriter>());
        services.AddHostedService<DataInitializer>();
        services.AddHostedService<Agency.Huddle.App.Teams.BlockersDoneBeforeStartMigration>();
        services.AddHostedService<Agency.Huddle.App.SecondBrain.RetiredFileScanService>();
        services.AddHostedService<Agency.Huddle.App.SecondBrain.DefaultAgentsMdService>();
        services.AddHostedService(sp => sp.GetRequiredService<HumanRenameCascade>());
        services.AddHostedService<PipeServer>();
        services.AddHostedService<DemoAgentHost>();

        // Registered immediately before PersonaSupervisor's own hosted service, and never behind
        // Team:Acp:Enabled: hosted services start in registration order (Spec §10), so this is what
        // guarantees the supervisor's first reconciliation already sees the Chief of Staff the
        // seeder just wrote, rather than racing a later PersonasChanged (Spec §6.12 Implementation
        // notes).
        services.AddHostedService<ConnectorsNoticeDecider>();
        services.AddHostedService<BuiltinAgentSeeder>();

        // Same instance as the hosted service, not a second registration - mirrors every other pair
        // in this file (AgentGateway/IAgentGateway, PersonaStore/IMentionAliasSource, PromptStore/IPromptSource).
        // A Restart button (T7.2) needs to reach the very PersonaSupervisor the host is running, not a
        // second, independently constructed one - the same class of intermittent test flake those other
        // pairs' remarks already document, this time for a component resolving it directly rather than
        // for a second FileSystemWatcher.
        services.AddSingleton<PersonaSupervisor>();
        services.AddHostedService(sp => sp.GetRequiredService<PersonaSupervisor>());

        return services;
    }

    /// <summary>Registers the task trigger, cadence, rename cascade, Needs You and Team room hosted services.</summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    private static IServiceCollection AddBackgroundHostedServices(this IServiceCollection services)
    {
        // Same instance as the hosted service, the same shape as PersonaSupervisor's pair just
        // above: Spec §10.1 models TaskTriggerService's registration on this app's existing
        // singleton-plus-AddHostedService(sp => sp.GetRequiredService<...>()) idiom, so something
        // else (the Task panel's "Allow N more") can resolve the very instance the host is running.
        // Task 9.3 (this registration) implements only Preview; Task 9.4 adds the
        // TaskEvents.TaskChanged subscription StartAsync will drive.
        //
        // This is the second factory-registered IHostedService in this method - PipeHostFixture's
        // RemovePersonaSupervisorHostedService (and its Conformance-test callers) had to stop
        // counting factory registrations and start matching PersonaSupervisor's by what its factory
        // returns, because a plain count can no longer tell the two apart (corrections-B3 blocking
        // item 2).
        services.AddSingleton<TaskTriggerService>();
        services.AddSingleton<IWakePauses>(sp => sp.GetRequiredService<TaskTriggerService>());
        services.AddHostedService(sp => sp.GetRequiredService<TaskTriggerService>());

        // Digest and retro wake-ups on schedule or count (OR-3, RT-2); singleton plus hosted, like the service above.
        services.AddSingleton<Agency.Huddle.App.Teams.CadenceService>();
        services.AddHostedService(sp => sp.GetRequiredService<Agency.Huddle.App.Teams.CadenceService>());

        // Scheduled Jobs: definitions in each owner's schedule.md, firing state in team.db, run once a minute like the cadences above.
        services.AddSingleton<Agency.Huddle.App.Jobs.JobStore>();
        services.AddSingleton<JobStateStore>();
        services.AddSingleton<TurnQueueStore>();
        services.AddSingleton<JobRoomStore>();
        services.AddSingleton<Agency.Huddle.App.Jobs.IJobRunner, Agency.Huddle.App.Jobs.JobRunner>();
        services.AddSingleton<Agency.Huddle.App.Jobs.JobScheduler>();
        services.AddHostedService(sp => sp.GetRequiredService<Agency.Huddle.App.Jobs.JobScheduler>());

        // Unconditional, unlike PersonaSupervisor's hosted service above: the Agent row a rename
        // cascades from may exist from an earlier session or a raw pipe client, so a Persona rename
        // must cascade into the Team Directory even when Team:Acp:Enabled is false and no runner is
        // ever started - and an Avatar exists whether or not any Agent has ever connected at all, so
        // its rename/removal cascade needs this running unconditionally too. A singleton nobody
        // resolves never subscribes to PersonaStore.PersonaRenamed or PersonaStore.PersonaRemoved,
        // so this must be constructed - hence AddHostedService rather than a plain AddSingleton.
        //
        // Registered by type, not by a sp => sp.GetRequiredService<...>() factory shape like the two
        // pairs above: nothing else in the app needs to resolve this same instance the way a Restart
        // button resolves the running PersonaSupervisor, so there is no second registration to keep
        // in sync here, and it stays out of PipeHostFixture.RemovePersonaSupervisorHostedService's
        // factory-based search entirely.
        services.AddHostedService<PersonaRenameCascade>();

        // Needs You (ADR-0050). The tracker is a singleton that observes RoomEvents and the Question and
        // Form stores, so it must exist before the first event, not on first use: the startup scan takes
        // it as a dependency, and AddHostedService constructs that at host start. Registered after
        // DataInitializer so the Team Directory is initialised before the scan reads it.
        services.AddSingleton<NeedsYouTracker>();
        services.AddSingleton<NeedsYouTaskSource>();
        services.AddHostedService<NeedsYouStartupScan>();

        // Team chats (ADR-0052): one Team Room per Team, reconciled from the catalog. Registered after
        // DataInitializer so the Team Directory exists, and after the catalog it subscribes to.
        services.AddSingleton<Agency.Huddle.App.Teams.TeamRoomReconciler>();
        services.AddHostedService(sp => sp.GetRequiredService<Agency.Huddle.App.Teams.TeamRoomReconciler>());

        return services;
    }
}

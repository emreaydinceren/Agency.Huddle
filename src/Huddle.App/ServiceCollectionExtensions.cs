using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Agency.Huddle.Acp.Hosting;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Appearance;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Demo;
using Agency.Huddle.App.Elicitation;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.HiringPool;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.NeedsYou;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Skills;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.Teammates;
using Agency.Huddle.App.Teams;
using Agency.Huddle.App.TeamTypes;

namespace Agency.Huddle.App;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTeamServices(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Acp.PersonaDir was renamed to Acp.TeamsDir (the "Teams" rename), and Acp.TeamsDir was in
        // turn renamed to Acp.TeammatesDir (Spec §6.15, the Teammates-beside-Teams layout), with no
        // back-compat fallback either time: nothing in appsettings*.json still sets either old key,
        // so a fallback would be dead weight. But a value left behind in a user secret or an
        // environment variable would otherwise bind to nothing, and PersonaStore would quietly scan
        // an empty default "Teammates" folder - zero teammates, no exception, no log anywhere. That
        // silent-degradation shape is exactly what docs/engineering/traps.md exists to catch, so fail
        // loudly at startup instead, naming the new key. This runs ahead of Configure<TeamOptions>
        // below, in the one place both this app's Program.cs and every test that composes it
        // (TeamWebApplicationFactory) are guaranteed to pass through.
        if (configuration[$"{TeamOptions.SectionName}:Acp:PersonaDir"] is not null)
        {
            throw new InvalidOperationException(
                $"Configuration key '{TeamOptions.SectionName}:Acp:PersonaDir' was renamed to " +
                $"'{TeamOptions.SectionName}:Acp:TeammatesDir'. Update the configuration source that sets it " +
                "(environment variable, user secret, etc.) - there is no automatic fallback.");
        }

        if (configuration[$"{TeamOptions.SectionName}:Acp:TeamsDir"] is not null)
        {
            throw new InvalidOperationException(
                $"Configuration key '{TeamOptions.SectionName}:Acp:TeamsDir' was renamed to " +
                $"'{TeamOptions.SectionName}:Acp:TeammatesDir'. Update the configuration source that sets it " +
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

        services.Configure<TeamOptions>(configuration.GetSection(TeamOptions.SectionName));
        services.PostConfigure<TeamOptions>(options =>
        {
            options.DataDir = Path.GetFullPath(options.DataDir);
            LayoutGuard.ValidateTeamsAndTeammates(options);
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
        // is: a DI singleton a runner writes to and the Teammate card reads. It limits nothing.
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

        // Spec §10.6-§10.7: per-Task wake history and the Agent-wake budget that guards against a
        // looping Agent. A leaf singleton, same shape as TurnActivity just above.
        services.AddSingleton<TaskActivity>();

        // FC §6.3: resolves a Watched Folder entry (a Teammate Name, a full path, or a path
        // relative to DataDir) into a full path, or refuses it with a reason.
        services.AddSingleton<WatchedFolderResolver>();

        // FC §6.6: one JSON file per Agent under {DataDir}/file-state/, holding what that Agent
        // last saw per Room.
        services.AddSingleton<FileStateStore>();

        // RS §6.6: a singleton like RoomFollows above - touches files, never the Team Directory -
        // one JSON file per Agent under {DataDir}/room-sessions/, holding what is kept about each
        // Room Session so it can be resumed.
        services.AddSingleton<RoomSessionStore>();

        services.AddSingleton<AgentGateway>();
        services.AddSingleton<IAgentGateway>(sp => sp.GetRequiredService<AgentGateway>());

        services.AddSingleton<TeammatePaths>();
        services.AddSingleton<SkillPackStore>();
        services.AddSingleton<HiringPoolCatalog>();
        services.AddSingleton<CustomRoleStore>();
        services.AddSingleton<TeamTypeCatalog>();
        services.AddSingleton<SkillsCatalogIndex>();
        services.AddSingleton<UserCandidateWriter>();
        services.AddSingleton<IConnectorStatus, FileConnectorStatus>();

        // Shared between PersonaRenameCascade (signals around a Teammate-folder move) and
        // DotAcpAgentHostFactory (waits on it before creating a Persona's Work Dir) - corrections-B2
        // item 20.
        services.AddSingleton<TeammateFolderMoves>();

        // Unconditional: this is what lets the /teammates page be built and tested with no agent
        // process and no tokens, regardless of whether Team:Acp:Enabled is set.
        services.AddSingleton<PersonaStore>();

        // FC §6.7: a singleton, like RoomFollows above - the watch_folder/unwatch_folder tools and
        // every runner share it through DI.
        services.AddSingleton<FileChangeTracker>();

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
        services.AddSingleton<CadenceStateStore>();
        services.AddSingleton<PersonaEffortStore>();
        services.AddSingleton<PersonaWorkModeStore>();
        services.AddSingleton<PersonaSpendStore>();
        services.AddSingleton<Agency.Huddle.App.Teams.RecordsBundleBuilder>();
        services.AddSingleton<Agency.Huddle.App.Teams.RetroFileStore>();

        // Retro proposals (Task 76): held under _proposals/retro/ in the Team folder; Huddle applies an accepted edit.
        services.AddSingleton<Agency.Huddle.App.Teams.RetroProposalStore>();
        services.AddSingleton<Agency.Huddle.App.Teams.RetroProposalService>();
        services.AddSingleton<TaskIdAllocator>();

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
        // would mean a second file handle on prompts.json now and (once T4.1 adds one) a second
        // FileSystemWatcher on it, the same class of intermittent test flake PersonaStore's remarks
        // already document for the closely related PersonaSupervisor case.
        services.AddSingleton<PromptStore>();
        services.AddSingleton<IPromptSource>(sp => sp.GetRequiredService<PromptStore>());

        // Same reasoning as PromptStore above: a singleton, so the app never has two independently
        // constructed stores each creating {DataDir}/Skills and (once a later task adds one) each
        // running their own FileSystemWatcher over it.
        services.AddSingleton<SkillStore>();

        // Skill import (SK-2 to SK-7): the downloader gets its own client with a long timeout for the archive; the importer's client only
        // resolves a branch to a commit, so it times out sooner. Both are plain singletons - nothing here is shared with other HTTP users.
        services.AddSingleton(static sp => new SkillDownloader(
            new HttpClient { Timeout = TimeSpan.FromMinutes(2) },
            sp.GetRequiredService<ILogger<SkillDownloader>>()));
        services.AddSingleton(static sp => new SkillImporter(
            sp.GetRequiredService<SkillsCatalogIndex>(),
            sp.GetRequiredService<SkillDownloader>(),
            sp.GetRequiredService<SkillStore>(),
            new HttpClient { Timeout = TimeSpan.FromSeconds(15) },
            sp.GetService<TimeProvider>()));
        services.AddSingleton<ISkillImporter>(static sp => sp.GetRequiredService<SkillImporter>());

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
        services.AddSingleton<QuestionStore>();

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

        // The one hire-limit check ProposalService, ProposeTeammatesTool and ProposalCard share. The
        // live-process count reads PersonaSupervisor lazily, per call, so registering it here (before
        // the supervisor) creates no construction cycle.
        services.AddSingleton(sp => new HireLimitGuard(
            sp.GetRequiredService<ITeamCatalog>(),
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<TeamOptions>>(),
            () => sp.GetService<PersonaSupervisor>()?.RunningHostCount ?? 0));

        // Hiring a role-based Team. The start gate is the supervisor, resolved lazily through a factory
        // so HireService does not force PersonaSupervisor to construct before it is registered below;
        // CardResolver and TierModelPicker are static and need no registration.
        services.AddSingleton<Agency.Huddle.App.Teams.ISkillInstaller, Agency.Huddle.App.Teams.ImportingSkillInstaller>();
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

        // Asked by WorkDirPermissionHandler before an Agent's git merge or git push (PR-7).
        services.AddSingleton<IMergeGate, Agency.Huddle.App.Teams.ProcessMergeGate>();
        services.AddSingleton<Agency.Huddle.App.Teams.TeamDuplicator>();

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

        // The real recycle bin on Windows (Task 6.9.i); everywhere else, a placeholder that refuses
        // every recycle so nothing is ever silently, permanently deleted.
        services.AddSingleton<IRecycleBin>(_ => OperatingSystem.IsWindows() ? new WindowsRecycleBin() : new NotAvailableRecycleBin());

        // No interface, same reasoning as AvatarStore above: nothing needs to substitute this.
        // Registered right before LibraryFileService (Task 8.4.i), which will take it as a
        // constructor dependency once the rewrite hooks land (8.5.i-a).
        services.AddSingleton<WikiLinkIndex>();

        // The real resolver behind ILibraryReferenceResolver/ILibraryNoteResolver (Task 9.3.i):
        // one singleton, forwarded to both interfaces with the same idiom as ITaskReferenceResolver
        // above, so MarkdownRenderer and Library note rendering share one instance.
        services.AddSingleton<LibraryReferenceResolver>();
        services.AddSingleton<ILibraryReferenceResolver>(sp => sp.GetRequiredService<LibraryReferenceResolver>());
        services.AddSingleton<ILibraryNoteResolver>(sp => sp.GetRequiredService<LibraryReferenceResolver>());

        // Registered right after LibraryPathResolver (corrections-B4 item 7): it depends on the
        // resolver above and on IRecycleBin, registered just above.
        services.AddSingleton<LibraryFileService>();

        // Chat uploads (ChatUploads spec 5): the folder lookup is an interface so the Room delete can be
        // tested against a fake; the service and the per-circuit chip list need no abstraction.
        services.AddSingleton<Uploads.IRoomUploadFolders, Uploads.RoomUploadFolders>();
        services.AddSingleton<Uploads.ChatUploadService>();
        services.AddScoped<Uploads.PendingUploads>();

        // Registered right after LibraryFileService (Task 10.2.i): collects the Library documents
        // mentioned in a Turn's messages. No Library.Enabled gate here (corrections-B6 item 8) -
        // PersonaSupervisor passes a null collector when the Library is disabled.
        services.AddSingleton<LibraryDocumentCollector>();

        // Same instance as the hosted service, the same singleton-plus-factory idiom as
        // PersonaSupervisor/TaskTriggerService above (corrections-B4 item 33): the Library Pane's
        // "New Project" trigger needs to call EnsureProject on the very instance the host is running.
        services.AddSingleton<TeamFolderProvisioner>();
        services.AddHostedService(sp => sp.GetRequiredService<TeamFolderProvisioner>());

        // Registered right after TeamFolderProvisioner (Task 2.2.i): it depends on PersonaStore and
        // TaskStore, both registered earlier. The interface resolves to the same instance, so the
        // pages and the container's Dispose see one catalog.
        services.AddSingleton<TeamCatalog>();
        services.AddSingleton<ITeamCatalog>(sp => sp.GetRequiredService<TeamCatalog>());

        // Task 4.2.i: stateless apart from its own lock; it reads and writes Personas through the
        // PersonaStore registered earlier and resolves Team spellings from the catalog above.
        services.AddSingleton<ITeamMembership, TeamMembership>();

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

        // Scoped: the open Tabs of one circuit (docs/Huddle.Tabs-Specifications.md).
        services.AddScoped<Tabs.TabService>();

        // Scoped: whether this circuit's Right sidebar is open, and what each mounted page put in it.
        services.AddScoped<Tabs.RightSidebarService>();
        services.AddSingleton<Sidebar.SidebarPanelCatalog>();
        services.AddScoped<Components.Shared.TeammateCardOpener>();

        // Stateless reader over singletons: builds the card the avatar profile popover shows.
        services.AddSingleton<Components.Shared.TeammateProfiles>();

        // Global Search (spec §7.3): the five providers are stateless readers, one each per category.
        services.AddSingleton<Search.ISearchProvider, Search.TeamSearchProvider>();
        services.AddSingleton<Search.ISearchProvider, Search.TeammateSearchProvider>();
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

        // Unconditional too, and for the same reason: the probe spends nothing on its own (it never
        // calls PromptAsync), so registering it costs nothing when Team:Acp:Enabled is off. What
        // keeps it honest is WHEN it runs — the /teammates page only calls it on card-open, never on
        // page-load.
        services.AddSingleton<IAdapterProbeRunner, AdapterProcessProbeRunner>();
        services.AddSingleton<WorkModePolicy>();
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
        services.AddHostedService(sp => sp.GetRequiredService<TaskTriggerService>());

        // Digest and retro wake-ups on schedule or count (OR-3, RT-2); singleton plus hosted, like the service above.
        services.AddSingleton<Agency.Huddle.App.Teams.CadenceService>();
        services.AddHostedService(sp => sp.GetRequiredService<Agency.Huddle.App.Teams.CadenceService>());

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
        services.AddHostedService<NeedsYouStartupScan>();

        // Team chats (ADR-0052): one Team Room per Team, reconciled from the catalog. Registered after
        // DataInitializer so the Team Directory exists, and after the catalog it subscribes to.
        services.AddHostedService<Agency.Huddle.App.Teams.TeamRoomReconciler>();

        return services;
    }
}
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.DotAcp;
using Agency.Huddle.Acp.Hosting;
using Agency.Huddle.Acp.Tools;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Memory;
using Agency.Huddle.App.Modules;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.SecondBrain;
using Agency.Huddle.App.Skills;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// The real <see cref="IAgentHostFactory"/>: launches the ACP Claude Code adapter as a child process
/// and wires it to the application's own tools over one <see cref="AppToolServer"/>.
/// </summary>
/// <remarks>
/// This runs one <see cref="AppToolServer"/> per Persona, on its own ephemeral loopback port, matching
/// <c>Team.Console</c>. acp-agent-guide.md §7.5 argues the eventual shape for a multi-persona host is a
/// single shared MCP endpoint, but §8 records that nothing is known about several agents calling one
/// endpoint concurrently, so per-Persona is the better-evidenced option today and needs zero new
/// JSON-RPC code. Revisit this past roughly four Personas, when N loopback listeners stops being free.
/// </remarks>
internal sealed class DotAcpAgentHostFactory : IAgentHostFactory
{
    /// <summary>
    /// The MCP tool-server name handed to <see cref="AppToolServer"/>. It is the server name, and it
    /// never changes — not even between Adapter profiles. What varies per profile is only whether the
    /// *model-facing* tool names in the system prompt carry it as a <c>mcp__{name}__</c> prefix; see
    /// <see cref="AdapterProfile.UsesToolNamePrefix"/> and <see cref="SystemPromptComposer"/>'s remarks
    /// on why that prefix is applied in code, never in a prompt's template.
    /// </summary>
    private const string ToolServerName = "team";

    private readonly TeamOptions options;
    private readonly IServiceProvider serviceProvider;
    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger<DotAcpAgentHostFactory> logger;
    private readonly AdapterProfileResolver resolver;
    private readonly IAgentProcessLauncher launcher;
    private readonly SkillStore skills;
    private readonly AgentPaths agentPaths;
    private readonly AgentFolderMoves folderMoves;
    private readonly ITeamCatalog teamCatalog;
    private readonly WorkModePolicy workModePolicy;

    /// <summary>Initializes a new instance of the <see cref="DotAcpAgentHostFactory"/> class.</summary>
    /// <param name="options">The bound <see cref="TeamOptions"/>.</param>
    /// <param name="serviceProvider">The application's own container, used to build each Persona's chat tools.</param>
    /// <param name="loggerFactory">Creates every logger this factory and the host it builds need.</param>
    /// <param name="resolver">Turns a Persona's Adapter id into the profile to launch.</param>
    /// <param name="launcher">
    /// Launches the agent process. Constructor-injected rather than newed up inline (CSharpPrinciples.md:
    /// "Constructor injection, always... no <c>new SomeService()</c> inside domain code") — the same
    /// change that lets <c>tests/Huddle.Tests/Conformance/MockAdapterFixture.cs</c> (D10, Task 10.1)
    /// substitute an in-process in-memory stream pair for a real child process, driving the rest of
    /// this factory's real host, session, tool server and prompt composition against a scripted ACP
    /// peer with no process spawned. <see cref="IAgentProcessLauncher"/> itself is not a new seam - it
    /// already existed for <see cref="DotAcpAgentHost"/> to accept - this only stops that seam being
    /// bypassed by a hardcoded <see cref="AgentProcessLauncher"/> here.
    /// </param>
    /// <param name="skills">
    /// Resolves a Persona's assigned Skill names to their current <see cref="Skill"/> records
    /// (Spec §6.4, §6.5). Read here from <see cref="Persona.Text"/> directly, rather than threaded
    /// through <see cref="IAgentHostFactory.StartAsync"/>'s parameters, because that signature stays
    /// narrow (Spec §6.4 Implementation notes) — <c>PersonaSupervisor</c> resolves the same names a
    /// second time, only for the Degraded warnings it reports before ever calling this factory.
    /// </param>
    /// <param name="agentPaths">Locates each Persona's Work Dir.</param>
    /// <param name="folderMoves">Awaited before creating a Persona's Work Dir, so a rename's Agent-folder move never races this factory creating a stale copy of it - corrections-B2 item 20.</param>
    /// <param name="teamCatalog">The Teams and their Projects, handed to each Persona's host so its session start can list the Team Memory of the Teams the Persona belongs to (Spec §6.4).</param>
    /// <param name="workModePolicy">Decides whether a Persona's Work Mode may be sent to its Adapter (ADR-0033).</param>
    public DotAcpAgentHostFactory(
        IOptions<TeamOptions> options,
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory,
        AdapterProfileResolver resolver,
        IAgentProcessLauncher launcher,
        SkillStore skills,
        AgentPaths agentPaths,
        AgentFolderMoves folderMoves,
        ITeamCatalog teamCatalog,
        WorkModePolicy workModePolicy)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentNullException.ThrowIfNull(skills);
        ArgumentNullException.ThrowIfNull(agentPaths);
        ArgumentNullException.ThrowIfNull(folderMoves);
        ArgumentNullException.ThrowIfNull(teamCatalog);
        ArgumentNullException.ThrowIfNull(workModePolicy);

        this.options = options.Value;
        this.workModePolicy = workModePolicy;
        this.teamCatalog = teamCatalog;
        this.serviceProvider = serviceProvider;
        this.loggerFactory = loggerFactory;
        this.logger = loggerFactory.CreateLogger<DotAcpAgentHostFactory>();
        this.resolver = resolver;
        this.launcher = launcher;
        this.skills = skills;
        this.agentPaths = agentPaths;
        this.folderMoves = folderMoves;
    }

    public async Task<IPersonaHost> StartAsync(Persona persona, string agentId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(persona);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);

        AdapterProfile profile = this.ResolveProfile(persona);

        await this.WaitForFolderMoveAsync(persona.Name, cancellationToken).ConfigureAwait(false);

        var (workDir, memoryDir) = this.CreateWorkDirs(persona.Name);
        AgentProcessOptions processOptions = CreateProcessOptions(persona, profile, workDir);

        var authToken = MintAuthToken();

        PersonaIdentity? parsed = ParseIdentity(persona);
        IReadOnlyList<string> skillNames = parsed?.Skills ?? [];
        IReadOnlyList<string> teamLabels = parsed?.Teams ?? [];
        SkillResolution skillResolution = this.skills.Resolve(skillNames);
        ToolSet toolSet = this.BuildToolSet(persona, agentId, profile, skillResolution, this.ProposesTeamMemory(persona.Name, teamLabels), teamLabels);

        var toolServer = new AppToolServer(ToolServerName, toolSet.Tools, this.loggerFactory, 0, authToken);
        await toolServer.StartAsync(cancellationToken).ConfigureAwait(false);

        IReadOnlyList<string> toolNames = [.. toolSet.Tools.Select(tool => $"{toolSet.ToolNamePrefix}{tool.Name}")];

        this.PrepareModuleWorkDirs(workDir, toolNames, parsed);
        InstallMergeGateHook(workDir, this.options.DataDir, this.serviceProvider.GetService<IMergeGate>(), this.logger);

        DotAcpAgentHost innerHost = await this.StartInnerHostAsync(processOptions, toolServer, cancellationToken).ConfigureAwait(false);

        IReadOnlyDictionary<string, object>? meta = BuildIsolationMeta(profile, ConnectorsPassed(profile, EffectiveConnectors(persona, this.workModePolicy)));
        Func<RoleBlockInput?> roleBlock = this.CreateRoleBlock(parsed, skillResolution);
        SessionPromptSources sources = this.CreatePromptSources(toolSet.Prompts);

        return new DotAcpPersonaHost(
            innerHost,
            toolServer,
            profile,
            persona,
            toolSet.Prompts,
            toolSet.HelpToolName,
            toolNames,
            skillResolution.Skills,
            toolSet.ReadSkillToolName,
            toolSet.AskHumanToolName,
            memoryDir,
            this.ReadsMemory(profile),
            this.options.FileChanges.MaxMemoryEntries,
            teamLabels,
            this.teamCatalog,
            this.TeamsRoot(),
            this.CreateGuards(toolSet.Prompts, workDir, sources),
            this.options.Teams,
            workDir,
            toolServer.Endpoint,
            meta,
            this.loggerFactory,
            this.workModePolicy,
            roleBlock,
            sources);
    }

    /// <summary>
    /// Whether the account connectors are passed to this Persona's sessions: the stored switch, unless its Work Mode never asks before
    /// a tool runs, where the approval prompt would be skipped and a stored "on" is ignored (Agent Connectors spec 8.4).
    /// </summary>
    /// <param name="persona">The Persona.</param>
    /// <param name="policy">Decides which Work Modes skip approval.</param>
    /// <returns><see langword="true"/> only when the switch is on and the mode asks.</returns>
    internal static bool EffectiveConnectors(Persona persona, WorkModePolicy policy)
    {
        return persona.Connectors && !policy.SkipsApproval(persona.WorkMode);
    }

    /// <summary>
    /// Whether the connectors are passed on this profile. A profile that does not isolate has no per-Agent switch (Agent Connectors
    /// spec 7.3), so a stored "on" is ignored there and the connectors stay off.
    /// </summary>
    /// <param name="profile">The Adapter Profile the session runs on.</param>
    /// <param name="effective">The result of <see cref="EffectiveConnectors"/>.</param>
    /// <returns><see langword="true"/> only for an isolating profile whose Agent is on.</returns>
    internal static bool ConnectorsPassed(AdapterProfile profile, bool effective) => effective && profile.IsolateUserSettings;

    /// <summary>
    /// Builds the guards one Persona's permission handler consults, in the order it consults them. There is no <c>~/.claude</c> guard when
    /// the profile directory cannot be resolved, and no merge gate guard when no <see cref="IMergeGate"/> is registered.
    /// </summary>
    /// <param name="prompts">Renders each guard's refusal reason.</param>
    /// <param name="workDir">The Persona's Work Dir, where a relative path resolves.</param>
    /// <param name="sources">Supplies the Team Memory access rules.</param>
    /// <returns>The guards.</returns>
    private IReadOnlyList<IToolCallGuard> CreateGuards(IPromptSource prompts, string workDir, SessionPromptSources sources)
    {
        string teamsRoot = this.TeamsRoot();
        string? protectedDirectory = WorkDirPermissionHandler.DefaultProtectedDirectory();
        IMergeGate? mergeGate = this.serviceProvider.GetService<IMergeGate>();
        return PermissionGuardList.InOrder(
            new AgentsMdGuard(teamsRoot, workDir, prompts),
            new TeamMemoryWriteGuard(teamsRoot, workDir, sources.TeamMemory, prompts),
            new RawGuard(teamsRoot, this.RawRoot(), this.ArchiveRoot(), workDir, prompts),
            new PromptsRootGuard(Path.GetFullPath(Path.Combine(this.options.DataDir, this.options.Library.Prompts.Dir)), workDir, prompts),
            new AgentSettingsGuard(workDir, Path.Combine(this.options.DataDir, "hooks")),
            protectedDirectory is null ? null : new ProtectedDirectoryGuard(protectedDirectory, workDir),
            mergeGate is null ? null : new MergeGateGuard(mergeGate));
    }

    /// <summary>Builds the AGENTS.md reader (capped by <c>Team:AgentsMd:MaxCharsPerFile</c>) with the Team Memory access rules and the memory warnings.</summary>
    /// <param name="prompts">The prompt source the reader renders its cut line from.</param>
    /// <returns>The reader.</returns>
    private SessionPromptSources CreatePromptSources(IPromptSource prompts)
    {
        return new SessionPromptSources(
            new AgentsMdReader(prompts, this.options.AgentsMd, this.loggerFactory.CreateLogger<AgentsMdReader>()),
            this.serviceProvider.GetRequiredService<TeamMemoryAccess>(),
            this.serviceProvider.GetRequiredService<MemoryWarnings>(),
            this.agentPaths,
            this.options.Outputs.MaxIndexEntries,
            this.serviceProvider.GetService<OverCapNotifier>());
    }

    /// <summary>What <see cref="BuildToolSet"/> built: the offered tools and the model-facing names the system prompt cites.</summary>
    /// <param name="Tools">The tools the session is offered, <c>get_help</c> first.</param>
    /// <param name="Prompts">The prompt source resolved from the application's container.</param>
    /// <param name="ToolNamePrefix">The <c>mcp__{server}__</c> prefix, or empty when the profile does not use one.</param>
    /// <param name="HelpToolName"><c>get_help</c>'s name with the prefix applied.</param>
    /// <param name="ReadSkillToolName"><c>read_skill</c>'s name with the prefix applied.</param>
    /// <param name="AskHumanToolName"><c>ask_human</c>'s name with the prefix applied.</param>
    private readonly record struct ToolSet(
        IReadOnlyList<IAppTool> Tools,
        IPromptSource Prompts,
        string ToolNamePrefix,
        string HelpToolName,
        string ReadSkillToolName,
        string AskHumanToolName);

    /// <summary>
    /// Whether the Persona is offered <c>propose_team_memory</c> (Team Memory Helper spec §6.1): when one of its Team labels names a
    /// Team with a folder, the same rule that gives it a Team Memory block, or when it is the Chief of Staff. Decided as the session
    /// starts; the tool checks membership again on every call, because it can change during the session.
    /// </summary>
    /// <param name="personaName">The Persona's Name.</param>
    /// <param name="teamLabels">The Persona's Team labels.</param>
    private bool ProposesTeamMemory(string personaName, IReadOnlyList<string> teamLabels) =>
        teamLabels.Any(label => this.teamCatalog.Find(label) is { HasFolder: true })
        || this.serviceProvider.GetRequiredService<TeamMemoryAccess>().IsChiefOfStaff(personaName);

    /// <summary>Builds, Skill-gates and prefixes the Persona's tools, ending with <c>get_help</c> over the offered list.</summary>
    /// <param name="persona">The Persona being started.</param>
    /// <param name="agentId">The Agent id the tools are bound to.</param>
    /// <param name="profile">The resolved Adapter Profile.</param>
    /// <param name="skillResolution">The Persona's resolved Skills.</param>
    /// <param name="proposesTeamMemory">Whether to offer <c>propose_team_memory</c>.</param>
    /// <param name="teamLabels">The Persona's Team labels; those with a Team folder become <c>get_help</c>'s Second Brain map.</param>
    /// <returns>The tool set.</returns>
    private ToolSet BuildToolSet(Persona persona, string agentId, AdapterProfile profile, SkillResolution skillResolution, bool proposesTeamMemory, IReadOnlyList<string> teamLabels)
    {
        // Built by our own service provider, bound to this Agent id, so each tool call runs the same
        // domain code the UI calls. AppToolServer's WebApplication.CreateBuilder() is a second,
        // genuinely separate DI container - the tool instances must never be resolved from it.
        // Captured in its own local, the same reason getHelpTool is below, so this method never
        // retypes "read_skill" - the Compose call reads it off readSkillTool.Name.
        var readSkillTool = ActivatorUtilities.CreateInstance<ReadSkillTool>(this.serviceProvider, persona.Name);
        var askHumanTool = ActivatorUtilities.CreateInstance<AskHumanTool>(this.serviceProvider, agentId);
        IReadOnlyList<IAppTool> chatTools = this.CreateChatTools(persona.Name, agentId, readSkillTool, askHumanTool);
        if (proposesTeamMemory)
        {
            chatTools = [.. chatTools, ActivatorUtilities.CreateInstance<ProposeTeamMemoryTool>(this.serviceProvider, persona.Name)];
        }

        chatTools = this.OfferChatTools(chatTools, persona, profile, skillResolution);

        var prompts = this.serviceProvider.GetRequiredService<IPromptSource>();

        // The mcp__{server}__ prefix is derived from the same server name above, never typed into a
        // prompt's template - see the ToolServerName remarks and SystemPromptComposer's. GetHelpTool
        // takes it explicitly rather than hard-coding its own copy, for the same reason. Whether it
        // is applied at all now follows the resolved profile (Spec §6.4): the server name itself
        // never changes, only whether model-facing names carry it.
        var toolNamePrefix = profile.UsesToolNamePrefix ? $"mcp__{ToolServerName}__" : string.Empty;

        // get_help is offered first and knows every other tool, so the system prompt can name one
        // tool instead of all of them. It is built last for the obvious reason: it takes the rest.
        // Captured in its own local, rather than only in the tools array below, so the system prompt
        // can name it from its own Name - never retyping "get_help" either.
        IReadOnlyList<string> teamNames = [.. teamLabels
            .Select(label => this.teamCatalog.Find(label))
            .OfType<TeamSummary>()
            .Where(static team => team.HasFolder)
            .Select(static team => team.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
        var getHelpTool = new GetHelpTool(
            chatTools,
            prompts,
            toolNamePrefix,
            teamNames,
            this.serviceProvider.GetRequiredService<Agency.Huddle.App.Data.RawBaselineStore>(),
            this.options);
        IReadOnlyList<IAppTool> tools = [getHelpTool, .. chatTools];

        return new ToolSet(
            tools,
            prompts,
            toolNamePrefix,
            toolNamePrefix + getHelpTool.Name,
            toolNamePrefix + readSkillTool.Name,
            toolNamePrefix + askHumanTool.Name);
    }
    /// <summary>Resolves the Persona's Adapter to the profile to launch, logging the resolver's warning when it has one.</summary>
    /// <param name="persona">The Persona being started.</param>
    /// <returns>The resolved Adapter Profile.</returns>
    private AdapterProfile ResolveProfile(Persona persona)
    {
        var (profile, warning) = this.resolver.Resolve(persona.Adapter);
        if (warning is not null)
        {
            this.logger.LogWarning(
                "Persona '{PersonaName}' Adapter resolution warning: {Warning}", persona.Name, warning);
        }

        return profile;
    }

    /// <summary>Lets each Feature Module prepare the Work Dir, before the Adapter process reads that folder's settings.</summary>
    /// <param name="workDir">The Persona's Work Dir.</param>
    /// <param name="toolNames">The session's tool names, already prefixed.</param>
    /// <param name="identity">The Persona's parsed identity, or <see langword="null"/>.</param>
    private void PrepareModuleWorkDirs(string workDir, IReadOnlyList<string> toolNames, PersonaIdentity? identity)
    {
        ModuleWorkDir moduleWorkDir = new(workDir, this.options.DataDir, toolNames, identity, this.serviceProvider, this.logger);
        foreach (IFeatureModule module in ModuleCatalog.All)
        {
            module.PrepareWorkDir(moduleWorkDir);
        }
    }

    /// <summary>
    /// Issue #396: when a merge gate is registered, puts the <c>PreToolUse</c> hook in the Work Dir that makes the Adapter
    /// send a permission request for <c>git merge</c> and <c>git push</c> even in its <c>auto</c> mode.
    /// </summary>
    /// <param name="workDir">The Persona's Work Dir.</param>
    /// <param name="dataDir">The App's data folder, which keeps the hook script.</param>
    /// <param name="mergeGate">The registered gate, or <see langword="null"/> when there is none, in which case nothing is installed.</param>
    /// <param name="logger">Receives an install failure.</param>
    internal static void InstallMergeGateHook(string workDir, string dataDir, IMergeGate? mergeGate, ILogger logger)
    {
        if (mergeGate is not null)
        {
            MergeGateHook.Install(workDir, dataDir, logger);
        }
    }

    /// <summary>Creates the Persona's Work Dir and the memory folder next to it.</summary>
    /// <param name="personaName">The Persona's Name.</param>
    /// <returns>The Work Dir and the memory folder paths.</returns>
    private (string WorkDir, string MemoryDir) CreateWorkDirs(string personaName)
    {
        var workDir = this.agentPaths.WorkDir(personaName);
        Directory.CreateDirectory(workDir);

        // FC §6.15: the folder is always created next to the Work Dir, even when the memory block
        // below is left out - so it is ready the moment File Changes and ReadsFiles both turn on.
        var memoryDir = Path.Combine(workDir, "memory");
        Directory.CreateDirectory(memoryDir);
        return (workDir, memoryDir);
    }

    /// <summary>Builds the process options for the Persona's Adapter, or throws when no adapter is installed.</summary>
    /// <param name="persona">The Persona being started.</param>
    /// <param name="profile">The resolved Adapter Profile.</param>
    /// <param name="workDir">The Persona's Work Dir.</param>
    /// <returns>The process options.</returns>
    private static AgentProcessOptions CreateProcessOptions(Persona persona, AdapterProfile profile, string workDir)
    {
        return AgentProcessOptionsFactory.TryCreate(profile, workDir, AppContext.BaseDirectory)
            ?? throw new InvalidOperationException(
                $"No ACP adapter is installed for Persona '{persona.Name}' on Adapter '{profile.Id}'. Run "
                + "tools/acp/install.ps1 (or set Team:Acp:AdapterPath / Team:Acp:Args) before enabling this Persona.");
    }

    /// <summary>
    /// Waits for a rename's Agent-folder move to settle (corrections-B2 item 20). The move can
    /// still be in flight (retrying against a not-yet-exited old process) when the runner it raced
    /// against restarts under the new Name and this factory starts it - wait for that move to settle
    /// first, or this would create a fresh, empty folder at the new Name while the move still holds
    /// the real one under the old Name, then have the move either collide with it or silently strand it.
    /// </summary>
    /// <param name="personaName">The Persona's Name.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    private async Task WaitForFolderMoveAsync(string personaName, CancellationToken cancellationToken)
    {
        await this.folderMoves.WhenSettledAsync(personaName, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Minted fresh per session and never logged: wire traces already leak it
    /// (acp-agent-guide.md §7.5), so this is the only place its value is held outside the tool server.
    /// </summary>
    /// <returns>A random bearer token.</returns>
    private static string MintAuthToken()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    }

    /// <summary>
    /// Whether the memory block is read at all. FC §6.15: the memory index used to be a snapshot
    /// taken once, at session start. RS §6.3 moves that into DotAcpPersonaHost.OpenAsync/ResumeAsync,
    /// rebuilt on every call, so a Room Session opened later sees what was remembered since - the
    /// gate itself (File Changes off, or an Adapter that cannot read files, means no memory block)
    /// is unchanged.
    /// </summary>
    /// <param name="profile">The resolved Adapter Profile.</param>
    /// <returns><see langword="true"/> when File Changes is on and the Adapter can read files.</returns>
    private bool ReadsMemory(AdapterProfile profile)
    {
        return this.options.FileChanges.Enabled && profile.ReadsFiles;
    }

    /// <summary>
    /// Parses the Persona's own text rather than threading the result through
    /// <see cref="IAgentHostFactory.StartAsync"/> (Spec §6.4 Implementation notes keeps it narrow). A
    /// Persona with no frontmatter at all - or malformed frontmatter - is not an error here: it simply
    /// holds no Skills. PersonaSupervisor resolves the same names a second time, purely to report a
    /// Degraded warning for one that does not exist (Task 5.2); this factory never surfaces that
    /// warning, only the resulting tool grants and Skill Index. The same parse gives the Teams line,
    /// whose Team Memory DotAcpPersonaHost lists at every session start (Spec §6.4); a Persona with
    /// no identity is in no Team.
    /// </summary>
    /// <param name="persona">The Persona being started.</param>
    /// <returns>The Persona's identity, or <see langword="null"/> when it has none.</returns>
    private static PersonaIdentity? ParseIdentity(Persona persona)
    {
        bool hasIdentity = PersonaFrontmatter.TryReadIdentity(persona.Text, out var identity, out _);
        return hasIdentity ? identity : null;
    }

    /// <summary>The full path of the Teams root folder.</summary>
    /// <returns>The absolute Teams root path.</returns>
    private string TeamsRoot()
    {
        return Path.GetFullPath(Path.Combine(this.options.DataDir, this.options.Teams.Dir));
    }

    /// <summary>The full path of the Raw root folder.</summary>
    /// <returns>The absolute Raw root path.</returns>
    private string RawRoot()
    {
        return Path.GetFullPath(Path.Combine(this.options.DataDir, this.options.Library.Raw.Dir));
    }

    /// <summary>The full path of the Archive root folder.</summary>
    /// <returns>The absolute Archive root path.</returns>
    private string ArchiveRoot()
    {
        return Path.GetFullPath(Path.Combine(this.options.DataDir, this.options.Library.Archive.Dir));
    }

    /// <summary>Creates the chat tools every Persona is offered before Skill gating.</summary>
    /// <param name="personaName">The Persona's Name, which the <c>memory</c> tool resolves the Persona's own folder and Team access from.</param>
    /// <param name="agentId">The Agent id the tools are bound to.</param>
    /// <param name="readSkillTool">The already-created <c>read_skill</c> tool.</param>
    /// <param name="askHumanTool">The already-created <c>ask_human</c> tool.</param>
    /// <returns>The chat tools, including the Task tools when Tasks is on.</returns>
    private IReadOnlyList<IAppTool> CreateChatTools(string personaName, string agentId, ReadSkillTool readSkillTool, AskHumanTool askHumanTool)
    {
        IReadOnlyList<IAppTool> chatTools =
        [
            ActivatorUtilities.CreateInstance<ListAgentsTool>(this.serviceProvider),
            ActivatorUtilities.CreateInstance<SearchTool>(this.serviceProvider, agentId),
            ActivatorUtilities.CreateInstance<CreateRoomTool>(this.serviceProvider, agentId),
            ActivatorUtilities.CreateInstance<InviteAgentTool>(this.serviceProvider, agentId),
            ActivatorUtilities.CreateInstance<PostMessageTool>(this.serviceProvider, agentId),
            ActivatorUtilities.CreateInstance<FollowRoomTool>(this.serviceProvider, agentId),
            ActivatorUtilities.CreateInstance<UnfollowRoomTool>(this.serviceProvider, agentId),
            askHumanTool,
            readSkillTool,
            ActivatorUtilities.CreateInstance<ValidateAgentTool>(this.serviceProvider),
            ActivatorUtilities.CreateInstance<ProposeAgentsTool>(this.serviceProvider, agentId),
            ActivatorUtilities.CreateInstance<ProposeSprintTool>(this.serviceProvider, agentId),
            ActivatorUtilities.CreateInstance<ListTeamsTool>(this.serviceProvider, agentId),
            ActivatorUtilities.CreateInstance<FileRequestTool>(this.serviceProvider, agentId),
            ActivatorUtilities.CreateInstance<TriageRequestTool>(this.serviceProvider, agentId),
            ActivatorUtilities.CreateInstance<ConfirmRequestTool>(this.serviceProvider, agentId),
            ActivatorUtilities.CreateInstance<ReadRecordsTool>(this.serviceProvider, agentId),
            ActivatorUtilities.CreateInstance<ProposeRetroChangesTool>(this.serviceProvider, agentId),
            ActivatorUtilities.CreateInstance<MarkRetroRowTool>(this.serviceProvider, agentId),

            // Memory is not optional know-how, so it is not gated by a Skill, and it is offered whether or not the
            // Adapter reads files (Instructions spec §5.1).
            ActivatorUtilities.CreateInstance<MemoryTool>(this.serviceProvider, personaName),
        ];

        // Spec §11.1: the six Task tools are not in SkillGrants.Grantable, so every Persona gets
        // them for free once Tasks is on - gated only by the feature flag, never by Skill. GetTaskTool
        // takes no agentId (corrections-B4 D10 item 1): reading a Task changes nothing, so it has no
        // actor to resolve.
        if (this.options.Tasks.Enabled)
        {
            chatTools =
            [
                .. chatTools,
                ActivatorUtilities.CreateInstance<CreateTaskTool>(this.serviceProvider, agentId),
                ActivatorUtilities.CreateInstance<GetTaskTool>(this.serviceProvider),
                ActivatorUtilities.CreateInstance<ListTasksTool>(this.serviceProvider, agentId),
                ActivatorUtilities.CreateInstance<UpdateTaskTool>(this.serviceProvider, agentId),
                ActivatorUtilities.CreateInstance<CloseTaskTool>(this.serviceProvider, agentId),
                ActivatorUtilities.CreateInstance<ReopenTaskTool>(this.serviceProvider, agentId),
                ActivatorUtilities.CreateInstance<AddEvidenceTool>(this.serviceProvider, agentId),
            ];
        }

        // Every Persona may schedule its own Jobs, so like the Task tools this is gated only by the feature flag; the
        // tool itself refuses a Job for anyone else unless the caller is the Chief of Staff.
        if (this.options.Jobs.Enabled)
        {
            chatTools = [.. chatTools, ActivatorUtilities.CreateInstance<JobsTool>(this.serviceProvider, personaName)];
        }

        return chatTools;
    }

    /// <summary>Applies Skill gating to the chat tools, then appends the File Changes folder-watch tools when they apply and each Feature Module's tools.</summary>
    /// <param name="chatTools">The ungated chat tools.</param>
    /// <param name="persona">The Persona being started.</param>
    /// <param name="profile">The resolved Adapter Profile.</param>
    /// <param name="skillResolution">The Persona's resolved Skills.</param>
    /// <returns>The tools this session is actually offered.</returns>
    private IReadOnlyList<IAppTool> OfferChatTools(
        IReadOnlyList<IAppTool> chatTools,
        Persona persona,
        AdapterProfile profile,
        SkillResolution skillResolution)
    {
        // Skill gating happens before GetHelpTool is built, so get_help's own listing and the system
        // prompt's tool list both reflect only the tools this session was actually offered (Spec §6.5:
        // "GetHelpTool is constructed from the offered list, so get_help and tools/list agree").
        chatTools = SkillGrants.Offer(chatTools, skillResolution.Skills);

        // FC §6.9, §6.11: watch_folder/unwatch_folder are offered only when File Changes is on for
        // this installation AND the resolved Adapter Profile can read files at all - an Adapter with
        // no file tools (agency-acp) would otherwise be handed paths it cannot open. Appended after
        // Skill gating and before GetHelpTool is built, so get_help lists them too (rules.md row 34).
        if (this.options.FileChanges.Enabled && profile.ReadsFiles)
        {
            _ = this.serviceProvider.GetRequiredService<FileChangeTracker>();
            IAppTool watchFolderTool = ActivatorUtilities.CreateInstance<WatchFolderTool>(this.serviceProvider, persona.Name);
            IAppTool unwatchFolderTool = ActivatorUtilities.CreateInstance<UnwatchFolderTool>(this.serviceProvider, persona.Name);
            chatTools = [.. chatTools, watchFolderTool, unwatchFolderTool];
        }

        // Feature Modules add theirs here, before GetHelpTool is built, so get_help lists them too.
        ModuleToolOffer offer = new(persona, profile, this.options, this.serviceProvider);
        foreach (IFeatureModule module in ModuleCatalog.All)
        {
            chatTools = [.. chatTools, .. module.OfferTools(offer)];
        }

        return chatTools;
    }

    /// <summary>Starts the ACP host, disposing the already-started tool server when the host fails to start.</summary>
    /// <param name="processOptions">How to launch the agent process.</param>
    /// <param name="toolServer">The Persona's started tool server.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>The started ACP host.</returns>
    private async Task<DotAcpAgentHost> StartInnerHostAsync(
        AgentProcessOptions processOptions,
        AppToolServer toolServer,
        CancellationToken cancellationToken)
    {
        try
        {
            var hostOptions = new DotAcpHostOptions(
                ClientName: "Team.App",
                TraceWire: this.options.Acp.TraceWire,
                AdvertiseElicitationForm: this.options.Acp.AdvertiseElicitation,
                AdvertiseSessionNotices: this.options.Acp.AdvertiseSessionNotices);
            var innerHost = new DotAcpAgentHost(processOptions, this.launcher, hostOptions, this.loggerFactory);
            await innerHost.StartAsync(cancellationToken).ConfigureAwait(false);
            return innerHost;
        }
        catch
        {
            await toolServer.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// RS §6.10 "Recommended" (P0-4), gated by finding P-11's flag: keeps a Persona's session off
    /// the Human's own Claude Code settings and CLAUDE.md, and off its auto-memory, so FC §6.15's
    /// memory folder is the only memory that session sees. Unverified until Task 14.3.m passes -
    /// see <see cref="AdapterProfile.IsolateUserSettings"/>'s remarks for the known settings-replacement risk.
    /// Minted once per host (RS §6.3): every session this Persona's host opens or resumes carries
    /// the same Meta, exactly as it always carried the same token.
    /// </summary>
    /// <param name="profile">The resolved Adapter Profile.</param>
    /// <param name="connectors">
    /// Whether the Human's claude.ai account connectors stay available. <see langword="false"/> adds
    /// <c>options.env</c> with the one key <c>ENABLE_CLAUDEAI_MCP_SERVERS=false</c> (Agent Connectors Spec 8.1); a
    /// future writer of <c>options.env</c> must add to that dictionary, not replace it. <see langword="true"/> adds nothing.
    /// No <c>strict-mcp-config</c> and no <c>tools</c> entry is added either way.
    /// </param>
    /// <param name="askRules">
    /// Whether the isolating profile's settings carry the <c>permissions.ask</c> list (<see cref="AskRules"/>). Only an Agent wants it: a Helper
    /// has no tools and passes <see langword="false"/>. A profile that runs Claude without isolating never gets <c>settings</c>, because it would
    /// replace the Adapter's computed settings and is known to drop a <c>CLAUDE_MODEL_CONFIG</c> override: for it the merge gate keeps only the hook
    /// (see known-limits.md).
    /// </param>
    /// <returns>The isolation <c>_meta</c>, or <see langword="null"/> when the profile does not ask for it.</returns>
    internal static Dictionary<string, object>? BuildIsolationMeta(AdapterProfile profile, bool connectors, bool askRules = true)
    {
        Dictionary<string, object> options = new(StringComparer.Ordinal);
        if (profile.IsolateUserSettings)
        {
            options["settingSources"] = new[] { "project", "local" };
            Dictionary<string, object> settings = new(StringComparer.Ordinal)
            {
                ["autoMemoryEnabled"] = false,
            };
            if (askRules)
            {
                // `settings` replaces the Adapter's computed settings and a `permissions` key replaces any other, so the one list is
                // sent here beside autoMemoryEnabled, whatever `connectors` is (issue #432).
                settings["permissions"] = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["ask"] = AskRules(connectors),
                };
            }

            options["settings"] = settings;
        }
        else if (connectors || !AdapterCatalog.RunsClaude(profile))
        {
            // A profile that does not ask for isolation gets no meta, with one exception below: a Claude profile with the connectors off.
            return null;
        }

        // A custom profile that runs Claude without isolating gets only the env key, so the connectors are off for it as well and
        // no isolation key is forced on a profile that did not ask for it. (Agent Connectors spec 7.3, first rows.)
        if (!connectors)
        {
            options["env"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["ENABLE_CLAUDEAI_MCP_SERVERS"] = "false",
            };
        }

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["claudeCode"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["options"] = options,
            },
        };
    }

    /// <summary>
    /// The rules every Agent session sends as <c>options.settings.permissions.ask</c> (issue #432). An <c>ask</c> rule is not on disk, so an
    /// Agent that rewrites <c>.claude/settings.local.json</c>, overwrites the merge hook script or sets <c>disableAllHooks</c> cannot remove it, and
    /// it beats the Agent's own local allow rule and allow hook: the request reaches <see cref="WorkDirPermissionHandler"/>, which decides.
    /// The git verbs are the ones <see cref="GitCommandLine"/> and <see cref="MergeGateHook"/> look for (merge, push, pull). The wildcard form
    /// matches <c>git -c x=y merge</c> and <c>bash -c "git merge"</c>, which the prefix form <c>Bash(git merge:*)</c> does not. <c>Edit</c> and
    /// <c>Write</c> send every file edit to the handler, so the <c>AGENTS.md</c>, Raw, Prompts, Team Memory and settings guards see it even
    /// when the Agent holds a local allow for them. MEASURED live (Sonnet, Adapter 0.100.0): the three <c>Bash</c> forms, <c>Edit</c>, <c>Write</c>
    /// and <c>mcp__claude_ai_*</c> against a stand-in server. UNMEASURED: the three <c>PowerShell(...)</c> forms (the Adapter's PowerShell tool is
    /// guarded by the same handler and hook matcher, but its rule form was not run), <c>auto</c> and <c>acceptEdits</c> mode, and a real connector.
    /// <c>MultiEdit</c> and <c>NotebookEdit</c> are left out: the Adapter reports neither as an edit (the file guards only read kind edit,
    /// delete and move), so no guard applies to them.
    /// </summary>
    private static readonly string[] AgentAskRules =
    [
        "Bash(*git*merge*)", "Bash(*git*push*)", "Bash(*git*pull*)",
        "PowerShell(*git*merge*)", "PowerShell(*git*push*)", "PowerShell(*git*pull*)",
        "Edit", "Write",
    ];

    /// <summary>The Agent's <c>permissions.ask</c> list: <see cref="AgentAskRules"/>, plus every claude.ai connector tool when the connectors are on (Agent Connectors spec 8.4).</summary>
    /// <param name="connectors">Whether the account connectors are passed.</param>
    /// <returns>A new array.</returns>
    internal static string[] AskRules(bool connectors) =>
        connectors ? [.. AgentAskRules, "mcp__claude_ai_*"] : [.. AgentAskRules];

    /// <summary>Builds the factory that produces the Persona's role block at each session start.</summary>
    /// <param name="parsed">The Persona's parsed identity, or <see langword="null"/> when it has none.</param>
    /// <param name="skillResolution">The Persona's resolved Skills.</param>
    /// <returns>A function returning the role block, or <see langword="null"/> for a Persona with no identity.</returns>
    private Func<RoleBlockInput?> CreateRoleBlock(PersonaIdentity? parsed, SkillResolution skillResolution)
    {
        PersonaStore personaStore = this.serviceProvider.GetRequiredService<PersonaStore>();

        return () => parsed is null
            ? null
            : RoleBlockBuilder.Build(
                parsed,
                skillResolution.Skills,
                this.teamCatalog,
                personaStore.Get);
    }

    /// <summary>
    /// Composes, as labelled parts, what the Persona's next root session would be told (Spec §6.5). Read-only: it builds the tool
    /// objects but starts no server or process, creates no folder and spends no Turn.
    /// </summary>
    /// <param name="persona">The Persona to preview.</param>
    /// <returns>The parts in prompt order and the id of the Adapter they were composed for.</returns>
    internal (IReadOnlyList<PromptPart> Parts, string Adapter) PreviewParts(Persona persona)
    {
        ArgumentNullException.ThrowIfNull(persona);

        AdapterProfile profile = this.ResolveProfile(persona);
        PersonaIdentity? parsed = ParseIdentity(persona);
        IReadOnlyList<string> teamLabels = parsed?.Teams ?? [];
        SkillResolution skillResolution = this.skills.Resolve(parsed?.Skills ?? []);
        ToolSet toolSet = this.BuildToolSet(persona, persona.Name, profile, skillResolution, this.ProposesTeamMemory(persona.Name, teamLabels), teamLabels);
        IReadOnlyList<string> toolNames = [.. toolSet.Tools.Select(tool => $"{toolSet.ToolNamePrefix}{tool.Name}")];
        SessionPromptSources sources = this.CreatePromptSources(toolSet.Prompts);

        SessionPromptSnapshot inputs = SessionPromptInputs.Build(
            sources.AgentsMd,
            sources.Warnings,
            this.teamCatalog.Teams,
            persona.Name,
            Path.Combine(this.agentPaths.WorkDir(persona.Name), "memory"),
            this.options.FileChanges.MaxMemoryEntries,
            teamLabels,
            this.TeamsRoot(),
            this.options.Teams.MaxMemoryEntries,
            this.options.Teams.MaxHuddleMemoryEntries,
            sources.MaxOutputsEntries);

        IReadOnlyList<PromptPart> parts = SystemPromptComposer.ComposeParts(
            persona,
            toolSet.Prompts,
            toolSet.HelpToolName,
            toolNames,
            skillResolution.Skills,
            toolSet.ReadSkillToolName,
            inputs.Memory,
            SessionScope.Shared,
            teamMemory: inputs.TeamMemory,
            askHumanToolName: toolSet.AskHumanToolName,
            roleBlock: this.CreateRoleBlock(parsed, skillResolution)(),
            agentsMd: inputs.AgentsMd,
            memoryChangesListed: this.ReadsMemory(profile),
            warnings: sources.Warnings,
            huddleMemory: inputs.HuddleMemory,
            outputs: inputs.Outputs,
            workspaces: inputs.Workspaces);
        return (parts, profile.Id);
    }
}

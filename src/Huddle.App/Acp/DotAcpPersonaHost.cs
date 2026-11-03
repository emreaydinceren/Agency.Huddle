using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.DotAcp;
using Agency.Huddle.Acp.Tools;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Skills;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// The real <see cref="IPersonaHost"/>: one Persona's <see cref="DotAcpAgentHost"/> and
/// <see cref="AppToolServer"/>, started once by <see cref="DotAcpAgentHostFactory.StartAsync"/>
/// (RS §6.3). Owns everything <see cref="SystemPromptComposer.Compose(Persona,IPromptSource,string,IReadOnlyList{string},IReadOnlyList{Skill},string,MemorySnapshot?)"/>
/// and <see cref="AgentSessionOptions"/> need to open or resume a Room's session, so that work can
/// run again, unchanged, on every call rather than only once.
/// </summary>
/// <remarks>
/// Disposing this type disposes the <see cref="AppToolServer"/> it was built with, exactly as the
/// factory's former <c>ToolServerOwningAgentHost</c> did: the factory owns the tool server's
/// lifetime, not the inner host alone.
/// </remarks>
internal sealed class DotAcpPersonaHost : IPersonaHost
{
    private readonly DotAcpAgentHost inner;
    private readonly AppToolServer toolServer;
    private readonly Persona persona;
    private readonly IPromptSource prompts;
    private readonly string helpToolName;
    private readonly IReadOnlyList<string> toolNames;
    private readonly IReadOnlyList<Skill> skills;
    private readonly string readSkillToolName;
    private readonly string askHumanToolName;
    private readonly string memoryDir;
    private readonly bool readsMemory;
    private readonly int maxMemoryEntries;
    private readonly IReadOnlyList<string> teamLabels;
    private readonly ITeamCatalog teamCatalog;
    private readonly string teamsRoot;
    private readonly PermissionGuards guards;
    private readonly int maxTeamMemoryEntries;
    private readonly string workDir;
    private readonly ToolServerEndpoint toolServerEndpoint;
    private readonly IReadOnlyDictionary<string, object>? meta;
    private readonly ILoggerFactory loggerFactory;
    private readonly WorkModePolicy workModePolicy;
    private readonly Func<RoleBlockInput?> roleBlock;
    private readonly IMergeGate? mergeGate;
    private readonly SessionPromptSources sources;

    /// <summary>Initializes a new instance of the <see cref="DotAcpPersonaHost"/> class.</summary>
    /// <param name="inner">The started ACP host, ready for <c>session/new</c> or <c>session/resume</c>.</param>
    /// <param name="toolServer">This Persona's already-started App Tool server, disposed alongside this host.</param>
    /// <param name="profile">The resolved Adapter Profile this host was started against (finding P-8).</param>
    /// <param name="persona">The Persona whose text, Model and Effort every opened session carries.</param>
    /// <param name="prompts">Resolves every prompt's current text for <see cref="SystemPromptComposer"/>.</param>
    /// <param name="helpToolName"><c>get_help</c>'s own name, already carrying its full prefix.</param>
    /// <param name="toolNames">Every tool name this host's session exposes, already prefixed.</param>
    /// <param name="skills">The Persona's resolved Skills for this host's sessions.</param>
    /// <param name="readSkillToolName"><c>read_skill</c>'s own name, already carrying its full prefix.</param>
    /// <param name="askHumanToolName"><c>ask_human</c>'s own name, already carrying its full prefix; named in the system prompt's asking-the-Human paragraph.</param>
    /// <param name="memoryDir">The Persona's memory folder (FC §6.15), created whether or not it is read.</param>
    /// <param name="readsMemory">Whether File Changes is on and the resolved Adapter can read files.</param>
    /// <param name="maxMemoryEntries">The cap <see cref="MemoryIndex.Build"/> applies when <paramref name="readsMemory"/>.</param>
    /// <param name="teamLabels">The Team labels of the Persona's Teams line, in the order written; empty for a Persona in no Team.</param>
    /// <param name="teamCatalog">The Teams and their Projects; its snapshot is read once per session build.</param>
    /// <param name="teamsRoot">The full path of the Teams root folder.</param>
    /// <param name="guards">The file guards every opened session's permission handler consults.</param>
    /// <param name="maxTeamMemoryEntries">The cap <see cref="TeamMemoryIndex.Build"/> applies across all of the Persona's Teams when <paramref name="readsMemory"/>.</param>
    /// <param name="workDir">The Persona's Work Dir, this host's session <c>cwd</c>.</param>
    /// <param name="toolServerEndpoint">The endpoint and bearer token every opened session is handed.</param>
    /// <param name="meta">D14's isolation <c>_meta</c> (RS §6.10), or <see langword="null"/> when the profile does not ask for it.</param>
    /// <param name="loggerFactory">Creates the logger a fresh <see cref="WorkDirPermissionHandler"/> needs on every open or resume.</param>
    /// <param name="workModePolicy">Decides whether the Persona's Work Mode may be sent (ADR-0033).</param>
    /// <param name="roleBlock">Builds the Persona's role block at each session start, so a changed Team reaches the next session; returns <see langword="null"/> for a Persona with no role.</param>
    /// <param name="sources">Reads the Human's AGENTS.md files, decides who writes a Team's Memory and warns about malformed memory files.</param>
    /// <param name="mergeGate">Decides the Persona's <c>git merge</c> and <c>git push</c> requests (PR-7); <see langword="null"/> approves them.</param>
    public DotAcpPersonaHost(
        DotAcpAgentHost inner,
        AppToolServer toolServer,
        AdapterProfile profile,
        Persona persona,
        IPromptSource prompts,
        string helpToolName,
        IReadOnlyList<string> toolNames,
        IReadOnlyList<Skill> skills,
        string readSkillToolName,
        string askHumanToolName,
        string memoryDir,
        bool readsMemory,
        int maxMemoryEntries,
        IReadOnlyList<string> teamLabels,
        ITeamCatalog teamCatalog,
        string teamsRoot,
        PermissionGuards guards,
        int maxTeamMemoryEntries,
        string workDir,
        ToolServerEndpoint toolServerEndpoint,
        IReadOnlyDictionary<string, object>? meta,
        ILoggerFactory loggerFactory,
        WorkModePolicy workModePolicy,
        Func<RoleBlockInput?> roleBlock,
        SessionPromptSources sources,
        IMergeGate? mergeGate = null)
    {
        this.mergeGate = mergeGate;
        ValidateSessionPromptSources(roleBlock, sources, guards);
        this.sources = sources;
        this.roleBlock = roleBlock;
        ValidateCollaborators(inner, toolServer, profile, persona, prompts);
        ArgumentException.ThrowIfNullOrWhiteSpace(helpToolName);
        ArgumentNullException.ThrowIfNull(toolNames);
        ArgumentNullException.ThrowIfNull(skills);
        ArgumentException.ThrowIfNullOrWhiteSpace(askHumanToolName);
        ArgumentException.ThrowIfNullOrWhiteSpace(memoryDir);
        ArgumentNullException.ThrowIfNull(teamLabels);
        ArgumentNullException.ThrowIfNull(teamCatalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(teamsRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(workDir);
        ArgumentNullException.ThrowIfNull(toolServerEndpoint);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(workModePolicy);

        this.inner = inner;
        this.toolServer = toolServer;
        this.Profile = profile;
        this.persona = persona;
        this.prompts = prompts;
        this.helpToolName = helpToolName;
        this.toolNames = toolNames;
        this.skills = skills;
        this.readSkillToolName = readSkillToolName;
        this.askHumanToolName = askHumanToolName;
        this.memoryDir = memoryDir;
        this.readsMemory = readsMemory;
        this.maxMemoryEntries = maxMemoryEntries;
        this.teamLabels = teamLabels;
        this.teamCatalog = teamCatalog;
        this.teamsRoot = teamsRoot;
        this.guards = guards;
        this.maxTeamMemoryEntries = maxTeamMemoryEntries;
        this.workDir = workDir;
        this.toolServerEndpoint = toolServerEndpoint;
        this.meta = meta;
        this.loggerFactory = loggerFactory;
        this.workModePolicy = workModePolicy;
    }

    /// <summary>Guards the collaborators that build the system prompt's role block and AGENTS.md block and decide who writes a Team's Memory.</summary>
    /// <param name="roleBlock">The role block factory.</param>
    /// <param name="sources">The AGENTS.md reader, Team Memory access rules and memory warnings.</param>
    /// <param name="guards">The file guards the permission handler consults.</param>
    private static void ValidateSessionPromptSources(Func<RoleBlockInput?> roleBlock, SessionPromptSources sources, PermissionGuards guards)
    {
        ArgumentNullException.ThrowIfNull(roleBlock);
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(guards);
    }

    /// <summary>Guards the constructor's collaborator arguments, in declaration order.</summary>
    /// <param name="inner">The started ACP host.</param>
    /// <param name="toolServer">The Persona's App Tool server.</param>
    /// <param name="profile">The resolved Adapter Profile.</param>
    /// <param name="persona">The Persona.</param>
    /// <param name="prompts">The prompt source.</param>
    private static void ValidateCollaborators(DotAcpAgentHost inner, AppToolServer toolServer, AdapterProfile profile, Persona persona, IPromptSource prompts)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(toolServer);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(persona);
        ArgumentNullException.ThrowIfNull(prompts);
    }

    public AdapterProfile Profile { get; }

    public bool CanResume => this.inner.Info.SupportsResumeSession;

    public AgentPromptCapabilities PromptCapabilities => this.inner.Info.PromptCapabilities ?? AgentPromptCapabilities.None;

    public async Task<IAgentSession> OpenAsync(CancellationToken cancellationToken)
    {
        return await this.inner.StartSessionAsync(this.BuildOptions(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resumes <paramref name="sessionId"/>, or returns <see langword="null"/> when the Adapter no
    /// longer has it (RS §6.1: "null means not found → fresh"). Resuming an id still open in the
    /// same Adapter process returns the existing session and ignores the new <c>_meta</c> this call
    /// builds: <c>claude-agent-acp</c> fingerprints a resumable session only on <c>cwd</c> and
    /// <c>mcpServers</c>, so a changed system prompt or memory index never reaches an already-open
    /// session this way — only a fresh <see cref="OpenAsync"/> composes one that is actually sent.
    /// </summary>
    /// <param name="sessionId">The id a previous open or resume minted.</param>
    /// <param name="cancellationToken">Cancels the resume.</param>
    public async Task<IAgentSession?> ResumeAsync(string sessionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        try
        {
            return await this.inner.ResumeSessionAsync(sessionId, this.BuildOptions(), cancellationToken).ConfigureAwait(false);
        }
        catch (AgentSessionNotFoundException)
        {
            return null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await this.inner.DisposeAsync().ConfigureAwait(false);
        await this.toolServer.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Builds one session's <see cref="AgentSessionOptions"/>: the system prompt composed now, with
    /// a memory index rebuilt now (FC §6.15's "the memory index is current"), so every call —
    /// whether <see cref="OpenAsync"/> or <see cref="ResumeAsync"/> — sends what
    /// <c>DotAcpAgentHostFactory.StartAsync</c> used to send exactly once.
    /// </summary>
    private AgentSessionOptions BuildOptions()
    {
        // Enforced here, where the options are built, so a database row written by hand for a hidden
        // mode is inert (ADR-0033). Whether the Adapter advertises the mode is DotAcpAgentHost's check.
        string? mode = this.workModePolicy.EffectiveMode(this.persona.Name, this.persona.WorkMode, this.loggerFactory.CreateLogger<DotAcpPersonaHost>());

        return new AgentSessionOptions(
            this.workDir,
            DotAcpPersonaHost.GuardPermissions(this.CreatePermissionHandler(), mode, this.loggerFactory),
            new SystemPromptOptions(this.ComposeSystemPrompt(), SystemPromptMode.Append),
            this.toolServerEndpoint,
            this.persona.Model,
            this.persona.Effort,
            this.meta,
            mode);
    }

    /// <summary>
    /// Approves the way <see cref="AutoApprovePermissionHandler"/> does, except inside the human's own
    /// agent configuration directory - see <see cref="WorkDirPermissionHandler"/> for why that one
    /// exception is drawn there and not around the Work Dir - and except for a write to an Instructions
    /// file and a non-manager's write into a Team's Memory. If the profile directory cannot be resolved the
    /// handler has no path to protect there, rather than inventing one, and the two file guards still apply. Built fresh per call: the handler itself holds no
    /// state worth reusing across opens. A plan-mode Persona's handler is wrapped so the Adapter's
    /// request to leave plan mode is refused (see <see cref="PlanModePermissionHandler"/>).
    /// </summary>
    /// <returns>The handler this session's permission requests go to.</returns>
    private WorkDirPermissionHandler CreatePermissionHandler()
    {
        // Both file guards stay on even when the profile directory cannot be resolved: that case only switches the ~/.claude refusal off.
        return new WorkDirPermissionHandler(
            WorkDirPermissionHandler.DefaultProtectedDirectory(),
            this.loggerFactory.CreateLogger<WorkDirPermissionHandler>(),
            this.mergeGate,
            this.persona.Name,
            this.guards,
            this.workDir);
    }

    /// <summary>
    /// Composes the system prompt now, with the memory index and the AGENTS.md files read afresh. The Memory and Team Memory blocks are
    /// built whenever the memory tool is offered, which is always, so an Adapter with no file tools gets them too; only the sentences
    /// that name File Changes depend on <see cref="readsMemory"/> (Instructions spec §6.1).
    /// </summary>
    /// <returns>The prompt appended to the Adapter's own.</returns>
    private string ComposeSystemPrompt()
    {
        var (entries, notListed) = MemoryIndex.Build(this.memoryDir, this.maxMemoryEntries, this.sources.Warnings);
        MemorySnapshot memory = new(this.memoryDir, entries, notListed);

        TeamMemorySnapshot? teamMemory = null;
        if (this.teamLabels.Count > 0)
        {
            // Read once into a local: this runs per open or resume, concurrently across Rooms, and
            // the catalog's snapshot is swapped atomically, so one read is one consistent view.
            IReadOnlyList<TeamSummary> catalogTeams = this.teamCatalog.Teams;
            TeamMemorySnapshot built = TeamMemoryIndex.Build(this.teamsRoot, this.teamLabels, catalogTeams, this.maxTeamMemoryEntries, this.persona.Name, this.sources.Warnings);
            teamMemory = built.Groups.Count > 0 ? built : null;
        }

        return SystemPromptComposer.Compose(
            this.persona,
            this.prompts,
            this.helpToolName,
            this.toolNames,
            this.skills,
            this.readSkillToolName,
            memory,
            this.Profile.SessionPerRoom ? SessionScope.PerRoom : SessionScope.Shared,
            teamMemory: teamMemory,
            askHumanToolName: this.askHumanToolName,
            roleBlock: this.roleBlock(),
            agentsMd: this.ReadAgentsMd(),
            memoryChangesListed: this.readsMemory,
            warnings: this.sources.Warnings);
    }

    /// <summary>
    /// Reads the Human's AGENTS.md files now, so an edit made since the last session reaches the next one.
    /// The Teams are the Persona's own, named by the catalog's folder spelling.
    /// </summary>
    /// <returns>The files read, or an empty snapshot.</returns>
    private AgentsMdSnapshot ReadAgentsMd()
    {
        IReadOnlyList<TeamSummary> catalogTeams = this.teamCatalog.Teams;
        List<string> teamNames =
        [
            .. this.teamLabels.Select(label =>
                catalogTeams.FirstOrDefault(team => string.Equals(team.Name, label, StringComparison.OrdinalIgnoreCase))?.Name ?? label),
        ];

        return this.sources.AgentsMd.Read(this.teamsRoot, teamNames);
    }

    /// <summary>
    /// Wraps <paramref name="handler"/> in the plan guard when the Persona's effective Work Mode is
    /// <c>plan</c>, and returns it unchanged for every other Persona, so nothing changes for a Persona
    /// with no Work Mode.
    /// </summary>
    /// <param name="handler">The handler every other request goes to.</param>
    /// <param name="mode">The Persona's effective Work Mode, or <see langword="null"/>.</param>
    /// <param name="loggerFactory">Creates the guard's logger.</param>
    /// <returns>The handler to hand the session.</returns>
    internal static IPermissionHandler GuardPermissions(IPermissionHandler handler, string? mode, ILoggerFactory loggerFactory)
    {
        return string.Equals(mode, PlanModePermissionHandler.PlanModeId, StringComparison.Ordinal)
            ? new PlanModePermissionHandler(handler, mode, loggerFactory.CreateLogger<PlanModePermissionHandler>())
            : handler;
    }
}

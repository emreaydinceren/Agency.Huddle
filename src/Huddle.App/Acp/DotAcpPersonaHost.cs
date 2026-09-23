using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.DotAcp;
using Agency.Huddle.Acp.Tools;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Skills;

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
    private readonly string memoryDir;
    private readonly bool readsMemory;
    private readonly int maxMemoryEntries;
    private readonly string workDir;
    private readonly ToolServerEndpoint toolServerEndpoint;
    private readonly IReadOnlyDictionary<string, object>? meta;
    private readonly ILoggerFactory loggerFactory;

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
    /// <param name="memoryDir">The Persona's memory folder (FC §6.15), created whether or not it is read.</param>
    /// <param name="readsMemory">Whether File Changes is on and the resolved Adapter can read files.</param>
    /// <param name="maxMemoryEntries">The cap <see cref="MemoryIndex.Build"/> applies when <paramref name="readsMemory"/>.</param>
    /// <param name="workDir">The Persona's Work Dir, this host's session <c>cwd</c>.</param>
    /// <param name="toolServerEndpoint">The endpoint and bearer token every opened session is handed.</param>
    /// <param name="meta">D14's isolation <c>_meta</c> (RS §6.10), or <see langword="null"/> when the profile does not ask for it.</param>
    /// <param name="loggerFactory">Creates the logger a fresh <see cref="WorkDirPermissionHandler"/> needs on every open or resume.</param>
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
        string memoryDir,
        bool readsMemory,
        int maxMemoryEntries,
        string workDir,
        ToolServerEndpoint toolServerEndpoint,
        IReadOnlyDictionary<string, object>? meta,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(toolServer);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(persona);
        ArgumentNullException.ThrowIfNull(prompts);
        ArgumentException.ThrowIfNullOrWhiteSpace(helpToolName);
        ArgumentNullException.ThrowIfNull(toolNames);
        ArgumentNullException.ThrowIfNull(skills);
        ArgumentException.ThrowIfNullOrWhiteSpace(memoryDir);
        ArgumentException.ThrowIfNullOrWhiteSpace(workDir);
        ArgumentNullException.ThrowIfNull(toolServerEndpoint);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        this.inner = inner;
        this.toolServer = toolServer;
        this.Profile = profile;
        this.persona = persona;
        this.prompts = prompts;
        this.helpToolName = helpToolName;
        this.toolNames = toolNames;
        this.skills = skills;
        this.readSkillToolName = readSkillToolName;
        this.memoryDir = memoryDir;
        this.readsMemory = readsMemory;
        this.maxMemoryEntries = maxMemoryEntries;
        this.workDir = workDir;
        this.toolServerEndpoint = toolServerEndpoint;
        this.meta = meta;
        this.loggerFactory = loggerFactory;
    }

    public AdapterProfile Profile { get; }

    public bool CanResume => this.inner.Info.SupportsResumeSession;

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
        MemorySnapshot? memory = null;
        if (this.readsMemory)
        {
            var (entries, notListed) = MemoryIndex.Build(this.memoryDir, this.maxMemoryEntries);
            memory = new MemorySnapshot(this.memoryDir, entries, notListed);
        }

        return new AgentSessionOptions(
            this.workDir,
            // Approves the way AutoApprovePermissionHandler does, except inside the human's own
            // agent configuration directory - see WorkDirPermissionHandler for why that one
            // exception is drawn there and not around the Work Dir. Falls back to the plain
            // auto-approve only if the profile directory cannot be resolved at all, rather than
            // inventing a path to protect. Built fresh per call: the handler itself holds no state
            // worth reusing across opens.
            WorkDirPermissionHandler.DefaultProtectedDirectory() is { } protectedDirectory
                ? new WorkDirPermissionHandler(protectedDirectory, this.loggerFactory.CreateLogger<WorkDirPermissionHandler>())
                : new AutoApprovePermissionHandler(),
            new SystemPromptOptions(
                SystemPromptComposer.Compose(
                    this.persona,
                    this.prompts,
                    this.helpToolName,
                    this.toolNames,
                    this.skills,
                    this.readSkillToolName,
                    memory),
                SystemPromptMode.Append),
            this.toolServerEndpoint,
            this.persona.Model,
            this.persona.Effort,
            this.meta);
    }
}

using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.DotAcp;
using Agency.Huddle.Acp.Hosting;
using Agency.Huddle.Acp.Tools;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Skills;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// The real <see cref="IAgentHostFactory"/>: launches the ACP Claude Code adapter as a child process
/// and wires it to the application's own tools over one <see cref="AppToolServer"/>.
/// </summary>
/// <remarks>
/// This runs one <see cref="AppToolServer"/> per Persona, on its own ephemeral loopback port, matching
/// <c>Team.Console</c>. agent-guide.md §7.5 argues the eventual shape for a multi-persona host is a
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
    /// through <see cref="IAgentHostFactory.CreateAsync"/>'s parameters, because that signature stays
    /// narrow (Spec §6.4 Implementation notes) — <c>PersonaSupervisor</c> resolves the same names a
    /// second time, only for the Degraded warnings it reports before ever calling this factory.
    /// </param>
    public DotAcpAgentHostFactory(
        IOptions<TeamOptions> options,
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory,
        AdapterProfileResolver resolver,
        IAgentProcessLauncher launcher,
        SkillStore skills)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentNullException.ThrowIfNull(skills);

        this.options = options.Value;
        this.serviceProvider = serviceProvider;
        this.loggerFactory = loggerFactory;
        this.logger = loggerFactory.CreateLogger<DotAcpAgentHostFactory>();
        this.resolver = resolver;
        this.launcher = launcher;
        this.skills = skills;
    }

    public async Task<(IAgentHost Host, IAgentSession Session)> CreateAsync(Persona persona, string agentId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(persona);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);

        var (profile, warning) = this.resolver.Resolve(persona.Adapter);
        if (warning is not null)
        {
            this.logger.LogWarning(
                "Persona '{PersonaName}' Adapter resolution warning: {Warning}", persona.Name, warning);
        }

        var workDir = Path.Combine(this.options.DataDir, this.options.Acp.WorkDir, persona.Name);
        Directory.CreateDirectory(workDir);

        var processOptions = AgentProcessOptionsFactory.TryCreate(profile, workDir, AppContext.BaseDirectory)
            ?? throw new InvalidOperationException(
                $"No ACP adapter is installed for Persona '{persona.Name}' on Adapter '{profile.Id}'. Run "
                + "tools/acp/install.ps1 (or set Team:Acp:AdapterPath / Team:Acp:Args) before enabling this Persona.");

        // Minted fresh per session and never logged: wire traces already leak it
        // (agent-guide.md §7.5), so this is the only place its value is held outside the tool server.
        var authToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

        // Parsed from the Persona's own text rather than threaded through this method's signature
        // (Spec §6.4 Implementation notes keeps IAgentHostFactory.CreateAsync narrow). A Persona with
        // no frontmatter at all - or malformed frontmatter - is not an error here: it simply holds no
        // Skills. PersonaSupervisor resolves the same names a second time, purely to report a Degraded
        // warning for one that does not exist (Task 5.2); this factory never surfaces that warning,
        // only the resulting tool grants and Skill Index.
        IReadOnlyList<string> skillNames = PersonaFrontmatter.TryReadIdentity(persona.Text, out var identity, out _)
            ? identity.Skills ?? []
            : [];
        SkillResolution skillResolution = this.skills.Resolve(skillNames);

        // Built by our own service provider, bound to this Agent id, so each tool call runs the same
        // domain code the UI calls. AppToolServer's WebApplication.CreateBuilder() is a second,
        // genuinely separate DI container - the tool instances must never be resolved from it.
        // Captured in its own local, the same reason getHelpTool is below, so this method never
        // retypes "read_skill" - the Compose call further down reads it off readSkillTool.Name.
        var readSkillTool = ActivatorUtilities.CreateInstance<ReadSkillTool>(this.serviceProvider, persona.Name);
        IReadOnlyList<IAppTool> chatTools =
        [
            ActivatorUtilities.CreateInstance<ListAgentsTool>(this.serviceProvider),
            ActivatorUtilities.CreateInstance<CreateRoomTool>(this.serviceProvider, agentId),
            ActivatorUtilities.CreateInstance<InviteAgentTool>(this.serviceProvider),
            ActivatorUtilities.CreateInstance<PostMessageTool>(this.serviceProvider, agentId),
            ActivatorUtilities.CreateInstance<FollowRoomTool>(this.serviceProvider, agentId),
            ActivatorUtilities.CreateInstance<UnfollowRoomTool>(this.serviceProvider, agentId),
            readSkillTool,
            ActivatorUtilities.CreateInstance<ValidateTeammateTool>(this.serviceProvider),
            ActivatorUtilities.CreateInstance<ProposeTeammatesTool>(this.serviceProvider, agentId),
        ];

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
            var fileChanges = this.serviceProvider.GetRequiredService<FileChangeTracker>();
            IAppTool watchFolderTool = ActivatorUtilities.CreateInstance<WatchFolderTool>(this.serviceProvider, persona.Name);
            IAppTool unwatchFolderTool = ActivatorUtilities.CreateInstance<UnwatchFolderTool>(this.serviceProvider, persona.Name);
            chatTools = [.. chatTools, watchFolderTool, unwatchFolderTool];
        }

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
        // can name it from its own Name below - never retyping "get_help" either.
        var getHelpTool = new GetHelpTool(chatTools, prompts, toolNamePrefix);
        IReadOnlyList<IAppTool> tools = [getHelpTool, .. chatTools];

        var toolServer = new AppToolServer(ToolServerName, tools, this.loggerFactory, 0, authToken);
        await toolServer.StartAsync(cancellationToken).ConfigureAwait(false);

        IReadOnlyList<string> toolNames = [.. tools.Select(tool => $"{toolNamePrefix}{tool.Name}")];

        DotAcpAgentHost innerHost;
        try
        {
            var hostOptions = new DotAcpHostOptions(ClientName: "Team.App", TraceWire: this.options.Acp.TraceWire);
            innerHost = new DotAcpAgentHost(processOptions, this.launcher, hostOptions, this.loggerFactory);
            await innerHost.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await toolServer.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        IAgentSession session;
        try
        {
            session = await innerHost.StartSessionAsync(
                new AgentSessionOptions(
                    workDir,
                    // Approves the way AutoApprovePermissionHandler does, except inside the human's
                    // own agent configuration directory - see WorkDirPermissionHandler for why that
                    // one exception is drawn there and not around the Work Dir. Falls back to the
                    // plain auto-approve only if the profile directory cannot be resolved at all,
                    // rather than inventing a path to protect.
                    WorkDirPermissionHandler.DefaultProtectedDirectory() is { } protectedDirectory
                        ? new WorkDirPermissionHandler(protectedDirectory, this.loggerFactory.CreateLogger<WorkDirPermissionHandler>())
                        : new AutoApprovePermissionHandler(),
                    new SystemPromptOptions(
                        SystemPromptComposer.Compose(
                            persona,
                            prompts,
                            toolNamePrefix + getHelpTool.Name,
                            toolNames,
                            skillResolution.Skills,
                            toolNamePrefix + readSkillTool.Name),
                        SystemPromptMode.Append),
                    toolServer.Endpoint,
                    persona.Model,
                    persona.Effort),
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await innerHost.DisposeAsync().ConfigureAwait(false);
            await toolServer.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        IAgentHost host = new ToolServerOwningAgentHost(innerHost, toolServer);
        return (host, session);
    }

    /// <summary>
    /// Wraps an <see cref="IAgentHost"/> so disposing it also disposes the <see cref="AppToolServer"/>
    /// the factory started for it: the factory owns the tool server's lifetime, not the host itself.
    /// </summary>
    private sealed class ToolServerOwningAgentHost(IAgentHost inner, AppToolServer toolServer) : IAgentHost
    {
        public AgentHostInfo Info => inner.Info;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return inner.StartAsync(cancellationToken);
        }

        public Task<IAgentSession> StartSessionAsync(AgentSessionOptions options, CancellationToken cancellationToken)
        {
            return inner.StartSessionAsync(options, cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await inner.DisposeAsync().ConfigureAwait(false);
            await toolServer.DisposeAsync().ConfigureAwait(false);
        }
    }
}
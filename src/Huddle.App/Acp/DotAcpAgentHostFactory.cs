using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.DotAcp;
using Agency.Huddle.Acp.Hosting;
using Agency.Huddle.Acp.Tools;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Hooks;

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
    /// The MCP tool-server name handed to <see cref="AppToolServer"/>. Every tool name in the system
    /// prompt must carry this same value as its <c>mcp__{name}__</c> prefix, so it is held here once
    /// and never retyped into a hook's template — see <see cref="SystemPromptComposer"/>'s remarks on
    /// why that prefix is applied in code.
    /// </summary>
    private const string ToolServerName = "team";

    private readonly TeamOptions options;
    private readonly IServiceProvider serviceProvider;
    private readonly ILoggerFactory loggerFactory;

    public DotAcpAgentHostFactory(IOptions<TeamOptions> options, IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        this.options = options.Value;
        this.serviceProvider = serviceProvider;
        this.loggerFactory = loggerFactory;
    }

    public async Task<(IAgentHost Host, IAgentSession Session)> CreateAsync(Persona persona, string agentId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(persona);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);

        var workDir = Path.Combine(this.options.DataDir, this.options.Acp.WorkDir, persona.Name);
        Directory.CreateDirectory(workDir);

        var processOptions = AgentProcessOptionsFactory.TryCreate(this.options.Acp, workDir, AppContext.BaseDirectory)
            ?? throw new InvalidOperationException(
                $"No ACP adapter is installed for Persona '{persona.Name}'. Run tools/acp/install.ps1 "
                + "(or set Team:Acp:AdapterPath / Team:Acp:Args) before enabling this Persona.");

        // Minted fresh per session and never logged: wire traces already leak it
        // (agent-guide.md §7.5), so this is the only place its value is held outside the tool server.
        var authToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

        // Built by our own service provider, bound to this Agent id, so each tool call runs the same
        // domain code the UI calls. AppToolServer's WebApplication.CreateBuilder() is a second,
        // genuinely separate DI container - the tool instances must never be resolved from it.
        IReadOnlyList<IAppTool> chatTools =
        [
            ActivatorUtilities.CreateInstance<ListAgentsTool>(this.serviceProvider),
            ActivatorUtilities.CreateInstance<CreateRoomTool>(this.serviceProvider, agentId),
            ActivatorUtilities.CreateInstance<InviteAgentTool>(this.serviceProvider),
            ActivatorUtilities.CreateInstance<PostMessageTool>(this.serviceProvider, agentId),
        ];

        var hooks = this.serviceProvider.GetRequiredService<IHookSource>();

        // The mcp__{server}__ prefix is derived from the same server name above, never typed into a
        // hook's template - see the ToolServerName remarks and SystemPromptComposer's. GetHelpTool
        // takes it explicitly rather than hard-coding its own copy, for the same reason.
        var toolNamePrefix = $"mcp__{ToolServerName}__";

        // get_help is offered first and knows every other tool, so the system prompt can name one
        // tool instead of all of them. It is built last for the obvious reason: it takes the rest.
        // Captured in its own local, rather than only in the tools array below, so the system prompt
        // can name it from its own Name below - never retyping "get_help" either.
        var getHelpTool = new GetHelpTool(chatTools, hooks, toolNamePrefix);
        IReadOnlyList<IAppTool> tools = [getHelpTool, .. chatTools];

        var toolServer = new AppToolServer(ToolServerName, tools, this.loggerFactory, 0, authToken);
        await toolServer.StartAsync(cancellationToken).ConfigureAwait(false);

        IReadOnlyList<string> toolNames = [.. tools.Select(tool => $"{toolNamePrefix}{tool.Name}")];

        DotAcpAgentHost innerHost;
        try
        {
            var launcher = new AgentProcessLauncher(this.loggerFactory.CreateLogger<AgentProcessLauncher>());
            var hostOptions = new DotAcpHostOptions(ClientName: "Team.App", TraceWire: this.options.Acp.TraceWire);
            innerHost = new DotAcpAgentHost(processOptions, launcher, hostOptions, this.loggerFactory);
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
                    new AutoApprovePermissionHandler(),
                    new SystemPromptOptions(
                        SystemPromptComposer.Compose(persona, hooks, toolNamePrefix + getHelpTool.Name, toolNames),
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
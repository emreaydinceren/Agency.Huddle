namespace Agency.Huddle.Acp.DotAcp;

using Microsoft.Extensions.Logging;
using StreamJsonRpc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.Hosting;

/// <summary>Hosts a connection to an ACP agent process using the dotacp client library.</summary>
public sealed partial class DotAcpAgentHost(
    AgentProcessOptions processOptions,
    IAgentProcessLauncher launcher,
    DotAcpHostOptions options,
    ILoggerFactory loggerFactory) : IAgentHost
{
    private readonly Lock gate = new Lock();

    private readonly ILogger logger = loggerFactory.CreateLogger<DotAcpAgentHost>();

    private IAgentProcess? process;

    private dotacp.client.Connection? connection;

    private DotAcpClientAdapter? adapter;

    private AgentHostInfo? info;

    private bool started;

    private bool disposed;

    public AgentHostInfo Info => this.info ?? throw new InvalidOperationException("Host not started.");

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        lock (this.gate)
        {
            if (this.started)
            {
                throw new InvalidOperationException("The host has already been started.");
            }

            this.started = true;
        }

        IAgentProcess launchedProcess = launcher.Launch(processOptions);
        this.process = launchedProcess;

        DotAcpClientAdapter clientAdapter = new DotAcpClientAdapter(loggerFactory.CreateLogger<DotAcpClientAdapter>());
        this.adapter = clientAdapter;

        TraceSource? trace = null;
        if (options.TraceWire)
        {
            trace = new TraceSource("Agency.Huddle.Acp.Wire", SourceLevels.Verbose);
            trace.Listeners.Clear();
            trace.Listeners.Add(new LoggerTraceListener(loggerFactory.CreateLogger("Agency.Huddle.Acp.Wire")));
        }

        dotacp.client.Connection establishedConnection = dotacp.client.Connection.RunClient(
            clientAdapter,
            launchedProcess.StandardInput,
            launchedProcess.StandardOutput,
            trace) ?? throw new AgentException("Failed to create connection.");
        this.connection = establishedConnection;

        dotacp.protocol.InitializeResponse response = await establishedConnection.InitializeAsync(
            new dotacp.protocol.InitializeRequest
            {
                ProtocolVersion = dotacp.protocol.ProtocolMeta.Version,
                ClientCapabilities = new dotacp.protocol.ClientCapabilities
                {
                    Fs = new dotacp.protocol.FileSystemCapabilities
                    {
                        ReadTextFile = false,
                        WriteTextFile = false,
                    },
                    Terminal = false,
                },
                ClientInfo = new dotacp.protocol.Implementation
                {
                    Name = options.ClientName,
                    Version = options.ClientVersion ?? "0.1.0",
                },
            },
            cancellationToken).ConfigureAwait(false);

        this.info = new AgentHostInfo(
            response.AgentInfo?.Name ?? "unknown",
            response.AgentInfo?.Version,
            (int)(ushort)response.ProtocolVersion,
            DotAcpAgentHost.MapAuthMethods(response.AuthMethods),
            response.AgentCapabilities?.LoadSession ?? false,
            response.AgentCapabilities?.SessionCapabilities?.Resume is not null);

        _ = Task.Run(() => this.WatchForDisconnectAsync(clientAdapter, establishedConnection, launchedProcess), CancellationToken.None);
    }

    public async Task<IAgentSession> StartSessionAsync(AgentSessionOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        dotacp.client.Connection? activeConnection = this.connection;
        DotAcpClientAdapter? activeAdapter = this.adapter;
        if (!this.started || activeConnection is null || activeAdapter is null)
        {
            throw new InvalidOperationException("The host has not been started.");
        }

        dotacp.protocol.NewSessionRequest request = new dotacp.protocol.NewSessionRequest
        {
            Cwd = options.Cwd,
            McpServers = DotAcpAgentHost.BuildMcpServers(options),
            Meta = DotAcpAgentHost.BuildMeta(options),
        };

        dotacp.protocol.NewSessionResponse response;
        try
        {
            response = await activeConnection.NewSessionAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (RemoteInvocationException ex) when (ex.ErrorCode == (int)dotacp.protocol.ErrorCode.AuthenticationRequired)
        {
            throw new AgentAuthenticationRequiredException(this.Info.AuthMethods);
        }
        catch (RemoteInvocationException ex)
        {
            throw new AgentException($"session/new failed: {ex.Message}", ex);
        }

        string sessionId = (string)response.SessionId;
        return await this.RegisterAndConfigureAsync(
            sessionId, activeConnection, activeAdapter, options, response.ConfigOptions, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resumes a session by id (RS §6.4 A-1): built exactly like <see cref="StartSessionAsync"/> -
    /// the same request shape, the same sink registration, the same Model-then-Effort application -
    /// but over <c>session/resume</c>. <c>ResumeSessionResponse</c> carries no session id of its
    /// own (dotacp 2026.7.19): the caller keeps the id it asked for.
    /// </summary>
    public async Task<IAgentSession> ResumeSessionAsync(string sessionId, AgentSessionOptions options, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(options);

        dotacp.client.Connection? activeConnection = this.connection;
        DotAcpClientAdapter? activeAdapter = this.adapter;
        if (!this.started || activeConnection is null || activeAdapter is null)
        {
            throw new InvalidOperationException("The host has not been started.");
        }

        dotacp.protocol.ResumeSessionRequest request = new dotacp.protocol.ResumeSessionRequest
        {
            SessionId = sessionId,
            Cwd = options.Cwd,
            McpServers = DotAcpAgentHost.BuildMcpServers(options),
            Meta = DotAcpAgentHost.BuildMeta(options),
        };

        dotacp.protocol.ResumeSessionResponse response;
        try
        {
            response = await activeConnection.ResumeSessionAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (RemoteInvocationException ex) when (ex.ErrorCode == (int)dotacp.protocol.ErrorCode.ResourceNotFound)
        {
            throw new AgentSessionNotFoundException(sessionId, ex);
        }
        catch (RemoteInvocationException ex) when (ex.ErrorCode == (int)dotacp.protocol.ErrorCode.AuthenticationRequired)
        {
            throw new AgentAuthenticationRequiredException(this.Info.AuthMethods);
        }
        catch (RemoteInvocationException ex)
        {
            throw new AgentException($"session/resume failed: {ex.Message}", ex);
        }

        return await this.RegisterAndConfigureAsync(
            sessionId, activeConnection, activeAdapter, options, response.ConfigOptions, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Shared tail of <see cref="StartSessionAsync"/> and <see cref="ResumeSessionAsync"/>: builds
    /// the session, registers its sink BEFORE touching <c>session/set_config_option</c> (which can
    /// provoke a <c>session/update</c> carrying a <c>ConfigOptionUpdate</c>, and an unregistered
    /// session would drop it), then applies Model and, downstream of it, Effort.
    /// </summary>
    private async Task<IAgentSession> RegisterAndConfigureAsync(
        string sessionId,
        dotacp.client.Connection activeConnection,
        DotAcpClientAdapter activeAdapter,
        AgentSessionOptions options,
        dotacp.protocol.SessionConfigOption[]? initialConfigOptions,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<AgentModelOption> models = ModelConfigOptions.Read(initialConfigOptions);
        DotAcpAgentSession session = new DotAcpAgentSession(
            sessionId,
            activeConnection,
            options.PermissionHandler,
            id => activeAdapter.Unregister(id),
            this.logger,
            models);

        activeAdapter.Register(session);

        dotacp.protocol.SessionConfigOption[]? currentConfigOptions = await this.ApplyModelAsync(
            activeConnection, sessionId, options.Model, initialConfigOptions, cancellationToken).ConfigureAwait(false);

        // Effort is downstream of the model: the adapter rebuilds "thought_level" on every model
        // switch, so both the advertised catalog and any resolution must use the snapshot true
        // AFTER the model switch, never the pre-switch response.
        session.SetEffortLevels(EffortConfigOptions.Read(currentConfigOptions));

        await this.ApplyEffortAsync(activeConnection, sessionId, options.Effort, currentConfigOptions, cancellationToken).ConfigureAwait(false);

        return session;
    }

    /// <summary>Maps <see cref="AgentSessionOptions.ToolServer"/> to the wire's <c>mcpServers</c> array; empty when there is none.</summary>
    private static dotacp.protocol.McpServer[] BuildMcpServers(AgentSessionOptions options)
    {
        return options.ToolServer is null
            ? []
            : [
                new dotacp.protocol.McpServerHttp
                {
                    Name = options.ToolServer.Name,
                    Url = options.ToolServer.Uri.ToString(),
                    Headers = DotAcpAgentHost.MapHeaders(options.ToolServer.Headers),
                },
            ];
    }

    /// <summary>
    /// Builds the <c>_meta</c> payload shared by <c>session/new</c> and <c>session/resume</c>: the
    /// caller's own <see cref="AgentSessionOptions.Meta"/> entries first, copied rather than
    /// aliased - this method must not mutate the caller's dictionary - then
    /// <see cref="AgentSessionOptions.SystemPrompt"/>'s own <c>systemPrompt</c> key set last, so it
    /// always wins over a same-named entry the caller supplied (RS §6.4 A-4). Null when there is
    /// nothing to send.
    /// </summary>
    private static Dictionary<string, object>? BuildMeta(AgentSessionOptions options)
    {
        if (options.Meta is not { Count: > 0 } && options.SystemPrompt is null)
        {
            return null;
        }

        Dictionary<string, object> meta = options.Meta is { Count: > 0 } supplied
            ? new Dictionary<string, object>(supplied, StringComparer.Ordinal)
            : new Dictionary<string, object>(StringComparer.Ordinal);

        if (options.SystemPrompt is not null)
        {
            object payload = options.SystemPrompt.Mode == SystemPromptMode.Replace
                ? options.SystemPrompt.Text
                : new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["append"] = options.SystemPrompt.Text,
                };

            meta["systemPrompt"] = payload;
        }

        return meta;
    }

    /// <summary>
    /// Resolves and applies <paramref name="model"/>, if one was requested, and returns the
    /// configOptions snapshot true of the session afterwards - the snapshot
    /// <see cref="DotAcpAgentSession.EffortLevels"/> and a subsequent effort resolution must read,
    /// never the pre-switch one.
    /// </summary>
    /// <param name="activeConnection">The connection to send <c>session/set_config_option</c> on.</param>
    /// <param name="sessionId">The session id to apply the model to.</param>
    /// <param name="model">The requested model id, or null to do nothing.</param>
    /// <param name="newSessionConfigOptions">The configOptions snapshot from the <c>session/new</c> response.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>
    /// The successful set's <c>ConfigOptions</c> (taken even when null/empty: an adapter answering
    /// with no options is saying the new model advertises none, and the pre-switch list would be a
    /// lie about a model that is no longer current); <paramref name="newSessionConfigOptions"/> on
    /// every other path (no model requested, not in the catalog, or the adapter rejected the set).
    /// </returns>
    private async Task<dotacp.protocol.SessionConfigOption[]?> ApplyModelAsync(
        dotacp.client.Connection activeConnection,
        string sessionId,
        string? model,
        dotacp.protocol.SessionConfigOption[]? newSessionConfigOptions,
        CancellationToken cancellationToken)
    {
        if (model is null)
        {
            return newSessionConfigOptions;
        }

        if (!ModelConfigOptions.TryResolve(newSessionConfigOptions, model, out dotacp.protocol.SessionConfigId configId, out dotacp.protocol.SessionConfigValueId value))
        {
            // A stale stored model must not brick a session: warn and continue on the agent's
            // default rather than throwing.
            DotAcpAgentHost.LogModelNotInCatalog(this.logger, model);
            return newSessionConfigOptions;
        }

        try
        {
            dotacp.protocol.SetSessionConfigOptionResponse setResponse = await activeConnection.SetSessionConfigOptionAsync(
                new dotacp.protocol.SetSessionConfigOptionRequest
                {
                    SessionId = sessionId,
                    ConfigId = configId,
                    Type = "select",
                    Value = value,
                },
                cancellationToken).ConfigureAwait(false);

            return setResponse.ConfigOptions;
        }
        catch (RemoteInvocationException ex)
        {
            DotAcpAgentHost.LogModelConfigFailed(this.logger, model, ex.Message);
            return newSessionConfigOptions;
        }
    }

    /// <summary>
    /// Resolves and applies <paramref name="effort"/>, if one was requested, against
    /// <paramref name="configOptions"/> - the POST-model-switch snapshot <see cref="ApplyModelAsync"/>
    /// returned. Mirrors <see cref="ApplyModelAsync"/>'s resolve-then-set-then-warn shape; a
    /// rejected or unmatched effort is a warning, never an exception.
    /// </summary>
    /// <param name="activeConnection">The connection to send <c>session/set_config_option</c> on.</param>
    /// <param name="sessionId">The session id to apply the effort to.</param>
    /// <param name="effort">The requested effort id, or null to do nothing.</param>
    /// <param name="configOptions">The configOptions snapshot to resolve <paramref name="effort"/> against.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    private async Task ApplyEffortAsync(
        dotacp.client.Connection activeConnection,
        string sessionId,
        string? effort,
        dotacp.protocol.SessionConfigOption[]? configOptions,
        CancellationToken cancellationToken)
    {
        if (effort is null)
        {
            return;
        }

        if (!EffortConfigOptions.TryResolve(configOptions, effort, out dotacp.protocol.SessionConfigId configId, out dotacp.protocol.SessionConfigValueId value))
        {
            DotAcpAgentHost.LogEffortNotInCatalog(this.logger, effort);
            return;
        }

        try
        {
            _ = await activeConnection.SetSessionConfigOptionAsync(
                new dotacp.protocol.SetSessionConfigOptionRequest
                {
                    SessionId = sessionId,
                    ConfigId = configId,
                    Type = "select",
                    Value = value,
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (RemoteInvocationException ex)
        {
            DotAcpAgentHost.LogEffortConfigFailed(this.logger, effort, ex.Message);
        }
    }

    public async ValueTask DisposeAsync()
    {
        lock (this.gate)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
        }

        DotAcpClientAdapter? activeAdapter = this.adapter;
        dotacp.client.Connection? activeConnection = this.connection;
        IAgentProcess? activeProcess = this.process;

        if (activeAdapter is not null && activeConnection is not null)
        {
            activeAdapter.OnDisconnected(activeConnection);
        }

        activeConnection?.Dispose();

        if (activeProcess is not null)
        {
            await Task.WhenAny(activeProcess.Exited, Task.Delay(TimeSpan.FromSeconds(3))).ConfigureAwait(false);

            if (!activeProcess.Exited.IsCompleted)
            {
                activeProcess.Kill();
            }

            activeProcess.Dispose();
        }
    }

    // With no headers, this returns [] - the byte-for-byte shape the wire already sends today.
    private static dotacp.protocol.HttpHeader[] MapHeaders(IReadOnlyDictionary<string, string> headers)
    {
        if (headers.Count == 0)
        {
            return [];
        }

        dotacp.protocol.HttpHeader[] mapped = new dotacp.protocol.HttpHeader[headers.Count];
        int index = 0;
        foreach (KeyValuePair<string, string> header in headers)
        {
            mapped[index] = new dotacp.protocol.HttpHeader { Name = header.Key, Value = header.Value };
            index++;
        }

        return mapped;
    }

    private static IReadOnlyList<AuthMethodInfo> MapAuthMethods(dotacp.protocol.AuthMethod[]? authMethods)
    {
        if (authMethods is null || authMethods.Length == 0)
        {
            return Array.Empty<AuthMethodInfo>();
        }

        List<AuthMethodInfo> mapped = new List<AuthMethodInfo>();
        foreach (dotacp.protocol.AuthMethod method in authMethods)
        {
            if (method is dotacp.protocol.AuthMethodAgent agentMethod)
            {
                mapped.Add(new AuthMethodInfo((string)agentMethod.Id, agentMethod.Name, agentMethod.Description));
            }
        }

        return mapped;
    }

    private async Task WatchForDisconnectAsync(DotAcpClientAdapter clientAdapter, dotacp.client.Connection watchedConnection, IAgentProcess watchedProcess)
    {
        await Task.WhenAny(watchedConnection.Completion, watchedProcess.Exited).ConfigureAwait(false);

        clientAdapter.OnDisconnected(watchedConnection);
        DotAcpAgentHost.LogAgentDisconnected(this.logger);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The agent process disconnected.")]
    private static partial void LogAgentDisconnected(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Requested model '{Model}' is not in the agent's advertised model catalog; continuing on the agent's default.")]
    private static partial void LogModelNotInCatalog(ILogger logger, string model);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to set model '{Model}' via session/set_config_option; continuing on the agent's default. {Reason}")]
    private static partial void LogModelConfigFailed(ILogger logger, string model, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Requested effort '{Effort}' is not in the agent's advertised effort catalog for the current model; continuing on the model's default.")]
    private static partial void LogEffortNotInCatalog(ILogger logger, string effort);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to set effort '{Effort}' via session/set_config_option; continuing on the model's default. {Reason}")]
    private static partial void LogEffortConfigFailed(ILogger logger, string effort, string reason);
}
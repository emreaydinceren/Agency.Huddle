namespace Agency.Huddle.Acp.DotAcp;

using Microsoft.Extensions.Logging;
using StreamJsonRpc;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Agency.Huddle.Acp.Abstractions;

/// <summary>Represents one ACP session: the concrete <see cref="IAgentSession"/> backed by a dotacp connection.</summary>
internal sealed class DotAcpAgentSession : IAgentSession, ISessionSink
{
    private readonly dotacp.client.Connection connection;

    private readonly Action<string> onDisposed;

    private readonly ILogger logger;

    private readonly Channel<AgentEvent> channel = Channel.CreateUnbounded<AgentEvent>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    private readonly Lock gate = new Lock();

    private CancellationTokenSource? promptCts;

    private bool disposed;

    internal DotAcpAgentSession(
        string sessionId,
        dotacp.client.Connection connection,
        IPermissionHandler permissionHandler,
        Action<string> onDisposed,
        ILogger logger,
        IReadOnlyList<AgentModelOption> models)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(permissionHandler);
        ArgumentNullException.ThrowIfNull(onDisposed);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(models);

        this.SessionId = sessionId;
        this.connection = connection;
        this.PermissionHandler = permissionHandler;
        this.onDisposed = onDisposed;
        this.logger = logger;
        this.Models = models;
    }

    public string SessionId { get; }

    public ChannelReader<AgentEvent> Events => this.channel.Reader;

    public IReadOnlyList<AgentModelOption> Models { get; }

    public IReadOnlyList<AgentEffortOption> EffortLevels { get; private set; } = [];

    public IPermissionHandler PermissionHandler { get; }

    public CancellationToken PromptCancellation { get; private set; } = CancellationToken.None;

    /// <summary>
    /// Sets <see cref="EffortLevels"/> after this session is constructed. This is deliberately a
    /// mutation rather than a constructor parameter: the effort catalog is only knowable after
    /// <c>session/set_config_option</c> has switched the model (the adapter rebuilds the
    /// "thought_level" option on every switch), and that call may only be sent once the session is
    /// REGISTERED with <see cref="DotAcpClientAdapter"/> - registering late would let it drop a
    /// <c>session/update</c> the switch can provoke. So the session must exist, and be registered,
    /// before the effort list does.
    ///
    /// This is safe because the only reference to this session published before this method runs
    /// is the one handed to <see cref="DotAcpClientAdapter.Register"/>, and that method holds it as
    /// an <see cref="ISessionSink"/> - an interface that does not expose <see cref="EffortLevels"/>
    /// or this method at all. No caller can observe the object through any other reference until
    /// <c>StartSessionAsync</c> returns it as an <see cref="IAgentSession"/>. Adding
    /// <see cref="EffortLevels"/> to <see cref="ISessionSink"/> would invalidate this argument, by
    /// handing every consumer of that interface a way to read a list that is not yet populated.
    /// </summary>
    /// <param name="effortLevels">The effort levels to publish.</param>
    internal void SetEffortLevels(IReadOnlyList<AgentEffortOption> effortLevels)
    {
        ArgumentNullException.ThrowIfNull(effortLevels);
        this.EffortLevels = effortLevels;
    }

    /// <summary>Exposed for diagnostics; the dotacp connection this session was created against.</summary>
    internal dotacp.client.Connection Connection => this.connection;

    /// <summary>Exposed for diagnostics; the logger this session was created with.</summary>
    internal ILogger Logger => this.logger;

    public async Task<PromptResult> PromptAsync(string text, CancellationToken cancellationToken)
    {
        CancellationTokenSource promptTokenSource;
        lock (this.gate)
        {
            if (this.promptCts is not null)
            {
                throw new InvalidOperationException("A prompt is already in flight.");
            }

            promptTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            this.promptCts = promptTokenSource;
            this.PromptCancellation = promptTokenSource.Token;
        }

        try
        {
            dotacp.protocol.PromptResponse response = await this.connection.PromptAsync(
                new dotacp.protocol.PromptRequest
                {
                    SessionId = this.SessionId,
                    Prompt = new dotacp.protocol.ContentBlock[] { new dotacp.protocol.TextContent { Text = text } },
                },
                cancellationToken).ConfigureAwait(false);

            StopReason stopReason = SessionUpdateMapper.MapStopReason(response.StopReason);
            this.TryPublish(new TurnCompleted(this.SessionId, stopReason));
            return new PromptResult(stopReason);
        }
        catch (ConnectionLostException ex)
        {
            throw new AgentDisconnectedException(ex);
        }
        catch (RemoteInvocationException ex)
        {
            throw new AgentException($"session/prompt failed: {ex.Message}", ex);
        }
        finally
        {
            lock (this.gate)
            {
                this.promptCts = null;
            }

            this.PromptCancellation = CancellationToken.None;
            promptTokenSource.Dispose();
        }
    }

    public async Task CancelAsync(CancellationToken cancellationToken)
    {
        CancellationTokenSource? currentPromptCts;
        lock (this.gate)
        {
            currentPromptCts = this.promptCts;
        }

        if (currentPromptCts is null)
        {
            return;
        }

        try
        {
            currentPromptCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The in-flight prompt finished (and disposed its token source) between the check
            // above and this call; there is nothing left to cancel.
            return;
        }

        await this.connection.CancelAsync(
            new dotacp.protocol.CancelNotification { SessionId = this.SessionId },
            cancellationToken).ConfigureAwait(false);
    }

    public bool TryPublish(AgentEvent agentEvent)
    {
        return this.channel.Writer.TryWrite(agentEvent);
    }

    public void Fault(Exception exception)
    {
        this.channel.Writer.TryComplete(exception);
    }

    public ValueTask DisposeAsync()
    {
        lock (this.gate)
        {
            if (this.disposed)
            {
                return ValueTask.CompletedTask;
            }

            this.disposed = true;
        }

        this.onDisposed(this.SessionId);
        this.channel.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}
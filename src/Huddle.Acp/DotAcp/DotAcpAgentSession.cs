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
internal sealed partial class DotAcpAgentSession : IAgentSession, ISessionSink
{
    // Bounds WaitForDispatchBarrierAsync. This is a safety net against an accounting bug between the
    // wire stream and the adapter (an update counted as read that never reaches dispatch), NOT an
    // ordering mechanism: when the accounting is right the barrier is exact and this never elapses.
    // Internal rather than const so a test can read it; TimeSpan has no const form regardless.
    internal static readonly TimeSpan DispatchBarrierCap = TimeSpan.FromSeconds(2);

    private readonly dotacp.client.Connection connection;

    private readonly WireOrderLedger ledger;

    private readonly Action<string> onDisposed;

    private readonly ILogger logger;

    private readonly Channel<AgentEvent> channel = Channel.CreateUnbounded<AgentEvent>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    private readonly Lock gate = new Lock();

    private CancellationTokenSource? promptCts;

    private IElicitationScope? elicitationScope;

    private bool disposed;

    internal DotAcpAgentSession(
        string sessionId,
        dotacp.client.Connection connection,
        IPermissionHandler permissionHandler,
        Action<string> onDisposed,
        ILogger logger,
        IReadOnlyList<AgentModelOption> models,
        WireOrderLedger ledger)
    {
        ArgumentNullException.ThrowIfNull(ledger);
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
        this.ledger = ledger;
    }

    public string SessionId { get; }

    public ChannelReader<AgentEvent> Events => this.channel.Reader;

    public IReadOnlyList<AgentModelOption> Models { get; }

    public IReadOnlyList<AgentEffortOption> EffortLevels { get; private set; } = [];

    public IReadOnlyList<AgentModeOption> ModeOptions { get; private set; } = [];

    public string? CurrentModeId { get; private set; }

    public string? CurrentModelId { get; private set; }

    public string? CurrentEffortId { get; private set; }

    public IPermissionHandler PermissionHandler { get; }

    public CancellationToken PromptCancellation { get; private set; } = CancellationToken.None;

    /// <summary>Gets the scope bound by <see cref="BindElicitationScope"/>, or null before one is bound.</summary>
    public IElicitationScope? ElicitationScope => Volatile.Read(ref this.elicitationScope);

    /// <summary>
    /// Stores <paramref name="scope"/> for <see cref="DotAcpClientAdapter"/> to find through
    /// <see cref="ISessionSink"/>; a later call replaces an earlier one. Read on whatever thread
    /// dotacp dispatches the agent's request on, hence the volatile field.
    /// </summary>
    /// <param name="scope">The scope that answers this session's elicitation requests.</param>
    public void BindElicitationScope(IElicitationScope scope)
    {
        Volatile.Write(ref this.elicitationScope, scope);
    }

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

    /// <summary>
    /// Sets <see cref="ModeOptions"/> after this session is constructed, for the same reason as
    /// <see cref="SetEffortLevels"/>: the catalog is only knowable after Model and Effort have been
    /// applied, and by then the session is already registered.
    /// </summary>
    /// <param name="modeOptions">The modes to publish.</param>
    internal void SetModeOptions(IReadOnlyList<AgentModeOption> modeOptions)
    {
        ArgumentNullException.ThrowIfNull(modeOptions);
        this.ModeOptions = modeOptions;
    }

    /// <summary>
    /// Sets <see cref="CurrentModelId"/> and <see cref="CurrentEffortId"/> from the configOptions
    /// snapshots read back after the model and the effort were applied.
    /// </summary>
    /// <param name="modelId">The model the agent reports running, or null when it advertises none.</param>
    /// <param name="effortId">The effort the agent reports running, or null when it advertises none.</param>
    internal void SetCurrentRuntime(string? modelId, string? effortId)
    {
        this.CurrentModelId = modelId;
        this.CurrentEffortId = effortId;
    }

    /// <summary>
    /// Sets <see cref="CurrentModeId"/> once the requested mode has been applied and read back.
    /// </summary>
    /// <param name="modeId">The effective mode id, or null when the agent advertised none.</param>
    internal void SetCurrentMode(string? modeId)
    {
        this.CurrentModeId = modeId;
    }

    /// <summary>Exposed for diagnostics; the dotacp connection this session was created against.</summary>
    internal dotacp.client.Connection Connection => this.connection;

    /// <summary>Exposed for diagnostics; the logger this session was created with.</summary>
    internal ILogger Logger => this.logger;

    /// <summary>
    /// Builds the wire content: one text block first, then each Prompt block in order. With no blocks
    /// the array is exactly the single text block a text-only prompt has always sent.
    /// </summary>
    /// <param name="prompt">The prompt to map.</param>
    /// <returns>The ACP content blocks, text first.</returns>
    private static dotacp.protocol.ContentBlock[] MapPrompt(AgentPrompt prompt)
    {
        IReadOnlyList<AgentPromptBlock> blocks = prompt.Blocks ?? [];
        List<dotacp.protocol.ContentBlock> content = new(blocks.Count + 1)
        {
            new dotacp.protocol.TextContent { Text = prompt.Text },
        };

        foreach (AgentPromptBlock block in blocks)
        {
            switch (block)
            {
                case AgentImageBlock image:
                    content.Add(new dotacp.protocol.ImageContent
                    {
                        Data = Convert.ToBase64String(image.Data.Span),
                        MimeType = image.MimeType,
                    });
                    break;
                case AgentTextResourceBlock resource:
                    content.Add(new dotacp.protocol.EmbeddedResource
                    {
                        Resource = new dotacp.protocol.TextResourceContents
                        {
                            Uri = resource.Uri,
                            MimeType = resource.MimeType,
                            Text = resource.Text,
                        },
                    });
                    break;
                case AgentBlobResourceBlock blob:
                    content.Add(new dotacp.protocol.EmbeddedResource
                    {
                        Resource = new dotacp.protocol.BlobResourceContents
                        {
                            Uri = blob.Uri,
                            MimeType = blob.MimeType,
                            Blob = Convert.ToBase64String(blob.Data.Span),
                        },
                    });
                    break;
                default:
                    throw new NotSupportedException($"Prompt block type {block.GetType().Name} has no ACP mapping.");
            }
        }

        return content.ToArray();
    }

    public async Task<PromptResult> PromptAsync(AgentPrompt prompt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prompt);

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
                    Prompt = DotAcpAgentSession.MapPrompt(prompt),
                },
                cancellationToken).ConfigureAwait(false);

            await this.WaitForDispatchBarrierAsync(cancellationToken).ConfigureAwait(false);

            StopReason stopReason = SessionUpdateMapper.MapStopReason(response.StopReason);
            this.TryPublish(new TurnCompleted(this.SessionId, stopReason));
            return new PromptResult(stopReason);
        }
        catch (ConnectionLostException ex)
        {
            throw new AgentDisconnectedException(ex);
        }
        catch (RemoteInvocationException ex) when (ex.ErrorCode == (int)dotacp.protocol.ErrorCode.AuthenticationRequired)
        {
            throw new AgentAuthenticationRequiredException([], ex.Message);
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

    /// <summary>
    /// Holds <see cref="TurnCompleted"/> back until every <c>session/update</c> the agent wrote ahead
    /// of its <c>session/prompt</c> response has been dispatched to this session's sink. This closes
    /// the dispatch-ordering defect documented in <c>docs/engineering/known-limits.md</c> ("Second
    /// known flake, pre-existing"): ACP guarantees the agent writes every update for a Turn to the
    /// wire strictly before the response, but StreamJsonRpc completes the response through a
    /// different path than the one that invokes <see cref="DotAcpClientAdapter.SessionUpdateAsync"/>,
    /// with nothing ordering the two - so <see cref="PromptAsync"/> could see the response, and
    /// publish <see cref="TurnCompleted"/>, before an earlier update had reached the sink.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this is exact.</b> The agent's output passes through <see cref="NdjsonMethodRewriteStream"/>,
    /// which sees lines in wire order and counts each <c>session/update</c> for this session into the
    /// <see cref="WireOrderLedger"/> as it queues the line, before StreamJsonRpc can consume any later
    /// one. The response is a later line than every update that preceded it, so by the time it has been
    /// delivered to <see cref="PromptAsync"/> the ledger's read count already includes all of them. The
    /// adapter counts a dispatch after handing each update to the sink. Waiting for dispatched to reach
    /// that read count therefore waits for exactly the updates that precede the response (and, if the
    /// stream had read further ahead, a few that follow it, which is harmless). No clock is involved,
    /// so a loaded machine cannot make it lose.
    /// </para>
    /// <para>
    /// <b>Why a barrier on the connection itself was not used.</b> That would need
    /// <c>StreamJsonRpc.JsonRpc.SynchronizationContext</c> installed before listening starts, and
    /// <c>dotacp.client.Connection</c> locks it inside its constructor (the setter throws <c>"This
    /// cannot be done after listening has started."</c>). Counting at the stream avoids needing it.
    /// </para>
    /// <para>
    /// <b>The cap.</b> The wait is bounded by <see cref="DispatchBarrierCap"/> as a safety net against
    /// an accounting bug (for example an update the stream counted that StreamJsonRpc never dispatched
    /// because it could not deserialise it). Past the cap this publishes anyway and logs a warning. It
    /// is not an ordering mechanism. Cancelling <paramref name="cancellationToken"/> (a Human's Stop)
    /// skips the wait at once and publishes with no warning.
    /// </para>
    /// </remarks>
    /// <param name="cancellationToken">The Turn's own token; cancelling ends the wait immediately.</param>
    private async Task WaitForDispatchBarrierAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        long target = this.ledger.ReadCount(this.SessionId);
        try
        {
            await this.ledger.WaitUntilDispatchedAsync(this.SessionId, target, cancellationToken)
                .WaitAsync(DotAcpAgentSession.DispatchBarrierCap, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // A Stop arrived mid-wait: publish TurnCompleted with the response already in hand
            // rather than holding it up further, exactly as if the wait had never started.
        }
        catch (TimeoutException)
        {
            // An update counted as read that never reached dispatch would otherwise stay owed for ever,
            // and every later Turn of this session would wait out the whole cap for it.
            this.ledger.Settle(this.SessionId, target);
            DotAcpAgentSession.LogDispatchBarrierCapExceeded(this.logger, this.SessionId, DotAcpAgentSession.DispatchBarrierCap.TotalMilliseconds);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Session {SessionId}: not every session/update the agent wrote before its response was dispatched within the {CapMilliseconds}ms barrier cap; publishing TurnCompleted anyway.")]
    private static partial void LogDispatchBarrierCapExceeded(ILogger logger, string sessionId, double capMilliseconds);

    public void Fault(Exception exception)
    {
        this.channel.Writer.TryComplete(exception);
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

        this.onDisposed(this.SessionId);

        using (CancellationTokenSource closeTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
        {
            try
            {
                // The token is passed through so a future dotacp version that honours it can send
                // the peer a cancellation courtesy sooner, but empirically (see
                // DotAcpAgentSessionTests.DisposeAsync_SessionCloseNeverAnswered_CompletesWithinBound)
                // dotacp.client.Connection.CloseAsync does not itself unblock locally when a
                // CancellationToken is cancelled and the peer never responds - StreamJsonRpc's
                // InvokeWithParameterObjectAsync only uses it to notify the peer, not to abandon the
                // local await. The outer WaitAsync is what actually enforces the 2-second bound.
                await this.connection.CloseAsync(
                    new dotacp.protocol.CloseSessionRequest { SessionId = this.SessionId },
                    closeTimeout.Token).WaitAsync(TimeSpan.FromSeconds(2), CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex) when (
                ex is IOException
                or ObjectDisposedException
                or OperationCanceledException
                or TimeoutException
                or RemoteInvocationException
                or ConnectionLostException)
            {
                // Spec §6.8: session/close is a courtesy to the agent, never a condition of our
                // own teardown. ACP has no capability flag for this method, so any agent that does
                // not implement it answers "method not found", which StreamJsonRpc surfaces here as
                // a RemoteInvocationException (confirmed empirically against FakeAcpAgent's
                // unhandled-method branch) rather than the IOException / ObjectDisposedException /
                // OperationCanceledException trio a process-death scenario produces. A peer that
                // never answers at all surfaces as TimeoutException from the WaitAsync bound above
                // (also confirmed empirically - the CancellationToken alone does not abort the local
                // await), and an already-disconnected peer can surface as ConnectionLostException,
                // the same exception PromptAsync above maps to AgentDisconnectedException. Disposal
                // must complete regardless of which of these the peer produces.
            }
        }

        this.channel.Writer.TryComplete();
    }
}
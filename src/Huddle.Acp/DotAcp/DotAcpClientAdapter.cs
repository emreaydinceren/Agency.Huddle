namespace Agency.Huddle.Acp.DotAcp;

using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Agency.Huddle.Acp.Abstractions;

/// <summary>Implements the ACP client contract on behalf of Team.Console, routing agent-originated calls to the session they belong to.</summary>
internal sealed partial class DotAcpClientAdapter(ILogger<DotAcpClientAdapter> logger, WireOrderLedger ledger) : dotacp.client.IAcpClient, IDisposable
{
    private const string ElicitationCreateMethod = "elicitation/create";

    // The notice session update after NdjsonMethodRewriteStream renamed it to _session/notice; dotacp
    // strips the leading underscore before calling ExtNotificationAsync.
    private const string SessionNoticeMethod = "session/notice";

    // The connection-scoped sign-in push of the Claude Adapter (_auth/status_update, underscore stripped by dotacp).
    private const string AuthStatusMethod = "auth/status_update";

    // At most this many early updates are held across all starting sessions; more are dropped with the Warning.
    private const int MaxEarlyEvents = 256;

    // The latest AgentSignInStatus as its int value (Volatile access), and the signal of the first push.
    private int signIn;

    private readonly TaskCompletionSource firstSignIn = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly ConcurrentDictionary<string, ISessionSink> sinks = new ConcurrentDictionary<string, ISessionSink>();

    // Serialises delivery against registration, so an update dispatched while its session is being registered is
    // published exactly once and in wire order, after any update held for it.
    private readonly Lock gate = new Lock();

    // Updates for a session not yet registered, held while a session/new or session/resume is in flight: the agent can
    // send one for the new session before the host has read the answer and registered it. Guarded by gate.
    private readonly Dictionary<string, List<AgentEvent>> early = new Dictionary<string, List<AgentEvent>>(StringComparer.Ordinal);

    // Guarded by gate.
    private int earlyCount;

    // How many session/new or session/resume calls are between BeginSessionStart and the end of their scope. Guarded by gate.
    private int startsInFlight;

    // One cancellation source per registered session, cancelled when that session is unregistered, so
    // an elicitation request open for a disposed session is ended rather than left waiting for the
    // Human. Keyed like sinks.
    private readonly ConcurrentDictionary<string, CancellationLifetime> sessionLifetimes = new ConcurrentDictionary<string, CancellationLifetime>();

    // Cancelled when the connection ends or the host is disposed. The handler's own token never fires
    // on either: StreamJsonRpc listens for $/cancelRequest, the adapter sends $/cancel_request, and
    // neither a closed peer nor Connection.Dispose cancels a running handler.
    private readonly CancellationLifetime hostLifetime = new CancellationLifetime();

    internal IReadOnlyCollection<ISessionSink> Sinks => this.sinks.Values.ToArray();

    internal void Register(ISessionSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        lock (this.gate)
        {
            this.sinks[sink.SessionId] = sink;
            if (this.early.Remove(sink.SessionId, out List<AgentEvent>? held))
            {
                this.earlyCount -= held.Count;
                foreach (AgentEvent agentEvent in held)
                {
                    sink.TryPublish(agentEvent);
                }
            }
        }

        CancellationLifetime lifetime = new CancellationLifetime();
        this.sessionLifetimes.AddOrUpdate(
            sink.SessionId,
            lifetime,
            (_, previous) =>
            {
                previous.Dispose();
                return lifetime;
            });
    }

    /// <summary>
    /// Marks a <c>session/new</c> or <c>session/resume</c> as in flight until the returned scope is disposed. Meanwhile an
    /// update for a session that is not registered yet is held and replayed by <see cref="Register"/>, because the agent
    /// can send one for the new session before the host has read the answer. When the last scope ends, whatever is still
    /// held belongs to no session and is dropped with the usual Warning.
    /// </summary>
    /// <returns>The scope; the host disposes it after it has registered the session, or when the start failed.</returns>
    internal IDisposable BeginSessionStart()
    {
        lock (this.gate)
        {
            this.startsInFlight++;
        }

        return new SessionStart(this);
    }

    internal bool Unregister(string sessionId)
    {
        bool removed = this.sinks.TryRemove(sessionId, out _);
        ledger.Forget(sessionId);
        if (this.sessionLifetimes.TryRemove(sessionId, out CancellationLifetime? lifetime))
        {
            lifetime.Dispose();
        }

        return removed;
    }

    /// <summary>Ends every open elicitation request of every session; called by the host when it is disposed.</summary>
    public void Dispose()
    {
        this.hostLifetime.Dispose();
        this.DisposeSessionLifetimes();
    }

    public Task SessionUpdateAsync(dotacp.protocol.SessionNotification notification, CancellationToken cancellationToken = default)
    {
#pragma warning disable HAA0601 // The protocol id is a struct with an implicit conversion to string: a method call, not a boxing conversion.
        string sessionId = (string)notification.SessionId;
#pragma warning restore HAA0601

        // The ledger must see every update dispatched, whatever becomes of it: a Turn's barrier waits
        // for the count the wire stream recorded, so a throwing sink or an unknown session that did
        // not count here would stall that Turn until the safety cap.
        try
        {
            this.Deliver(sessionId, SessionUpdateMapper.Map(sessionId, notification.Update));
            return Task.CompletedTask;
        }
        finally
        {
            ledger.NoteDispatched(sessionId);
        }
    }

    public async Task<dotacp.protocol.RequestPermissionResponse> RequestPermissionAsync(dotacp.protocol.RequestPermissionRequest request, CancellationToken cancellationToken = default)
    {
#pragma warning disable HAA0601 // The protocol id is a struct with an implicit conversion to string: a method call, not a boxing conversion.
        string sessionId = (string)request.SessionId;
#pragma warning restore HAA0601
        if (!this.sinks.TryGetValue(sessionId, out ISessionSink? sink))
        {
            DotAcpClientAdapter.LogUnknownPermissionSession(logger, sessionId);
            return new dotacp.protocol.RequestPermissionResponse { Outcome = new dotacp.protocol.RequestPermissionOutcomeCancelled() };
        }

        // A handler may wait for the Human (the connector approval prompt), so the host's and the session's lifetimes end it
        // as they end an elicitation: the host's disposal must not block on a question nobody can answer.
        CancellationToken sessionToken = this.sessionLifetimes.TryGetValue(sessionId, out CancellationLifetime? sessionLifetime)
            ? sessionLifetime.Token
            : CancellationToken.None;
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, sink.PromptCancellation, this.hostLifetime.Token, sessionToken);

        try
        {
            PermissionDecision decision = await sink.PermissionHandler.DecideAsync(
                SessionUpdateMapper.MapPermission(request, sink.ElicitationScope), linked.Token).ConfigureAwait(false);

            if (decision is SelectedDecision selected)
            {
                return new dotacp.protocol.RequestPermissionResponse
                {
                    Outcome = new dotacp.protocol.SelectedPermissionOutcome { OptionId = selected.OptionId },
                };
            }

            return new dotacp.protocol.RequestPermissionResponse { Outcome = new dotacp.protocol.RequestPermissionOutcomeCancelled() };
        }
        catch (OperationCanceledException)
        {
            DotAcpClientAdapter.LogPermissionCancelled(logger, sessionId);
            return new dotacp.protocol.RequestPermissionResponse { Outcome = new dotacp.protocol.RequestPermissionOutcomeCancelled() };
        }
        catch (Exception ex)
        {
            DotAcpClientAdapter.LogPermissionHandlerFailed(logger, ex, sessionId);
            return new dotacp.protocol.RequestPermissionResponse { Outcome = new dotacp.protocol.RequestPermissionOutcomeCancelled() };
        }
    }

    public Task<dotacp.protocol.ReadTextFileResponse> ReadTextFileAsync(dotacp.protocol.ReadTextFileRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("ReadTextFileAsync is not supported by Team.Console.");
    }

    public Task<dotacp.protocol.WriteTextFileResponse> WriteTextFileAsync(dotacp.protocol.WriteTextFileRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("WriteTextFileAsync is not supported by Team.Console.");
    }

    public Task<dotacp.protocol.CreateTerminalResponse> CreateTerminalAsync(dotacp.protocol.CreateTerminalRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("CreateTerminalAsync is not supported by Team.Console.");
    }

    public Task<dotacp.protocol.KillTerminalResponse> KillTerminalAsync(dotacp.protocol.KillTerminalRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("KillTerminalAsync is not supported by Team.Console.");
    }

    public Task<dotacp.protocol.TerminalOutputResponse> TerminalOutputAsync(dotacp.protocol.TerminalOutputRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("TerminalOutputAsync is not supported by Team.Console.");
    }

    public Task<dotacp.protocol.ReleaseTerminalResponse> ReleaseTerminalAsync(dotacp.protocol.ReleaseTerminalRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("ReleaseTerminalAsync is not supported by Team.Console.");
    }

    public Task<dotacp.protocol.WaitForTerminalExitResponse> WaitForTerminalExitAsync(dotacp.protocol.WaitForTerminalExitRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("WaitForTerminalExitAsync is not supported by Team.Console.");
    }

    public async Task<object> ExtMethodAsync(string method, object request, CancellationToken cancellationToken = default)
    {
        if (string.Equals(method, DotAcpClientAdapter.ElicitationCreateMethod, StringComparison.Ordinal))
        {
            return await this.CreateElicitationAsync(request, cancellationToken).ConfigureAwait(false);
        }

        throw new NotSupportedException($"ExtMethodAsync ({method}) is not supported by Team.Console.");
    }

    public Task ExtNotificationAsync(string method, object notification, CancellationToken cancellationToken = default)
    {
        if (string.Equals(method, DotAcpClientAdapter.SessionNoticeMethod, StringComparison.Ordinal))
        {
            this.PublishNotice(notification);
            return Task.CompletedTask;
        }

        if (string.Equals(method, DotAcpClientAdapter.AuthStatusMethod, StringComparison.Ordinal))
        {
            this.RecordSignIn(notification);
            return Task.CompletedTask;
        }

        DotAcpClientAdapter.LogExtensionNotificationIgnored(logger, method);
        return Task.CompletedTask;
    }

    /// <summary>The latest sign-in status the agent pushed, or <see cref="AgentSignInStatus.Unknown"/> when it has not pushed one.</summary>
    internal AgentSignInStatus SignIn => (AgentSignInStatus)Volatile.Read(ref this.signIn);

    /// <summary>
    /// Waits for the agent's first sign-in push, for at most <paramref name="wait"/>.
    /// </summary>
    /// <param name="wait">The longest to wait; a status that already arrived is returned at once.</param>
    /// <param name="cancellationToken">Ends the wait early, answering what is known so far.</param>
    /// <returns>The latest status, <see cref="AgentSignInStatus.Unknown"/> when none arrived in time.</returns>
    internal async Task<AgentSignInStatus> WaitForSignInAsync(TimeSpan wait, CancellationToken cancellationToken)
    {
        if (this.SignIn == AgentSignInStatus.Unknown && wait > TimeSpan.Zero)
        {
            try
            {
                await this.firstSignIn.Task.WaitAsync(wait, cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                // No push in time: the answer is Unknown, which is the honest "cannot determine".
            }
            catch (OperationCanceledException)
            {
                // The caller gave up waiting: answer what is known so far.
            }
        }

        return this.SignIn;
    }

    /// <summary>
    /// Records the sign-in status from an <c>auth/status_update</c> push. Reads <c>authStatus.kind</c> and nothing else:
    /// the same params carry account details, which are never read, stored or logged. A push with no readable kind is ignored.
    /// </summary>
    /// <param name="notification">The notification's params: a dictionary of Newtonsoft tokens, as dotacp hands them over.</param>
    private void RecordSignIn(object notification)
    {
        if (notification is not IDictionary<string, object> arguments
            || !arguments.TryGetValue("authStatus", out object? status)
            || status is not JObject fields
            || fields["kind"] is not JValue { Value: string { Length: > 0 } kind })
        {
            return;
        }

        AgentSignInStatus parsed = string.Equals(kind, "none", StringComparison.Ordinal)
            ? AgentSignInStatus.SignedOut
            : AgentSignInStatus.SignedIn;
        Volatile.Write(ref this.signIn, (int)parsed);
        this.firstSignIn.TrySetResult();
    }

    public void OnDisconnected(dotacp.client.Connection connection)
    {
        this.hostLifetime.Cancel();

        foreach (ISessionSink sink in this.sinks.Values)
        {
            sink.Fault(new AgentDisconnectedException());
        }

        this.sinks.Clear();
    }

    /// <summary>
    /// Answers an agent's <c>elicitation/create</c> (reached as the extension method, see
    /// <see cref="NdjsonMethodRewriteStream"/>): finds the session's scope, asks it, and maps the
    /// result to the wire dictionary. Every way the request cannot be asked (unknown session, no scope,
    /// unreadable arguments) or can no longer be answered (a token fired) is answered
    /// <c>cancel</c>, so the agent is never left waiting; only a scope that throws something other than
    /// a cancellation propagates, which dotacp reports as JSON-RPC -32000.
    /// </summary>
    /// <param name="request">The arguments: a dictionary of Newtonsoft tokens, as dotacp hands them over.</param>
    /// <param name="cancellationToken">The request's own token.</param>
    /// <returns>The reply dictionary: <c>action</c>, and <c>content</c> only for an accepted answer.</returns>
    private async Task<object> CreateElicitationAsync(object request, CancellationToken cancellationToken)
    {
        if (request is not IDictionary<string, object> arguments
            || DotAcpClientAdapter.ReadString(arguments, "sessionId") is not { } sessionId)
        {
            DotAcpClientAdapter.LogElicitationMalformed(logger);
            return DotAcpClientAdapter.ElicitationReply("cancel");
        }

        if (!this.sinks.TryGetValue(sessionId, out ISessionSink? sink)
            || !this.sessionLifetimes.TryGetValue(sessionId, out CancellationLifetime? sessionLifetime))
        {
            DotAcpClientAdapter.LogElicitationUnknownSession(logger, sessionId);
            return DotAcpClientAdapter.ElicitationReply("cancel");
        }

        IElicitationScope? scope = sink.ElicitationScope;
        if (scope is null)
        {
            DotAcpClientAdapter.LogElicitationNoScope(logger, sessionId);
            return DotAcpClientAdapter.ElicitationReply("cancel");
        }

        ElicitationRequest elicitation = new ElicitationRequest(
            sessionId,
            DotAcpClientAdapter.ReadString(arguments, "toolCallId"),
            DotAcpClientAdapter.ReadString(arguments, "message") ?? string.Empty,
            DotAcpClientAdapter.ReadSchemaJson(arguments));

        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, sink.PromptCancellation, this.hostLifetime.Token, sessionLifetime.Token);

        try
        {
            ElicitationResult result = await scope.ElicitAsync(elicitation, linked.Token).ConfigureAwait(false);
            return DotAcpClientAdapter.MapElicitationResult(result);
        }
        catch (OperationCanceledException)
        {
            DotAcpClientAdapter.LogElicitationCancelled(logger, sessionId);
            return DotAcpClientAdapter.ElicitationReply("cancel");
        }
        catch (Exception ex)
        {
            DotAcpClientAdapter.LogElicitationFailed(logger, ex, sessionId);
            throw;
        }
    }

    /// <summary>
    /// Publishes a <c>notice</c> session update (reached as the extension notification, see
    /// <see cref="NdjsonMethodRewriteStream"/>) to its session as an <see cref="AgentNotice"/>. The
    /// stream counted the line as a <c>session/update</c>, so it is marked dispatched whatever becomes of
    /// it, exactly as <see cref="SessionUpdateAsync"/> does; a notice with no title, or for no known
    /// session, is dropped with a log line.
    /// </summary>
    /// <param name="notification">The notification's params: a dictionary of Newtonsoft tokens, as dotacp hands them over.</param>
    private void PublishNotice(object notification)
    {
        if (notification is not IDictionary<string, object> arguments
            || DotAcpClientAdapter.ReadString(arguments, "sessionId") is not { } sessionId)
        {
            // The stream renames only a line whose params.sessionId is a string, so this is unreachable.
            DotAcpClientAdapter.LogNoticeMalformed(logger);
            return;
        }

        try
        {
            if (!arguments.TryGetValue("update", out object? update)
                || update is not JObject fields
                || fields.Value<string>("title") is not { Length: > 0 } title)
            {
                DotAcpClientAdapter.LogNoticeMalformed(logger);
                return;
            }

            NoticeSeverity severity = fields.Value<string>("severity") switch
            {
                "warning" => NoticeSeverity.Warning,
                "error" => NoticeSeverity.Error,
                _ => NoticeSeverity.Info,
            };
            string? description = fields.Value<string>("description") is { Length: > 0 } text ? text : null;
            this.Deliver(sessionId, new AgentNotice(sessionId, severity, title, description));
        }
        finally
        {
            ledger.NoteDispatched(sessionId);
        }
    }

    /// <summary>
    /// Publishes <paramref name="agentEvent"/> to its session's sink. For a session not registered yet, it is held while a
    /// start is in flight (see <see cref="BeginSessionStart"/>) and otherwise dropped with a Warning.
    /// </summary>
    /// <param name="sessionId">The session the update names.</param>
    /// <param name="agentEvent">The mapped update.</param>
    private void Deliver(string sessionId, AgentEvent agentEvent)
    {
        lock (this.gate)
        {
            if (this.sinks.TryGetValue(sessionId, out ISessionSink? sink))
            {
                sink.TryPublish(agentEvent);
                return;
            }

            if (this.startsInFlight > 0 && this.earlyCount < DotAcpClientAdapter.MaxEarlyEvents)
            {
                if (!this.early.TryGetValue(sessionId, out List<AgentEvent>? held))
                {
                    held = [];
                    this.early[sessionId] = held;
                }

                held.Add(agentEvent);
                this.earlyCount++;
                DotAcpClientAdapter.LogEarlyUpdateHeld(logger, sessionId);
                return;
            }
        }

        DotAcpClientAdapter.LogUnknownSessionUpdate(logger, sessionId);
    }

    /// <summary>Ends one start; when it was the last, drops what is still held, one Warning per update as if it had just arrived.</summary>
    private void EndSessionStart()
    {
        List<string> dropped = [];
        lock (this.gate)
        {
            this.startsInFlight--;
            if (this.startsInFlight == 0)
            {
                foreach (KeyValuePair<string, List<AgentEvent>> entry in this.early)
                {
                    dropped.AddRange(Enumerable.Repeat(entry.Key, entry.Value.Count));
                }

                this.early.Clear();
                this.earlyCount = 0;
            }
        }

        foreach (string sessionId in dropped)
        {
            DotAcpClientAdapter.LogUnknownSessionUpdate(logger, sessionId);
        }
    }

    /// <summary>Reads a string argument. dotacp hands each value over as a Newtonsoft <see cref="JValue"/>.</summary>
    /// <param name="arguments">The request arguments.</param>
    /// <param name="key">The argument name.</param>
    /// <returns>The string, or null when the argument is absent, null or not a string.</returns>
    private static string? ReadString(IDictionary<string, object> arguments, string key)
    {
        return arguments.TryGetValue(key, out object? value) && value is JValue { Value: string text } ? text : null;
    }

    /// <summary>Converts the <c>requestedSchema</c> token to compact JSON text, once, so nothing downstream sees a Newtonsoft type.</summary>
    /// <param name="arguments">The request arguments.</param>
    /// <returns>The schema text, or <c>{}</c> when the argument is absent.</returns>
    private static string ReadSchemaJson(IDictionary<string, object> arguments)
    {
        return arguments.TryGetValue("requestedSchema", out object? schema) && schema is JToken token
            ? token.ToString(Formatting.None)
            : "{}";
    }

    private static Dictionary<string, object> MapElicitationResult(ElicitationResult result)
    {
        return result switch
        {
            ElicitationAccepted accepted => new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["action"] = "accept",
                ["content"] = new Dictionary<string, object>(accepted.Content, StringComparer.Ordinal),
            },
            ElicitationDeclined => DotAcpClientAdapter.ElicitationReply("decline"),
            _ => DotAcpClientAdapter.ElicitationReply("cancel"),
        };
    }

    private static Dictionary<string, object> ElicitationReply(string action)
    {
        return new Dictionary<string, object>(StringComparer.Ordinal) { ["action"] = action };
    }

    private void DisposeSessionLifetimes()
    {
        foreach (string sessionId in this.sessionLifetimes.Keys)
        {
            if (this.sessionLifetimes.TryRemove(sessionId, out CancellationLifetime? lifetime))
            {
                lifetime.Dispose();
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Dropping update for unknown session {SessionId}")]
    private static partial void LogUnknownSessionUpdate(ILogger logger, string sessionId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Holding an update for session {SessionId} until it is registered.")]
    private static partial void LogEarlyUpdateHeld(ILogger logger, string sessionId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Dropping permission request for unknown session {SessionId}")]
    private static partial void LogUnknownPermissionSession(ILogger logger, string sessionId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Permission request for session {SessionId} was cancelled.")]
    private static partial void LogPermissionCancelled(ILogger logger, string sessionId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Permission handler failed for session {SessionId}.")]
    private static partial void LogPermissionHandlerFailed(ILogger logger, Exception exception, string sessionId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Ignoring extension notification {Method}.")]
    private static partial void LogExtensionNotificationIgnored(ILogger logger, string method);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Dropping a session notice that carries no readable session id or title.")]
    private static partial void LogNoticeMalformed(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cancelling an elicitation request that carries no readable session id.")]
    private static partial void LogElicitationMalformed(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cancelling elicitation request for unknown session {SessionId}")]
    private static partial void LogElicitationUnknownSession(ILogger logger, string sessionId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cancelling elicitation request for session {SessionId}: no elicitation scope is bound.")]
    private static partial void LogElicitationNoScope(ILogger logger, string sessionId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Elicitation request for session {SessionId} was cancelled.")]
    private static partial void LogElicitationCancelled(ILogger logger, string sessionId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Elicitation scope failed for session {SessionId}.")]
    private static partial void LogElicitationFailed(ILogger logger, Exception exception, string sessionId);

    /// <summary>
    /// A <see cref="CancellationTokenSource"/> whose token is captured once, so it stays readable after
    /// the source is disposed, and whose cancellation is safe to request after disposal.
    /// </summary>
    private sealed class CancellationLifetime : IDisposable
    {
        private readonly CancellationTokenSource source = new CancellationTokenSource();

        internal CancellationLifetime()
        {
            this.Token = this.source.Token;
        }

        internal CancellationToken Token { get; }

        internal void Cancel()
        {
            try
            {
                this.source.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Disposal cancels first, so a source disposed before this call is already cancelled.
            }
        }

        public void Dispose()
        {
            this.Cancel();
            this.source.Dispose();
        }
    }

    /// <summary>The scope <see cref="BeginSessionStart"/> returns: ends its start once, however often it is disposed.</summary>
    /// <param name="owner">The adapter whose start this is.</param>
    private sealed class SessionStart(DotAcpClientAdapter owner) : IDisposable
    {
        private int ended;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref this.ended, 1) == 0)
            {
                owner.EndSessionStart();
            }
        }
    }
}
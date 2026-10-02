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
internal sealed partial class DotAcpClientAdapter(ILogger<DotAcpClientAdapter> logger) : dotacp.client.IAcpClient, IDisposable
{
    private const string ElicitationCreateMethod = "elicitation/create";

    private readonly ConcurrentDictionary<string, ISessionSink> sinks = new ConcurrentDictionary<string, ISessionSink>();

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
        this.sinks[sink.SessionId] = sink;

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

    internal bool Unregister(string sessionId)
    {
        bool removed = this.sinks.TryRemove(sessionId, out _);
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
        string sessionId = (string)notification.SessionId;
        if (!this.sinks.TryGetValue(sessionId, out ISessionSink? sink))
        {
            DotAcpClientAdapter.LogUnknownSessionUpdate(logger, sessionId);
            return Task.CompletedTask;
        }

        sink.TryPublish(SessionUpdateMapper.Map(sessionId, notification.Update));
        return Task.CompletedTask;
    }

    public async Task<dotacp.protocol.RequestPermissionResponse> RequestPermissionAsync(dotacp.protocol.RequestPermissionRequest request, CancellationToken cancellationToken = default)
    {
        string sessionId = (string)request.SessionId;
        if (!this.sinks.TryGetValue(sessionId, out ISessionSink? sink))
        {
            DotAcpClientAdapter.LogUnknownPermissionSession(logger, sessionId);
            return new dotacp.protocol.RequestPermissionResponse { Outcome = new dotacp.protocol.RequestPermissionOutcomeCancelled() };
        }

        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, sink.PromptCancellation);

        try
        {
            PermissionDecision decision = await sink.PermissionHandler.DecideAsync(SessionUpdateMapper.MapPermission(request), linked.Token).ConfigureAwait(false);

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
        DotAcpClientAdapter.LogExtensionNotificationIgnored(logger, method);
        return Task.CompletedTask;
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Dropping permission request for unknown session {SessionId}")]
    private static partial void LogUnknownPermissionSession(ILogger logger, string sessionId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Permission request for session {SessionId} was cancelled.")]
    private static partial void LogPermissionCancelled(ILogger logger, string sessionId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Permission handler failed for session {SessionId}.")]
    private static partial void LogPermissionHandlerFailed(ILogger logger, Exception exception, string sessionId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Ignoring extension notification {Method}.")]
    private static partial void LogExtensionNotificationIgnored(ILogger logger, string method);

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
}
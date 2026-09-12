namespace Agency.Huddle.Acp.DotAcp;

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Agency.Huddle.Acp.Abstractions;

/// <summary>Implements the ACP client contract on behalf of Team.Console, routing agent-originated calls to the session they belong to.</summary>
internal sealed partial class DotAcpClientAdapter(ILogger<DotAcpClientAdapter> logger) : dotacp.client.IAcpClient
{
    private readonly ConcurrentDictionary<string, ISessionSink> sinks = new ConcurrentDictionary<string, ISessionSink>();

    internal IReadOnlyCollection<ISessionSink> Sinks => this.sinks.Values.ToArray();

    internal void Register(ISessionSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        this.sinks[sink.SessionId] = sink;
    }

    internal bool Unregister(string sessionId)
    {
        return this.sinks.TryRemove(sessionId, out _);
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

    public Task<object> ExtMethodAsync(string method, object request, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException($"ExtMethodAsync ({method}) is not supported by Team.Console.");
    }

    public Task ExtNotificationAsync(string method, object notification, CancellationToken cancellationToken = default)
    {
        DotAcpClientAdapter.LogExtensionNotificationIgnored(logger, method);
        return Task.CompletedTask;
    }

    public void OnDisconnected(dotacp.client.Connection connection)
    {
        foreach (ISessionSink sink in this.sinks.Values)
        {
            sink.Fault(new AgentDisconnectedException());
        }

        this.sinks.Clear();
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
}
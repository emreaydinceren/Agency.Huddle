using Agency.Huddle.App.Pipes;

namespace Agency.Huddle.App.Services;

/// <summary>Stops an Agent in a Room: the queue rows close first, so a Turn that ends after the stop cannot start the next one.</summary>
/// <param name="control">Closes the rows.</param>
/// <param name="gateway">Stops the live Turn.</param>
/// <param name="logger">Receives a failed close.</param>
internal sealed class TurnControl(ITurnQueueControl control, IAgentGateway gateway, ILogger<TurnControl> logger) : ITurnControl
{
    /// <inheritdoc />
    public async Task StopAsync(string agentId, string roomId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);

        try
        {
            control.CloseForStop(agentId, roomId);
        }
        catch (Exception ex) when (ex is Microsoft.Data.Sqlite.SqliteException or IOException)
        {
            // A queue that cannot be written must not keep the live Turn running: the Stop still reaches the Agent.
            logger.LogWarning(ex, "Closing the Turn Queue rows of {AgentId} in room {RoomId} failed; stopping its Turn anyway.", agentId, roomId);
        }

        await gateway.StopTurnAsync(agentId, roomId, ct).ConfigureAwait(false);
    }
}

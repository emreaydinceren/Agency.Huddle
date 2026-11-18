using Agency.Huddle.App.Goldfish;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Profile;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Services;

/// <summary>Stops an Agent in a Room: the queue rows close first, so a Turn that ends after the stop cannot start the next one.</summary>
/// <param name="control">Closes the rows.</param>
/// <param name="gateway">Stops the live Turn.</param>
/// <param name="logger">Receives a failed close.</param>
/// <param name="goldfish">Ends a possession the Stop lands on, or <see langword="null"/> where goldfish mode is not wired.</param>
/// <param name="human">Names the Human who called it back, or <see langword="null"/>.</param>
internal sealed class TurnControl(
    ITurnQueueControl control,
    IAgentGateway gateway,
    ILogger<TurnControl> logger,
    GoldfishSupervisor? goldfish = null,
    IHumanIdentity? human = null) : ITurnControl
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

        if (goldfish?.FindOpen(agentId) is { State: not PossessionState.Returning } open && string.Equals(open.RoomId, roomId, StringComparison.Ordinal))
        {
            // E-13: the Human's Stop on a goldfish's Draft is a comeback by the Human; ending it cancels the goldfish Turn, so no StopTurn.
            _ = await goldfish.EndAsync(open.Id, GoldfishExit.CalledBack, human?.Name, ct).ConfigureAwait(false);
            return;
        }

        await gateway.StopTurnAsync(agentId, roomId, ct).ConfigureAwait(false);
    }
}

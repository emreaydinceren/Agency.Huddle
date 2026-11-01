namespace Agency.Huddle.App.Pipes;

/// <summary>
/// The Agent connections registry, as seen by callers outside <see cref="AgentGateway"/> itself.
/// </summary>
public interface IAgentGateway
{
    /// <summary>Whether the Agent identified by <paramref name="agentId"/> currently holds a live connection.</summary>
    bool IsOnline(string agentId);

    /// <summary>Asks one Agent to stop: its live Turn ends and its queued work is discarded.</summary>
    Task StopTurnAsync(string agentId, string roomId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks one Agent to start its session in one Room afresh (LR-R15): the Agent forgets that Room's stored session only.
    /// An Agent that is not connected is a silent no-op.
    /// </summary>
    /// <param name="agentId">The Agent to ask.</param>
    /// <param name="roomId">The Room whose stored session the Agent forgets.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    Task StartAfreshAsync(string agentId, string roomId, CancellationToken cancellationToken = default);

    /// <summary>Raised when any Agent connects or disconnects.</summary>
    event Action? PresenceChanged;
}
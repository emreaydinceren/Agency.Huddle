namespace Agency.Huddle.App.Pipes;

/// <summary>
/// The Agent connections registry, as seen by callers outside <see cref="AgentGateway"/> itself.
/// </summary>
public interface IAgentGateway
{
    /// <summary>Whether the Agent identified by <paramref name="agentId"/> currently holds a live connection.</summary>
    bool IsOnline(string agentId);

    /// <summary>The ids of every Agent currently connected.</summary>
    IReadOnlyCollection<string> OnlineAgentIds { get; }

    /// <summary>Asks one Agent to stop: its live Turn ends and its queued work is discarded.</summary>
    Task StopTurnAsync(string agentId, string roomId, CancellationToken cancellationToken = default);

    /// <summary>Raised when any Agent connects or disconnects.</summary>
    event Action? PresenceChanged;
}
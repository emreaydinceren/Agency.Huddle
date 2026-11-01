namespace Agency.Huddle.Tests.Ui;

using Agency.Huddle.App.Pipes;

/// <summary>
/// A hand-written stand-in for <see cref="IAgentGateway"/> used by the page-level UI tests in this
/// folder, distinct from <see cref="Agency.Huddle.Tests.Acp.Tools.FakeAgentGateway"/> which serves
/// the Tools tests and never needs to raise <see cref="PresenceChanged"/>. Every Agent id defaults
/// to offline, matching the real <c>AgentGateway</c>'s behaviour when nothing ever registers a pipe
/// connection - the same default <c>AgentsPageTests</c> already relies on - but a test can flip
/// one Agent online (or back off) and observe <see cref="PresenceChanged"/> fire, which the real
/// gateway can only be driven to do by dialing an actual named pipe. Not a mocking framework - just
/// the smallest class satisfying the interface.
/// </summary>
internal sealed class FakeAgentGateway : IAgentGateway
{
    private readonly HashSet<string> onlineAgentIds = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public event Action? PresenceChanged;

    /// <inheritdoc />
    public bool IsOnline(string agentId) => this.onlineAgentIds.Contains(agentId);

    /// <summary>Not needed by the tests this fake serves; returns immediately.</summary>
    public Task StopTurnAsync(string agentId, string roomId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>Every <see cref="StartAfreshAsync"/> call, in the order received, for a test to assert against.</summary>
    public List<(string AgentId, string RoomId)> StartAfreshCalls { get; } = [];

    /// <summary>Records the call in <see cref="StartAfreshCalls"/>; sends nothing.</summary>
    public Task StartAfreshAsync(string agentId, string roomId, CancellationToken cancellationToken = default)
    {
        this.StartAfreshCalls.Add((agentId, roomId));
        return Task.CompletedTask;
    }

    /// <summary>Marks <paramref name="agentId"/> online and raises <see cref="PresenceChanged"/>, the way a real Hello over the pipe would.</summary>
    /// <param name="agentId">The Agent to mark online.</param>
    public void SetOnline(string agentId)
    {
        this.onlineAgentIds.Add(agentId);
        this.PresenceChanged?.Invoke();
    }

    /// <summary>Marks <paramref name="agentId"/> offline and raises <see cref="PresenceChanged"/>, the way a real disconnect would.</summary>
    /// <param name="agentId">The Agent to mark offline.</param>
    public void SetOffline(string agentId)
    {
        this.onlineAgentIds.Remove(agentId);
        this.PresenceChanged?.Invoke();
    }
}

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
/// <para>
/// Thread-safe: a test flips an Agent online while <c>Eventually.DirectRoomAsync</c> polls
/// <see cref="IsOnline"/> from the thread pool, and bUnit's <c>WaitForAssertion</c> reads the recorded calls
/// while a component handler records one. Every read and write takes <see cref="gate"/>, the recorded calls
/// are handed out as snapshots, and <see cref="PresenceChanged"/> is raised outside the lock so a subscriber
/// that calls back into <see cref="IsOnline"/> cannot re-enter it.
/// </para>
/// </summary>
internal sealed class FakeAgentGateway : IAgentGateway
{
    private readonly Lock gate = new();
    private readonly HashSet<string> onlineAgentIds = new(StringComparer.Ordinal);
    private readonly List<(string AgentId, string RoomId)> stopTurnCalls = [];
    private readonly List<(string AgentId, string MessageId, Agency.Huddle.Contracts.Redelivery Redelivery)> deliverToCalls = [];
    private readonly List<(string AgentId, string RoomId)> startAfreshCalls = [];

    /// <inheritdoc />
    public event Action? PresenceChanged;

    /// <inheritdoc />
    public bool IsOnline(string agentId)
    {
        lock (this.gate)
        {
            return this.onlineAgentIds.Contains(agentId);
        }
    }

    /// <summary>A snapshot of every <see cref="StopTurnAsync"/> call, in the order received, for a test to assert against.</summary>
    public IReadOnlyList<(string AgentId, string RoomId)> StopTurnCalls => this.Snapshot(this.stopTurnCalls);

    /// <summary>Records the call in <see cref="StopTurnCalls"/>; sends nothing.</summary>
    public Task StopTurnAsync(string agentId, string roomId, CancellationToken cancellationToken = default)
    {
        this.Record(this.stopTurnCalls, (agentId, roomId));
        return Task.CompletedTask;
    }

    /// <summary>A snapshot of every <see cref="DeliverToAsync"/> call, in the order received, for a test to assert against.</summary>
    public IReadOnlyList<(string AgentId, string MessageId, Agency.Huddle.Contracts.Redelivery Redelivery)> DeliverToCalls => this.Snapshot(this.deliverToCalls);

    /// <summary>Records the call in <see cref="DeliverToCalls"/>; sends nothing.</summary>
    public Task DeliverToAsync(string agentId, Agency.Huddle.App.Services.MessagePostedEvent e, Agency.Huddle.Contracts.Redelivery redelivery, CancellationToken ct)
    {
        this.Record(this.deliverToCalls, (agentId, e.Message.Id, redelivery));
        return Task.CompletedTask;
    }

    /// <summary>A snapshot of every <see cref="StartAfreshAsync"/> call, in the order received, for a test to assert against.</summary>
    public IReadOnlyList<(string AgentId, string RoomId)> StartAfreshCalls => this.Snapshot(this.startAfreshCalls);

    /// <summary>Records the call in <see cref="StartAfreshCalls"/>; sends nothing.</summary>
    public Task StartAfreshAsync(string agentId, string roomId, CancellationToken cancellationToken = default)
    {
        this.Record(this.startAfreshCalls, (agentId, roomId));
        return Task.CompletedTask;
    }

    /// <summary>Marks <paramref name="agentId"/> online and raises <see cref="PresenceChanged"/>, the way a real Hello over the pipe would.</summary>
    /// <param name="agentId">The Agent to mark online.</param>
    public void SetOnline(string agentId)
    {
        lock (this.gate)
        {
            this.onlineAgentIds.Add(agentId);
        }

        this.PresenceChanged?.Invoke();
    }

    /// <summary>Marks <paramref name="agentId"/> offline and raises <see cref="PresenceChanged"/>, the way a real disconnect would.</summary>
    /// <param name="agentId">The Agent to mark offline.</param>
    public void SetOffline(string agentId)
    {
        lock (this.gate)
        {
            this.onlineAgentIds.Remove(agentId);
        }

        this.PresenceChanged?.Invoke();
    }

    /// <summary>Appends <paramref name="call"/> to <paramref name="calls"/> under <see cref="gate"/>.</summary>
    private void Record<T>(List<T> calls, T call)
    {
        lock (this.gate)
        {
            calls.Add(call);
        }
    }

    /// <summary>Copies <paramref name="calls"/> under <see cref="gate"/>, so a reader never enumerates a list being appended to.</summary>
    private T[] Snapshot<T>(List<T> calls)
    {
        lock (this.gate)
        {
            return [.. calls];
        }
    }
}

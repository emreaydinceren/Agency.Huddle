namespace Agency.Huddle.Tests.Acp.Tools;

using Agency.Huddle.App.Pipes;

/// <summary>
/// A minimal, hand-written stand-in for <see cref="IAgentGateway"/>. The real <c>AgentGateway</c> tracks live pipe
/// connections, which the Tools tests have no need to stand up; this fake lets a test mark an Agent id online
/// without any of that machinery. Not a mocking framework — just the smallest class satisfying the interface.
/// Thread-safe for the same reason as <see cref="Agency.Huddle.Tests.Ui.FakeAgentGateway"/>: product code may read
/// <see cref="IsOnline"/> on another thread while a test writes, so every read and write takes <see cref="gate"/>.
/// </summary>
internal sealed class FakeAgentGateway : IAgentGateway
{
    private readonly Lock gate = new();
    private readonly HashSet<string> onlineAgentIds = new(StringComparer.Ordinal);
    private readonly List<(string AgentId, string RoomId)> startAfreshCalls = [];

    // The Tools tests this fake serves never go offline/online through it, so nothing ever raises
    // this - it exists only to satisfy the interface.
#pragma warning disable CS0067 // PresenceChanged is part of IAgentGateway; this fake never raises it.
    /// <inheritdoc />
    public event Action? PresenceChanged;
#pragma warning restore CS0067

    /// <inheritdoc />
    public bool IsOnline(string agentId)
    {
        lock (this.gate)
        {
            return this.onlineAgentIds.Contains(agentId);
        }
    }

    /// <summary>Records that <paramref name="agentId"/> is online, for a test to arrange against.</summary>
    public void SetOnline(string agentId)
    {
        lock (this.gate)
        {
            this.onlineAgentIds.Add(agentId);
        }
    }

    /// <summary>Not needed by the Tools tests this fake serves; returns immediately.</summary>
    public Task StopTurnAsync(string agentId, string roomId, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>Not needed by the Tools tests this fake serves; sends nothing.</summary>
    public Task DeliverToAsync(string agentId, Agency.Huddle.App.Services.MessagePostedEvent e, Agency.Huddle.Contracts.Redelivery redelivery, CancellationToken ct) =>
        Task.CompletedTask;

    /// <summary>A snapshot of every <see cref="StartAfreshAsync"/> call, in the order received, for a test to assert against.</summary>
    public IReadOnlyList<(string AgentId, string RoomId)> StartAfreshCalls
    {
        get
        {
            lock (this.gate)
            {
                return [.. this.startAfreshCalls];
            }
        }
    }

    /// <summary>Records the call in <see cref="StartAfreshCalls"/>; sends nothing.</summary>
    public Task StartAfreshAsync(string agentId, string roomId, CancellationToken cancellationToken = default)
    {
        lock (this.gate)
        {
            this.startAfreshCalls.Add((agentId, roomId));
        }

        return Task.CompletedTask;
    }
}
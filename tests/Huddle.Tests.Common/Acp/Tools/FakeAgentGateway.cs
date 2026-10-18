namespace Agency.Huddle.Tests.Acp.Tools;

using Agency.Huddle.App.Pipes;

/// <summary>
/// A minimal, hand-written stand-in for <see cref="IAgentGateway"/>. The real <c>AgentGateway</c> tracks live pipe
/// connections, which the Tools tests have no need to stand up; this fake lets a test mark an Agent id online
/// without any of that machinery. Not a mocking framework — just the smallest class satisfying the interface.
/// </summary>
internal sealed class FakeAgentGateway : IAgentGateway
{
    private readonly HashSet<string> onlineAgentIds = new(StringComparer.Ordinal);

    // The Tools tests this fake serves never go offline/online through it, so nothing ever raises
    // this - it exists only to satisfy the interface.
#pragma warning disable CS0067 // PresenceChanged is part of IAgentGateway; this fake never raises it.
    /// <inheritdoc />
    public event Action? PresenceChanged;
#pragma warning restore CS0067

    /// <inheritdoc />
    public bool IsOnline(string agentId)
    {
        return this.onlineAgentIds.Contains(agentId);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> OnlineAgentIds => this.onlineAgentIds.ToArray();

    /// <summary>Records that <paramref name="agentId"/> is online, for a test to arrange against.</summary>
    public void SetOnline(string agentId)
    {
        this.onlineAgentIds.Add(agentId);
    }

    /// <summary>Not needed by the Tools tests this fake serves; returns immediately.</summary>
    public Task StopTurnAsync(string agentId, string roomId, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
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

    public bool IsOnline(string agentId)
    {
        return this.onlineAgentIds.Contains(agentId);
    }

    public IReadOnlyCollection<string> OnlineAgentIds => this.onlineAgentIds.ToArray();

    public void SetOnline(string agentId)
    {
        this.onlineAgentIds.Add(agentId);
    }
}
namespace Agency.Huddle.App.Pipes;

public interface IAgentGateway
{
    bool IsOnline(string agentId);

    IReadOnlyCollection<string> OnlineAgentIds { get; }
}
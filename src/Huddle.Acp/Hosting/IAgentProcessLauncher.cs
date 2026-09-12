namespace Agency.Huddle.Acp.Hosting;

/// <summary>Launches an agent process from a set of options.</summary>
public interface IAgentProcessLauncher
{
    IAgentProcess Launch(AgentProcessOptions options);
}
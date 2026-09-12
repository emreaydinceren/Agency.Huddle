namespace Agency.Huddle.Acp.Abstractions;

using System;

/// <summary>Thrown when the agent process disconnects unexpectedly.</summary>
public sealed class AgentDisconnectedException : AgentException
{
    public AgentDisconnectedException()
        : base("The agent process disconnected.")
    {
    }

    public AgentDisconnectedException(Exception inner)
        : base("The agent process disconnected.", inner)
    {
    }
}
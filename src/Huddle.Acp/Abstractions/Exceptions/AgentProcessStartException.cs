namespace Agency.Huddle.Acp.Abstractions;

using System;

/// <summary>Thrown when the agent process fails to start.</summary>
public sealed class AgentProcessStartException : AgentException
{
    public AgentProcessStartException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
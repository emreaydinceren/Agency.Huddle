namespace Agency.Huddle.Acp.Abstractions;

using System;

/// <summary>Base type for exceptions raised by this library's agent abstractions.</summary>
public class AgentException : Exception
{
    public AgentException(string message)
        : base(message)
    {
    }

    public AgentException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
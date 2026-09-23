namespace Agency.Huddle.Acp.Abstractions;

using System;

/// <summary>Thrown when a resume is asked for a session id the agent no longer has (RS §6.4 A-2).</summary>
public sealed class AgentSessionNotFoundException : AgentException
{
    /// <summary>Initializes a new instance of the <see cref="AgentSessionNotFoundException"/> class for <paramref name="sessionId"/>.</summary>
    /// <param name="sessionId">The session id the agent reported it does not have.</param>
    public AgentSessionNotFoundException(string sessionId)
        : base($"No session was found for id '{sessionId}'.")
    {
        this.SessionId = sessionId;
    }

    /// <summary>Initializes a new instance of the <see cref="AgentSessionNotFoundException"/> class for <paramref name="sessionId"/>, wrapping <paramref name="inner"/>.</summary>
    /// <param name="sessionId">The session id the agent reported it does not have.</param>
    /// <param name="inner">The underlying error the agent raised.</param>
    public AgentSessionNotFoundException(string sessionId, Exception inner)
        : base($"No session was found for id '{sessionId}'.", inner)
    {
        this.SessionId = sessionId;
    }

    /// <summary>The session id that was not found.</summary>
    public string SessionId { get; }
}

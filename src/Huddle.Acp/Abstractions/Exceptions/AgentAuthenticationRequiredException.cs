namespace Agency.Huddle.Acp.Abstractions;

using System.Collections.Generic;

/// <summary>Thrown when the agent requires authentication before a session can be started.</summary>
public sealed class AgentAuthenticationRequiredException : AgentException
{
    public AgentAuthenticationRequiredException(IReadOnlyList<AuthMethodInfo> authMethods)
        : base("The agent requires authentication.")
    {
        this.AuthMethods = authMethods;
    }

    public IReadOnlyList<AuthMethodInfo> AuthMethods { get; }
}
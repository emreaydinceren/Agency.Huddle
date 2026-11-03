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

    /// <summary>Initializes the exception with the adapter's own message, as when a prompt fails because the sign-in expired.</summary>
    /// <param name="authMethods">The authentication methods the agent offers, possibly empty.</param>
    /// <param name="message">The adapter's message, shown as the exception message.</param>
    public AgentAuthenticationRequiredException(IReadOnlyList<AuthMethodInfo> authMethods, string message)
        : base(message)
    {
        ArgumentNullException.ThrowIfNull(authMethods);
        this.AuthMethods = authMethods;
    }

    public IReadOnlyList<AuthMethodInfo> AuthMethods { get; }
}
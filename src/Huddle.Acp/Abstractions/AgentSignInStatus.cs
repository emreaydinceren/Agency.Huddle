namespace Agency.Huddle.Acp.Abstractions;

/// <summary>
/// Whether an agent reports that a person is signed in. Only an agent that pushes its identity says so (the Claude
/// Adapter does, through <c>_auth/status_update</c>); for any other agent, or before it has spoken, the answer is
/// <see cref="Unknown"/>, never a guess.
/// </summary>
public enum AgentSignInStatus
{
    /// <summary>The agent did not say: it never advertised that it reports identity, or it has not reported yet.</summary>
    Unknown = 0,

    /// <summary>The agent reported that nobody is signed in.</summary>
    SignedOut = 1,

    /// <summary>The agent reported some sign-in (an account, an API key, a gateway or an external credential).</summary>
    SignedIn = 2,
}

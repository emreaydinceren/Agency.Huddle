namespace Agency.Huddle.App.Acp;

/// <summary>
/// What is known about one Persona's Agent right now. Pipe liveness — whether
/// <c>AgentGateway.IsOnline</c> reports a connection — and health — whether the Agent is doing what
/// was asked of it — are different facts; see <see cref="PersonaStatusResolver"/> for how the two
/// combine into the single value every UI surface renders.
/// </summary>
public enum PersonaState
{
    /// <summary>The Adapter is launching and the session is not yet created.</summary>
    Starting,

    /// <summary>Connected and working.</summary>
    Online,

    /// <summary>Connected, but not doing what was asked of it. The Reason says what.</summary>
    Degraded,

    /// <summary>Not working. The Reason says why, when one is known.</summary>
    Offline,
}

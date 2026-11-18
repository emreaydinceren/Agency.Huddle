namespace Agency.Huddle.App.Goldfish;

/// <summary>One Agent that is in goldfish mode now, as the UI draws it.</summary>
/// <param name="Row">The open possession.</param>
/// <param name="AgentName">The Agent's display name, or its stored id when the directory no longer knows it.</param>
/// <param name="Waiting">Whether the goldfish is waiting for an answer rather than working.</param>
internal sealed record LivePossession(Possession Row, string AgentName, bool Waiting);

namespace Agency.Huddle.App.Services;

/// <summary>How many Messages wait for one Agent in one Room (Turn Queue spec §5.9, <c>C14</c>).</summary>
/// <param name="AgentId">The Agent that owes the Turns.</param>
/// <param name="AgentName">The Agent's current Name.</param>
/// <param name="Count">The Messages in <c>waiting</c> rows, not counting the running Turn.</param>
/// <param name="Held">Whether the Room's Budget is exhausted now, so the Messages wait for it.</param>
internal sealed record WaitingCount(string AgentId, string AgentName, int Count, bool Held);

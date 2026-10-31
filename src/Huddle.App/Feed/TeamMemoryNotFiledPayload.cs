namespace Agency.Huddle.App.Feed;

/// <summary>A member's Team Memory proposal that the Team Memory Helper did not file, or that was dropped (Team Memory Helper spec §6.6).</summary>
/// <param name="HelperId">The Helper's id, <c>TeamMemory</c>.</param>
/// <param name="Name">The Helper's display name.</param>
/// <param name="Team">The Team the proposal was for.</param>
/// <param name="Proposer">The proposing Persona's Name.</param>
/// <param name="Fact">The start of the proposed fact.</param>
/// <param name="Reason">Why it was not filed.</param>
internal sealed record TeamMemoryNotFiledPayload(string HelperId, string Name, string Team, string Proposer, string Fact, string Reason) : FeedPayload;

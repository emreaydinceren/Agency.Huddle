namespace Agency.Huddle.App.Helpers.TeamMemory;

/// <summary>One member's proposal for its Team's shared Memory, checked by <c>propose_team_memory</c> (Team Memory Helper spec §6.2).</summary>
/// <param name="Id">A new id per call, the dispatcher's key.</param>
/// <param name="Team">The Team's name as the catalog spells it.</param>
/// <param name="Fact">The fact, trimmed.</param>
/// <param name="Why">Where it was decided or learned, trimmed, or <see langword="null"/>.</param>
/// <param name="Proposer">The proposing Persona's Name.</param>
internal sealed record TeamMemoryProposal(Guid Id, string Team, string Fact, string? Why, string Proposer);

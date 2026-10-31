namespace Agency.Huddle.App.Teams;

/// <summary>Up to three changes one retro agent proposed to the Human, held under <c>Teams/&lt;Team&gt;/_proposals/retro/</c>.</summary>
/// <param name="Id">The set's id, a Guid in <c>"N"</c> format.</param>
/// <param name="Team">The Team the retro was for.</param>
/// <param name="Proposer">The Name of the Agent that proposed the changes.</param>
/// <param name="Changes">The changes still waiting for the Human.</param>
internal sealed record RetroProposalSet(string Id, string Team, string Proposer, IReadOnlyList<RetroChange> Changes);

namespace Agency.Huddle.App.Helpers.TeamMemory;

/// <summary>Everything the Team Memory Helper sees for one proposal, captured when it was made (Team Memory Helper spec §6.3).</summary>
/// <param name="Proposal">The proposal.</param>
/// <param name="Folders">The folder labels the Verdict may write: the Team's own, then each Project's.</param>
/// <param name="Index">One line per memory file in those folders, <c>folder/file — description</c>, capped.</param>
/// <param name="Candidates">The files that best match the proposal, in full.</param>
/// <param name="Attempt">1 for the first judgement, 2 for the one after a stale Verdict.</param>
internal sealed record TeamMemoryInput(
    TeamMemoryProposal Proposal,
    IReadOnlyList<string> Folders,
    IReadOnlyList<string> Index,
    IReadOnlyList<TeamMemoryCandidate> Candidates,
    int Attempt = 1);

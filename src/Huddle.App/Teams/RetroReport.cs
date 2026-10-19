namespace Agency.Huddle.App.Teams;

/// <summary>A retro report: the top issues and the proposed changes (RT-5).</summary>
/// <param name="Date">The day of the retro.</param>
/// <param name="TopIssues">At most three issues.</param>
/// <param name="Proposals">At most three proposed changes.</param>
internal sealed record RetroReport(DateOnly Date, IReadOnlyList<RetroIssue> TopIssues, IReadOnlyList<RetroProposal> Proposals);

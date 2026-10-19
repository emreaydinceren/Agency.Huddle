namespace Agency.Huddle.App.Teams;

/// <summary>One of the top issues of a retro report.</summary>
/// <param name="Title">A short name for the issue.</param>
/// <param name="Detail">What went wrong, in a sentence or two.</param>
/// <param name="EvidenceLinks">Task ids, log references or report files that show it.</param>
internal sealed record RetroIssue(string Title, string Detail, IReadOnlyList<string> EvidenceLinks);

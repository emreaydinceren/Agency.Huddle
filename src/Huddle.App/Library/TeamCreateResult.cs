namespace Agency.Huddle.App.Library;

/// <summary>The result of <see cref="ITeamFolders.CreateTeam"/>.</summary>
/// <param name="Outcome">How the call ended.</param>
/// <param name="Path">The full path of the written <c>_team.md</c> when <paramref name="Outcome"/> is <see cref="TeamCreateOutcome.Created"/>; otherwise <see langword="null"/>.</param>
internal sealed record TeamCreateResult(TeamCreateOutcome Outcome, string? Path);

namespace Agency.Huddle.App.Library;

/// <summary>How <see cref="ITeamFolders.CreateTeam"/> ended.</summary>
internal enum TeamCreateOutcome
{
    /// <summary>The Team folder exists and its <c>_team.md</c> was written.</summary>
    Created,

    /// <summary>The Team already has a <c>_team.md</c>; nothing was changed.</summary>
    AlreadyExists,

    /// <summary>The name breaks the Team-name rules; nothing was created.</summary>
    InvalidName,
}

/// <summary>The result of <see cref="ITeamFolders.CreateTeam"/>.</summary>
/// <param name="Outcome">How the call ended.</param>
/// <param name="Path">The full path of the written <c>_team.md</c> when <paramref name="Outcome"/> is <see cref="TeamCreateOutcome.Created"/>; otherwise <see langword="null"/>.</param>
internal sealed record TeamCreateResult(TeamCreateOutcome Outcome, string? Path);

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

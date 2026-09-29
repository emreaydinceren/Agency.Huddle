namespace Agency.Huddle.App.Components.Teams;

/// <summary>What <see cref="NewTeamDialog"/> is naming: a new Team, or a new Project inside a Team.</summary>
public enum NewTeamDialogMode
{
    /// <summary>A new Team, validated by <c>TeamNames.ValidateTeamName</c>.</summary>
    Team,

    /// <summary>A new Project in the dialog's Team, validated by <c>TeamNames.ValidateProjectName</c>.</summary>
    Project,
}

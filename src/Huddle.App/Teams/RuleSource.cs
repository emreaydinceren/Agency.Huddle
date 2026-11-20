namespace Agency.Huddle.App.Teams;

/// <summary>Which level of settings decided the effective <c>oneInProgressPerProject</c> rule (ADR-0103).</summary>
internal enum RuleSource
{
    /// <summary>The Team's <c>_team.md</c> decided: its value, or its default of off when it has none.</summary>
    Team,

    /// <summary>The Project's <c>_project.md</c> decided.</summary>
    Project,
}

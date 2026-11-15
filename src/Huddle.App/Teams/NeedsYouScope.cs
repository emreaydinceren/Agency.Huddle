namespace Agency.Huddle.App.Teams;

/// <summary>The place a Needs You level is set (ADR-0087). The most specific scope that sets a level wins.</summary>
internal enum NeedsYouScope
{
    /// <summary>Settings: the <c>Team:NeedsYouLevel</c> configuration key, overridden by the value Settings saves.</summary>
    Global,

    /// <summary>The <c>needsYouLevel</c> key of a Team's <c>_team.md</c>.</summary>
    Team,

    /// <summary>The <c>needsYouLevel</c> key of a Project's <c>_project.md</c>.</summary>
    Project,
}

namespace Agency.Huddle.App.Teams;

/// <summary>The place a Needs You level is set (ADR-0088). The most specific scope that sets a level wins.</summary>
internal enum NeedsYouScope
{
    /// <summary>Settings: the <c>Team:NeedsYouLevel</c> configuration key, overridden by the value Settings saves.</summary>
    Global,

    /// <summary>The level the Human set for a Team, kept in <c>team.db</c>.</summary>
    Team,

    /// <summary>The level the Human set for a Project, kept in <c>team.db</c>.</summary>
    Project,
}

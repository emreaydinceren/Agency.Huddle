namespace Agency.Huddle.App.Teams;

/// <summary>One level's value of the <c>oneInProgressPerProject</c> key in <c>_team.md</c> or <c>_project.md</c> (ADR-0103).</summary>
internal enum RuleSetting
{
    /// <summary>The key is absent or unreadable: a Team then counts as off and a Project inherits its Team's value.</summary>
    Absent,

    /// <summary>The key reads <c>'on'</c>: one Task In Progress at a time in each Project.</summary>
    On,

    /// <summary>The key reads <c>'off'</c>: Tasks of a Project may run in parallel.</summary>
    Off,
}

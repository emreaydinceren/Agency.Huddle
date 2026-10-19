namespace Agency.Huddle.App.Teammates;

/// <summary>Which hire limit refused a hire.</summary>
internal enum HireLimitKind
{
    /// <summary>No limit refused the hire.</summary>
    None,

    /// <summary>The per-Team member limit refused the hire.</summary>
    PerTeam,

    /// <summary>The live-process limit refused the hire.</summary>
    LiveProcesses,
}

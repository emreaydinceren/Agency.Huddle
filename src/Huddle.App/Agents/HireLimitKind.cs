namespace Agency.Huddle.App.Agents;

/// <summary>Which hire limit refused a hire.</summary>
internal enum HireLimitKind
{
    /// <summary>No limit refused the hire.</summary>
    None,

    /// <summary>The per-Agent limit refused the hire.</summary>
    PerTeam,

    /// <summary>The live-process limit refused the hire.</summary>
    LiveProcesses,
}

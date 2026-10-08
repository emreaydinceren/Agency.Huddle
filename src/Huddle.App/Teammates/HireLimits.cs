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

/// <summary>The outcome of a <see cref="HireLimits.Check"/>.</summary>
/// <param name="Allowed">True when the hire fits both limits.</param>
/// <param name="Kind">The limit that refused, or <see cref="HireLimitKind.None"/>.</param>
/// <param name="Limit">The value of the limit that refused; 0 when allowed.</param>
internal sealed record HireLimitResult(bool Allowed, HireLimitKind Kind, int Limit);

/// <summary>Decides whether a hire fits the per-Team and live-process limits.</summary>
internal static class HireLimits
{
    /// <summary>Checks a hire against both limits; the per-Team limit is checked first and a limit of zero or less is off.</summary>
    /// <param name="membersInTeam">Current members of the candidate's Team.</param>
    /// <param name="hiresRequested">How many Teammates are being hired.</param>
    /// <param name="liveProcesses">Agent processes currently running.</param>
    /// <param name="maxPerTeam">Per-Team limit; zero or less is off.</param>
    /// <param name="maxLive">Live-process limit; zero or less is off.</param>
    /// <returns>The result naming the refusing limit, if any.</returns>
    public static HireLimitResult Check(int membersInTeam, int hiresRequested, int liveProcesses, int maxPerTeam, int maxLive)
    {
        if (maxPerTeam > 0 && (membersInTeam + hiresRequested) > maxPerTeam)
        {
            return new HireLimitResult(false, HireLimitKind.PerTeam, maxPerTeam);
        }

        if (maxLive > 0 && (liveProcesses + hiresRequested) > maxLive)
        {
            return new HireLimitResult(false, HireLimitKind.LiveProcesses, maxLive);
        }

        return new HireLimitResult(true, HireLimitKind.None, 0);
    }
}

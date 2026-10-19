namespace Agency.Huddle.App.Teammates;

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

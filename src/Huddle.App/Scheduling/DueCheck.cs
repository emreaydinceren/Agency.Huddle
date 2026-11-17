namespace Agency.Huddle.App.Scheduling;

/// <summary>The one due-time rule for Jobs and the Memory Tidy timer. Pure: no clock, no store.</summary>
internal static class DueCheck
{
    /// <summary>Decides one tick for one timetable.</summary>
    /// <param name="state">What is stored about the timetable, or null when nothing is.</param>
    /// <param name="whenKey">What the timetable is worked out from now.</param>
    /// <param name="nextAfter">The timetable: the first run strictly after a time, or null when none is left.</param>
    /// <param name="oneOff">Whether the timetable runs once.</param>
    /// <param name="now">The caller's clock reading.</param>
    /// <param name="policy">How lateness and failure are treated.</param>
    /// <returns>What to do and the state to save.</returns>
    internal static DueDecision Next(DueState? state, string whenKey, Func<DateTimeOffset, DateTimeOffset?> nextAfter, bool oneOff, DateTimeOffset now, DuePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(whenKey);
        ArgumentNullException.ThrowIfNull(nextAfter);
        ArgumentNullException.ThrowIfNull(policy);

        DateTimeOffset? next = nextAfter(now);
        if (state is null || !string.Equals(state.WhenKey, whenKey, StringComparison.Ordinal))
        {
            return NewBaseline(state, whenKey, nextAfter, oneOff, now, policy, next);
        }

        if (state.NextRunUtc is not { } due || due > now)
        {
            return new DueDecision.NotDue();
        }

        if (state.FailedTries >= policy.MaxFailedTries)
        {
            return new DueDecision.GiveUp(state with { NextRunUtc = next, FailedTries = 0 }, oneOff);
        }

        TimeSpan lateness = now - due;
        if (lateness <= policy.Grace)
        {
            DateTimeOffset? following = nextAfter(due);
            if (following <= now)
            {
                following = next;
            }

            return new DueDecision.Run(state with { NextRunUtc = following }, false);
        }

        return DecideLate(state, oneOff, lateness, policy, next, now);
    }

    /// <summary>The state to store after a run ended, by how it ended.</summary>
    /// <param name="saved">The state stored before the run.</param>
    /// <param name="beforeRun">The state the run started from.</param>
    /// <param name="result">How the run ended.</param>
    /// <returns>The state to store.</returns>
    internal static DueState AfterRun(DueState saved, DueState beforeRun, DueRunResult result)
    {
        ArgumentNullException.ThrowIfNull(saved);
        ArgumentNullException.ThrowIfNull(beforeRun);

        return result switch
        {
            DueRunResult.Ran => saved with { FailedTries = 0, EverRan = true },
            DueRunResult.Held => beforeRun,
            DueRunResult.Failed => beforeRun with { FailedTries = beforeRun.FailedTries + 1 },
            _ => throw new ArgumentOutOfRangeException(nameof(result), "Unknown run result."),
        };
    }

    /// <summary>Rows 1 and 2: a timetable with no state or a changed key. A one-off whose time has passed unrun runs; anything else is only noted.</summary>
    private static DueDecision NewBaseline(DueState? state, string whenKey, Func<DateTimeOffset, DateTimeOffset?> nextAfter, bool oneOff, DateTimeOffset now, DuePolicy policy, DateTimeOffset? next)
    {
        bool everRan = state?.EverRan ?? false;
        DueState baseline = new(whenKey, next, 0, everRan);
        if (oneOff && !everRan)
        {
            DateTimeOffset since = policy.MaxLateness is { } maxLateness ? now - maxLateness : DateTimeOffset.MinValue;
            if (nextAfter(since) is { } due && due <= now)
            {
                TimeSpan lateness = now - due;
                if (lateness <= policy.Grace || policy.RunLate)
                {
                    return new DueDecision.Run(baseline, lateness > policy.Grace);
                }
            }
        }

        return new DueDecision.FirstSight(baseline);
    }

    /// <summary>Rows 6 to 9: a due time later than Grace.</summary>
    private static DueDecision DecideLate(DueState state, bool oneOff, TimeSpan lateness, DuePolicy policy, DateTimeOffset? next, DateTimeOffset now)
    {
        DueState moved = state with { NextRunUtc = next, FailedTries = 0 };
        if (!policy.RunLate)
        {
            return new DueDecision.Skip(moved);
        }

        if (oneOff && policy.MaxLateness is { } maxLateness && lateness > maxLateness)
        {
            return new DueDecision.GiveUp(moved, true);
        }

        if (!oneOff && next is { } regular && policy.MergeWindow > TimeSpan.Zero && regular - now <= policy.MergeWindow)
        {
            return new DueDecision.Skip(moved);
        }

        return new DueDecision.Run(state with { NextRunUtc = next }, true);
    }
}

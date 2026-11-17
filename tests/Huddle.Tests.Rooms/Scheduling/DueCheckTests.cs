using Agency.Huddle.App.Scheduling;

namespace Agency.Huddle.Tests.Scheduling;

/// <summary>Pins every row of the <see cref="DueCheck"/> table, each precedence pair and <see cref="DueCheck.AfterRun"/>, on a fixed clock.</summary>
public sealed class DueCheckTests
{
    private const string Key = "daily at 09:00|";
    private const string OldKey = "daily at 08:00|";

    private static readonly DateTimeOffset Now = At(16, 12, 0);

    private static readonly DuePolicy Policy = new(TimeSpan.FromMinutes(2), true, TimeSpan.FromHours(24), TimeSpan.FromHours(1), 3);

    private static DateTimeOffset At(int day, int hour, int minute, int second = 0) =>
        new(2026, 11, day, hour, minute, second, TimeSpan.Zero);

    /// <summary>A timetable that runs daily at 09:00 UTC.</summary>
    private static DateTimeOffset? Daily9(DateTimeOffset after)
    {
        DateTimeOffset candidate = new(after.Year, after.Month, after.Day, 9, 0, 0, TimeSpan.Zero);
        return candidate <= after ? candidate.AddDays(1) : candidate;
    }

    /// <summary>A timetable that runs every half hour on the half hour.</summary>
    private static DateTimeOffset? Every30(DateTimeOffset after)
    {
        long period = TimeSpan.FromMinutes(30).Ticks;
        return new DateTimeOffset((((after.UtcTicks / period) + 1) * period), TimeSpan.Zero);
    }

    /// <summary>A timetable that runs once, at the given time.</summary>
    private static Func<DateTimeOffset, DateTimeOffset?> Once(DateTimeOffset at) =>
        after => after < at ? at : null;

    private static DueDecision Recurring(DueState? state, DateTimeOffset now, DuePolicy? policy = null) =>
        DueCheck.Next(state, Key, Daily9, false, now, policy ?? Policy);

    private static DueDecision OneOff(DueState? state, DateTimeOffset at, DuePolicy? policy = null) =>
        DueCheck.Next(state, Key, Once(at), true, Now, policy ?? Policy);

    /// <summary>Row 1: a one-off seen for the first time a minute after its time runs, and within Grace it is not late.</summary>
    [Fact]
    public void Next_Row1_OneOffNeverSeenWithinGrace_RunsOnTime()
    {
        DueDecision decision = OneOff(null, At(16, 11, 59));

        Assert.Equal(new DueDecision.Run(new DueState(Key, null, 0, false), false), decision);
    }

    /// <summary>Row 1: a one-off first seen later than Grace but within MaxLateness runs, marked late.</summary>
    [Fact]
    public void Next_Row1_OneOffNeverSeenLate_RunsLate()
    {
        DueDecision decision = OneOff(null, At(16, 10, 0));

        Assert.Equal(new DueDecision.Run(new DueState(Key, null, 0, false), true), decision);
    }

    /// <summary>Row 1: with RunLate false, a one-off later than Grace is only noted, not run.</summary>
    [Fact]
    public void Next_Row1_OneOffLateAndRunLateFalse_IsFirstSight()
    {
        DuePolicy policy = Policy with { RunLate = false };

        DueDecision decision = OneOff(null, At(16, 10, 0), policy);

        Assert.Equal(new DueDecision.FirstSight(new DueState(Key, null, 0, false)), decision);
    }

    /// <summary>Row 1: with RunLate false, a one-off within Grace still runs.</summary>
    [Fact]
    public void Next_Row1_OneOffWithinGraceAndRunLateFalse_Runs()
    {
        DuePolicy policy = Policy with { RunLate = false };

        DueDecision decision = OneOff(null, At(16, 11, 59), policy);

        Assert.Equal(new DueDecision.Run(new DueState(Key, null, 0, false), false), decision);
    }

    /// <summary>Row 1: a one-off later than MaxLateness is not run.</summary>
    [Fact]
    public void Next_Row1_OneOffBeyondMaxLateness_IsFirstSight()
    {
        DueDecision decision = OneOff(null, At(14, 12, 0));

        Assert.Equal(new DueDecision.FirstSight(new DueState(Key, null, 0, false)), decision);
    }

    /// <summary>Row 1: with no MaxLateness a long-passed one-off still runs, late.</summary>
    [Fact]
    public void Next_Row1_NoMaxLateness_OldOneOffRunsLate()
    {
        DuePolicy policy = Policy with { MaxLateness = null };

        DueDecision decision = OneOff(null, At(1, 12, 0), policy);

        Assert.Equal(new DueDecision.Run(new DueState(Key, null, 0, false), true), decision);
    }

    /// <summary>Row 1 does not apply to a recurring timetable: it is only noted.</summary>
    [Fact]
    public void Next_Row1_RecurringTimetable_IsFirstSight()
    {
        DueDecision decision = Recurring(null, Now);

        Assert.Equal(new DueDecision.FirstSight(new DueState(Key, At(17, 9, 0), 0, false)), decision);
    }

    /// <summary>Row 1 does not apply to a one-off that has run before, even when its key changed; its ever-ran flag is kept.</summary>
    [Fact]
    public void Next_Row1_EverRanWithChangedKey_IsFirstSightKeepingEverRan()
    {
        DueState state = new(OldKey, At(15, 9, 0), 2, true);

        DueDecision decision = OneOff(state, At(16, 11, 59));

        Assert.Equal(new DueDecision.FirstSight(new DueState(Key, null, 0, true)), decision);
    }

    /// <summary>Precedence 1 over 2: a changed key on an unrun one-off whose time has passed runs instead of only being noted.</summary>
    [Fact]
    public void Next_Row1BeatsRow2_ChangedKeyOneOffPassed_Runs()
    {
        DueState state = new(OldKey, At(20, 9, 0), 0, false);

        DueDecision decision = OneOff(state, At(16, 11, 59));

        Assert.Equal(new DueDecision.Run(new DueState(Key, null, 0, false), false), decision);
    }

    /// <summary>Row 2: a timetable with no state is noted with its next run and a clean count.</summary>
    [Fact]
    public void Next_Row2_NoState_IsFirstSight()
    {
        DueDecision decision = Recurring(null, Now);

        Assert.Equal(new DueDecision.FirstSight(new DueState(Key, At(17, 9, 0), 0, false)), decision);
    }

    /// <summary>Row 2: a changed key is a new baseline: next run recomputed, tries reset, ever-ran kept.</summary>
    [Fact]
    public void Next_Row2_ChangedKey_IsFirstSightResettingTries()
    {
        DueState state = new(OldKey, At(15, 9, 0), 2, true);

        DueDecision decision = Recurring(state, Now);

        Assert.Equal(new DueDecision.FirstSight(new DueState(Key, At(17, 9, 0), 0, true)), decision);
    }

    /// <summary>Row 2: a one-off whose time is still ahead is noted with that time.</summary>
    [Fact]
    public void Next_Row2_OneOffInTheFuture_IsFirstSight()
    {
        DueDecision decision = OneOff(null, At(16, 13, 0));

        Assert.Equal(new DueDecision.FirstSight(new DueState(Key, At(16, 13, 0), 0, false)), decision);
    }

    /// <summary>Row 3: a stored next run in the future is not due.</summary>
    [Fact]
    public void Next_Row3_NextRunInTheFuture_IsNotDue()
    {
        DueDecision decision = Recurring(new DueState(Key, At(16, 12, 0, 1)), Now);

        Assert.Equal(new DueDecision.NotDue(), decision);
    }

    /// <summary>Row 3: a stored state with no next run is not due.</summary>
    [Fact]
    public void Next_Row3_NoNextRun_IsNotDue()
    {
        DueDecision decision = Recurring(new DueState(Key, null), Now);

        Assert.Equal(new DueDecision.NotDue(), decision);
    }

    /// <summary>Row 1: a one-off first seen exactly Grace after its time is on time, even with RunLate false.</summary>
    [Fact]
    public void Next_Row1_OneOffExactlyGraceLate_RunsOnTime()
    {
        DuePolicy policy = Policy with { RunLate = false };

        DueDecision decision = OneOff(null, At(16, 11, 58), policy);

        Assert.Equal(new DueDecision.Run(new DueState(Key, null, 0, false), false), decision);
    }

    /// <summary>Row 1: a changed key on a recurring timetable resets the failed tries.</summary>
    [Fact]
    public void Next_Row1_ChangedKeyRecurringWithFailedTries_ResetsTries()
    {
        DueDecision decision = Recurring(new DueState(OldKey, At(15, 9, 0), 2), Now);

        Assert.Equal(new DueDecision.FirstSight(new DueState(Key, At(17, 9, 0), 0, false)), decision);
    }

    /// <summary>Row 1: a changed key on a passed, unrun one-off runs with the failed tries reset.</summary>
    [Fact]
    public void Next_Row1_ChangedKeyOneOffWithFailedTries_RunsWithTriesReset()
    {
        DueDecision decision = OneOff(new DueState(OldKey, At(20, 9, 0), 2), At(16, 11, 59));

        Assert.Equal(new DueDecision.Run(new DueState(Key, null, 0, false), false), decision);
    }

    /// <summary>Precedence 7 over 8: a one-off beyond MaxLateness is given up even when its timetable still has a run within MergeWindow.</summary>
    [Fact]
    public void Next_Row7BeatsRow8_OneOffBeyondMaxLatenessWithNextRunSoon_GivesUp()
    {
        DueDecision decision = DueCheck.Next(new DueState(Key, At(14, 12, 0), 1), Key, Every30, true, Now, Policy);

        Assert.Equal(new DueDecision.GiveUp(new DueState(Key, At(16, 12, 30), 0), true), decision);
    }

    /// <summary>Row 3: a stored next run exactly at now is due, and on time.</summary>
    [Fact]
    public void Next_Row3_NextRunExactlyNow_IsDue()
    {
        DueDecision decision = Recurring(new DueState(Key, Now), Now);

        Assert.Equal(new DueDecision.Run(new DueState(Key, At(17, 9, 0)), false), decision);
    }

    /// <summary>Row 8 is for recurring timetables only: a one-off that still has a next run soon is run, not skipped.</summary>
    [Fact]
    public void Next_Row8_OneOffWithNextRunSoon_RunsLate()
    {
        DueDecision decision = DueCheck.Next(new DueState(Key, At(16, 11, 0)), Key, Every30, true, Now, Policy);

        Assert.Equal(new DueDecision.Run(new DueState(Key, At(16, 12, 30)), true), decision);
    }

    /// <summary>A zero MergeWindow never skips, even for a timetable that wrongly answers the time it was asked about.</summary>
    [Fact]
    public void Next_MergeWindowZero_NextRunAtNow_StillRuns()
    {
        DuePolicy policy = Policy with { MergeWindow = TimeSpan.Zero };

        DueDecision decision = DueCheck.Next(new DueState(Key, At(16, 11, 0)), Key, static after => after, false, Now, policy);

        Assert.Equal(new DueDecision.Run(new DueState(Key, Now), true), decision);
    }

    /// <summary>Row 3 beats row 4: a count at the limit does nothing until the due time comes.</summary>
    [Fact]
    public void Next_Row3BeatsRow4_FailedTriesAtLimitButNotDue_IsNotDue()
    {
        DueDecision decision = Recurring(new DueState(Key, At(17, 9, 0), 3), Now);

        Assert.Equal(new DueDecision.NotDue(), decision);
    }

    /// <summary>Row 4: a recurring timetable at the failure limit gives up this due time and keeps going.</summary>
    [Fact]
    public void Next_Row4_RecurringAtMaxFailedTries_GivesUpWithoutRemoving()
    {
        DueDecision decision = Recurring(new DueState(Key, At(16, 11, 59), 3, true), Now);

        Assert.Equal(new DueDecision.GiveUp(new DueState(Key, At(17, 9, 0), 0, true), false), decision);
    }

    /// <summary>Row 4: a one-off at the failure limit is given up and removed.</summary>
    [Fact]
    public void Next_Row4_OneOffAtMaxFailedTries_GivesUpAndRemoves()
    {
        DueDecision decision = OneOff(new DueState(Key, At(16, 11, 59), 3), At(16, 11, 59));

        Assert.Equal(new DueDecision.GiveUp(new DueState(Key, null, 0, false), true), decision);
    }

    /// <summary>Precedence 4 over 5: an on-time due time at the limit is given up, not run a fourth time.</summary>
    [Fact]
    public void Next_Row4BeatsRow5_OnTimeAtMaxFailedTries_GivesUp()
    {
        DueDecision decision = Recurring(new DueState(Key, At(16, 11, 59), 4), Now);

        Assert.Equal(new DueDecision.GiveUp(new DueState(Key, At(17, 9, 0), 0), false), decision);
    }

    /// <summary>Precedence 4 over 9: a late due time at the limit is given up, not run.</summary>
    [Fact]
    public void Next_Row4BeatsRow9_LateAtMaxFailedTries_GivesUp()
    {
        DueDecision decision = Recurring(new DueState(Key, At(15, 9, 0), 3), Now);

        Assert.Equal(new DueDecision.GiveUp(new DueState(Key, At(17, 9, 0), 0), false), decision);
    }

    /// <summary>A recurring timetable that failed its limit on one due time gives up, and at its next due time runs normally.</summary>
    [Fact]
    public void Next_RecurringGaveUp_NextDueTimeRunsOnTime()
    {
        DateTimeOffset firstTick = At(16, 9, 0, 30);
        DueDecision gaveUp = Recurring(new DueState(Key, At(16, 9, 0), 3, true), firstTick);
        DueDecision.GiveUp giveUp = Assert.IsType<DueDecision.GiveUp>(gaveUp);

        DueDecision next = Recurring(giveUp.Save, At(17, 9, 0, 30));

        Assert.Equal(new DueDecision.Run(new DueState(Key, At(18, 9, 0), 0, true), false), next);
    }

    /// <summary>Row 5: a run a minute late is on time, keeps its tries and ever-ran flag, and stores the following run.</summary>
    [Fact]
    public void Next_Row5_WithinGrace_RunsOnTime()
    {
        DueDecision decision = Recurring(new DueState(Key, At(16, 11, 59), 1, true), Now);

        Assert.Equal(new DueDecision.Run(new DueState(Key, At(17, 9, 0), 1, true), false), decision);
    }

    /// <summary>Row 5: a run exactly Grace late is still on time.</summary>
    [Fact]
    public void Next_Row5_ExactlyGraceLate_RunsOnTime()
    {
        DueDecision decision = Recurring(new DueState(Key, At(16, 11, 58)), Now);

        Assert.Equal(new DueDecision.Run(new DueState(Key, At(17, 9, 0)), false), decision);
    }

    /// <summary>Row 5: when the following run is itself already past, the run after now is stored instead.</summary>
    [Fact]
    public void Next_Row5_FollowingRunAlreadyPassed_StoresNextAfterNow()
    {
        DueDecision decision = DueCheck.Next(new DueState(Key, At(16, 11, 59)), Key, Every30, false, Now, Policy);

        Assert.Equal(new DueDecision.Run(new DueState(Key, At(16, 12, 30)), false), decision);
    }

    /// <summary>Row 5: a one-off that runs on time stores no next run.</summary>
    [Fact]
    public void Next_Row5_OneOffOnTime_StoresNoNextRun()
    {
        DueDecision decision = OneOff(new DueState(Key, At(16, 11, 59)), At(16, 11, 59));

        Assert.Equal(new DueDecision.Run(new DueState(Key, null), false), decision);
    }

    /// <summary>Row 6: with RunLate false, a late due time is skipped with a clean count.</summary>
    [Fact]
    public void Next_Row6_LateAndRunLateFalse_SkipsResettingTries()
    {
        DuePolicy policy = Policy with { RunLate = false };

        DueDecision decision = Recurring(new DueState(Key, At(15, 9, 0), 2, true), Now, policy);

        Assert.Equal(new DueDecision.Skip(new DueState(Key, At(17, 9, 0), 0, true)), decision);
    }

    /// <summary>Precedence 6 over 7: a one-off beyond MaxLateness with RunLate false is skipped, not given up.</summary>
    [Fact]
    public void Next_Row6BeatsRow7_OneOffBeyondMaxLatenessRunLateFalse_Skips()
    {
        DuePolicy policy = Policy with { RunLate = false };

        DueDecision decision = OneOff(new DueState(Key, At(14, 6, 0), 1), At(14, 6, 0), policy);

        Assert.Equal(new DueDecision.Skip(new DueState(Key, null, 0)), decision);
    }

    /// <summary>Precedence 6 over 8: a recurring late run inside the merge window with RunLate false is skipped on the same terms.</summary>
    [Fact]
    public void Next_Row6BeatsRow8_RecurringInsideMergeWindowRunLateFalse_Skips()
    {
        DuePolicy policy = Policy with { RunLate = false };

        DueDecision decision = DueCheck.Next(new DueState(Key, At(16, 11, 0), 2), Key, Every30, false, Now, policy);

        Assert.Equal(new DueDecision.Skip(new DueState(Key, At(16, 12, 30), 0)), decision);
    }

    /// <summary>Row 7: a one-off later than MaxLateness is given up and removed, with a clean count.</summary>
    [Fact]
    public void Next_Row7_OneOffBeyondMaxLateness_GivesUpAndRemoves()
    {
        DueDecision decision = OneOff(new DueState(Key, At(15, 6, 0), 2), At(15, 6, 0));

        Assert.Equal(new DueDecision.GiveUp(new DueState(Key, null, 0), true), decision);
    }

    /// <summary>Precedence 7 over 9: a one-off beyond MaxLateness is given up rather than run late.</summary>
    [Fact]
    public void Next_Row7BeatsRow9_OneOffBeyondMaxLateness_DoesNotRun()
    {
        DueDecision decision = OneOff(new DueState(Key, At(14, 12, 0), 1, true), At(14, 12, 0));

        Assert.Equal(new DueDecision.GiveUp(new DueState(Key, null, 0, true), true), decision);
    }

    /// <summary>Row 7: a one-off exactly MaxLateness late is not beyond it and runs late.</summary>
    [Fact]
    public void Next_Row7_OneOffExactlyMaxLateness_RunsLate()
    {
        DueDecision decision = OneOff(new DueState(Key, At(15, 12, 0)), At(15, 12, 0));

        Assert.Equal(new DueDecision.Run(new DueState(Key, null), true), decision);
    }

    /// <summary>Row 7: with no MaxLateness a one-off is never given up for lateness.</summary>
    [Fact]
    public void Next_Row7_NoMaxLateness_OneOffRunsLate()
    {
        DuePolicy policy = Policy with { MaxLateness = null };

        DueDecision decision = OneOff(new DueState(Key, At(1, 6, 0)), At(1, 6, 0), policy);

        Assert.Equal(new DueDecision.Run(new DueState(Key, null), true), decision);
    }

    /// <summary>Row 8: a late recurring run whose next regular run is within MergeWindow is skipped.</summary>
    [Fact]
    public void Next_Row8_NextRegularRunWithinMergeWindow_Skips()
    {
        DueDecision decision = Recurring(new DueState(Key, At(15, 9, 0), 1, true), At(16, 8, 30));

        Assert.Equal(new DueDecision.Skip(new DueState(Key, At(16, 9, 0), 0, true)), decision);
    }

    /// <summary>Row 8: a next regular run exactly MergeWindow away is still within it.</summary>
    [Fact]
    public void Next_Row8_NextRegularRunExactlyMergeWindowAway_Skips()
    {
        DueDecision decision = Recurring(new DueState(Key, At(15, 9, 0)), At(16, 8, 0));

        Assert.Equal(new DueDecision.Skip(new DueState(Key, At(16, 9, 0), 0)), decision);
    }

    /// <summary>Precedence 8 over 9: a late recurring run with its next run soon is skipped, not run.</summary>
    [Fact]
    public void Next_Row8BeatsRow9_LateRecurringWithNextRunSoon_Skips()
    {
        DueDecision decision = DueCheck.Next(new DueState(Key, At(16, 11, 0), 1), Key, Every30, false, Now, Policy);

        Assert.Equal(new DueDecision.Skip(new DueState(Key, At(16, 12, 30), 0)), decision);
    }

    /// <summary>A zero MergeWindow never skips a late recurring run.</summary>
    [Fact]
    public void Next_MergeWindowZero_NeverSkips()
    {
        DuePolicy policy = Policy with { MergeWindow = TimeSpan.Zero };

        DueDecision decision = Recurring(new DueState(Key, At(15, 9, 0), 1, true), At(16, 8, 30), policy);

        Assert.Equal(new DueDecision.Run(new DueState(Key, At(16, 9, 0), 1, true), true), decision);
    }

    /// <summary>Row 9: a late recurring run whose next run is far off runs once, late, keeping its tries.</summary>
    [Fact]
    public void Next_Row9_LateRecurring_RunsOnceLate()
    {
        DueDecision decision = Recurring(new DueState(Key, At(15, 9, 0), 2, true), Now);

        Assert.Equal(new DueDecision.Run(new DueState(Key, At(17, 9, 0), 2, true), true), decision);
    }

    /// <summary>Row 9: a late run just past Grace is late.</summary>
    [Fact]
    public void Next_Row9_JustPastGrace_RunsLate()
    {
        DueDecision decision = Recurring(new DueState(Key, At(16, 11, 57, 59)), Now);

        Assert.Equal(new DueDecision.Run(new DueState(Key, At(17, 9, 0)), true), decision);
    }

    /// <summary>Row 9: a one-off within MaxLateness runs late and stores no next run.</summary>
    [Fact]
    public void Next_Row9_LateOneOffWithinMaxLateness_RunsLate()
    {
        DueDecision decision = OneOff(new DueState(Key, At(16, 7, 0), 1), At(16, 7, 0));

        Assert.Equal(new DueDecision.Run(new DueState(Key, null, 1), true), decision);
    }

    /// <summary>A state with the default failure count never gives up, whatever the limit.</summary>
    [Fact]
    public void Next_DefaultFailedTries_NeverGivesUpOnTries()
    {
        DuePolicy policy = Policy with { MaxFailedTries = 1 };

        DueDecision decision = Recurring(new DueState(Key, At(16, 11, 59)), Now, policy);

        Assert.Equal(new DueDecision.Run(new DueState(Key, At(17, 9, 0)), false), decision);
    }

    /// <summary>After a run that ran, the failure count is cleared and ever-ran is set.</summary>
    [Fact]
    public void AfterRun_Ran_StoresSavedWithCleanCountAndEverRan()
    {
        DueState saved = new(Key, At(17, 9, 0), 2, false);
        DueState before = new(Key, At(16, 9, 0), 2, false);

        DueState stored = DueCheck.AfterRun(saved, before, DueRunResult.Ran);

        Assert.Equal(new DueState(Key, At(17, 9, 0), 0, true), stored);
    }

    /// <summary>After a held run, the state from before the run is stored unchanged.</summary>
    [Fact]
    public void AfterRun_Held_StoresBeforeRun()
    {
        DueState saved = new(Key, At(17, 9, 0), 0, true);
        DueState before = new(Key, At(16, 9, 0), 2, false);

        DueState stored = DueCheck.AfterRun(saved, before, DueRunResult.Held);

        Assert.Equal(new DueState(Key, At(16, 9, 0), 2, false), stored);
    }

    /// <summary>After a failed run, the state from before the run is stored with one more failed try.</summary>
    [Fact]
    public void AfterRun_Failed_StoresBeforeRunWithOneMoreTry()
    {
        DueState saved = new(Key, At(17, 9, 0), 0, true);
        DueState before = new(Key, At(16, 9, 0), 2, false);

        DueState stored = DueCheck.AfterRun(saved, before, DueRunResult.Failed);

        Assert.Equal(new DueState(Key, At(16, 9, 0), 3, false), stored);
    }
}

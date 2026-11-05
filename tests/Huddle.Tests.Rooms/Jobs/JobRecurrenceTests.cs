using Agency.Huddle.App.Jobs;

namespace Agency.Huddle.Tests.Jobs;

/// <summary>Pins the <c>when</c> grammar and the next-run rule of <see cref="JobRecurrence"/>.</summary>
public sealed class JobRecurrenceTests
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    /// <summary>Every preset's text reads back to the same recurrence and writes out as the same text.</summary>
    /// <param name="text">A <c>when</c> text in Huddle's own spelling.</param>
    [Theory]
    [InlineData("once on 2026-11-05 at 14:30")]
    [InlineData("daily at 09:00")]
    [InlineData("weekdays at 07:15")]
    [InlineData("weekly on mon,thu at 09:00")]
    [InlineData("monthly on 15 at 09:00")]
    [InlineData("monthly on 1 at 00:00")]
    [InlineData("monthly on 31 at 23:59")]
    [InlineData("every 4 hours")]
    public void TryParse_EachPreset_RoundTrips(string text)
    {
        Assert.True(JobRecurrence.TryParse(text, out JobRecurrence? recurrence, out _));

        Assert.Equal(text, recurrence.ToString());
    }

    /// <summary>Case, extra spaces, a one-digit hour, full day names and "hour" are all read.</summary>
    [Fact]
    public void TryParse_LooseSpelling_IsNormalised()
    {
        Assert.True(JobRecurrence.TryParse("  Weekly  on Monday, Thursday at 9:00 ", out JobRecurrence? weekly, out _));
        Assert.True(JobRecurrence.TryParse("every 1 hour", out JobRecurrence? hourly, out _));

        Assert.Equal("weekly on mon,thu at 09:00", weekly.ToString());
        Assert.Equal(JobRecurrence.EveryHours(1), hourly);
    }

    /// <summary>Each kind of bad text is refused with a reason that says how to fix it.</summary>
    /// <param name="text">The bad text.</param>
    /// <param name="expected">The reason.</param>
    [Theory]
    [InlineData("", "A Job needs a time: 'once on 2026-11-05 at 14:30', 'daily at 09:00', 'weekdays at 09:00', 'weekly on mon,thu at 09:00', 'monthly on 15 at 09:00' or 'every 4 hours'.")]
    [InlineData("monthly", "'monthly' is not a time Huddle can read. Use one of: 'once on 2026-11-05 at 14:30', 'daily at 09:00', 'weekdays at 09:00', 'weekly on mon,thu at 09:00', 'monthly on 15 at 09:00' or 'every 4 hours'.")]
    [InlineData("daily at 25:00", "'25:00' is not a time of day; write it as HH:mm, for example 09:00.")]
    [InlineData("once on 05/11/2026 at 09:00", "'05/11/2026' is not a date; write it as yyyy-MM-dd.")]
    [InlineData("weekly on mon,xyz at 09:00", "'xyz' is not a day; use mon, tue, wed, thu, fri, sat or sun.")]
    [InlineData("every 0 hours", "An interval must be 1 to 168 hours.")]
    [InlineData("every 169 hours", "An interval must be 1 to 168 hours.")]
    public void TryParse_BadText_IsRefusedWithAReason(string text, string expected)
    {
        Assert.False(JobRecurrence.TryParse(text, out _, out string? error));

        Assert.Equal(expected, error);
    }

    /// <summary>A daily Job runs later the same day, and a run at exactly the given instant is not "after" it.</summary>
    [Fact]
    public void NextAfter_Daily_IsTheNextWallClockTime()
    {
        JobRecurrence daily = JobRecurrence.Daily(new TimeOnly(9, 0));

        Assert.Equal(Utc(2026, 11, 2, 9, 0), daily.NextAfter(Utc(2026, 11, 2, 8, 0), TimeZoneInfo.Utc));
        Assert.Equal(Utc(2026, 11, 3, 9, 0), daily.NextAfter(Utc(2026, 11, 2, 9, 0), TimeZoneInfo.Utc));
    }

    /// <summary>A weekday Job after Friday's run skips the weekend.</summary>
    [Fact]
    public void NextAfter_Weekdays_SkipsTheWeekend()
    {
        JobRecurrence weekdays = JobRecurrence.Weekdays(new TimeOnly(9, 0));

        Assert.Equal(Utc(2026, 11, 9, 9, 0), weekdays.NextAfter(Utc(2026, 11, 6, 10, 0), TimeZoneInfo.Utc));
    }

    /// <summary>A Monday-and-Thursday Job seen on a Tuesday runs on Thursday, and after Thursday on the next Monday.</summary>
    [Fact]
    public void NextAfter_Weekly_RunsOnlyOnItsDays()
    {
        JobRecurrence weekly = JobRecurrence.Weekly(JobWeekdays.Monday | JobWeekdays.Thursday, new TimeOnly(9, 0));

        Assert.Equal(Utc(2026, 11, 5, 9, 0), weekly.NextAfter(Utc(2026, 11, 3, 12, 0), TimeZoneInfo.Utc));
        Assert.Equal(Utc(2026, 11, 9, 9, 0), weekly.NextAfter(Utc(2026, 11, 5, 9, 0), TimeZoneInfo.Utc));
    }

    /// <summary>A one-off runs at its time while that is ahead, and never once it has passed.</summary>
    [Fact]
    public void NextAfter_Once_IsNullOnceItsTimeHasPassed()
    {
        JobRecurrence once = JobRecurrence.Once(new DateOnly(2026, 11, 5), new TimeOnly(14, 30));

        Assert.Equal(Utc(2026, 11, 5, 14, 30), once.NextAfter(Utc(2026, 11, 1, 0, 0), TimeZoneInfo.Utc));
        Assert.Null(once.NextAfter(Utc(2026, 11, 5, 14, 30), TimeZoneInfo.Utc));
    }

    /// <summary>An interval Job counts from the instant it is given.</summary>
    [Fact]
    public void NextAfter_EveryHours_CountsFromTheGivenInstant()
    {
        Assert.Equal(Utc(2026, 11, 2, 12, 10), JobRecurrence.EveryHours(4).NextAfter(Utc(2026, 11, 2, 8, 10), Berlin));
    }

    /// <summary>A 09:00 Job in Berlin stays at 09:00 local across the spring change, so its UTC time moves an hour.</summary>
    [Fact]
    public void NextAfter_AcrossDaylightSaving_KeepsTheWallClockTime()
    {
        JobRecurrence daily = JobRecurrence.Daily(new TimeOnly(9, 0));

        Assert.Equal(Utc(2026, 3, 28, 8, 0), daily.NextAfter(Utc(2026, 3, 28, 0, 0), Berlin));
        Assert.Equal(Utc(2026, 3, 29, 7, 0), daily.NextAfter(Utc(2026, 3, 28, 8, 0), Berlin));
    }

    /// <summary>02:30 does not exist in Berlin on 29 March 2026 (02:00 jumps to 03:00), so the Job runs at 03:30 local.</summary>
    [Fact]
    public void NextAfter_SkippedLocalTime_RunsAsFarPastTheJump()
    {
        JobRecurrence daily = JobRecurrence.Daily(new TimeOnly(2, 30));

        Assert.Equal(Utc(2026, 3, 29, 1, 30), daily.NextAfter(Utc(2026, 3, 28, 12, 0), Berlin));
    }

    /// <summary>02:30 happens twice in Berlin on 25 October 2026; the Job runs at the first, still in summer time.</summary>
    [Fact]
    public void NextAfter_RepeatedLocalTime_RunsAtTheFirst()
    {
        JobRecurrence daily = JobRecurrence.Daily(new TimeOnly(2, 30));

        Assert.Equal(Utc(2026, 10, 25, 0, 30), daily.NextAfter(Utc(2026, 10, 24, 12, 0), Berlin));
    }

    /// <summary>The sentence the Scheduled view shows for each preset.</summary>
    [Fact]
    public void Describe_EachPreset_ReadsForTheHuman()
    {
        Assert.Equal("Once on 5 Nov 2026 at 14:30", JobRecurrence.Once(new DateOnly(2026, 11, 5), new TimeOnly(14, 30)).Describe());
        Assert.Equal("Daily at 09:00", JobRecurrence.Daily(new TimeOnly(9, 0)).Describe());
        Assert.Equal("Weekdays at 09:00", JobRecurrence.Weekdays(new TimeOnly(9, 0)).Describe());
        Assert.Equal("Mon, Thu at 09:00", JobRecurrence.Weekly(JobWeekdays.Monday | JobWeekdays.Thursday, new TimeOnly(9, 0)).Describe());
        Assert.Equal("Every hour", JobRecurrence.EveryHours(1).Describe());
        Assert.Equal("Every 4 hours", JobRecurrence.EveryHours(4).Describe());
    }

    /// <summary>Day 0, day 32 and a day that is not a number are refused with the allowed range.</summary>
    /// <param name="text">The bad text.</param>
    [Theory]
    [InlineData("monthly on 0 at 09:00")]
    [InlineData("monthly on 32 at 09:00")]
    [InlineData("monthly on 100 at 09:00")]
    public void TryParse_MonthlyDayOutOfRange_IsRefused(string text)
    {
        Assert.False(JobRecurrence.TryParse(text, out _, out string? error));

        Assert.Equal("A monthly Job runs on day 1 to 31.", error);
    }

    /// <summary>A monthly Job with no day is not a time Huddle can read, and the reason quotes the monthly form.</summary>
    [Fact]
    public void TryParse_MonthlyWithoutDay_IsRefused()
    {
        Assert.False(JobRecurrence.TryParse("monthly at 09:00", out _, out string? error));

        Assert.Equal(
            "'monthly at 09:00' is not a time Huddle can read. Use one of: 'once on 2026-11-05 at 14:30', 'daily at 09:00', 'weekdays at 09:00', 'weekly on mon,thu at 09:00', 'monthly on 15 at 09:00' or 'every 4 hours'.",
            error);
    }

    /// <summary>The monthly text reads into a Monthly recurrence holding the day and the time, ignoring case and spaces.</summary>
    [Fact]
    public void TryParse_Monthly_HoldsTheDayAndTime()
    {
        Assert.True(JobRecurrence.TryParse("  Monthly  on 15 at 9:00 ", out JobRecurrence? recurrence, out _));

        Assert.Equal(JobRecurrence.Monthly(15, new TimeOnly(9, 0)), recurrence);
        Assert.Equal(JobCadence.Monthly, recurrence.Cadence);
        Assert.Equal(15, recurrence.DayOfMonth);
    }

    /// <summary>Before the 15th a monthly Job runs this month; at the run and after it, next month.</summary>
    [Fact]
    public void NextAfter_Monthly_RunsThisMonthBeforeTheDayAndNextMonthAfter()
    {
        JobRecurrence monthly = JobRecurrence.Monthly(15, new TimeOnly(9, 0));

        Assert.Equal(Utc(2026, 11, 15, 9, 0), monthly.NextAfter(Utc(2026, 11, 10, 12, 0), TimeZoneInfo.Utc));
        Assert.Equal(Utc(2026, 11, 15, 9, 0), monthly.NextAfter(Utc(2026, 11, 15, 8, 59), TimeZoneInfo.Utc));
        Assert.Equal(Utc(2026, 12, 15, 9, 0), monthly.NextAfter(Utc(2026, 11, 15, 9, 0), TimeZoneInfo.Utc));
        Assert.Equal(Utc(2026, 12, 15, 9, 0), monthly.NextAfter(Utc(2026, 11, 20, 12, 0), TimeZoneInfo.Utc));
    }

    /// <summary>The 31st runs on the last day of a 30-day month and never skips it.</summary>
    [Fact]
    public void NextAfter_MonthlyOn31_RunsOnTheLastDayOfAShortMonth()
    {
        JobRecurrence monthly = JobRecurrence.Monthly(31, new TimeOnly(9, 0));

        Assert.Equal(Utc(2026, 4, 30, 9, 0), monthly.NextAfter(Utc(2026, 4, 1, 0, 0), TimeZoneInfo.Utc));
        Assert.Equal(Utc(2026, 5, 31, 9, 0), monthly.NextAfter(Utc(2026, 4, 30, 9, 0), TimeZoneInfo.Utc));
    }

    /// <summary>The 31st runs on 28 February in a common year, so January's run is followed by February's.</summary>
    [Fact]
    public void NextAfter_MonthlyOn31_RunsOn28FebruaryInACommonYear()
    {
        JobRecurrence monthly = JobRecurrence.Monthly(31, new TimeOnly(9, 0));

        Assert.Equal(Utc(2027, 2, 28, 9, 0), monthly.NextAfter(Utc(2027, 1, 31, 9, 0), TimeZoneInfo.Utc));
        Assert.Equal(Utc(2027, 3, 31, 9, 0), monthly.NextAfter(Utc(2027, 2, 28, 9, 0), TimeZoneInfo.Utc));
    }

    /// <summary>The 30th and the 31st run on 29 February in a leap year.</summary>
    [Fact]
    public void NextAfter_MonthlyOn30And31_RunOn29FebruaryInALeapYear()
    {
        Assert.Equal(Utc(2028, 2, 29, 9, 0), JobRecurrence.Monthly(31, new TimeOnly(9, 0)).NextAfter(Utc(2028, 2, 1, 0, 0), TimeZoneInfo.Utc));
        Assert.Equal(Utc(2028, 2, 29, 9, 0), JobRecurrence.Monthly(30, new TimeOnly(9, 0)).NextAfter(Utc(2028, 2, 1, 0, 0), TimeZoneInfo.Utc));
    }

    /// <summary>After this year's last run the next one is in January of the next year.</summary>
    [Fact]
    public void NextAfter_MonthlyAcrossTheYearEnd_RollsIntoJanuary()
    {
        Assert.Equal(Utc(2027, 1, 15, 9, 0), JobRecurrence.Monthly(15, new TimeOnly(9, 0)).NextAfter(Utc(2026, 12, 20, 0, 0), TimeZoneInfo.Utc));
        Assert.Equal(Utc(2027, 1, 31, 9, 0), JobRecurrence.Monthly(31, new TimeOnly(9, 0)).NextAfter(Utc(2026, 12, 31, 9, 0), TimeZoneInfo.Utc));
    }

    /// <summary>A 09:00 monthly Job in Berlin is 08:00 UTC before the spring change and 07:00 UTC after it.</summary>
    [Fact]
    public void NextAfter_MonthlyAcrossDaylightSaving_KeepsTheWallClockTime()
    {
        Assert.Equal(Utc(2026, 3, 15, 8, 0), JobRecurrence.Monthly(15, new TimeOnly(9, 0)).NextAfter(Utc(2026, 3, 1, 0, 0), Berlin));
        Assert.Equal(Utc(2026, 4, 15, 7, 0), JobRecurrence.Monthly(15, new TimeOnly(9, 0)).NextAfter(Utc(2026, 3, 15, 8, 0), Berlin));
    }

    /// <summary>A monthly Job set for 02:30 on the day of the spring jump runs at 03:30 local, as a daily Job does.</summary>
    [Fact]
    public void NextAfter_MonthlyOnTheJumpDay_RunsAsFarPastTheJump()
    {
        Assert.Equal(Utc(2026, 3, 29, 1, 30), JobRecurrence.Monthly(29, new TimeOnly(2, 30)).NextAfter(Utc(2026, 3, 1, 0, 0), Berlin));
    }

    /// <summary>The Scheduled view says "Monthly on day 15 at 09:00", and the day is clamped only in the run, not in the text.</summary>
    [Fact]
    public void Describe_Monthly_ReadsForTheHuman()
    {
        Assert.Equal("Monthly on day 15 at 09:00", JobRecurrence.Monthly(15, new TimeOnly(9, 0)).Describe());
        Assert.Equal("Monthly on day 31 at 18:30", JobRecurrence.Monthly(31, new TimeOnly(18, 30)).Describe());
    }

    private static DateTimeOffset Utc(int year, int month, int day, int hour, int minute) => new(year, month, day, hour, minute, 0, TimeSpan.Zero);
}

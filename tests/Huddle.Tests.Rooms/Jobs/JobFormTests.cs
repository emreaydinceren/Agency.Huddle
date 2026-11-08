using Agency.Huddle.App.Components.Jobs;
using Agency.Huddle.App.Jobs;
using Agency.Huddle.App.Tabs;
using Microsoft.Extensions.Time.Testing;

namespace Agency.Huddle.Tests.Jobs;

/// <summary>Pins the New job form's rules (<see cref="JobForm"/>) and the words the Scheduled view shows (<see cref="JobText"/>).</summary>
public sealed class JobFormTests
{
    private static readonly DateTimeOffset Monday0800 = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

    private static readonly FakeTimeProvider Clock = new(Monday0800);

    /// <summary>Each preset's fields become the same <c>when</c> text the file and the tool use.</summary>
    [Fact]
    public void WhenText_EachPreset_UsesTheSharedGrammar()
    {
        JobForm form = new() { Date = "2026-11-05", Time = "14:30", Hours = 6, DayOfMonth = 15 };
        form.Set(DayOfWeek.Monday, true);
        form.Set(DayOfWeek.Thursday, true);

        Assert.Equal(
            ["once on 2026-11-05 at 14:30", "daily at 14:30", "weekdays at 14:30", "weekly on mon,thu at 14:30", "monthly on 15 at 14:30", "every 6 hours"],
            Enum.GetValues<JobCadence>().Select(cadence =>
            {
                form.Cadence = cadence;
                return form.WhenText();
            }));
    }

    /// <summary>A browser's time input that hands back seconds is still read, as found in the live run of 2026-11-02.</summary>
    [Fact]
    public void TryBuild_TimeWithSeconds_IsRead()
    {
        JobForm form = new() { Cadence = JobCadence.Once, Date = "2026-11-02", Time = "16:48:00" };

        Assert.True(form.TryBuild(out JobRecurrence? when, out _));
        Assert.Equal(JobRecurrence.Once(new DateOnly(2026, 11, 2), new TimeOnly(16, 48)), when);
    }

    /// <summary>A Job's timetable fills the form and reads back unchanged.</summary>
    [Fact]
    public void From_ThenTryBuild_RoundTrips()
    {
        JobRecurrence weekly = JobRecurrence.Weekly(JobWeekdays.Tuesday | JobWeekdays.Saturday, new TimeOnly(7, 45));

        Assert.True(JobForm.From(weekly).TryBuild(out JobRecurrence? built, out _));

        Assert.Equal(weekly, built);
    }

    /// <summary>A weekly form with no day chosen says so, rather than the grammar's error.</summary>
    [Fact]
    public void TryBuild_WeeklyWithNoDay_AsksForADay()
    {
        JobForm form = new() { Cadence = JobCadence.Weekly };
        form.Set(DayOfWeek.Monday, false);

        Assert.False(form.TryBuild(out _, out string? error));
        Assert.Equal("Choose at least one day.", error);
    }

    /// <summary>A monthly Job's timetable fills the form (day and time) and reads back unchanged, including the 31st.</summary>
    [Fact]
    public void From_Monthly_RoundTripsThroughWhenText()
    {
        JobRecurrence monthly = JobRecurrence.Monthly(31, new TimeOnly(9, 30));

        JobForm form = JobForm.From(monthly);

        Assert.Equal(31, form.DayOfMonth);
        Assert.Equal("monthly on 31 at 09:30", form.WhenText());
        Assert.True(form.TryBuild(out JobRecurrence? built, out _));
        Assert.Equal(monthly, built);
    }

    /// <summary>A monthly form with no day (zero) or one past 31 is refused with the grammar's message.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(32)]
    public void TryBuild_MonthlyWithoutAValidDay_IsRefused(int day)
    {
        JobForm form = new() { Cadence = JobCadence.Monthly, DayOfMonth = day };

        Assert.False(form.TryBuild(out JobRecurrence? when, out string? error));
        Assert.Null(when);
        Assert.Equal("A monthly Job runs on day 1 to 31.", error);
    }

    /// <summary>A form for any other preset ignores its day of month, and a new form's day is not zero.</summary>
    [Fact]
    public void DayOfMonth_OtherPresets_AreUnaffected()
    {
        JobForm form = new() { Cadence = JobCadence.Daily, Time = "08:00" };

        Assert.InRange(form.DayOfMonth, 1, 31);
        Assert.Equal("daily at 08:00", form.WhenText());
        Assert.Equal(1, JobForm.From(JobRecurrence.Daily(new TimeOnly(8, 0))).DayOfMonth);
    }

    /// <summary>A new form starts on today's date in the Human's zone.</summary>
    [Fact]
    public void New_StartsOnTodayInTheHumansZone()
    {
        TimeZoneInfo tokyo = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");

        Assert.Equal("2026-11-03", JobForm.New(new DateTimeOffset(2026, 11, 2, 20, 0, 0, TimeSpan.Zero), tokyo).Date);
    }

    /// <summary>The preview lists the next runs in order, and fewer when a one-off has only one.</summary>
    [Fact]
    public void NextRuns_ListsTheNextRuns()
    {
        Assert.Equal(
            [Monday0800.AddHours(1), Monday0800.AddHours(25), Monday0800.AddHours(49)],
            JobForm.NextRuns(JobRecurrence.Daily(new TimeOnly(9, 0)), Monday0800, TimeZoneInfo.Utc, 3));
        Assert.Single(JobForm.NextRuns(JobRecurrence.Once(new DateOnly(2026, 11, 5), new TimeOnly(9, 0)), Monday0800, TimeZoneInfo.Utc, 3));
    }

    /// <summary>The next-run line says Paused, the time, or that the Job will not run again.</summary>
    [Fact]
    public void NextRun_SaysPausedTheTimeOrNever()
    {
        Job job = new("job-1", JobOwner.Human, "T", "I", JobRecurrence.Daily(new TimeOnly(9, 0)), null, null, false);

        Assert.Equal("Next: Mon 2 Nov, 09:00", JobText.NextRun(job, Monday0800.AddHours(1), TimeZoneInfo.Utc, Clock));
        Assert.Equal("Paused", JobText.NextRun(job with { Paused = true }, Monday0800, TimeZoneInfo.Utc, Clock));
        Assert.Equal("Will not run again", JobText.NextRun(job, null, TimeZoneInfo.Utc, Clock));
    }

    /// <summary>A run in another year than the clock's shows its year, in the next-run line, the last-run line and the preview.</summary>
    [Fact]
    public void Moment_OutsideTheCurrentYear_ShowsTheYear()
    {
        Job job = new("job-1", JobOwner.Human, "T", "I", JobRecurrence.Daily(new TimeOnly(9, 0)), null, null, false);
        DateTimeOffset far = new(2099, 12, 31, 9, 0, 0, TimeSpan.Zero);

        Assert.Equal("Thu 31 Dec 2099, 09:00", JobText.Moment(far, TimeZoneInfo.Utc, Clock));
        Assert.Equal("Next: Thu 31 Dec 2099, 09:00", JobText.NextRun(job, far, TimeZoneInfo.Utc, Clock));
        Assert.Equal("Last ran Wed 31 Dec 2025, 09:00", JobText.LastRun(new DateTimeOffset(2025, 12, 31, 9, 0, 0, TimeSpan.Zero), JobOutcome.Delivered, TimeZoneInfo.Utc, Clock));
        Assert.Equal("Thu 1 Jan, 09:00", JobText.Moment(new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc, Clock));
    }

    /// <summary>The year is the one in the shown zone: 23:30 UTC on 31 Dec is already next year in Tokyo.</summary>
    [Fact]
    public void Moment_ComparesTheYearInTheShownZone()
    {
        TimeZoneInfo tokyo = TimeZoneInfo.CreateCustomTimeZone("tokyo", TimeSpan.FromHours(9), "Tokyo", "Tokyo");
        DateTimeOffset at = new(2026, 12, 31, 23, 30, 0, TimeSpan.Zero);

        Assert.Equal("Fri 1 Jan 2027, 08:30", JobText.Moment(at, tokyo, Clock));
    }

    /// <summary>A list row adds Next only when the timetable does not already name the date.</summary>
    [Fact]
    public void RowNextRun_OnlyWhenItAddsSomething()
    {
        Job once = new("job-1", JobOwner.Human, "T", "I", JobRecurrence.Once(new DateOnly(2099, 12, 31), new TimeOnly(9, 0)), null, null, false);
        Job daily = once with { When = JobRecurrence.Daily(new TimeOnly(9, 0)) };
        DateTimeOffset next = Monday0800.AddHours(1);

        Assert.Null(JobText.RowNextRun(once, next, TimeZoneInfo.Utc, Clock));
        Assert.Equal("Next: Mon 2 Nov, 09:00", JobText.RowNextRun(daily, next, TimeZoneInfo.Utc, Clock));
        Assert.Equal("Paused", JobText.RowNextRun(once with { Paused = true }, next, TimeZoneInfo.Utc, Clock));
        Assert.Equal("Will not run again", JobText.RowNextRun(once, null, TimeZoneInfo.Utc, Clock));
    }

    /// <summary>The last-run line names why a run did not post.</summary>
    [Fact]
    public void LastRun_NamesWhyARunDidNotPost()
    {
        Assert.Null(JobText.LastRun(null, null, TimeZoneInfo.Utc, Clock));
        Assert.Equal("Last ran Mon 2 Nov, 08:00", JobText.LastRun(Monday0800, JobOutcome.Delivered, TimeZoneInfo.Utc, Clock));
        Assert.Equal("Last ran Mon 2 Nov, 08:00: not posted, the Room's Budget is spent", JobText.LastRun(Monday0800, JobOutcome.BudgetSpent, TimeZoneInfo.Utc, Clock));
        Assert.Equal("Last ran Mon 2 Nov, 08:00: not posted, the Agent has never come online", JobText.LastRun(Monday0800, JobOutcome.NoAgent, TimeZoneInfo.Utc, Clock));
    }

    /// <summary>The form's own answers: no title or instruction, a zone it does not know, and a one-off already past.</summary>
    [Fact]
    public void FormAnswers_AreWordedExactly()
    {
        Assert.Equal("Give the job a title and an instruction.", JobText.NeedsTitleAndInstruction);
        Assert.Equal("'Mars/Olympus' is not a time zone this machine knows; use an IANA name such as Europe/Berlin.", JobTimeZones.Unknown("Mars/Olympus"));
        Assert.Equal("Once on 1 Nov 2026 at 09:00 has no run after now.", JobText.NoRunAfterNow(JobRecurrence.Once(new DateOnly(2026, 11, 1), new TimeOnly(9, 0))));
    }

    /// <summary>A new Job stores the IANA name of a zone found by its Windows id, and an IANA zone as it is.</summary>
    [Fact]
    public void IdOf_PrefersTheIanaName()
    {
        Assert.Equal("America/Los_Angeles", JobTimeZones.IdOf(TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time")));
        Assert.Equal("Europe/Berlin", JobTimeZones.IdOf(TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin")));
    }

    /// <summary>The Run now snackbar for each outcome.</summary>
    [Fact]
    public void RanNow_NamesTheOutcome()
    {
        Assert.Equal("\"Ping\" ran.", JobText.RanNow("Ping", JobOutcome.Delivered));
        Assert.Equal("\"Ping\" did not run: its Room's Budget is spent.", JobText.RanNow("Ping", JobOutcome.BudgetSpent));
        Assert.Equal("\"Ping\" did not run: its Agent has never come online.", JobText.RanNow("Ping", JobOutcome.NoAgent));
        Assert.Equal("\"Ping\" no longer exists.", JobText.RanNow("Ping", null));
        Assert.Equal("\"Ping\" did not run.", JobText.RanNow("Ping", JobOutcome.Failed));
        Assert.Equal("Last ran Mon 2 Nov, 08:00: failed, trying again", JobText.LastRun(Monday0800, JobOutcome.Failed, TimeZoneInfo.Utc, Clock));
    }

    /// <summary>The Scheduled page and a Job's page each get their own Tab, with the Scheduled icon.</summary>
    [Fact]
    public void TabKeys_DescribeTheScheduledPages()
    {
        Assert.Equal(new TabDescriptor("scheduled", "Scheduled", "scheduled"), TabKeys.Describe("/scheduled"));
        Assert.Equal(new TabDescriptor("job:job-7f3a2c", "Job", "scheduled"), TabKeys.Describe("/scheduled/job-7f3a2c"));
    }
}

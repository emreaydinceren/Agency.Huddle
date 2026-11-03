using Agency.Huddle.App.Components.Jobs;
using Agency.Huddle.App.Jobs;
using Agency.Huddle.App.Tabs;

namespace Agency.Huddle.Tests.Jobs;

/// <summary>Pins the New job form's rules (<see cref="JobForm"/>) and the words the Scheduled view shows (<see cref="JobText"/>).</summary>
public sealed class JobFormTests
{
    private static readonly DateTimeOffset Monday0800 = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

    /// <summary>Each preset's fields become the same <c>when</c> text the file and the tool use.</summary>
    [Fact]
    public void WhenText_EachPreset_UsesTheSharedGrammar()
    {
        JobForm form = new() { Date = "2026-11-05", Time = "14:30", Hours = 6 };
        form.Set(DayOfWeek.Monday, true);
        form.Set(DayOfWeek.Thursday, true);

        Assert.Equal(
            ["once on 2026-11-05 at 14:30", "daily at 14:30", "weekdays at 14:30", "weekly on mon,thu at 14:30", "every 6 hours"],
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

        Assert.Equal("Next: Mon 2 Nov, 09:00", JobText.NextRun(job, Monday0800.AddHours(1), TimeZoneInfo.Utc));
        Assert.Equal("Paused", JobText.NextRun(job with { Paused = true }, Monday0800, TimeZoneInfo.Utc));
        Assert.Equal("Will not run again", JobText.NextRun(job, null, TimeZoneInfo.Utc));
    }

    /// <summary>The last-run line names why a run did not post.</summary>
    [Fact]
    public void LastRun_NamesWhyARunDidNotPost()
    {
        Assert.Null(JobText.LastRun(null, null, TimeZoneInfo.Utc));
        Assert.Equal("Last ran Mon 2 Nov, 08:00", JobText.LastRun(Monday0800, JobOutcome.Delivered, TimeZoneInfo.Utc));
        Assert.Equal("Last ran Mon 2 Nov, 08:00: not posted, the Room's Budget is spent", JobText.LastRun(Monday0800, JobOutcome.BudgetSpent, TimeZoneInfo.Utc));
        Assert.Equal("Last ran Mon 2 Nov, 08:00: not posted, the Agent has never come online", JobText.LastRun(Monday0800, JobOutcome.NoAgent, TimeZoneInfo.Utc));
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
        Assert.Equal("Last ran Mon 2 Nov, 08:00: failed, trying again", JobText.LastRun(Monday0800, JobOutcome.Failed, TimeZoneInfo.Utc));
    }

    /// <summary>The Scheduled page and a Job's page each get their own Tab, with the Scheduled icon.</summary>
    [Fact]
    public void TabKeys_DescribeTheScheduledPages()
    {
        Assert.Equal(new TabDescriptor("scheduled", "Scheduled", "scheduled"), TabKeys.Describe("/scheduled"));
        Assert.Equal(new TabDescriptor("job:job-7f3a2c", "Job", "scheduled"), TabKeys.Describe("/scheduled/job-7f3a2c"));
    }
}

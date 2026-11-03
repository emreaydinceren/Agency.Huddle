using System.Globalization;
using Agency.Huddle.App.Jobs;

namespace Agency.Huddle.App.Components.Jobs;

/// <summary>Every word the Scheduled view shows about a Job, in one place.</summary>
internal static class JobText
{
    /// <summary>The empty Scheduled page's title.</summary>
    public const string EmptyTitle = "No jobs scheduled";

    /// <summary>The empty Scheduled page's next step.</summary>
    public const string EmptyText = "A job posts an instruction on a timetable, in its own Room. Schedule one for yourself or an Agent with New job.";

    /// <summary>How an instant is shown: "Mon 3 Nov, 09:00".</summary>
    private const string MomentFormat = "ddd d MMM, HH:mm";

    /// <summary>The form's answer to Save with no title or no instruction.</summary>
    public const string NeedsTitleAndInstruction = "Give the job a title and an instruction.";

    /// <summary>The form's preview line for a timetable with nothing ahead of now: a one-off in the past.</summary>
    /// <param name="when">The timetable.</param>
    /// <returns>The line.</returns>
    public static string NoRunAfterNow(JobRecurrence when)
    {
        ArgumentNullException.ThrowIfNull(when);
        return $"{when.Describe()} has no run after now.";
    }

    /// <summary>Who a Job is for, by Name.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="humanName">The Human's current Name.</param>
    /// <returns>The Name.</returns>
    public static string OwnerName(JobOwner owner, string humanName)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return owner.AgentName ?? humanName;
    }

    /// <summary>When a Job runs next, in the Human's zone: "Next: Mon 3 Nov, 09:00", "Paused" or "Will not run again".</summary>
    /// <param name="job">The Job.</param>
    /// <param name="next">Its next run, or <see langword="null"/>.</param>
    /// <param name="zone">The zone to show times in.</param>
    /// <returns>The line.</returns>
    public static string NextRun(Job job, DateTimeOffset? next, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(zone);

        if (job.Paused)
        {
            return "Paused";
        }

        return next is { } at ? "Next: " + Moment(at, zone) : "Will not run again";
    }

    /// <summary>What the last run did: "Last ran Mon 2 Nov, 09:00", with why it did not post when it did not.</summary>
    /// <param name="lastRun">When it last ran, or <see langword="null"/>.</param>
    /// <param name="outcome">What that run did.</param>
    /// <param name="zone">The zone to show times in.</param>
    /// <returns>The line, or <see langword="null"/> when it never ran.</returns>
    public static string? LastRun(DateTimeOffset? lastRun, JobOutcome? outcome, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        if (lastRun is not { } at)
        {
            return null;
        }

        string when = "Last ran " + Moment(at, zone);
        return outcome switch
        {
            JobOutcome.BudgetSpent => when + ": not posted, the Room's Budget is spent",
            JobOutcome.NoAgent => when + ": not posted, the Agent has never come online",
            JobOutcome.Failed => when + ": failed, trying again",
            _ => when,
        };
    }

    /// <summary>The snackbar after Run now.</summary>
    /// <param name="title">The Job's title.</param>
    /// <param name="outcome">What the run did, or <see langword="null"/> when the Job was gone.</param>
    /// <returns>The message.</returns>
    public static string RanNow(string title, JobOutcome? outcome) => outcome switch
    {
        JobOutcome.Delivered => $"\"{title}\" ran.",
        JobOutcome.BudgetSpent => $"\"{title}\" did not run: its Room's Budget is spent.",
        JobOutcome.NoAgent => $"\"{title}\" did not run: its Agent has never come online.",
        null => $"\"{title}\" no longer exists.",
        _ => $"\"{title}\" did not run.",
    };

    /// <summary>An instant as the view shows it: "Mon 3 Nov, 09:00".</summary>
    /// <param name="at">The instant.</param>
    /// <param name="zone">The zone to show it in.</param>
    /// <returns>The text.</returns>
    public static string Moment(DateTimeOffset at, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        return TimeZoneInfo.ConvertTime(at, zone).ToString(MomentFormat, CultureInfo.InvariantCulture);
    }
}

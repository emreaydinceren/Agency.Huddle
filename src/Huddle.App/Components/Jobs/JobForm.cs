using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Agency.Huddle.App.Jobs;

namespace Agency.Huddle.App.Components.Jobs;

/// <summary>
/// The New job and Edit job form's fields, kept apart from the dialog so its rules are tested without rendering. The preset
/// and its inputs are turned into a <c>when</c> text and read back through <see cref="JobRecurrence.TryParse"/>, so the form,
/// <c>schedule.md</c> and the <c>jobs</c> tool share one grammar and one set of error messages.
/// </summary>
internal sealed class JobForm
{
    /// <summary>The days in the order the form shows them.</summary>
    public static IReadOnlyList<DayOfWeek> Week { get; } =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];

    /// <summary>The preset.</summary>
    public JobCadence Cadence { get; set; } = JobCadence.Weekdays;

    /// <summary>The date of a one-off, as <c>yyyy-MM-dd</c> (the browser's date input).</summary>
    public string Date { get; set; } = string.Empty;

    /// <summary>The time of day, as <c>HH:mm</c> (the browser's time input).</summary>
    public string Time { get; set; } = "09:00";

    /// <summary>The days of a weekly Job.</summary>
    public JobWeekdays Days { get; set; } = JobWeekdays.Monday;

    /// <summary>The interval of an every-so-many-hours Job.</summary>
    public int Hours { get; set; } = 4;

    /// <summary>A form filled from a Job's timetable.</summary>
    /// <param name="when">The timetable.</param>
    /// <returns>The form.</returns>
    public static JobForm From(JobRecurrence when)
    {
        ArgumentNullException.ThrowIfNull(when);
        return new JobForm
        {
            Cadence = when.Cadence,
            Date = when.On?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
            Time = when.At.ToString("HH:mm", CultureInfo.InvariantCulture),
            Days = when.Days == JobWeekdays.None ? JobWeekdays.Monday : when.Days,
            Hours = when.Hours > 0 ? when.Hours : 4,
        };
    }

    /// <summary>A new one-off's starting date: today in the Human's zone.</summary>
    /// <param name="now">The current instant.</param>
    /// <param name="zone">The Human's zone.</param>
    /// <returns>The form, with today as its date.</returns>
    public static JobForm New(DateTimeOffset now, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        return new JobForm { Date = TimeZoneInfo.ConvertTime(now, zone).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) };
    }

    /// <summary>A day's three-letter name, "Mon", whatever the machine's culture.</summary>
    /// <param name="day">The day.</param>
    /// <returns>The name.</returns>
    public static string ShortName(DayOfWeek day) => CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedDayName(day);

    /// <summary>Whether a day is chosen.</summary>
    /// <param name="day">The day.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    public bool Has(DayOfWeek day) => (this.Days & JobRecurrence.FlagOf(day)) != 0;

    /// <summary>Chooses or clears a day.</summary>
    /// <param name="day">The day.</param>
    /// <param name="chosen">Whether it is chosen.</param>
    public void Set(DayOfWeek day, bool chosen) =>
        this.Days = chosen ? this.Days | JobRecurrence.FlagOf(day) : this.Days & ~JobRecurrence.FlagOf(day);

    /// <summary>The <c>when</c> text the fields stand for.</summary>
    /// <returns>The text.</returns>
    public string WhenText()
    {
        string at = TimeOfDay(this.Time);
        string days = string.Join(',', Week.Where(this.Has).Select(day => ShortName(day).ToLowerInvariant()));
        return this.Cadence switch
        {
            JobCadence.Once => $"once on {this.Date} at {at}",
            JobCadence.Daily => $"daily at {at}",
            JobCadence.Weekdays => $"weekdays at {at}",
            JobCadence.Weekly => $"weekly on {days} at {at}",
            _ => string.Create(CultureInfo.InvariantCulture, $"every {this.Hours} hours"),
        };
    }

    /// <summary>
    /// The browser's time input as <c>HH:mm</c>. A browser may hand back seconds (<c>16:48:00</c>), which the <c>when</c>
    /// grammar does not take; anything else is passed through for the grammar to refuse with its own message.
    /// </summary>
    /// <param name="value">The input's value.</param>
    /// <returns>The time of day.</returns>
    private static string TimeOfDay(string value) =>
        TimeOnly.TryParseExact(value.Trim(), ["H:mm", "H:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly time)
            ? time.ToString("HH:mm", CultureInfo.InvariantCulture)
            : value;

    /// <summary>Reads the fields as a timetable.</summary>
    /// <param name="when">The timetable, when the fields are valid.</param>
    /// <param name="error">What to fix, when they are not.</param>
    /// <returns><see langword="true"/> when the fields are valid.</returns>
    public bool TryBuild([NotNullWhen(true)] out JobRecurrence? when, [NotNullWhen(false)] out string? error)
    {
        if (this.Cadence == JobCadence.Weekly && this.Days == JobWeekdays.None)
        {
            when = null;
            error = "Choose at least one day.";
            return false;
        }

        return JobRecurrence.TryParse(this.WhenText(), out when, out error);
    }

    /// <summary>The next runs, for the form's preview.</summary>
    /// <param name="when">The timetable.</param>
    /// <param name="now">The current instant.</param>
    /// <param name="zone">The Job's zone.</param>
    /// <param name="count">How many to list.</param>
    /// <returns>Up to <paramref name="count"/> runs, earliest first.</returns>
    public static IReadOnlyList<DateTimeOffset> NextRuns(JobRecurrence when, DateTimeOffset now, TimeZoneInfo zone, int count)
    {
        ArgumentNullException.ThrowIfNull(when);
        ArgumentNullException.ThrowIfNull(zone);

        List<DateTimeOffset> runs = [];
        DateTimeOffset after = now;
        while (runs.Count < count && when.NextAfter(after, zone) is { } next)
        {
            runs.Add(next);
            after = next;
        }

        return runs;
    }
}

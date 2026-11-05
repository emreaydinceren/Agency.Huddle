using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Agency.Huddle.App.Jobs;

/// <summary>
/// When a Job runs, and the pure rule for its next run. A time of day is a wall-clock time in the Job's time zone, so a
/// 09:00 Job stays at 09:00 across a daylight-saving change. The text form is the <c>when:</c> line of <c>schedule.md</c>
/// and the <c>when</c> argument of the <c>jobs</c> tool, so both read one grammar:
/// <c>once on 2026-11-05 at 14:30</c>, <c>daily at 09:00</c>, <c>weekdays at 09:00</c>,
/// <c>weekly on mon,thu at 09:00</c>, <c>monthly on 15 at 09:00</c> and <c>every 4 hours</c>.
/// </summary>
/// <param name="Cadence">The preset.</param>
/// <param name="At">The time of day; unused by <see cref="JobCadence.EveryHours"/>.</param>
/// <param name="Days">The days of a <see cref="JobCadence.Weekly"/> Job; <see cref="JobWeekdays.None"/> otherwise.</param>
/// <param name="Hours">The interval of an <see cref="JobCadence.EveryHours"/> Job; 0 otherwise.</param>
/// <param name="On">The date of a <see cref="JobCadence.Once"/> Job; <see langword="null"/> otherwise.</param>
/// <param name="DayOfMonth">The day of a <see cref="JobCadence.Monthly"/> Job, 1 to 31; 0 otherwise. A day past the month's end runs on its last day.</param>
internal sealed partial record JobRecurrence(JobCadence Cadence, TimeOnly At, JobWeekdays Days, int Hours, DateOnly? On, int DayOfMonth = 0)
{
    /// <summary>The longest interval an <see cref="JobCadence.EveryHours"/> Job may have: one week.</summary>
    public const int MaxHours = 168;

    /// <summary>The last day a <see cref="JobCadence.Monthly"/> Job may name.</summary>
    public const int MaxDayOfMonth = 31;

    private static readonly (string Short, DayOfWeek Day, JobWeekdays Flag)[] DayNames =
    [
        ("mon", DayOfWeek.Monday, JobWeekdays.Monday),
        ("tue", DayOfWeek.Tuesday, JobWeekdays.Tuesday),
        ("wed", DayOfWeek.Wednesday, JobWeekdays.Wednesday),
        ("thu", DayOfWeek.Thursday, JobWeekdays.Thursday),
        ("fri", DayOfWeek.Friday, JobWeekdays.Friday),
        ("sat", DayOfWeek.Saturday, JobWeekdays.Saturday),
        ("sun", DayOfWeek.Sunday, JobWeekdays.Sunday),
    ];

    /// <summary>A one-off Job.</summary>
    /// <param name="on">The date.</param>
    /// <param name="at">The time of day.</param>
    /// <returns>The recurrence.</returns>
    public static JobRecurrence Once(DateOnly on, TimeOnly at) => new(JobCadence.Once, at, JobWeekdays.None, 0, on);

    /// <summary>A daily Job.</summary>
    /// <param name="at">The time of day.</param>
    /// <returns>The recurrence.</returns>
    public static JobRecurrence Daily(TimeOnly at) => new(JobCadence.Daily, at, JobWeekdays.None, 0, null);

    /// <summary>A Monday-to-Friday Job.</summary>
    /// <param name="at">The time of day.</param>
    /// <returns>The recurrence.</returns>
    public static JobRecurrence Weekdays(TimeOnly at) => new(JobCadence.Weekdays, at, JobWeekdays.None, 0, null);

    /// <summary>A Job on chosen days of the week.</summary>
    /// <param name="days">The days; at least one.</param>
    /// <param name="at">The time of day.</param>
    /// <returns>The recurrence.</returns>
    public static JobRecurrence Weekly(JobWeekdays days, TimeOnly at) => new(JobCadence.Weekly, at, days, 0, null);

    /// <summary>A Job on one day of every month; a day past the month's end runs on its last day.</summary>
    /// <param name="dayOfMonth">The day, 1 to <see cref="MaxDayOfMonth"/>.</param>
    /// <param name="at">The time of day.</param>
    /// <returns>The recurrence.</returns>
    public static JobRecurrence Monthly(int dayOfMonth, TimeOnly at) => new(JobCadence.Monthly, at, JobWeekdays.None, 0, null, dayOfMonth);

    /// <summary>A Job every so many hours.</summary>
    /// <param name="hours">The interval, 1 to <see cref="MaxHours"/>.</param>
    /// <returns>The recurrence.</returns>
    public static JobRecurrence EveryHours(int hours) => new(JobCadence.EveryHours, TimeOnly.MinValue, JobWeekdays.None, hours, null);

    /// <summary>Reads a <c>when</c> text, ignoring case and extra spaces.</summary>
    /// <param name="text">The text, for example <c>weekly on mon,thu at 09:00</c>.</param>
    /// <param name="recurrence">The recurrence, when the text is valid.</param>
    /// <param name="error">Why the text is not valid, worded for the Human or the model that wrote it.</param>
    /// <returns><see langword="true"/> when the text is valid.</returns>
    public static bool TryParse(string? text, [NotNullWhen(true)] out JobRecurrence? recurrence, [NotNullWhen(false)] out string? error)
    {
        recurrence = null;
        string normalized = string.Join(' ', (text ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).ToLowerInvariant();
        if (normalized.Length == 0)
        {
            error = "A Job needs a time: " + Grammar;
            return false;
        }

        Match match = WhenPattern().Match(normalized);
        if (!match.Success)
        {
            error = $"'{text}' is not a time Huddle can read. Use one of: {Grammar}";
            return false;
        }

        if (match.Groups["hours"].Success)
        {
            int hours = int.Parse(match.Groups["hours"].Value, CultureInfo.InvariantCulture);
            if (hours is < 1 or > MaxHours)
            {
                error = $"An interval must be 1 to {MaxHours} hours.";
                return false;
            }

            recurrence = EveryHours(hours);
            error = null;
            return true;
        }

        if (!TimeOnly.TryParseExact(match.Groups["at"].Value, "H:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly at))
        {
            error = $"'{match.Groups["at"].Value}' is not a time of day; write it as HH:mm, for example 09:00.";
            return false;
        }

        return TryBuild(match, at, out recurrence, out error);
    }

    /// <summary>The <c>when</c> text, the inverse of <see cref="TryParse"/>.</summary>
    /// <returns>The text.</returns>
    public override string ToString()
    {
        string at = this.At.ToString("HH:mm", CultureInfo.InvariantCulture);
        string on = this.On?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;
        string days = string.Join(',', DayNames.Where(day => (this.Days & day.Flag) != 0).Select(day => day.Short));
        return this.Cadence switch
        {
            JobCadence.Once => $"once on {on} at {at}",
            JobCadence.Daily => $"daily at {at}",
            JobCadence.Weekdays => $"weekdays at {at}",
            JobCadence.Weekly => $"weekly on {days} at {at}",
            JobCadence.Monthly => string.Create(CultureInfo.InvariantCulture, $"monthly on {this.DayOfMonth} at {at}"),
            _ => string.Create(CultureInfo.InvariantCulture, $"every {this.Hours} hours"),
        };
    }

    /// <summary>
    /// The first run strictly after <paramref name="after"/>, or <see langword="null"/> when there is none: a one-off whose
    /// time has passed. An <see cref="JobCadence.EveryHours"/> Job counts from <paramref name="after"/>, so the caller passes
    /// the run that just fired. A local time skipped by a daylight-saving jump runs as many minutes after the jump as it was
    /// set after the jump's start (02:30 on a 02:00-to-03:00 night runs at 03:30); a local time that happens twice runs at the
    /// first.
    /// </summary>
    /// <param name="after">The instant to look after.</param>
    /// <param name="zone">The Job's time zone.</param>
    /// <returns>The next run, in UTC, or <see langword="null"/>.</returns>
    public DateTimeOffset? NextAfter(DateTimeOffset after, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        if (this.Cadence == JobCadence.EveryHours)
        {
            return after.ToUniversalTime().AddHours(this.Hours);
        }

        if (this.Cadence == JobCadence.Once)
        {
            DateTimeOffset once = ToInstant(this.On ?? DateOnly.MinValue, this.At, zone);
            return once > after ? once : null;
        }

        DateOnly start = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(after, zone).DateTime);
        if (this.Cadence == JobCadence.Monthly)
        {
            return this.NextMonthly(start, after, zone);
        }

        for (int offset = 0; offset <= 8; offset++)
        {
            DateOnly day = start.AddDays(offset);
            if (!this.RunsOn(day.DayOfWeek))
            {
                continue;
            }

            DateTimeOffset candidate = ToInstant(day, this.At, zone);
            if (candidate > after)
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>A sentence for the Human, for example "Weekdays at 09:00" or "Every 4 hours".</summary>
    /// <returns>The summary.</returns>
    public string Describe()
    {
        string at = this.At.ToString("HH:mm", CultureInfo.InvariantCulture);
        string on = this.On?.ToString("d MMM yyyy", CultureInfo.InvariantCulture) ?? string.Empty;
        string days = string.Join(", ", DayNames.Where(day => (this.Days & day.Flag) != 0).Select(day => CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedDayName(day.Day)));
        return this.Cadence switch
        {
            JobCadence.Once => $"Once on {on} at {at}",
            JobCadence.Daily => $"Daily at {at}",
            JobCadence.Weekdays => $"Weekdays at {at}",
            JobCadence.Weekly => $"{days} at {at}",
            JobCadence.Monthly => string.Create(CultureInfo.InvariantCulture, $"Monthly on day {this.DayOfMonth} at {at}"),
            _ => this.Hours == 1 ? "Every hour" : string.Create(CultureInfo.InvariantCulture, $"Every {this.Hours} hours"),
        };
    }

    /// <summary>The flag for one day of the week.</summary>
    /// <param name="day">The day.</param>
    /// <returns>Its flag.</returns>
    public static JobWeekdays FlagOf(DayOfWeek day) => DayNames.First(entry => entry.Day == day).Flag;

    /// <summary>The grammar, quoted in every parse error so a model can correct itself in one Turn.</summary>
    internal const string Grammar = "'once on 2026-11-05 at 14:30', 'daily at 09:00', 'weekdays at 09:00', 'weekly on mon,thu at 09:00', 'monthly on 15 at 09:00' or 'every 4 hours'.";

    private static bool TryBuild(Match match, TimeOnly at, [NotNullWhen(true)] out JobRecurrence? recurrence, [NotNullWhen(false)] out string? error)
    {
        recurrence = null;
        error = null;
        switch (match.Groups["kind"].Value)
        {
            case "daily":
                recurrence = Daily(at);
                return true;
            case "weekdays":
                recurrence = Weekdays(at);
                return true;
            case "monthly":
                int day = int.Parse(match.Groups["day"].Value, CultureInfo.InvariantCulture);
                if (day is < 1 or > MaxDayOfMonth)
                {
                    error = $"A monthly Job runs on day 1 to {MaxDayOfMonth}.";
                    return false;
                }

                recurrence = Monthly(day, at);
                return true;
            case "once":
                if (!DateOnly.TryParseExact(match.Groups["on"].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly on))
                {
                    error = $"'{match.Groups["on"].Value}' is not a date; write it as yyyy-MM-dd.";
                    return false;
                }

                recurrence = Once(on, at);
                return true;
            default:
                return TryBuildWeekly(match.Groups["days"].Value, at, out recurrence, out error);
        }
    }

    private static bool TryBuildWeekly(string days, TimeOnly at, [NotNullWhen(true)] out JobRecurrence? recurrence, [NotNullWhen(false)] out string? error)
    {
        recurrence = null;
        JobWeekdays flags = JobWeekdays.None;
        foreach (string name in days.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int index = Array.FindIndex(DayNames, day => name.StartsWith(day.Short, StringComparison.Ordinal));
            if (index < 0)
            {
                error = $"'{name}' is not a day; use mon, tue, wed, thu, fri, sat or sun.";
                return false;
            }

            flags |= DayNames[index].Flag;
        }

        if (flags == JobWeekdays.None)
        {
            error = "A weekly Job needs at least one day.";
            return false;
        }

        recurrence = Weekly(flags, at);
        error = null;
        return true;
    }

    /// <summary>This month's day, clamped to the month's length, when still ahead; otherwise next month's.</summary>
    private DateTimeOffset NextMonthly(DateOnly start, DateTimeOffset after, TimeZoneInfo zone)
    {
        DateOnly month = new(start.Year, start.Month, 1);
        DateTimeOffset candidate = ToInstant(this.DayIn(month), this.At, zone);
        if (candidate > after)
        {
            return candidate;
        }

        return ToInstant(this.DayIn(month.AddMonths(1)), this.At, zone);
    }

    private DateOnly DayIn(DateOnly month) => new(month.Year, month.Month, Math.Min(this.DayOfMonth, DateTime.DaysInMonth(month.Year, month.Month)));

    private bool RunsOn(DayOfWeek day) => this.Cadence switch
    {
        JobCadence.Daily => true,
        JobCadence.Weekdays => day is not (DayOfWeek.Saturday or DayOfWeek.Sunday),
        _ => (this.Days & FlagOf(day)) != 0,
    };

    /// <summary>The instant a wall-clock time in <paramref name="zone"/> names, following the daylight-saving rule above.</summary>
    private static DateTimeOffset ToInstant(DateOnly day, TimeOnly at, TimeZoneInfo zone)
    {
        DateTime local = day.ToDateTime(at, DateTimeKind.Unspecified);
        TimeSpan offset;
        if (zone.IsInvalidTime(local))
        {
            // Skipped by a jump forward: keep the offset in force before the jump, which lands as far past the jump as the time was past its start.
            offset = zone.GetUtcOffset(local.AddHours(-3));
        }
        else if (zone.IsAmbiguousTime(local))
        {
            // Happens twice: the larger offset is the earlier instant, so the first occurrence.
            offset = zone.GetAmbiguousTimeOffsets(local).Max();
        }
        else
        {
            offset = zone.GetUtcOffset(local);
        }

        return new DateTimeOffset(local, offset).ToUniversalTime();
    }

    [GeneratedRegex(
        @"\A(?:(?<kind>once) on (?<on>\S+) at (?<at>\S+)|(?<kind>daily|weekdays) at (?<at>\S+)|(?<kind>weekly) on (?<days>[a-z, ]+?) at (?<at>\S+)|(?<kind>monthly) on (?<day>\d{1,3}) at (?<at>\S+)|every (?<hours>\d{1,3}) hours?)\z",
        RegexOptions.CultureInvariant)]
    private static partial Regex WhenPattern();
}

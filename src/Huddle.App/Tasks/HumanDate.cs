namespace Agency.Huddle.App.Tasks;

using System.Globalization;

/// <summary>Formats a date the way a Human says it ("Today", "Fri", "Oct 27"), for every Tasks surface that shows a date.</summary>
internal static class HumanDate
{
    /// <summary>
    /// The short human form of <paramref name="date"/> relative to <paramref name="today"/>: "Today", "Tomorrow" or "Yesterday";
    /// the abbreviated weekday for the two to six days ahead; <c>MMM d</c> for any other date in today's year; <c>MMM d, yyyy</c>
    /// for another year. Always the invariant culture.
    /// </summary>
    /// <param name="date">The date to show.</param>
    /// <param name="today">The local date that counts as today.</param>
    public static string Format(DateOnly date, DateOnly today)
    {
        int offset = date.DayNumber - today.DayNumber;
        return offset switch
        {
            0 => "Today",
            1 => "Tomorrow",
            -1 => "Yesterday",
            >= 2 and <= 6 => date.ToString("ddd", CultureInfo.InvariantCulture),
            _ => date.Year == today.Year
                ? date.ToString("MMM d", CultureInfo.InvariantCulture)
                : date.ToString("MMM d, yyyy", CultureInfo.InvariantCulture),
        };
    }

    /// <summary>
    /// The form for a date in a table column (design-system "Dates and times"): the same as <see cref="Format(DateOnly, DateOnly)"/>,
    /// except that a date two to six days ahead reads <c>MMM d</c> and never a weekday, so a column does not read
    /// "Tue, Wed, Mon, Thu". "Today", "Tomorrow" and "Yesterday" stay, and another year keeps its year.
    /// </summary>
    /// <param name="date">The date to show.</param>
    /// <param name="today">The local date that counts as today.</param>
    public static string FormatColumn(DateOnly date, DateOnly today)
    {
        int offset = date.DayNumber - today.DayNumber;
        return offset is >= 2 and <= 6
            ? date.ToString("MMM d", CultureInfo.InvariantCulture)
            : Format(date, today);
    }

    /// <summary>
    /// The form for a moment that has already happened, as the activity log says it: the same as
    /// <see cref="Format(DateOnly, DateOnly)"/>, except that a date two to six days back reads as its abbreviated weekday ("Mon"),
    /// where <see cref="Format(DateOnly, DateOnly)"/> reads weekdays only for days ahead.
    /// </summary>
    /// <param name="date">The date to show.</param>
    /// <param name="today">The local date that counts as today.</param>
    public static string FormatPast(DateOnly date, DateOnly today) =>
        (date.DayNumber - today.DayNumber) is >= -6 and <= -2
            ? date.ToString("ddd", CultureInfo.InvariantCulture)
            : Format(date, today);

    /// <summary>The 24-hour clock every screen shows after a day word ("08:30"), the form the transcript, the Needs You card and the Settings pages already use.</summary>
    public const string ClockFormat = "HH:mm";

    /// <summary>
    /// The form of <paramref name="humanDate"/> for the middle of a phrase ("Archived today", "Created yesterday 08:30"): the relative words
    /// Today, Tomorrow and Yesterday lose their capital, since a capital belongs only to the start of a phrase. A weekday or a date is
    /// unchanged, because it is a name.
    /// </summary>
    /// <param name="humanDate">A string that starts with a result of <see cref="Format(DateOnly, DateOnly)"/> or <see cref="FormatPast"/>.</param>
    public static string InPhrase(string humanDate)
    {
        ArgumentNullException.ThrowIfNull(humanDate);
        return humanDate.StartsWith("Today", StringComparison.Ordinal)
            || humanDate.StartsWith("Tomorrow", StringComparison.Ordinal)
            || humanDate.StartsWith("Yesterday", StringComparison.Ordinal)
            ? string.Concat(char.ToLowerInvariant(humanDate[0]).ToString(), humanDate.AsSpan(1))
            : humanDate;
    }

    /// <summary>
    /// The day and the 24-hour time of <paramref name="at"/> in <paramref name="zone"/>, such as <c>Today 08:30</c>, <c>Mon 16:20</c> or
    /// <c>Oct 3 09:05</c>: the day is <see cref="FormatPast"/>, the time is <see cref="ClockFormat"/>.
    /// </summary>
    /// <param name="at">The instant to show.</param>
    /// <param name="today">The local date that counts as today.</param>
    /// <param name="zone">The zone whose wall clock is shown.</param>
    public static string Moment(DateTimeOffset at, DateOnly today, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        DateTime local = TimeZoneInfo.ConvertTime(at, zone).DateTime;
        return string.Join(' ', FormatPast(DateOnly.FromDateTime(local), today), local.ToString(ClockFormat, CultureInfo.InvariantCulture));
    }

    /// <summary>The full form for a tooltip, such as <c>Tue, Oct 27, 2026</c>.</summary>
    /// <param name="date">The date to show.</param>
    public static string Full(DateOnly date) => date.ToString("ddd, MMM d, yyyy", CultureInfo.InvariantCulture);
}

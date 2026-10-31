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

    /// <summary>The full form for a tooltip, such as <c>Tue, Oct 27, 2026</c>.</summary>
    /// <param name="date">The date to show.</param>
    public static string Full(DateOnly date) => date.ToString("ddd, MMM d, yyyy", CultureInfo.InvariantCulture);
}

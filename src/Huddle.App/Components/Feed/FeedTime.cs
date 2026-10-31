using System.Globalization;

namespace Agency.Huddle.App.Components.Feed;

/// <summary>Ages and day headings for the Feed, from the injected clock and the Human's own time zone (Feed spec §6.12, §6.13).</summary>
internal static class FeedTime
{
    /// <summary>The compact age on a row: <c>now</c> under a minute, then <c>19m</c>, <c>1h</c>, <c>3d</c>.</summary>
    /// <param name="now">The current time.</param>
    /// <param name="at">When the item happened.</param>
    /// <returns>The age.</returns>
    public static string Age(DateTimeOffset now, DateTimeOffset at) => Spell(now - at, "m", "h", "d");

    /// <summary>The spelled age on a card: <c>now</c>, <c>16 min</c>, <c>2 h</c>, <c>1 d</c>.</summary>
    /// <param name="now">The current time.</param>
    /// <param name="at">When the item happened.</param>
    /// <returns>The age.</returns>
    public static string CardAge(DateTimeOffset now, DateTimeOffset at) => Spell(now - at, " min", " h", " d");

    /// <summary>The heading for the day <paramref name="at"/> falls on, by the Human's local days.</summary>
    /// <param name="now">The current time.</param>
    /// <param name="at">When the item happened.</param>
    /// <param name="zone">The Human's time zone.</param>
    /// <returns>The heading.</returns>
    public static FeedDayHeading DayHeading(DateTimeOffset now, DateTimeOffset at, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        DateOnly today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        DateOnly day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, zone).DateTime);
        int daysAgo = today.DayNumber - day.DayNumber;
        string monthDay = day.ToString("MMM d", CultureInfo.InvariantCulture);
        return daysAgo switch
        {
            0 => new("Today", day.ToString("ddd, MMM d", CultureInfo.InvariantCulture)),
            1 => new("Yesterday", day.ToString("ddd, MMM d", CultureInfo.InvariantCulture)),
            > 1 and < 7 => new(day.ToString("dddd", CultureInfo.InvariantCulture), monthDay),
            _ => new(monthDay, null),
        };
    }

    /// <summary>The local day an instant falls on, used to keep a group of rows inside one day heading.</summary>
    /// <param name="at">The instant.</param>
    /// <param name="zone">The Human's time zone.</param>
    /// <returns>The local date.</returns>
    public static DateOnly DayOf(DateTimeOffset at, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, zone).DateTime);
    }

    private static string Spell(TimeSpan span, string minutes, string hours, string days)
    {
        if (span.TotalMinutes < 1)
        {
            return "now";
        }

        if (span.TotalHours < 1)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{(int)span.TotalMinutes}{minutes}");
        }

        return span.TotalDays < 1
            ? string.Create(CultureInfo.InvariantCulture, $"{(int)span.TotalHours}{hours}")
            : string.Create(CultureInfo.InvariantCulture, $"{(int)span.TotalDays}{days}");
    }
}

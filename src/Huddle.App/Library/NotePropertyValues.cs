using System.Globalization;
using System.Text.RegularExpressions;

namespace Agency.Huddle.App.Library;

/// <summary>The scalar forms the reference app tells apart (D-2 step 3): booleans, numbers, dates and dates with a time. Shared by the reader, the typing step and the drawing, so they cannot disagree.</summary>
internal static partial class NotePropertyValues
{
    [GeneratedRegex(@"^[-+]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][-+]?[0-9]+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex NumberPattern();

    [GeneratedRegex(@"^(\d{4})-(\d{2})-(\d{2})$", RegexOptions.CultureInvariant)]
    private static partial Regex DatePattern();

    [GeneratedRegex(@"^(\d{4})-(\d{2})-(\d{2})[Tt ](\d{2}):(\d{2})(?::(\d{2})(?:\.\d+)?)?(Z|z|[+-]\d{2}:?\d{2})?$", RegexOptions.CultureInvariant)]
    private static partial Regex DateTimePattern();

    /// <summary>Whether <paramref name="value"/> is a YAML 1.2 core boolean (<c>true</c>, <c>True</c>, <c>TRUE</c> and the false spellings).</summary>
    /// <param name="value">The scalar text.</param>
    /// <param name="truth">The boolean when this returns <see langword="true"/>.</param>
    internal static bool TryParseBoolean(string value, out bool truth)
    {
        truth = value is "true" or "True" or "TRUE";
        return truth || value is "false" or "False" or "FALSE";
    }

    /// <summary>Whether <paramref name="value"/> is an integer or decimal, with an optional exponent.</summary>
    /// <param name="value">The scalar text.</param>
    internal static bool IsNumber(string value) => NumberPattern().IsMatch(value);

    /// <summary>Whether <paramref name="value"/> is a real <c>YYYY-MM-DD</c> date.</summary>
    /// <param name="value">The scalar text.</param>
    /// <param name="date">The date when this returns <see langword="true"/>.</param>
    internal static bool TryParseDate(string value, out DateOnly date)
    {
        date = default;
        Match match = DatePattern().Match(value);
        return match.Success && TryBuildDate(match, out date);
    }

    /// <summary>Whether <paramref name="value"/> is a real date and time, with an optional zone: the form the reference app calls Date and time.</summary>
    /// <param name="value">The scalar text.</param>
    /// <param name="wall">The wall-clock time as written when this returns <see langword="true"/>.</param>
    /// <param name="offset">The written offset (<c>Z</c> is zero), or <see langword="null"/> when none was written.</param>
    internal static bool TryParseDateTime(string value, out DateTime wall, out TimeSpan? offset)
    {
        wall = default;
        offset = null;
        Match match = DateTimePattern().Match(value);
        if (!match.Success || !TryBuildDate(match, out DateOnly date))
        {
            return false;
        }

        int hour = int.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture);
        int minute = int.Parse(match.Groups[5].Value, CultureInfo.InvariantCulture);
        int second = match.Groups[6].Success ? int.Parse(match.Groups[6].Value, CultureInfo.InvariantCulture) : 0;
        if (hour > 23 || minute > 59 || second > 59)
        {
            return false;
        }

        if (match.Groups[7].Success && !TryReadOffset(match.Groups[7].Value, out offset))
        {
            return false;
        }

        wall = date.ToDateTime(new TimeOnly(hour, minute, second));
        return true;
    }

    /// <summary>Builds the calendar date from the first three groups of a date pattern match; <see langword="false"/> for an impossible date.</summary>
    private static bool TryBuildDate(Match match, out DateOnly date)
    {
        date = default;
        int year = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        int month = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        int day = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
        if (year < 1 || month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
        {
            return false;
        }

        date = new DateOnly(year, month, day);
        return true;
    }

    /// <summary>Reads <c>Z</c> or <c>±HH:MM</c> into an offset; <see langword="false"/> when it is out of range.</summary>
    private static bool TryReadOffset(string text, out TimeSpan? offset)
    {
        offset = TimeSpan.Zero;
        if (text is "Z" or "z")
        {
            return true;
        }

        string digits = text[1..].Replace(":", string.Empty, StringComparison.Ordinal);
        int hours = int.Parse(digits[..2], CultureInfo.InvariantCulture);
        int minutes = int.Parse(digits[2..], CultureInfo.InvariantCulture);
        if (hours > 14 || minutes > 59)
        {
            offset = null;
            return false;
        }

        TimeSpan span = new(hours, minutes, 0);
        offset = text[0] == '-' ? -span : span;
        return true;
    }
}

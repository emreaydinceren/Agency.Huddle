using System.Globalization;
using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.App.Skills.Ui;

/// <summary>Says how long ago SKILLS.md was read, for the Pack bar's status line (Skill Packs spec §6.9.5).</summary>
internal static class ReadAgoText
{
    /// <summary><i>just now</i>, <i>1 minute ago</i>, <i>N minutes ago</i>, <i>1 hour ago</i>, <i>N hours ago</i>, then the
    /// date as <see cref="HumanDate.Format(DateOnly, DateOnly)"/> gives it, in the local time zone of <paramref name="now"/>.</summary>
    public static string Format(DateTimeOffset readAt, DateTimeOffset now)
    {
        TimeSpan ago = now - readAt;
        if (ago < TimeSpan.FromMinutes(1))
        {
            return "just now";
        }

        if (ago < TimeSpan.FromHours(1))
        {
            int minutes = (int)ago.TotalMinutes;
            return minutes == 1 ? "1 minute ago" : string.Create(CultureInfo.InvariantCulture, $"{minutes} minutes ago");
        }

        if (ago < TimeSpan.FromDays(1))
        {
            int hours = (int)ago.TotalHours;
            return hours == 1 ? "1 hour ago" : string.Create(CultureInfo.InvariantCulture, $"{hours} hours ago");
        }

        DateOnly day = DateOnly.FromDateTime(readAt.ToOffset(now.Offset).DateTime);
        DateOnly today = DateOnly.FromDateTime(now.DateTime);
        return HumanDate.Format(day, today);
    }
}

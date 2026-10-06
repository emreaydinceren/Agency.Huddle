using System.Globalization;
using System.Text.RegularExpressions;

namespace Agency.Huddle.App.Components.Shared;

/// <summary>A Task wake-up Message, read back into the parts a slim system row shows.</summary>
/// <param name="TaskId">The Task's id, such as <c>PLAT-0004</c>.</param>
/// <param name="Title">The Task's title.</param>
/// <param name="Actor">Who changed the Task.</param>
/// <param name="Changes">One line per change, without the leading bullet.</param>
internal sealed record TaskWakeInfo(string TaskId, string Title, string Actor, IReadOnlyList<string> Changes);

/// <summary>
/// The pure decisions <c>MessageList</c> makes about how a Transcript reads: where a day
/// separator goes, which Messages share a sender header, and which Messages are Task wake-ups that
/// deserve a slim system row instead of a bubble.
/// </summary>
internal static partial class MessageDisplay
{
    /// <summary>Consecutive Messages from one sender further apart than this get their own header again.</summary>
    internal static readonly TimeSpan GroupWindow = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The wake-up text <c>task.wake.message</c> renders by default. A Prompt override that no longer
    /// matches simply fails to parse and the Message renders as an ordinary bubble, which is the safe
    /// direction: nothing is hidden, only less tidy.
    /// </summary>
    [GeneratedRegex(
        """\A@(?<assignee>.+?) Task (?<id>\S+) "(?<title>.*)" \((?<status>[^,()]*), (?<team>.*)\) was changed by (?<actor>.+?):\n(?<changes>(?:- .*\n)+)Call get_task with taskId \k<id> for the full task\.\s*\z""",
        RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex WakePattern();

    /// <summary>Reads <paramref name="text"/> as a Task wake-up Message.</summary>
    /// <param name="text">The Message text.</param>
    /// <param name="info">The parsed parts when this returns <see langword="true"/>.</param>
    /// <returns>Whether <paramref name="text"/> is a wake-up in the default wording.</returns>
    internal static bool TryParseTaskWake(string text, out TaskWakeInfo info)
    {
        ArgumentNullException.ThrowIfNull(text);
        Match match = WakePattern().Match(text.Replace("\r\n", "\n", StringComparison.Ordinal));
        if (!match.Success)
        {
            info = new TaskWakeInfo(string.Empty, string.Empty, string.Empty, []);
            return false;
        }

        List<string> changes = [];
        foreach (string line in match.Groups["changes"].Value.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            changes.Add(line[2..]);
        }

        info = new TaskWakeInfo(match.Groups["id"].Value, match.Groups["title"].Value, match.Groups["actor"].Value, changes);
        return true;
    }

    /// <summary>The local calendar day <paramref name="timestamp"/> falls on.</summary>
    /// <param name="timestamp">A Message time.</param>
    internal static DateOnly LocalDay(DateTimeOffset timestamp) => DateOnly.FromDateTime(timestamp.ToLocalTime().DateTime);

    /// <summary>Whether a day separator belongs above a Message.</summary>
    /// <param name="previous">The previous Message's time, or <see langword="null"/> for the first.</param>
    /// <param name="current">This Message's time.</param>
    internal static bool StartsNewDay(DateTimeOffset? previous, DateTimeOffset current) =>
        previous is not { } before || LocalDay(before) != LocalDay(current);

    /// <summary>"Today", "Yesterday", or a short date such as "Fri 2 Oct" (with the year when it is not this one).</summary>
    /// <param name="timestamp">The Message time.</param>
    /// <param name="now">The current time.</param>
    internal static string DayLabel(DateTimeOffset timestamp, DateTimeOffset now)
    {
        DateOnly day = LocalDay(timestamp);
        DateOnly today = LocalDay(now);
        if (day == today)
        {
            return "Today";
        }

        if (day == today.AddDays(-1))
        {
            return "Yesterday";
        }

        string format = day.Year == today.Year ? "ddd d MMM" : "ddd d MMM yyyy";
        return day.ToString(format, CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// Whether a Message continues the previous one's header: the same sender, the same day and
    /// within <see cref="GroupWindow"/>, so the name and avatar are not repeated.
    /// </summary>
    /// <param name="previousSenderId">The previous Message's sender, or <see langword="null"/> for the first.</param>
    /// <param name="previous">The previous Message's time, or <see langword="null"/> for the first.</param>
    /// <param name="senderId">This Message's sender.</param>
    /// <param name="current">This Message's time.</param>
    internal static bool ContinuesGroup(string? previousSenderId, DateTimeOffset? previous, string senderId, DateTimeOffset current) =>
        previous is { } before
        && string.Equals(previousSenderId, senderId, StringComparison.Ordinal)
        && !StartsNewDay(before, current)
        && (current - before) <= GroupWindow;
}

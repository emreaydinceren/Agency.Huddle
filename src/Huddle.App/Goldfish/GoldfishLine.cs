using System.Globalization;
using System.Text.RegularExpressions;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Goldfish;

/// <summary>The words the goldfish posts: the return line per exit (spec 6.5), the result Message (6.6) and the not-in-mode reply (6.7).</summary>
internal static partial class GoldfishLine
{
    /// <summary>What ends the first line of a start row, after the Agent's name.</summary>
    internal const string StartMarker = " went into goldfish mode. They don't remember this Room.";

    /// <summary>What follows the Agent's name in a return row.</summary>
    internal const string ReturnMarker = " is back. ";

    /// <summary>The label above the brief in a start row's detail.</summary>
    internal const string BriefLabel = "Their brief:";

    private static readonly string[] KnownRests = ["Goldfish finished", "Gave up", "Called back", "Didn't finish", "Interrupted by a restart"];

    /// <summary>
    /// The return row's text. It Mentions the Agent with an <c>@</c> when the Agent is still a Member of the Room;
    /// otherwise the name stands bare (E-4).
    /// </summary>
    /// <param name="agentName">The Agent's display name, or its stored id when it is no longer known.</param>
    /// <param name="exit">How the possession ended.</param>
    /// <param name="calledBackBy">Who called the Agent back; only <see cref="GoldfishExit.CalledBack"/> reads it.</param>
    /// <param name="elapsed">How long the possession lasted; only <see cref="GoldfishExit.CalledBack"/> reads it.</param>
    /// <param name="isMember">Whether the Agent is a Member of the Room.</param>
    /// <param name="findings">How many list items the posted result holds (<see cref="CountListItems"/>); only <see cref="GoldfishExit.Finished"/> reads it, and zero leaves the count out.</param>
    /// <param name="firstLine">The first line of the result; only <see cref="GoldfishExit.Stuck"/> reads it.</param>
    internal static string ReturnLine(string agentName, GoldfishExit exit, string? calledBackBy, TimeSpan elapsed, bool isMember, int findings = 0, string? firstLine = null)
    {
        string who = isMember ? "@" + agentName : agentName;
        string byWho = calledBackBy is null ? string.Empty : $" by {calledBackBy}";
        string rest = exit switch
        {
            GoldfishExit.Finished when findings <= 0 => "Goldfish finished: result above.",
            GoldfishExit.Finished => string.Create(CultureInfo.InvariantCulture, $"Goldfish finished: {findings} {(findings == 1 ? "finding" : "findings")}, result above."),
            GoldfishExit.Stuck => string.IsNullOrWhiteSpace(firstLine) ? "Gave up." : $"Gave up: {firstLine}.",
            GoldfishExit.CalledBack => $"Called back{byWho} after {Span(elapsed)}. Partial result above.",
            GoldfishExit.Failed => "Didn't finish: the Adapter stopped.",
            GoldfishExit.Restarted => "Interrupted by a restart. Partial result above.",
            _ => throw new ArgumentOutOfRangeException(nameof(exit), "Unknown goldfish exit."),
        };

        return who + ReturnMarker + rest;
    }

    /// <summary>
    /// How many list items the posted result holds: every line that starts, after any indent, with a bullet
    /// (<c>-</c>, <c>*</c>, <c>+</c>) or a number and a dot or bracket. Nested items count. Zero for prose.
    /// </summary>
    /// <param name="postedResult">The result text as posted, without the brief.</param>
    internal static int CountListItems(string? postedResult)
    {
        if (string.IsNullOrEmpty(postedResult))
        {
            return 0;
        }

        return ListItemPattern().Count(postedResult);
    }

    [GeneratedRegex(@"^[ \t]*(?:[-*+]|\d+[.)])[ \t]+\S", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex ListItemPattern();

    /// <summary>Reads a start row back (the transcript recognises it by its stamp, then by this).</summary>
    /// <param name="text">The Message text.</param>
    /// <param name="summary">The first line, which ends with <see cref="StartMarker"/>; empty when not a start row.</param>
    /// <param name="brief">The brief, word for word, after the first blank line; empty when not a start row.</param>
    /// <returns>Whether the text is a start row.</returns>
    internal static bool TryParseStartRow(string text, out string summary, out string brief)
    {
        ArgumentNullException.ThrowIfNull(text);

        summary = string.Empty;
        brief = string.Empty;
        int split = text.IndexOf("\n\n", StringComparison.Ordinal);
        if (split <= StartMarker.Length || text.IndexOf('\n') != split || !text.AsSpan(0, split).EndsWith(StartMarker, StringComparison.Ordinal))
        {
            return false;
        }

        summary = text[..split];
        brief = text[(split + 2)..];
        return true;
    }

    /// <summary>Reads a return row back: one line whose text after the return marker begins with a known ending.</summary>
    /// <param name="text">The Message text.</param>
    /// <param name="summary">The line without a leading <c>@</c>; empty when not a return row.</param>
    /// <returns>Whether the text is a return row.</returns>
    internal static bool TryParseReturnRow(string text, out string summary)
    {
        ArgumentNullException.ThrowIfNull(text);

        summary = string.Empty;
        int marker = text.IndexOf(ReturnMarker, StringComparison.Ordinal);
        if (marker <= 0 || text.Contains('\n'))
        {
            return false;
        }

        ReadOnlySpan<char> rest = text.AsSpan(marker + ReturnMarker.Length);
        foreach (string known in KnownRests)
        {
            if (rest.StartsWith(known, StringComparison.Ordinal))
            {
                summary = text[0] == '@' ? text[1..] : text;
                return true;
            }
        }

        return false;
    }

    /// <summary>The detail under a return row: when it started and ended, its Turns and, when known, its spend.</summary>
    /// <param name="started">When the possession started.</param>
    /// <param name="ended">When the return row was posted.</param>
    /// <param name="turns">How many Turns it ran.</param>
    /// <param name="spend">What it cost.</param>
    /// <param name="currency">The currency of <paramref name="spend"/>, or null when unknown.</param>
    /// <param name="zone">The zone the times read in; the local zone when null.</param>
    internal static string ReturnDetail(DateTimeOffset started, DateTimeOffset ended, int turns, decimal spend, string? currency, TimeZoneInfo? zone = null)
    {
        TimeZoneInfo resolved = zone ?? TimeZoneInfo.Local;
        DateTimeOffset start = TimeZoneInfo.ConvertTime(started, resolved);
        DateTimeOffset end = TimeZoneInfo.ConvertTime(ended, resolved);
        string detail = $"Started {MessageDisplay.ClockTime(start, start)}, ended {MessageDisplay.ClockTime(end, end)}, {Plural.Of(turns, "Turn", "Turns")}";
        return spend > 0 && currency is not null
            ? string.Create(CultureInfo.InvariantCulture, $"{detail}, spent {spend.ToString("N3", CultureInfo.InvariantCulture)} {currency}")
            : detail;
    }

    /// <summary>
    /// The result Message's text, in spec order: the brief word for word, then the result, then the findings.
    /// </summary>
    /// <param name="brief">The brief, quoted as given.</param>
    /// <param name="result">The result or last reply; null when nothing was posted.</param>
    /// <param name="findings">The findings, or null.</param>
    /// <param name="partial">Whether the work was cut short.</param>
    internal static string ResultText(string brief, string? result, string? findings, bool partial)
    {
        string body = result ?? "No result was posted.";
        if (result is not null && partial)
        {
            body = "Partial result:\n" + result;
        }

        string text = $"Brief: {brief}\n\n{body}";
        return string.IsNullOrWhiteSpace(findings) ? text : $"{text}\n\n{findings}";
    }

    /// <summary>The start row (spec 6.2): the Agent went into goldfish mode, then the brief word for word.</summary>
    /// <param name="agentName">The Agent's display name.</param>
    /// <param name="brief">The brief, quoted as given.</param>
    internal static string StartRow(string agentName, string brief) =>
        $"{agentName}{StartMarker}\n\n{brief}";

    /// <summary>The comment on the Task in hand: the brief word for word.</summary>
    /// <param name="agentName">The Agent's display name.</param>
    /// <param name="brief">The brief, quoted as given.</param>
    internal static string TaskComment(string agentName, string brief) => $"Goldfish brief for {agentName}: {brief}";

    /// <summary>The reply when a comeback names an Agent that is not in goldfish mode.</summary>
    /// <param name="agentName">The Agent's display name.</param>
    internal static string NotInMode(string agentName) => $"{agentName} isn't in goldfish mode.";

    /// <summary>Whole minutes, rounded down; "less than a minute" at zero.</summary>
    private static string Span(TimeSpan elapsed)
    {
        int minutes = Math.Max(0, (int)elapsed.TotalMinutes);
        return minutes switch
        {
            0 => "less than a minute",
            1 => "1 minute",
            _ => string.Create(CultureInfo.InvariantCulture, $"{minutes} minutes"),
        };
    }
}

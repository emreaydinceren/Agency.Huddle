using System.Globalization;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Goldfish;

/// <summary>The words the goldfish posts: the return line per exit (spec 6.5), the result Message (6.6) and the not-in-mode reply (6.7).</summary>
internal static class GoldfishLine
{
    /// <summary>
    /// The return row's text. It Mentions the Agent with an <c>@</c> when the Agent is still a Member of the Room;
    /// otherwise the name stands bare (E-4).
    /// </summary>
    /// <param name="agentName">The Agent's display name, or its stored id when it is no longer known.</param>
    /// <param name="exit">How the possession ended.</param>
    /// <param name="calledBackBy">Who called the Agent back; only <see cref="GoldfishExit.CalledBack"/> reads it.</param>
    /// <param name="elapsed">How long the possession lasted; only <see cref="GoldfishExit.CalledBack"/> reads it.</param>
    /// <param name="isMember">Whether the Agent is a Member of the Room.</param>
    /// <param name="findings">How many findings the goldfish reported; only <see cref="GoldfishExit.Finished"/> reads it.</param>
    /// <param name="firstLine">The first line of the result; only <see cref="GoldfishExit.Stuck"/> reads it.</param>
    internal static string ReturnLine(string agentName, GoldfishExit exit, string? calledBackBy, TimeSpan elapsed, bool isMember, int findings = 0, string? firstLine = null)
    {
        string who = isMember ? "@" + agentName : agentName;
        string byWho = calledBackBy is null ? string.Empty : $" by {calledBackBy}";
        string rest = exit switch
        {
            GoldfishExit.Finished => string.Create(CultureInfo.InvariantCulture, $"Goldfish finished: {findings} {(findings == 1 ? "finding" : "findings")}, result above."),
            GoldfishExit.Stuck => string.IsNullOrWhiteSpace(firstLine) ? "Gave up." : $"Gave up: {firstLine}.",
            GoldfishExit.CalledBack => $"Called back{byWho} after {Span(elapsed)}. Partial result above.",
            GoldfishExit.Failed => "Didn't finish: the Adapter stopped.",
            GoldfishExit.Restarted => "Interrupted by a restart. Partial result above.",
            _ => throw new ArgumentOutOfRangeException(nameof(exit), "Unknown goldfish exit."),
        };

        return $"{who} is back. {rest}";
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
        $"{agentName} went into goldfish mode. They don't remember this Room.\n\n{brief}";

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

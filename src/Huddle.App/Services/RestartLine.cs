using System.Globalization;
using System.Text;

namespace Agency.Huddle.App.Services;

/// <summary>The text of the restart line <see cref="TurnQueueRecovery"/> posts, and the words <c>MessageDisplay.TryParseRestartLine</c> recognises it by (spec §5.11).</summary>
internal static class RestartLine
{
    /// <summary>The first words of a line in which at least one Message was picked up.</summary>
    internal const string Prefix = "Picked up after a restart.";

    /// <summary>The first words of a line in which nothing was picked up.</summary>
    internal const string NotPickedUpPrefix = "Not picked up after a restart.";

    /// <summary>Builds the line.</summary>
    /// <param name="entries">One entry per Agent, in the order first seen.</param>
    /// <param name="resumeWithin">The age limit, named in the text of the rows that expired.</param>
    /// <returns>The text, or <see langword="null"/> when there is nothing to say.</returns>
    internal static string? Build(IReadOnlyList<Entry> entries, TimeSpan resumeWithin)
    {
        ArgumentNullException.ThrowIfNull(entries);

        List<Entry> pickedUp = [.. entries.Where(static entry => entry.PickedUp > 0)];
        List<string> notPickedUp = NotPickedUpClauses(entries, resumeWithin);
        if (pickedUp.Count == 0 && notPickedUp.Count == 0)
        {
            return null;
        }

        StringBuilder text = new(pickedUp.Count > 0 ? Prefix : NotPickedUpPrefix);
        if (pickedUp.Count > 0)
        {
            _ = text.Append(' ').Append(Count(pickedUp[0].PickedUp, true)).Append(" waiting for ").Append(pickedUp[0].AgentName);
            foreach (Entry entry in pickedUp.Skip(1))
            {
                _ = text.Append(", ").Append(Count(entry.PickedUp, false)).Append(" for ").Append(entry.AgentName);
            }

            _ = text.Append('.');
        }

        if (notPickedUp.Count > 0)
        {
            _ = text.Append(" Not picked up: ").Append(string.Join("; ", notPickedUp)).Append('.');
        }

        return text.ToString();
    }

    private static List<string> NotPickedUpClauses(IReadOnlyList<Entry> entries, TimeSpan resumeWithin)
    {
        List<string> clauses = [];
        foreach (Entry entry in entries)
        {
            if (entry.Expired > 0)
            {
                string noun = clauses.Count == 0 ? Count(entry.Expired, true) : entry.Expired.ToString(CultureInfo.InvariantCulture);
                clauses.Add($"{noun} for {entry.AgentName} that waited more than {Limit(resumeWithin)}");
            }

            if (entry.Abandoned > 0)
            {
                string noun = clauses.Count == 0 ? Count(entry.Abandoned, true) : entry.Abandoned.ToString(CultureInfo.InvariantCulture);
                string pronoun = entry.Abandoned == 1 ? "it" : "them";
                clauses.Add($"{noun} that Huddle stopped twice while {entry.AgentName} was answering {pronoun}");
            }
        }

        return clauses;
    }

    private static string Count(int count, bool withNoun)
    {
        string number = count.ToString(CultureInfo.InvariantCulture);
        if (!withNoun)
        {
            return number;
        }

        return count == 1 ? number + " Message" : number + " Messages";
    }

    private static string Limit(TimeSpan span)
    {
        double hours = span.TotalHours;
        if (hours >= 1 && Math.Abs(hours - Math.Round(hours)) < 1e-9)
        {
            long whole = (long)Math.Round(hours);
            return whole.ToString(CultureInfo.InvariantCulture) + (whole == 1 ? " hour" : " hours");
        }

        long minutes = Math.Max(1, (long)Math.Round(span.TotalMinutes));
        return minutes.ToString(CultureInfo.InvariantCulture) + (minutes == 1 ? " minute" : " minutes");
    }

    /// <summary>What a pass did for one Agent in one Room.</summary>
    /// <param name="AgentName">The Agent's Name.</param>
    /// <param name="PickedUp">Rows left waiting or set back to waiting.</param>
    /// <param name="Expired">Rows closed as expired.</param>
    /// <param name="Abandoned">Rows closed as abandoned after two cut-offs.</param>
    internal sealed record Entry(string AgentName, int PickedUp, int Expired, int Abandoned);
}

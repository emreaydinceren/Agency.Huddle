namespace Agency.Huddle.App.Tasks.HumanGate;

/// <summary>Reads whether the Human has accepted a Task from its Change log (Human gate Spec §6.5).</summary>
internal static class Acceptance
{
    private const string AcceptedPrefix = "accepted ";
    private const string OutsidePrefix = "edited outside Huddle: ";
    private const string StatusPrefix = "status: ";
    private const string IntoNeedsYou = " → Needs You";
    private const string PartSeparator = "; ";
    private const int HashLength = 12;

    /// <summary>Works out the acceptance of a Task.</summary>
    /// <param name="task">The Task.</param>
    /// <returns>
    /// <see cref="AcceptanceState.Valid"/> or <see cref="AcceptanceState.Stale"/> when the Task is in Needs You and its latest
    /// <c>accepted</c> entry follows its latest move into Needs You; otherwise <see cref="AcceptanceState.None"/>.
    /// </returns>
    public static AcceptanceInfo Of(TaskItem task)
    {
        ArgumentNullException.ThrowIfNull(task);

        if (task.Status != TaskState.NeedsYou)
        {
            return AcceptanceInfo.NoAcceptance;
        }

        IReadOnlyList<ChangeLogEntry> log = task.ChangeLog;
        int entered = -1;
        int accepted = -1;
        for (int index = 0; index < log.Count; index++)
        {
            if (EntersNeedsYou(log[index].Summary))
            {
                entered = index;
            }

            if (HashOf(log[index].Summary) is not null)
            {
                accepted = index;
            }
        }

        if (entered < 0 || accepted < entered)
        {
            return AcceptanceInfo.NoAcceptance;
        }

        ChangeLogEntry entry = log[accepted];
        string? hash = HashOf(entry.Summary);
        string current = ContentFingerprint.Of(task.Title, task.Action, task.Description);
        AcceptanceState state = string.Equals(hash, current, StringComparison.Ordinal) ? AcceptanceState.Valid : AcceptanceState.Stale;
        return new AcceptanceInfo(state, entry.At, entry.Actor, hash);
    }

    /// <summary>True when a Change log summary records a move into Needs You.</summary>
    /// <param name="summary">The summary.</param>
    private static bool EntersNeedsYou(string summary)
    {
        string text = summary.StartsWith(OutsidePrefix, StringComparison.Ordinal) ? summary[OutsidePrefix.Length..] : summary;
        foreach (string part in text.Split(PartSeparator))
        {
            if (part.StartsWith(StatusPrefix, StringComparison.Ordinal) && part.Contains(IntoNeedsYou, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The hash of an <c>accepted &lt;hash&gt;</c> summary, or <see langword="null"/> when the summary is not one.</summary>
    /// <param name="summary">The summary.</param>
    private static string? HashOf(string summary)
    {
        if (!summary.StartsWith(AcceptedPrefix, StringComparison.Ordinal) || summary.Length != AcceptedPrefix.Length + HashLength)
        {
            return null;
        }

        string hash = summary[AcceptedPrefix.Length..];
        foreach (char c in hash)
        {
            if (!char.IsAsciiHexDigitLower(c))
            {
                return null;
            }
        }

        return hash;
    }
}

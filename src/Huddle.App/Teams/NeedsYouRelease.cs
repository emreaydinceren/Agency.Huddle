using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// The release a Task gets under the level Run inside the Team (ADR-0087): its Proof is complete in Review, so its merge into main
/// needs no Accept. Huddle writes the release as a Change log entry in one fixed wording; the merge gate, the wake and the Feed read it
/// back through this class rather than from the Task's Action, so the wording lives in this one place.
/// </summary>
internal static class NeedsYouRelease
{
    private const string Prefix = "proceeded under ";
    private const string Suffix = " level Run inside the Team: its merge into main needs no Accept";
    private const string SprintInfix = " level Run inside the Team: Sprint '";
    private const string SprintSuffix = "' approved without the Human";
    private const string StatusPrefix = "status: ";
    private const string IntoDone = " → Done";
    private const string PartSeparator = "; ";

    /// <summary>How long a release stays fresh: it is not written again, and carries a merge, for this long.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromHours(24);

    /// <summary>How long after a released Task reached Done its Team may still merge.</summary>
    public static readonly TimeSpan DoneWindow = TimeSpan.FromHours(1);

    /// <summary>The Change log summary of a release.</summary>
    /// <param name="scope">The scope that supplied the level.</param>
    /// <returns><c>proceeded under &lt;scope&gt; level Run inside the Team: its merge into main needs no Accept</c>.</returns>
    public static string SummaryFor(NeedsYouScope scope) => Prefix + scope.ToDisplay() + Suffix;

    /// <summary>
    /// The Change log summary a Task gets when its Sprint was approved without the Human. It is not a release: it never lets a merge go
    /// ahead, and <see cref="IsRelease"/> is <see langword="false"/> for it.
    /// </summary>
    /// <param name="scope">The scope that supplied the level.</param>
    /// <param name="sprintName">The Sprint's name.</param>
    /// <returns><c>proceeded under &lt;scope&gt; level Run inside the Team: Sprint '&lt;name&gt;' approved without the Human</c>.</returns>
    public static string SprintSummaryFor(NeedsYouScope scope, string sprintName)
    {
        ArgumentNullException.ThrowIfNull(sprintName);
        return Prefix + scope.ToDisplay() + SprintInfix + sprintName + SprintSuffix;
    }

    /// <summary>Whether a Change log summary records a Sprint approved without the Human.</summary>
    /// <param name="summary">The summary.</param>
    public static bool IsSprintApproval(string summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        return summary.StartsWith(Prefix, StringComparison.Ordinal)
            && summary.Contains(SprintInfix, StringComparison.Ordinal)
            && summary.EndsWith(SprintSuffix, StringComparison.Ordinal);
    }

    /// <summary>Whether a Change log summary is a release.</summary>
    /// <param name="summary">The summary.</param>
    public static bool IsRelease(string summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        return summary.StartsWith(Prefix, StringComparison.Ordinal) && summary.EndsWith(Suffix, StringComparison.Ordinal);
    }

    /// <summary>The scope word a release summary names (<c>global</c>, <c>Team</c> or <c>project</c>).</summary>
    /// <param name="summary">A release summary.</param>
    /// <returns>The word, or <see langword="null"/> when the summary is not a release.</returns>
    public static string? ScopeWordOf(string summary) =>
        IsRelease(summary) && summary.Length > Prefix.Length + Suffix.Length ? summary[Prefix.Length..^Suffix.Length] : null;

    /// <summary>When the Task was last released, read from its Change log.</summary>
    /// <param name="task">The Task.</param>
    /// <returns>The time of the latest release entry, or <see langword="null"/> when it has none.</returns>
    public static DateTimeOffset? LatestAt(TaskItem task)
    {
        ArgumentNullException.ThrowIfNull(task);

        DateTimeOffset? latest = null;
        foreach (ChangeLogEntry entry in task.ChangeLog)
        {
            // Only Huddle writes a release; a line by anyone else that reads the same is not one.
            if (string.Equals(entry.Actor, TaskActors.Huddle.Name, StringComparison.Ordinal) && IsRelease(entry.Summary) && (latest is null || entry.At > latest))
            {
                latest = entry.At;
            }
        }

        return latest;
    }

    /// <summary>When a Task that is Done moved into Done, read from its Change log.</summary>
    /// <param name="task">The Task.</param>
    /// <returns>The time of the latest entry that moved the Task into Done, or <see langword="null"/> when it is not Done or the log records none.</returns>
    public static DateTimeOffset? ReachedDoneAt(TaskItem task)
    {
        ArgumentNullException.ThrowIfNull(task);

        if (task.Status != TaskState.Done)
        {
            return null;
        }

        for (int index = task.ChangeLog.Count - 1; index >= 0; index--)
        {
            foreach (string part in task.ChangeLog[index].Summary.Split(PartSeparator))
            {
                if (part.StartsWith(StatusPrefix, StringComparison.Ordinal) && part.EndsWith(IntoDone, StringComparison.Ordinal))
                {
                    return task.ChangeLog[index].At;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Whether the Task's release still carries a merge: it is open and released less than <see cref="Window"/> ago, or it is Done, was
    /// released and reached Done less than <see cref="DoneWindow"/> ago.
    /// </summary>
    /// <param name="task">The Task.</param>
    /// <param name="now">The time now.</param>
    public static bool Holds(TaskItem task, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(task);

        if (LatestAt(task) is not { } releasedAt)
        {
            return false;
        }

        return task.Status.IsTerminal()
            ? ReachedDoneAt(task) is { } doneAt && now - doneAt <= DoneWindow
            : !task.Location.Closed && now - releasedAt <= Window;
    }

    /// <summary>Whether the Task was released and reached Done less than <see cref="DoneWindow"/> ago, so its Evidence still counts for a merge.</summary>
    /// <param name="task">The Task.</param>
    /// <param name="now">The time now.</param>
    public static bool InDoneWindow(TaskItem task, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(task);
        return LatestAt(task) is not null && ReachedDoneAt(task) is { } doneAt && now - doneAt <= DoneWindow;
    }
}

namespace Agency.Huddle.App.Tasks;

/// <summary>
/// The one rule that merges a Task's comments with its events into the timeline order (Spec §7.7.2): by time, each list
/// keeping its own order, a comment before an event at the same time. Reading <see cref="TaskItem.Timeline"/> and writing
/// a converted file (<c>TaskFileFormat.Upgrade</c>) both use it, so a file reads back in the order it was written.
/// </summary>
internal static class TimelineMerge
{
    /// <summary>Merges the two lists and names each slot of the result.</summary>
    /// <param name="comments">The Comment and Evidence entries, in file order.</param>
    /// <param name="events">The events, in file order.</param>
    /// <returns>For each timeline position, whether it is an event and its index in the list it came from.</returns>
    public static List<(bool IsEvent, int Index)> Order(IReadOnlyList<TimelineEntry> comments, IReadOnlyList<ChangeLogEntry> events)
    {
        List<(bool IsEvent, int Index)> slots = new(comments.Count + events.Count);
        int commentIndex = 0;
        int eventIndex = 0;
        while (commentIndex < comments.Count || eventIndex < events.Count)
        {
            bool takeComment = eventIndex >= events.Count
                || (commentIndex < comments.Count && comments[commentIndex].At <= events[eventIndex].At);
            if (takeComment)
            {
                slots.Add((false, commentIndex));
                commentIndex++;
            }
            else
            {
                slots.Add((true, eventIndex));
                eventIndex++;
            }
        }

        return slots;
    }

    /// <summary>The time of the last timeline slot, without building the merged list.</summary>
    /// <param name="comments">The Comment and Evidence entries, in file order.</param>
    /// <param name="events">The events, in file order.</param>
    /// <returns>The last entry's time, or null when both lists are empty.</returns>
    public static DateTimeOffset? LastAt(IReadOnlyList<TimelineEntry> comments, IReadOnlyList<ChangeLogEntry> events)
    {
        if (comments.Count == 0)
        {
            return events.Count > 0 ? events[^1].At : null;
        }

        if (events.Count == 0)
        {
            return comments[^1].At;
        }

        int commentIndex = 0;
        int eventIndex = 0;
        DateTimeOffset last = default;
        while (commentIndex < comments.Count || eventIndex < events.Count)
        {
            if (eventIndex >= events.Count || (commentIndex < comments.Count && comments[commentIndex].At <= events[eventIndex].At))
            {
                last = comments[commentIndex].At;
                commentIndex++;
            }
            else
            {
                last = events[eventIndex].At;
                eventIndex++;
            }
        }

        return last;
    }
}

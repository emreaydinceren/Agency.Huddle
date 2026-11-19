namespace Agency.Huddle.App.Tasks.Ordering;

/// <summary>The Task order: Started, then Next, then Backlog, each Task once.</summary>
/// <param name="Entries">Every entry in listed order.</param>
internal sealed record TaskOrderResult(IReadOnlyList<TaskOrderEntry> Entries)
{
    /// <summary>The first startable entry, or null when nothing can start.</summary>
    public TaskOrderEntry? Next => this.Entries.FirstOrDefault(entry => entry.IsStartable);
}

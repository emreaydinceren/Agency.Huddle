namespace Agency.Huddle.App.Tasks;

/// <summary>
/// One successful change to a Task (Spec §9.5): what it looked like before (or <see langword="null"/>
/// for a Create), what it looks like now, what changed, who changed it, and the Change log entry
/// recorded for it.
/// </summary>
/// <param name="Before">The Task before the change, or <see langword="null"/> when it was just created.</param>
/// <param name="After">The Task after the change.</param>
/// <param name="Changes">What changed, from <see cref="TaskDiff.Compare(TaskItem, TaskItem)"/>.</param>
/// <param name="Actor">Who made the change.</param>
/// <param name="Entry">The Change log entry recorded for this change.</param>
internal sealed record TaskChange(TaskItem? Before, TaskItem After, IReadOnlyList<FieldChange> Changes, TaskActor Actor, ChangeLogEntry Entry);

/// <summary>
/// A plain hub for Task-related notifications (Spec §9.5): nothing here reads or writes a Task
/// itself. <see cref="TaskService"/> raises <see cref="TaskChanged"/> after every successful
/// mutation, outside any lock, and re-raises <see cref="TaskStore.IndexChanged"/> as
/// <see cref="TasksReloaded"/> so UI lists have one event to subscribe to regardless of whether a
/// change came through <see cref="TaskService"/> or the watcher. Internal (Settled corrections-B2
/// D6 item 12).
/// </summary>
internal sealed class TaskEvents
{
    /// <summary>Raised after a Task is created, updated, closed or reopened.</summary>
    public event Action<TaskChange>? TaskChanged;

    /// <summary>Raised whenever the Task index was rebuilt, for UI lists that don't need to know what changed.</summary>
    public event Action? TasksReloaded;

    /// <summary>
    /// Invokes every <see cref="TaskChanged"/> subscriber in turn, logging nothing itself - a
    /// handler that throws propagates, following the plain-hub design; callers that need
    /// throw-and-skip semantics (Spec §9.5) wrap their own subscription.
    /// </summary>
    /// <param name="change">The change to publish.</param>
    internal void RaiseTaskChanged(TaskChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        this.TaskChanged?.Invoke(change);
    }

    /// <summary>Invokes every <see cref="TasksReloaded"/> subscriber in turn (Settled corrections-B2 item 15, for D11/D14 bUnit tests to drive it directly).</summary>
    internal void RaiseTasksReloaded() => this.TasksReloaded?.Invoke();
}

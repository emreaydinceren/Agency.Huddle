using Agency.Huddle.App.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.App.Tasks;

/// <summary>
/// A plain hub for Task-related notifications (Spec §9.5): nothing here reads or writes a Task
/// itself. <see cref="TaskService"/> raises <see cref="TaskChanged"/> after every successful
/// mutation, outside any lock, and re-raises <see cref="TaskStore.IndexChanged"/> as
/// <see cref="TasksReloaded"/> so UI lists have one event to subscribe to regardless of whether a
/// change came through <see cref="TaskService"/> or the watcher. Internal (Settled corrections-B2
/// D6 item 12).
/// </summary>
/// <param name="logger">Reports a subscriber that threw; defaults to a logger that discards.</param>
internal sealed class TaskEvents(ILogger<TaskEvents>? logger = null)
{
    private readonly ILogger<TaskEvents> logger = logger ?? NullLogger<TaskEvents>.Instance;

    /// <summary>Raised after a Task is created, updated, closed or reopened.</summary>
    public event Action<TaskChange>? TaskChanged;

    /// <summary>Raised whenever the Task index was rebuilt, for UI lists that don't need to know what changed.</summary>
    public event Action? TasksReloaded;

    /// <summary>
    /// Invokes every <see cref="TaskChanged"/> subscriber in turn. A handler that throws is logged
    /// at Error and skipped, so it neither stops the subscribers after it nor reaches the caller.
    /// </summary>
    /// <param name="change">The change to publish.</param>
    internal void RaiseTaskChanged(TaskChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        EventRaise.Isolated(this.TaskChanged, change, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.TaskChanged)));
    }

    /// <summary>Invokes every <see cref="TasksReloaded"/> subscriber in turn (Settled corrections-B2 item 15, for D11/D14 bUnit tests to drive it directly).</summary>
    internal void RaiseTasksReloaded() =>
        EventRaise.Isolated(this.TasksReloaded, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.TasksReloaded)));
}

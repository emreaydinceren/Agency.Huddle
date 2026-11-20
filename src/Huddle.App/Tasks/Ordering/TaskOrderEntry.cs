using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Tasks.Ordering;

/// <summary>One Task in the Task order.</summary>
/// <param name="Task">The Task.</param>
/// <param name="Group">The part of the order it is listed in.</param>
/// <param name="IsStartable">True for a To Do Task with no open blocker that is not in a blocker cycle and does not wait for another Task of its group (<paramref name="WaitsFor"/>).</param>
/// <param name="Blocks">The Task this entry was listed ahead of because it blocks it, or null.</param>
/// <param name="BlockedBy">Its open blockers, in or out of scope, in <c>blocked_by</c> order.</param>
/// <param name="Cycle">The ids of the blocker cycle the Task is in, in walk order; empty when it is in none.</param>
/// <param name="WaitsFor">
/// Set when the Task could start but its Project (or the Team's Tasks with no Project) admits one In Progress Task at a time
/// (ADR-0103): the Task already In Progress there, or else the Task that is Next there. Such an entry is not startable.
/// </param>
internal sealed record TaskOrderEntry(
    TaskItem Task,
    TaskOrderGroup Group,
    bool IsStartable,
    TaskId? Blocks,
    IReadOnlyList<OpenBlocker> BlockedBy,
    IReadOnlyList<TaskId> Cycle,
    TaskId? WaitsFor = null);

using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Tasks;

/// <summary>The one test of what blocks a Task, shared by the start gate and the Task order so "blocked" means the same to both.</summary>
internal static class TaskBlockers
{
    /// <summary>Lists the Tasks in a Task's <c>blocked_by</c> that are not finished, in <c>blocked_by</c> order; a blocker that no longer exists blocks nothing.</summary>
    /// <param name="task">The Task as stored.</param>
    /// <param name="lookup">Finds any Task by id, or null when it does not exist.</param>
    public static IReadOnlyList<OpenBlocker> Open(TaskItem task, Func<TaskId, TaskItem?> lookup)
    {
        List<OpenBlocker> open = [];
        foreach (TaskId id in task.BlockedBy)
        {
            if (lookup(id) is { } blocker && !blocker.Status.IsTerminal())
            {
                open.Add(new OpenBlocker(id, blocker.Status, blocker.Assignee));
            }
        }

        return open;
    }
}

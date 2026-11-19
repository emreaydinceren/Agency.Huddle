using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>Tests for <see cref="TaskBlockers.Open"/>, the one blocker test the gate and the Task order share.</summary>
public sealed class TaskBlockersTests
{
    /// <summary>A lookup over <paramref name="tasks"/>.</summary>
    private static Func<TaskId, TaskItem?> Lookup(params TaskItem[] tasks) =>
        id => tasks.FirstOrDefault(task => task.Id == id);

    /// <summary>Finished blockers (Done, Cancelled, Duplicate, Rejected) are not listed.</summary>
    [Fact]
    public void Open_TerminalBlockers_AreIgnored()
    {
        TaskItem done = TestTasks.Make(id: "PLAT-0002", status: TaskState.Done);
        TaskItem open = TestTasks.Make(id: "PLAT-0003", status: TaskState.ToDo);
        TaskItem task = TestTasks.Make(id: "PLAT-0001", blockedBy: [done.Id, open.Id]);

        IReadOnlyList<OpenBlocker> result = TaskBlockers.Open(task, Lookup(done, open));

        Assert.Equal(open.Id, Assert.Single(result).Id);
    }

    /// <summary>A blocker the lookup cannot find blocks nothing.</summary>
    [Fact]
    public void Open_MissingBlocker_BlocksNothing()
    {
        TaskItem task = TestTasks.Make(id: "PLAT-0001", blockedBy: [new TaskId("PLAT", 9)]);

        Assert.Empty(TaskBlockers.Open(task, Lookup()));
    }

    /// <summary>The open blockers follow the order of the Task's blocked_by, and each carries its status and assignee.</summary>
    [Fact]
    public void Open_FollowsBlockedByOrderAndCarriesAssignee()
    {
        TaskItem first = TestTasks.Make(id: "PLAT-0005", status: TaskState.InProgress, assignee: "Ann");
        TaskItem second = TestTasks.Make(id: "PLAT-0002", status: TaskState.Backlog);
        TaskItem task = TestTasks.Make(id: "PLAT-0001", blockedBy: [first.Id, second.Id]);

        IReadOnlyList<OpenBlocker> result = TaskBlockers.Open(task, Lookup(second, first));

        Assert.Equal([first.Id, second.Id], result.Select(blocker => blocker.Id));
        Assert.Equal(["Ann", null], result.Select(blocker => blocker.Assignee));
        Assert.Equal([TaskState.InProgress, TaskState.Backlog], result.Select(blocker => blocker.Status));
    }
}

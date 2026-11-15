using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;

namespace Agency.Huddle.Tests.Tasks.Views;

/// <summary>
/// Tests for <see cref="TaskQuery.NeedsYou"/>, the assignment rule (open and assigned to the Human) that the Feed's
/// <c>TaskAssignedToYou</c> uses and that <see cref="TaskQuery.NeedsHuman"/> builds on for the "need you" count and the pill.
/// </summary>
public sealed class TaskQueryNeedsYouTests
{
    /// <summary>An open Task assigned to the Human needs them, in every open state.</summary>
    /// <param name="status">The Task's state.</param>
    [Theory]
    [InlineData(TaskState.Backlog)]
    [InlineData(TaskState.ToDo)]
    [InlineData(TaskState.InProgress)]
    [InlineData(TaskState.Review)]
    public void NeedsYou_OpenAndAssignedToTheHuman_IsTrue(TaskState status)
    {
        TaskItem task = TestTasks.Make(id: "PLAT-0001", status: status, assignee: "Sam");

        Assert.True(TaskQuery.NeedsYou(task, "Sam"));
    }

    /// <summary>A closed Task never needs the Human, even when it is theirs.</summary>
    /// <param name="status">A terminal state.</param>
    [Theory]
    [InlineData(TaskState.Done)]
    [InlineData(TaskState.Cancelled)]
    [InlineData(TaskState.Duplicate)]
    [InlineData(TaskState.Rejected)]
    public void NeedsYou_Terminal_IsFalse(TaskState status)
    {
        TaskItem task = TestTasks.Make(id: "PLAT-0001", status: status, assignee: "Sam");

        Assert.False(TaskQuery.NeedsYou(task, "Sam"));
    }

    /// <summary>The assignee is matched ignoring case, as Names are everywhere else.</summary>
    [Fact]
    public void NeedsYou_AssigneeDiffersOnlyInCase_IsTrue()
    {
        TaskItem task = TestTasks.Make(id: "PLAT-0001", status: TaskState.ToDo, assignee: "sam");

        Assert.True(TaskQuery.NeedsYou(task, "Sam"));
    }

    /// <summary>A Task assigned to an Agent, or to nobody, does not need the Human.</summary>
    /// <param name="assignee">The assignee, or <see langword="null"/> for none.</param>
    [Theory]
    [InlineData("Nova")]
    [InlineData(null)]
    public void NeedsYou_SomeoneElsesOrUnassigned_IsFalse(string? assignee)
    {
        TaskItem task = TestTasks.Make(id: "PLAT-0001", status: TaskState.ToDo, assignee: assignee);

        Assert.False(TaskQuery.NeedsYou(task, "Sam"));
    }
}

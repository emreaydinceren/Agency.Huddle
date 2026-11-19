using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;

namespace Agency.Huddle.Tests.Tasks.Views;

/// <summary>Tests that <see cref="TaskQuery.DefaultOrder"/> is the order <see cref="TaskQuery.Sort"/> applies when given no keys.</summary>
public sealed class TaskQueryDefaultOrderTests
{
    /// <summary>Sort with no keys equals ordering by DefaultOrder on a set mixing rank, priority, due date and id.</summary>
    [Fact]
    public void Sort_NoKeys_EqualsOrderByDefaultOrder()
    {
        IReadOnlyList<TaskItem> items =
        [
            TestTasks.Make(id: "PLAT-0001", priority: TaskPriority.Low),
            TestTasks.Make(id: "PLAT-0002", priority: TaskPriority.High, dueDate: new DateOnly(2030, 2, 1)),
            TestTasks.Make(id: "PLAT-0003", priority: TaskPriority.High, dueDate: new DateOnly(2030, 1, 1)),
            TestTasks.Make(id: "PLAT-0004", priority: TaskPriority.Urgent) with { Rank = 20 },
            TestTasks.Make(id: "PLAT-0005", priority: TaskPriority.Low) with { Rank = 10 },
            TestTasks.Make(id: "PLAT-0006", priority: TaskPriority.High, dueDate: new DateOnly(2030, 1, 1)),
        ];

        IReadOnlyList<TaskItem> sorted = TaskQuery.Sort(items, []);

        Assert.Equal(items.OrderBy(item => item, TaskQuery.DefaultOrder).Select(item => item.Id), sorted.Select(item => item.Id));
        Assert.Equal(new TaskId("PLAT", 5), sorted[0].Id);
    }
}

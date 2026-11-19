using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;

namespace Agency.Huddle.Tests.Tasks.Views;

/// <summary>Tests for the Rank dimension of <see cref="TaskQuery.Sort"/>: rank first by default, unranked Tasks last in both directions, and the old tiebreak between equal ranks.</summary>
public sealed class TaskQueryRankTests
{
    /// <summary>A Task with the given id, rank, priority and due date.</summary>
    private static TaskItem Make(string id, int? rank, TaskPriority priority = TaskPriority.Medium, DateOnly? dueDate = null) =>
        TestTasks.Make(id: id, priority: priority, dueDate: dueDate) with { Rank = rank };

    /// <summary>The ids of <paramref name="items"/> after sorting with <paramref name="keys"/>.</summary>
    private static string[] Sorted(IReadOnlyList<TaskItem> items, params SortKey[] keys) =>
        [.. TaskQuery.Sort(items, keys).Select(t => t.Id.ToString())];

    /// <summary>With no explicit keys, ranked Tasks come first in rank order, whatever their priority, and unranked ones follow.</summary>
    [Fact]
    public void Sort_NoKeys_RankedAscendingThenUnranked()
    {
        IReadOnlyList<TaskItem> items =
        [
            Make("PLAT-0001", null, TaskPriority.Urgent),
            Make("PLAT-0002", 20, TaskPriority.Urgent),
            Make("PLAT-0003", 10, TaskPriority.Low),
        ];

        Assert.Equal(["PLAT-0003", "PLAT-0002", "PLAT-0001"], Sorted(items));
    }

    /// <summary>Unranked Tasks keep the old order among themselves: priority descending, due date ascending, id ascending.</summary>
    [Fact]
    public void Sort_NoKeys_UnrankedFallBackToPriorityDueAndId()
    {
        IReadOnlyList<TaskItem> items =
        [
            Make("PLAT-0001", null, TaskPriority.Low),
            Make("PLAT-0002", null, TaskPriority.High, new DateOnly(2030, 2, 1)),
            Make("PLAT-0003", null, TaskPriority.High, new DateOnly(2030, 1, 1)),
            Make("PLAT-0004", null, TaskPriority.High, new DateOnly(2030, 1, 1)),
        ];

        Assert.Equal(["PLAT-0003", "PLAT-0004", "PLAT-0002", "PLAT-0001"], Sorted(items));
    }

    /// <summary>Equal ranks tie-break by priority descending, then due date ascending, then id ascending.</summary>
    [Fact]
    public void Sort_NoKeys_EqualRanksFallBackToPriorityDueAndId()
    {
        IReadOnlyList<TaskItem> items =
        [
            Make("PLAT-0001", 5, TaskPriority.Low),
            Make("PLAT-0002", 5, TaskPriority.Urgent, new DateOnly(2030, 2, 1)),
            Make("PLAT-0003", 5, TaskPriority.Urgent, new DateOnly(2030, 1, 1)),
            Make("PLAT-0004", 5, TaskPriority.Low),
        ];

        Assert.Equal(["PLAT-0003", "PLAT-0002", "PLAT-0001", "PLAT-0004"], Sorted(items));
    }

    /// <summary>An explicit sort key wins over the default rank order.</summary>
    [Fact]
    public void Sort_ExplicitKey_BeatsRank()
    {
        IReadOnlyList<TaskItem> items =
        [
            Make("PLAT-0001", 1, TaskPriority.Low),
            Make("PLAT-0002", 2, TaskPriority.Urgent),
        ];

        Assert.Equal(["PLAT-0002", "PLAT-0001"], Sorted(items, new SortKey("priority", SortDirection.Descending)));
    }

    /// <summary>Sorting by rank ascending puts unranked Tasks last.</summary>
    [Fact]
    public void Sort_ByRankAscending_UnrankedLast()
    {
        IReadOnlyList<TaskItem> items = [Make("PLAT-0001", null), Make("PLAT-0002", 3), Make("PLAT-0003", 1)];

        Assert.Equal(["PLAT-0003", "PLAT-0002", "PLAT-0001"], Sorted(items, new SortKey("rank", SortDirection.Ascending)));
    }

    /// <summary>Sorting by rank descending reverses the ranked Tasks but still puts unranked Tasks last.</summary>
    [Fact]
    public void Sort_ByRankDescending_UnrankedStillLast()
    {
        IReadOnlyList<TaskItem> items = [Make("PLAT-0001", null), Make("PLAT-0002", 3), Make("PLAT-0003", 1)];

        Assert.Equal(["PLAT-0002", "PLAT-0003", "PLAT-0001"], Sorted(items, new SortKey("rank", SortDirection.Descending)));
    }
}

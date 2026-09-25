using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;

namespace Agency.Huddle.Tests.Tasks.Views;

/// <summary>Tests for <see cref="TaskQuery.Sort"/>, <see cref="TaskQuery.GroupLabel"/> and <see cref="TaskQuery.Group"/> (Spec §12.5).</summary>
public sealed class TaskQuerySortGroupTests
{
    /// <summary>With no explicit keys, Sort falls back to priority descending, then due date ascending with nulls last, then id ascending.</summary>
    [Fact]
    public void Sort_NoKeys_DefaultsToPriorityDescendingThenDueDateAscendingThenId()
    {
        TaskItem lowNoDue = TestTasks.Make(id: "PLAT-0002", priority: TaskPriority.Low);
        TaskItem urgentNoDue = TestTasks.Make(id: "PLAT-0001", priority: TaskPriority.Urgent);
        TaskItem urgentWithDue = TestTasks.Make(id: "PLAT-0003", priority: TaskPriority.Urgent, dueDate: new DateOnly(2026, 1, 1));

        IReadOnlyList<TaskItem> result = TaskQuery.Sort([lowNoDue, urgentNoDue, urgentWithDue], []);

        Assert.Equal(["PLAT-0003", "PLAT-0001", "PLAT-0002"], result.Select(t => t.Id.ToString()));
    }

    /// <summary>Several explicit keys apply in order before the default tiebreaker.</summary>
    [Fact]
    public void Sort_MultipleKeys_AppliedInOrder()
    {
        TaskItem toDoUrgent = TestTasks.Make(id: "PLAT-0001", status: TaskState.ToDo, priority: TaskPriority.Urgent);
        TaskItem toDoLow = TestTasks.Make(id: "PLAT-0002", status: TaskState.ToDo, priority: TaskPriority.Low);
        TaskItem doneUrgent = TestTasks.Make(id: "PLAT-0003", status: TaskState.Done, priority: TaskPriority.Urgent);

        IReadOnlyList<SortKey> keys = [new("status", SortDirection.Ascending), new("priority", SortDirection.Descending)];
        IReadOnlyList<TaskItem> result = TaskQuery.Sort([doneUrgent, toDoLow, toDoUrgent], keys);

        Assert.Equal(["PLAT-0001", "PLAT-0002", "PLAT-0003"], result.Select(t => t.Id.ToString()));
    }

    /// <summary>Status sorts by declaration order, not alphabetically.</summary>
    [Fact]
    public void Sort_ByStatus_UsesDeclarationOrder()
    {
        TaskItem rejected = TestTasks.Make(id: "PLAT-0001", status: TaskState.Rejected);
        TaskItem backlog = TestTasks.Make(id: "PLAT-0002", status: TaskState.Backlog);
        TaskItem review = TestTasks.Make(id: "PLAT-0003", status: TaskState.Review);

        IReadOnlyList<SortKey> keys = [new("status", SortDirection.Ascending)];
        IReadOnlyList<TaskItem> result = TaskQuery.Sort([rejected, backlog, review], keys);

        Assert.Equal(["PLAT-0002", "PLAT-0003", "PLAT-0001"], result.Select(t => t.Id.ToString()));
    }

    /// <summary>Priority sorts by severity, not alphabetically.</summary>
    [Fact]
    public void Sort_ByPriority_UsesSeverityOrder()
    {
        TaskItem urgent = TestTasks.Make(id: "PLAT-0001", priority: TaskPriority.Urgent);
        TaskItem low = TestTasks.Make(id: "PLAT-0002", priority: TaskPriority.Low);
        TaskItem high = TestTasks.Make(id: "PLAT-0003", priority: TaskPriority.High);

        IReadOnlyList<SortKey> keys = [new("priority", SortDirection.Ascending)];
        IReadOnlyList<TaskItem> result = TaskQuery.Sort([urgent, low, high], keys);

        Assert.Equal(["PLAT-0002", "PLAT-0003", "PLAT-0001"], result.Select(t => t.Id.ToString()));
    }

    /// <summary>A null due date sorts last whether the key is ascending or descending.</summary>
    [Fact]
    public void Sort_DescendingKey_NullsStillComeLast()
    {
        TaskItem noDue = TestTasks.Make(id: "PLAT-0001");
        TaskItem earlyDue = TestTasks.Make(id: "PLAT-0002", dueDate: new DateOnly(2026, 1, 1));
        TaskItem lateDue = TestTasks.Make(id: "PLAT-0003", dueDate: new DateOnly(2026, 6, 1));

        IReadOnlyList<SortKey> keys = [new("due_date", SortDirection.Descending)];
        IReadOnlyList<TaskItem> result = TaskQuery.Sort([earlyDue, noDue, lateDue], keys);

        Assert.Equal(["PLAT-0003", "PLAT-0002", "PLAT-0001"], result.Select(t => t.Id.ToString()));
    }

    /// <summary>GroupLabel labels a null Assignee as "Unassigned".</summary>
    [Fact]
    public void GroupLabel_AssigneeNull_ReturnsUnassigned()
    {
        TaskItem task = TestTasks.Make(assignee: null);

        string label = TaskQuery.GroupLabel(task, TaskGroupField.Assignee, teamAlsoGrouped: false);

        Assert.Equal("Unassigned", label);
    }

    /// <summary>GroupLabel labels a null Project as "No project".</summary>
    [Fact]
    public void GroupLabel_ProjectNull_ReturnsNoProject()
    {
        TaskItem task = TestTasks.Make(location: new("Platform", null, false));

        string label = TaskQuery.GroupLabel(task, TaskGroupField.Project, teamAlsoGrouped: false);

        Assert.Equal("No project", label);
    }

    /// <summary>Grouping by Project without also grouping by Team labels each group "Team / Project".</summary>
    [Fact]
    public void GroupLabel_ProjectWithoutTeamGrouping_ReturnsTeamSlashProject()
    {
        TaskItem task = TestTasks.Make(location: new("Platform", "Auth v2", false));

        string label = TaskQuery.GroupLabel(task, TaskGroupField.Project, teamAlsoGrouped: false);

        Assert.Equal("Platform / Auth v2", label);
    }

    /// <summary>Grouping by Project while also grouping by Team labels each group with the Project's name alone.</summary>
    [Fact]
    public void GroupLabel_ProjectWithTeamGrouping_ReturnsProjectNameOnly()
    {
        TaskItem task = TestTasks.Make(location: new("Platform", "Auth v2", false));

        string label = TaskQuery.GroupLabel(task, TaskGroupField.Project, teamAlsoGrouped: true);

        Assert.Equal("Auth v2", label);
    }

    /// <summary>Two levels of grouping nest, with each node's Count matching the size of its subtree.</summary>
    [Fact]
    public void Group_TwoLevels_NestWithCorrectCounts()
    {
        List<TaskItem> tasks =
        [
            TestTasks.Make(id: "PLAT-0001", assignee: "Nova", location: new("Platform", null, false)),
            TestTasks.Make(id: "PLAT-0002", assignee: "Nova", location: new("Platform", null, false)),
            TestTasks.Make(id: "PLAT-0003", assignee: "Sable", location: new("Platform", null, false)),
            TestTasks.Make(id: "GROW-0001", assignee: "Nova", location: new("Growth", null, false)),
        ];

        TaskGroupNode root = TaskQuery.Group(tasks, [TaskGroupField.Team, TaskGroupField.Assignee]);

        Assert.Equal(4, root.Count);
        Assert.Equal(2, root.Children.Count);

        TaskGroupNode growth = root.Children[0];
        Assert.Equal("Growth", growth.Label);
        Assert.Equal(1, growth.Count);

        TaskGroupNode platform = root.Children[1];
        Assert.Equal("Platform", platform.Label);
        Assert.Equal(3, platform.Count);
        Assert.Equal(2, platform.Children.Count);
        Assert.Equal("Nova", platform.Children[0].Label);
        Assert.Equal(2, platform.Children[0].Count);
        Assert.Equal("Sable", platform.Children[1].Label);
        Assert.Equal(1, platform.Children[1].Count);
    }

    /// <summary>The Unassigned group is sorted last among its siblings, even ordinally out of place.</summary>
    [Fact]
    public void Group_ByAssignee_UnassignedComesLast()
    {
        List<TaskItem> tasks =
        [
            TestTasks.Make(id: "PLAT-0001", assignee: null),
            TestTasks.Make(id: "PLAT-0002", assignee: "Zara"),
            TestTasks.Make(id: "PLAT-0003", assignee: "Amy"),
        ];

        TaskGroupNode root = TaskQuery.Group(tasks, [TaskGroupField.Assignee]);

        Assert.Equal(["Amy", "Zara", "Unassigned"], root.Children.Select(c => c.Label));
    }

    /// <summary>Only groups with at least one Task appear; no empty groups are produced.</summary>
    [Fact]
    public void Group_ByTeam_ProducesNoEmptyGroups()
    {
        List<TaskItem> tasks =
        [
            TestTasks.Make(id: "PLAT-0001", location: new("Platform", null, false)),
            TestTasks.Make(id: "GROW-0001", location: new("Growth", null, false)),
        ];

        TaskGroupNode root = TaskQuery.Group(tasks, [TaskGroupField.Team]);

        Assert.Equal(2, root.Children.Count);
        Assert.Equal(["Growth", "Platform"], root.Children.Select(c => c.Label));
    }

    /// <summary>A Teammate literally named "Unassigned" gets a group of its own, kept apart from the null-Assignee group, which still sorts last.</summary>
    [Fact]
    public void Group_TeammateNamedUnassigned_KeptApartFromNullGroup()
    {
        List<TaskItem> tasks =
        [
            TestTasks.Make(id: "PLAT-0001", assignee: null),
            TestTasks.Make(id: "PLAT-0002", assignee: "Unassigned"),
        ];

        TaskGroupNode root = TaskQuery.Group(tasks, [TaskGroupField.Assignee]);

        Assert.Equal(2, root.Children.Count);
        Assert.Equal(["Unassigned", "Unassigned"], root.Children.Select(c => c.Label));
        Assert.Equal(["PLAT-0002"], root.Children[0].Items.Select(t => t.Id.ToString()));
        Assert.Equal(["PLAT-0001"], root.Children[1].Items.Select(t => t.Id.ToString()));
    }

    /// <summary>
    /// A Project literally named "No project" gets a group of its own, kept apart from the null-Project group, which
    /// still sorts last. Grouped alongside Team, so the Project label is bare and the two labels read identically.
    /// </summary>
    [Fact]
    public void Group_ProjectNamedNoProject_KeptApartFromNullGroup()
    {
        List<TaskItem> tasks =
        [
            TestTasks.Make(id: "PLAT-0001", location: new("Platform", null, false)),
            TestTasks.Make(id: "PLAT-0002", location: new("Platform", "No project", false)),
        ];

        TaskGroupNode root = TaskQuery.Group(tasks, [TaskGroupField.Team, TaskGroupField.Project]);

        TaskGroupNode platform = Assert.Single(root.Children);
        Assert.Equal(2, platform.Children.Count);
        Assert.Equal(["No project", "No project"], platform.Children.Select(c => c.Label));
        Assert.Equal(["PLAT-0002"], platform.Children[0].Items.Select(t => t.Id.ToString()));
        Assert.Equal(["PLAT-0001"], platform.Children[1].Items.Select(t => t.Id.ToString()));
    }
}

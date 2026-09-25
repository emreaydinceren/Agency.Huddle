using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;

namespace Agency.Huddle.Tests.Tasks.Views;

/// <summary>Tests for <see cref="TaskQuery.Filter"/> (Spec §12.5).</summary>
public sealed class TaskQueryFilterTests
{
    /// <summary>Ten hand-built Tasks spanning teams, projects, assignees, states, priorities and scope.</summary>
    private static IReadOnlyList<TaskItem> Fixture() =>
    [
        TestTasks.Make(id: "PLAT-0001", title: "Auth login bug", status: TaskState.ToDo, priority: TaskPriority.High,
            assignee: "Nova", location: new("Platform", "Auth v2", false)),
        TestTasks.Make(id: "PLAT-0002", title: "Board layout", status: TaskState.InProgress, priority: TaskPriority.Urgent,
            assignee: "Sable", location: new("Platform", "Auth v2", false)),
        TestTasks.Make(id: "PLAT-0003", title: "Views JSON", status: TaskState.Review, priority: TaskPriority.Medium,
            assignee: null, location: new("Platform", null, false)),
        TestTasks.Make(id: "PLAT-0004", title: "Old cleanup", status: TaskState.Done, priority: TaskPriority.Low,
            assignee: "Nova", location: new("Platform", null, true)),
        TestTasks.Make(id: "GROW-0001", title: "Signup funnel", status: TaskState.Backlog, priority: TaskPriority.Medium,
            assignee: "Human", location: new("Growth", "Onboarding", false)),
        TestTasks.Make(id: "GROW-0002", title: "Pricing page", status: TaskState.ToDo, priority: TaskPriority.High,
            assignee: "Sable", location: new("Growth", "Onboarding", false)),
        TestTasks.Make(id: "GROW-0003", title: "Analytics dash", status: TaskState.Cancelled, priority: TaskPriority.Low,
            assignee: null, location: new("Growth", null, true)),
        TestTasks.Make(id: "GROW-0004", title: "Referral program", status: TaskState.InProgress, priority: TaskPriority.Urgent,
            assignee: "Human", location: new("Growth", null, false)),
        TestTasks.Make(id: "PLAT-0005", title: "Samlon setup", status: TaskState.ToDo, priority: TaskPriority.Medium,
            assignee: "Nova", location: new("Platform", "Auth v2", false)),
        TestTasks.Make(id: "PLAT-0006", title: "Rejected idea", status: TaskState.Rejected, priority: TaskPriority.Low,
            assignee: "Sable", location: new("Platform", null, true)),
    ];

    /// <summary>An empty filter returns every Task in scope.</summary>
    [Fact]
    public void Filter_EmptyFilter_ReturnsEverythingInScope()
    {
        IReadOnlyList<TaskItem> result = TaskQuery.Filter(Fixture(), ViewScope.Active, new TaskFilter(), null, "Human");

        Assert.Equal(Fixture().Count(t => !t.Location.Closed), result.Count);
    }

    /// <summary>The scope splits Active Tasks from Closed ones.</summary>
    [Fact]
    public void Filter_ClosedScope_ReturnsOnlyClosedTasks()
    {
        IReadOnlyList<TaskItem> result = TaskQuery.Filter(Fixture(), ViewScope.Closed, new TaskFilter(), null, "Human");

        Assert.All(result, task => Assert.True(task.Location.Closed));
        Assert.Equal(Fixture().Count(t => t.Location.Closed), result.Count);
    }

    /// <summary>Values within one dimension are OR'd.</summary>
    [Fact]
    public void Filter_MultipleTeams_OrsWithinDimension()
    {
        TaskFilter filter = new() { Teams = ["Platform", "Growth"] };

        IReadOnlyList<TaskItem> result = TaskQuery.Filter(Fixture(), ViewScope.Active, filter, null, "Human");

        Assert.Equal(Fixture().Count(t => !t.Location.Closed), result.Count);
    }

    /// <summary>Dimensions are AND'd together.</summary>
    [Fact]
    public void Filter_TeamAndPriority_AndsAcrossDimensions()
    {
        TaskFilter filter = new() { Teams = ["Platform"], Priorities = [TaskPriority.High] };

        IReadOnlyList<TaskItem> result = TaskQuery.Filter(Fixture(), ViewScope.Active, filter, null, "Human");

        TaskItem only = Assert.Single(result);
        Assert.Equal("PLAT-0001", only.Id.ToString());
    }

    /// <summary><c>@me</c> resolves to the Human's name.</summary>
    [Fact]
    public void Filter_AtMe_ResolvesToHumanName()
    {
        TaskFilter filter = new() { Assignees = ["@me"] };

        IReadOnlyList<TaskItem> result = TaskQuery.Filter(Fixture(), ViewScope.Active, filter, null, "Human");

        Assert.All(result, task => Assert.Equal("Human", task.Assignee));
        Assert.Equal(2, result.Count);
    }

    /// <summary><c>@unassigned</c> matches Tasks with a null Assignee.</summary>
    [Fact]
    public void Filter_AtUnassigned_MatchesNullAssignee()
    {
        TaskFilter filter = new() { Assignees = ["@unassigned"] };

        IReadOnlyList<TaskItem> result = TaskQuery.Filter(Fixture(), ViewScope.Active, filter, null, "Human");

        TaskItem only = Assert.Single(result);
        Assert.Null(only.Assignee);
    }

    /// <summary>A ProjectRef with a null Project matches only Tasks directly under that Team.</summary>
    [Fact]
    public void Filter_ProjectRefWithNullProject_MatchesTasksWithoutProject()
    {
        TaskFilter filter = new() { Projects = [new ProjectRef("Growth", null)] };

        IReadOnlyList<TaskItem> result = TaskQuery.Filter(Fixture(), ViewScope.Active, filter, null, "Human");

        TaskItem only = Assert.Single(result);
        Assert.Equal("Growth", only.Location.Team);
        Assert.Null(only.Location.Project);
    }

    /// <summary>Assignee names match ignoring case.</summary>
    [Fact]
    public void Filter_AssigneeName_MatchesIgnoringCase()
    {
        TaskFilter filter = new() { Assignees = ["nova"] };

        IReadOnlyList<TaskItem> result = TaskQuery.Filter(Fixture(), ViewScope.Active, filter, null, "Human");

        Assert.All(result, task => Assert.Equal("Nova", task.Assignee));
        Assert.Equal(2, result.Count);
    }

    /// <summary>Search matches the id, case-insensitively.</summary>
    [Fact]
    public void Filter_SearchMatchesId_CaseInsensitively()
    {
        IReadOnlyList<TaskItem> result = TaskQuery.Filter(Fixture(), ViewScope.Active, new TaskFilter(), "plat-0001", "Human");

        TaskItem only = Assert.Single(result);
        Assert.Equal("PLAT-0001", only.Id.ToString());
    }

    /// <summary>Search matches the title, case-insensitively.</summary>
    [Fact]
    public void Filter_SearchMatchesTitle_CaseInsensitively()
    {
        IReadOnlyList<TaskItem> result = TaskQuery.Filter(Fixture(), ViewScope.Active, new TaskFilter(), "SAMLON", "Human");

        TaskItem only = Assert.Single(result);
        Assert.Equal("PLAT-0005", only.Id.ToString());
    }

    /// <summary>Search does not match other fields, such as the description.</summary>
    [Fact]
    public void Filter_SearchDoesNotMatchDescription()
    {
        List<TaskItem> tasks = [.. Fixture()];
        tasks[0] = tasks[0] with { Description = "This mentions unicorn somewhere in the body." };

        IReadOnlyList<TaskItem> result = TaskQuery.Filter(tasks, ViewScope.Active, new TaskFilter(), "unicorn", "Human");

        Assert.Empty(result);
    }
}

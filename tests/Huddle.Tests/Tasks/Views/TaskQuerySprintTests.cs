using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;

namespace Agency.Huddle.Tests.Tasks.Views;

/// <summary>Tests for the Sprint dimension of <see cref="TaskQuery"/> and <see cref="BoardLayout"/> (Sprints spec §11.2 to §11.4).</summary>
public sealed class TaskQuerySprintTests
{
    /// <summary>S1, S2, S3 and S9 Tasks and one with no Sprint, all in the same Team.</summary>
    private static IReadOnlyList<TaskItem> Fixture() =>
    [
        TestTasks.Make(id: "PLAT-0001", sprint: new SprintId(1)),
        TestTasks.Make(id: "PLAT-0002", sprint: new SprintId(2)),
        TestTasks.Make(id: "PLAT-0003", sprint: new SprintId(3), priority: TaskPriority.High),
        TestTasks.Make(id: "PLAT-0004"),
        TestTasks.Make(id: "PLAT-0005", sprint: new SprintId(9)),
    ];

    /// <summary>Runs the filter over <see cref="Fixture"/> and returns the matching ids, ordered.</summary>
    /// <param name="filter">The filter to apply.</param>
    /// <param name="current">The current Sprint, if any.</param>
    private static string[] Ids(TaskFilter filter, SprintId? current) =>
        [.. TaskQuery.Filter(Fixture(), ViewScope.Active, filter, null, "Human", current).Select(t => t.Id.ToString()).Order(StringComparer.Ordinal)];

    /// <summary>An empty Sprints dimension matches every Task.</summary>
    [Fact]
    public void Filter_NoSprints_MatchesEverything() =>
        Assert.Equal(5, Ids(new TaskFilter(), null).Length);

    /// <summary><c>@current</c> matches the Tasks of the current Sprint only.</summary>
    [Fact]
    public void Filter_AtCurrent_MatchesCurrentSprint() =>
        Assert.Equal(["PLAT-0003"], Ids(new TaskFilter { Sprints = ["@current"] }, new SprintId(3)));

    /// <summary><c>@current</c> with no current Sprint matches nothing.</summary>
    [Fact]
    public void Filter_AtCurrentWithNullCurrent_MatchesNothing() =>
        Assert.Empty(Ids(new TaskFilter { Sprints = ["@current"] }, null));

    /// <summary><c>@none</c> matches Tasks with no Sprint, and the tokens ignore case.</summary>
    [Fact]
    public void Filter_AtNone_MatchesTasksWithoutSprint() =>
        Assert.Equal(["PLAT-0004"], Ids(new TaskFilter { Sprints = ["@NONE"] }, null));

    /// <summary>An id matches that Sprint, and an id that is not in any file still matches.</summary>
    [Fact]
    public void Filter_Id_MatchesIncludingMissing()
    {
        Assert.Equal(["PLAT-0002"], Ids(new TaskFilter { Sprints = ["S2"] }, null));
        Assert.Equal(["PLAT-0005"], Ids(new TaskFilter { Sprints = ["s9"] }, null));
    }

    /// <summary>Values in the dimension are OR'd.</summary>
    [Fact]
    public void Filter_Several_AreOrd() =>
        Assert.Equal(["PLAT-0001", "PLAT-0004"], Ids(new TaskFilter { Sprints = ["S1", "@none"] }, null));

    /// <summary>The Sprint dimension is AND'd with the others.</summary>
    [Fact]
    public void Filter_SprintAndPriority_AreAnded() =>
        Assert.Empty(Ids(new TaskFilter { Sprints = ["S1"], Priorities = [TaskPriority.High] }, null));

    /// <summary>Sorting by sprint orders by number ascending, with no Sprint last.</summary>
    [Fact]
    public void Sort_BySprintAscending_NoSprintLast()
    {
        IReadOnlyList<TaskItem> result = TaskQuery.Sort(Fixture(), [new SortKey("sprint", SortDirection.Ascending)]);

        Assert.Equal(["PLAT-0001", "PLAT-0002", "PLAT-0003", "PLAT-0005", "PLAT-0004"], result.Select(t => t.Id.ToString()));
    }

    /// <summary>Sorting by sprint descending still puts no Sprint last.</summary>
    [Fact]
    public void Sort_BySprintDescending_NoSprintStillLast()
    {
        IReadOnlyList<TaskItem> result = TaskQuery.Sort(Fixture(), [new SortKey("sprint", SortDirection.Descending)]);

        Assert.Equal(["PLAT-0005", "PLAT-0003", "PLAT-0002", "PLAT-0001", "PLAT-0004"], result.Select(t => t.Id.ToString()));
    }

    /// <summary>A resolved id is labelled with its name.</summary>
    [Fact]
    public void GroupLabel_Resolved_IsName() =>
        Assert.Equal("Auth hardening", TaskQuery.GroupLabel(Fixture()[1], TaskGroupField.Sprint, false, static id => id.Number == 2 ? "Auth hardening" : null));

    /// <summary>An id the resolver does not know is labelled as missing.</summary>
    [Fact]
    public void GroupLabel_Unresolved_IsMissing() =>
        Assert.Equal("S9 (missing)", TaskQuery.GroupLabel(Fixture()[4], TaskGroupField.Sprint, false, static _ => null));

    /// <summary>With no resolver the label is the bare id.</summary>
    [Fact]
    public void GroupLabel_NoResolver_IsBareId() =>
        Assert.Equal("S9", TaskQuery.GroupLabel(Fixture()[4], TaskGroupField.Sprint, false));

    /// <summary>A Task with no Sprint is in the No sprint group.</summary>
    [Fact]
    public void GroupLabel_NoSprint_IsNoSprint() =>
        Assert.Equal("No sprint", TaskQuery.GroupLabel(Fixture()[3], TaskGroupField.Sprint, false));

    /// <summary>Sprint groups run newest first with No sprint last, whatever the labels say.</summary>
    [Fact]
    public void Group_BySprint_NewestFirstNoSprintLast()
    {
        TaskGroupNode root = TaskQuery.Group(
            Fixture(), [TaskGroupField.Sprint], static id => id.Number == 1 ? "Zebra" : $"Name{id.Number}");

        Assert.Equal(["Name9", "Name3", "Name2", "Zebra", "No sprint"], root.Children.Select(c => c.Label));
    }

    /// <summary>The Board passes the resolver on, so its lanes are named and ordered by Sprint.</summary>
    [Fact]
    public void BoardLayoutBuild_BySprint_UsesResolver()
    {
        TaskView view = new()
        {
            Id = "b",
            Name = "B",
            Kind = ViewKind.Kanban,
            Grouping = [TaskGroupField.Sprint],
            Columns = BoardLayout.DefaultColumns,
        };

        BoardModel model = BoardLayout.Build(Fixture(), view, static id => $"Name{id.Number}");

        Assert.Equal(["Name9", "Name3", "Name2", "Name1", "No sprint"], model.Lanes.Select(l => l.Label));
    }
}

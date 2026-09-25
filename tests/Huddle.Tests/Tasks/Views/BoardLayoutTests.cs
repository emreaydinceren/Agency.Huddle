using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;

namespace Agency.Huddle.Tests.Tasks.Views;

/// <summary>Tests for <see cref="BoardLayout"/> (Spec §12.6).</summary>
public sealed class BoardLayoutTests
{
    /// <summary>A View with no grouping produces exactly one lane, with the empty string as its Key.</summary>
    [Fact]
    public void Build_NoGrouping_ProducesOneLaneWithEmptyKey()
    {
        List<TaskItem> tasks = [TestTasks.Make(id: "PLAT-0001", status: TaskState.ToDo)];
        TaskView view = View(grouping: [], columns: [new("To Do", [TaskState.ToDo])]);

        BoardModel model = BoardLayout.Build(tasks, view);

        BoardLane lane = Assert.Single(model.Lanes);
        Assert.Equal("", lane.Key);
        Assert.Null(lane.Label);
    }

    /// <summary>With grouping, lanes come from the leaf groups, labelled with the path joined by " · ".</summary>
    [Fact]
    public void Build_WithGrouping_LanesComeFromLeafGroupsJoinedByMiddleDot()
    {
        List<TaskItem> tasks =
        [
            TestTasks.Make(id: "PLAT-0001", assignee: "Nova", status: TaskState.ToDo, location: new("Platform", null, false)),
            TestTasks.Make(id: "PLAT-0002", assignee: "Sable", status: TaskState.ToDo, location: new("Platform", null, false)),
            TestTasks.Make(id: "GROW-0001", assignee: "Nova", status: TaskState.ToDo, location: new("Growth", null, false)),
        ];
        TaskView view = View(grouping: [TaskGroupField.Team, TaskGroupField.Assignee], columns: [new("To Do", [TaskState.ToDo])]);

        BoardModel model = BoardLayout.Build(tasks, view);

        List<string?> labels = [.. model.Lanes.Select(lane => lane.Label).OrderBy(label => label, StringComparer.Ordinal)];
        Assert.Equal(["Growth · Nova", "Platform · Nova", "Platform · Sable"], labels);
    }

    /// <summary>Each lane's cells follow the View's columns, one cell per column, holding the Tasks in that column's states.</summary>
    [Fact]
    public void Build_Cells_FollowTheViewsColumns()
    {
        List<TaskItem> tasks =
        [
            TestTasks.Make(id: "PLAT-0001", status: TaskState.ToDo),
            TestTasks.Make(id: "PLAT-0002", status: TaskState.InProgress),
            TestTasks.Make(id: "PLAT-0003", status: TaskState.Done),
        ];
        BoardColumn doing = new("Doing", [TaskState.ToDo, TaskState.InProgress]);
        BoardColumn done = new("Done", [TaskState.Done]);
        TaskView view = View(grouping: [], columns: [doing, done]);

        BoardModel model = BoardLayout.Build(tasks, view);

        BoardLane lane = Assert.Single(model.Lanes);
        Assert.Equal(2, lane.Cells.Count);
        Assert.Equal(["PLAT-0001", "PLAT-0002"], lane.Cells[0].Items.Select(t => t.Id.ToString()));
        Assert.Equal(["PLAT-0003"], lane.Cells[1].Items.Select(t => t.Id.ToString()));
    }

    /// <summary>Hidden columns are excluded from VisibleColumns, and their Tasks are counted in HiddenTaskCount.</summary>
    [Fact]
    public void Build_HiddenColumns_ExcludedAndCountedInHiddenTaskCount()
    {
        List<TaskItem> tasks =
        [
            TestTasks.Make(id: "PLAT-0001", status: TaskState.ToDo),
            TestTasks.Make(id: "PLAT-0002", status: TaskState.Done),
            TestTasks.Make(id: "PLAT-0003", status: TaskState.Done),
        ];
        BoardColumn toDo = new("To Do", [TaskState.ToDo]);
        BoardColumn done = new("Done", [TaskState.Done], Hidden: true);
        TaskView view = View(grouping: [], columns: [toDo, done]);

        BoardModel model = BoardLayout.Build(tasks, view);

        BoardColumn only = Assert.Single(model.VisibleColumns);
        Assert.Equal("To Do", only.Label);
        Assert.Equal(2, model.HiddenTaskCount);
    }

    /// <summary>ZoneId and TryParseZone round-trip, even for a lane key containing "|" or ":".</summary>
    [Fact]
    public void ZoneId_TryParseZone_RoundTripsALaneKeyWithPipeAndColon()
    {
        string zoneId = BoardLayout.ZoneId("weird|key:value", TaskState.InProgress);

        bool parsed = BoardLayout.TryParseZone(zoneId, out string laneKey, out TaskState state);

        Assert.True(parsed);
        Assert.Equal("weird|key:value", laneKey);
        Assert.Equal(TaskState.InProgress, state);
    }

    /// <summary>Builds a minimal Board TaskView for the tests.</summary>
    private static TaskView View(IReadOnlyList<TaskGroupField> grouping, IReadOnlyList<BoardColumn> columns) => new()
    {
        Id = "test-view",
        Name = "Test",
        Kind = ViewKind.Board,
        Grouping = grouping,
        Columns = columns,
    };
}

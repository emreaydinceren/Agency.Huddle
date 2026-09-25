using System.Text.Json;
using System.Text.Json.Nodes;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;

namespace Agency.Huddle.Tests.Tasks.Views;

/// <summary>Tests pinning the View records (Spec §12.1) and the <c>views.json</c> shape (Spec §12.3).</summary>
public sealed class ViewJsonTests
{
    /// <summary>The Spec §12.3 example document, with one View.</summary>
    private const string SpecExampleJson = """
        {
          "version": 1,
          "views": [
            { "id": "3f2a9c…", "name": "Platform board", "description": "Engineering work in flight",
              "kind": "board", "scope": "active",
              "fields": ["assignee", "priority", "due_date"],
              "filter": { "teams": ["Platform"], "priorities": ["High", "Urgent"],
                          "projects": [{ "team": "Platform", "project": "Auth v2" }, { "team": "Platform", "project": null }],
                          "assignees": ["Nova", "@unassigned"] },
              "grouping": ["assignee"],
              "sort": [{ "field": "priority", "direction": "descending" }, { "field": "due_date", "direction": "ascending" }],
              "columns": [
                { "label": "Backlog", "states": ["Backlog"] }, { "label": "To Do", "states": ["To Do"] },
                { "label": "In Progress", "states": ["In Progress"] }, { "label": "Review", "states": ["Review"] },
                { "label": "Done", "states": ["Done"] },
                { "label": "Won't do", "states": ["Cancelled", "Duplicate", "Rejected"] } ] }
          ]
        }
        """;

    /// <summary>Deserialising the Spec §12.3 example reads every field of the View.</summary>
    [Fact]
    public void Deserialize_SpecExample_ReadsEveryField()
    {
        ViewsDocument? document = JsonSerializer.Deserialize<ViewsDocument>(SpecExampleJson, ViewJson.Options);

        Assert.NotNull(document);
        Assert.Equal(1, document.Version);
        TaskView view = Assert.Single(document.Views);

        Assert.Equal("3f2a9c…", view.Id);
        Assert.Equal("Platform board", view.Name);
        Assert.Equal("Engineering work in flight", view.Description);
        Assert.Equal(ViewKind.Board, view.Kind);
        Assert.Equal(ViewScope.Active, view.Scope);
        Assert.Equal(["assignee", "priority", "due_date"], view.Fields);

        Assert.Equal(["Platform"], view.Filter.Teams);
        Assert.Equal([TaskPriority.High, TaskPriority.Urgent], view.Filter.Priorities);
        Assert.Equal(
            [new ProjectRef("Platform", "Auth v2"), new ProjectRef("Platform", null)],
            view.Filter.Projects);
        Assert.Equal(["Nova", "@unassigned"], view.Filter.Assignees);

        Assert.Equal([TaskGroupField.Assignee], view.Grouping);

        Assert.Equal(
            [new SortKey("priority", SortDirection.Descending), new SortKey("due_date", SortDirection.Ascending)],
            view.Sort);

        Assert.Equal(6, view.Columns.Count);
        BoardColumn last = view.Columns[^1];
        Assert.Equal("Won't do", last.Label);
        Assert.Equal([TaskState.Cancelled, TaskState.Duplicate, TaskState.Rejected], last.States);
    }

    /// <summary>Enum members serialise as camelCase strings.</summary>
    [Fact]
    public void Serialize_EnumsAreCamelCaseStrings()
    {
        TaskView view = new()
        {
            Id = "v1",
            Name = "Board",
            Kind = ViewKind.Board,
            Sort = [new SortKey("priority", SortDirection.Descending)],
        };

        string json = JsonSerializer.Serialize(view, ViewJson.Options);

        JsonNode? root = JsonNode.Parse(json);
        Assert.NotNull(root);
        Assert.Equal("board", root["kind"]?.GetValue<string>());
        Assert.Equal("descending", root["sort"]?[0]?["direction"]?.GetValue<string>());
    }

    /// <summary>Task states serialise as their wire names, not a camelCase enum identifier.</summary>
    [Fact]
    public void Serialize_StatesUseWireNames()
    {
        TaskView view = new()
        {
            Id = "v1",
            Name = "Board",
            Kind = ViewKind.Board,
            Filter = new TaskFilter { States = [TaskState.InProgress] },
        };

        string json = JsonSerializer.Serialize(view, ViewJson.Options);

        JsonNode? root = JsonNode.Parse(json);
        Assert.NotNull(root);
        Assert.Equal("In Progress", root["filter"]?["states"]?[0]?.GetValue<string>());
    }

    /// <summary>Serialising and deserialising a View round-trips to identical text.</summary>
    [Fact]
    public void RoundTrip_IsStable()
    {
        TaskView view = new()
        {
            Id = "v1",
            Name = "Platform board",
            Description = "Engineering work in flight",
            Kind = ViewKind.Board,
            Scope = ViewScope.Active,
            Fields = ["assignee", "priority", "due_date"],
            Filter = new TaskFilter
            {
                Teams = ["Platform"],
                Projects = [new ProjectRef("Platform", "Auth v2"), new ProjectRef("Platform", null)],
                Assignees = ["Nova", "@unassigned"],
                Priorities = [TaskPriority.High, TaskPriority.Urgent],
            },
            Grouping = [TaskGroupField.Assignee],
            Sort = [new SortKey("priority", SortDirection.Descending), new SortKey("due_date", SortDirection.Ascending)],
            Columns =
            [
                new BoardColumn("Backlog", [TaskState.Backlog]),
                new BoardColumn("Won't do", [TaskState.Cancelled, TaskState.Duplicate, TaskState.Rejected]),
            ],
        };

        string first = JsonSerializer.Serialize(view, ViewJson.Options);
        TaskView? reparsed = JsonSerializer.Deserialize<TaskView>(first, ViewJson.Options);
        string second = JsonSerializer.Serialize(reparsed, ViewJson.Options);

        Assert.Equal(first, second);
    }
}

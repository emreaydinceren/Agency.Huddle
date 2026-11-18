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
              "kind": "kanban", "scope": "active",
              "fields": ["assignee", "priority", "due_date"],
              "filter": { "teams": ["Platform"], "priorities": ["High", "Urgent"],
                          "projects": [{ "team": "Platform", "project": "Auth v2" }, { "team": "Platform", "project": null }],
                          "assignees": ["Nova", "@unassigned"] },
              "grouping": ["assignee"],
              "sort": [{ "field": "priority", "direction": "descending" }, { "field": "due_date", "direction": "ascending" }],
              "columns": [
                { "label": "Backlog", "states": ["Backlog"] }, { "label": "To Do", "states": ["To Do"] },
                { "label": "In Progress", "states": ["In Progress"] },
                { "label": "Done", "states": ["Done"] },
                { "label": "Won't do", "states": ["Cancelled", "Duplicate", "Rejected"] } ] }
          ]
        }
        """;

    /// <summary>Deserialising the Spec §12.3 example reads every field of the View.</summary>
    [Fact]
    public void Deserialize_SpecExample_ReadsEveryField()
    {
        // The shape ViewStore reads: a JSON object with a version and a "views" array whose entries each deserialise as a TaskView.
        JsonObject? document = JsonNode.Parse(SpecExampleJson)?.AsObject();

        Assert.NotNull(document);
        Assert.Equal(1, document["version"]?.GetValue<int>());
        JsonArray? entries = document["views"]?.AsArray();
        Assert.NotNull(entries);
        JsonNode? entry = Assert.Single(entries);
        Assert.NotNull(entry);
        TaskView? view = entry.Deserialize<TaskView>(ViewJson.Options);
        Assert.NotNull(view);

        Assert.Equal("3f2a9c…", view.Id);
        Assert.Equal("Platform board", view.Name);
       Assert.Equal(ViewKind.Kanban, view.Kind);
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

        Assert.Equal(5, view.Columns.Count);
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
            Kind = ViewKind.Kanban,
            Sort = [new SortKey("priority", SortDirection.Descending)],
        };

        string json = JsonSerializer.Serialize(view, ViewJson.Options);

        JsonNode? root = JsonNode.Parse(json);
        Assert.NotNull(root);
        Assert.Equal("kanban", root["kind"]?.GetValue<string>());
        Assert.Equal("descending", root["sort"]?[0]?["direction"]?.GetValue<string>());
    }

    /// <summary>A views.json written before the rename, with "kind": "board", still reads as a Kanban View.</summary>
    [Theory]
    [InlineData("board")]
    [InlineData("Board")]
    [InlineData("kanban")]
    public void Deserialize_KanbanOrLegacyBoardKind_ReadsAsKanban(string wire)
    {
        string json = $$"""{ "id": "v1", "name": "Old board", "kind": "{{wire}}" }""";

        TaskView? view = JsonSerializer.Deserialize<TaskView>(json, ViewJson.Options);

        Assert.NotNull(view);
        Assert.Equal(ViewKind.Kanban, view.Kind);
    }

    /// <summary>A kind that is neither a List nor a Kanban is refused rather than read as some default.</summary>
    [Fact]
    public void Deserialize_UnknownKind_Throws()
    {
        const string json = """{ "id": "v1", "name": "Odd", "kind": "gantt" }""";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<TaskView>(json, ViewJson.Options));
    }

    /// <summary>Task states serialise as their wire names, not a camelCase enum identifier.</summary>
    [Fact]
    public void Serialize_StatesUseWireNames()
    {
        TaskView view = new()
        {
            Id = "v1",
            Name = "Board",
            Kind = ViewKind.Kanban,
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
           Kind = ViewKind.Kanban,
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

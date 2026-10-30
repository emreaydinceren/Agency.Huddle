using Agency.Huddle.Seeder.Model;

namespace Agency.Huddle.Seeder.Scenarios;

/// <summary>
/// A small software company and the family of the Human who runs it: three working Teams (Platform, Growth, Support) with
/// shared Agents, the household Team Home and Family (<see cref="SoftwareCoHome"/>), plus two edge-case Teams (Sandbox with
/// nobody in it, Archive with people but no Projects). Seeder spec §8 (SEED-SC-1 to -15) and §9 (SEED-HOME-1 to -9).
/// </summary>
internal sealed class SoftwareCoScenario : IScenario
{
    private const string ViewsJson = """
        {
          "version": 1,
          "views": [
            {
              "id": "platform-board",
              "name": "Platform board",
              "kind": "kanban",
              "scope": "active",
              "fields": ["priority", "assignee", "due_date"],
              "filter": { "teams": ["Platform"] },
              "grouping": ["project"],
              "sort": [],
              "columns": [
                { "label": "Backlog", "states": ["Backlog"] },
                { "label": "To Do", "states": ["To Do"] },
                { "label": "In Progress", "states": ["In Progress"] },
                { "label": "Review", "states": ["Review"] },
                { "label": "Done", "states": ["Done"] },
                { "label": "Won't do", "states": ["Cancelled", "Duplicate", "Rejected"] }
              ]
            },
            {
              "id": "growth-by-project",
              "name": "Growth by project",
              "kind": "list",
              "scope": "active",
              "fields": ["status", "priority", "assignee", "due_date"],
              "filter": { "teams": ["Growth"] },
              "grouping": ["project"],
              "sort": [{ "field": "due_date", "direction": "ascending" }],
              "columns": []
            },
            {
              "id": "my-open-work",
              "name": "My open work",
              "kind": "list",
              "scope": "active",
              "fields": ["team", "project", "status", "due_date"],
              "filter": { "assignees": ["@me"] },
              "grouping": [],
              "sort": [{ "field": "due_date", "direction": "ascending" }],
              "columns": []
            },
            {
              "id": "blocked-work",
              "name": "Blocked work",
              "kind": "list",
              "scope": "active",
              "fields": ["status", "assignee", "blocked_by"],
              "filter": { "blocked": "blocked" },
              "grouping": ["team"],
              "sort": [],
              "columns": []
            },
            {
              "id": "awaiting-review",
              "name": "Awaiting review",
              "kind": "list",
              "scope": "active",
              "fields": ["team", "priority", "due_date"],
              "filter": { "states": ["Review"] },
              "grouping": ["assignee"],
              "sort": [],
              "columns": []
            },
            {
              "id": "support-urgent",
              "name": "Support urgent",
              "kind": "list",
              "scope": "active",
              "fields": ["project", "status", "priority", "due_date"],
              "filter": { "teams": ["Support"], "priorities": ["High", "Urgent"] },
              "grouping": ["assignee"],
              "sort": [{ "field": "priority", "direction": "descending" }],
              "columns": []
            },
            {
              "id": "unassigned",
              "name": "Unassigned",
              "kind": "list",
              "scope": "active",
              "fields": ["team", "project", "priority", "status"],
              "filter": { "assignees": ["@unassigned"] },
              "grouping": [],
              "sort": [],
              "columns": []
            },
            {
              "id": "current-sprint",
              "name": "Current sprint",
              "kind": "list",
              "scope": "active",
              "fields": ["team", "status", "assignee", "sprint"],
              "filter": { "sprints": ["@current"] },
              "grouping": ["assignee"],
              "sort": [],
              "columns": []
            },
            {
              "id": "closed-work",
              "name": "Closed work",
              "kind": "list",
              "scope": "closed",
              "fields": ["team", "status", "assignee", "closed"],
              "filter": {},
              "grouping": ["team"],
              "sort": [{ "field": "closed", "direction": "descending" }],
              "columns": []
            },
            {
              "id": "home-this-week",
              "name": "Home this week",
              "kind": "list",
              "scope": "active",
              "fields": ["project", "status", "assignee", "due_date", "tags"],
              "filter": { "teams": ["Home and Family"] },
              "grouping": [],
              "sort": [{ "field": "due_date", "direction": "ascending" }],
              "columns": []
            },
            {
              "id": "who-owns-what",
              "name": "Who owns what",
              "kind": "list",
              "scope": "active",
              "fields": ["project", "status", "due_date", "tags"],
              "filter": { "teams": ["Home and Family"] },
              "grouping": ["assignee"],
              "sort": [{ "field": "due_date", "direction": "ascending" }],
              "columns": []
            },
            {
              "id": "home-board",
              "name": "Home board",
              "kind": "kanban",
              "scope": "active",
              "fields": ["priority", "assignee", "due_date"],
              "filter": { "teams": ["Home and Family"] },
              "grouping": ["project"],
              "sort": [],
              "columns": [
                { "label": "Backlog", "states": ["Backlog"] },
                { "label": "To Do", "states": ["To Do"] },
                { "label": "In Progress", "states": ["In Progress"] },
                { "label": "Review", "states": ["Review"] },
                { "label": "Done", "states": ["Done"] },
                { "label": "Won't do", "states": ["Cancelled", "Duplicate", "Rejected"] }
              ]
            }
          ]
        }
        """;

    /// <inheritdoc/>
    public string Name => "software-co";

    /// <inheritdoc/>
    public string Description => "A software company and a family: Platform, Growth, Support and Home and Family (12 Projects, shared Agents, Team Rooms, Sprints and Requests), plus empty-state Teams Sandbox and Archive.";

    /// <inheritdoc/>
    public SeedPlan Build(DateOnly today) => new()
    {
        Scenario = this.Name,
        HumanName = "You",
        Agents = [.. SoftwareCoAgents.All, .. SoftwareCoHome.Agents],
        Skills = SoftwareCoAgents.Skills,
        Teams = [.. SoftwareCoLibrary.Teams, SoftwareCoHome.Definition],
        Tasks = [.. SoftwareCoTasks.All, .. SoftwareCoHome.Tasks],
        Files = [.. SoftwareCoLibrary.Files, .. SoftwareCoNotes.Documents, .. SoftwareCoNotes.Memory, .. SoftwareCoHomeNotes.Documents, .. SoftwareCoHomeNotes.Memory],
        Rooms = [.. SoftwareCoRooms.All, .. SoftwareCoHome.Rooms],
        BinaryFiles = SoftwareCoLibrary.BinaryFiles,
        Uploads = SoftwareCoLibrary.Uploads,
        TaskUploads = SoftwareCoLibrary.TaskUploads,
        DocHistory = SoftwareCoLibrary.DocHistory,
        Sprints = SoftwareCoTasks.Sprints,
        Feed = [.. SoftwareCoFeed.All, .. SoftwareCoHome.Feed],
        ViewsJson = ViewsJson,
        Today = today,
    };
}

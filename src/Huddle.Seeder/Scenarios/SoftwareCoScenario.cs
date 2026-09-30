using Agency.Huddle.Seeder.Model;

namespace Agency.Huddle.Seeder.Scenarios;

/// <summary>
/// A small software company: three working Teams (Platform, Growth, Support) with shared Teammates, plus two
/// edge-case Teams (Sandbox with nobody in it, Archive with people but no Projects).
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
              "kind": "board",
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
              "id": "closed-work",
              "name": "Closed work",
              "kind": "list",
              "scope": "closed",
              "fields": ["team", "status", "assignee", "closed"],
              "filter": {},
              "grouping": ["team"],
              "sort": [{ "field": "closed", "direction": "descending" }],
              "columns": []
            }
          ]
        }
        """;

    private const string AvatarsJson = """
        {
          "You": { "label": "ME", "background": "#d46002" },
          "Grace": { "label": "GR", "background": "#2e7d6b" },
          "Marcus": { "label": "MA", "background": "#3b6ea5" },
          "Lena": { "label": "LE", "background": "#8a4f9e" },
          "Owen": { "label": "OW", "background": "#b5651d" },
          "Sofia": { "label": "SO", "background": "#c2456b" },
          "Dana": { "label": "DA", "background": "#4a7c59" },
          "Alan": { "label": "AL", "background": "#5b5f97" },
          "Maya": { "label": "MY", "background": "#d18a00" },
          "Ben": { "label": "BE", "background": "#6b7280" }
        }
        """;

    /// <inheritdoc/>
    public string Name => "software-co";

    /// <inheritdoc/>
    public string Description => "A software company: Platform, Growth and Support (7 Projects, shared Teammates), plus empty-state Teams Sandbox and Archive.";

    /// <inheritdoc/>
    public SeedPlan Build(DateOnly today) => new()
    {
        Scenario = this.Name,
        HumanName = "You",
        Teammates = SoftwareCoTeammates.All,
        Skills = SoftwareCoTeammates.Skills,
        Teams = SoftwareCoLibrary.Teams,
        Tasks = SoftwareCoTasks.All,
        Files = SoftwareCoLibrary.Files,
        Rooms = SoftwareCoRooms.All,
        ViewsJson = ViewsJson,
        AvatarsJson = AvatarsJson,
        Today = today,
    };
}

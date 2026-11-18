namespace Agency.Huddle.App.Acp.Tools;

using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;

/// <summary>
/// Lists Tasks matching a filter, sorted the same way Views are (Spec §11.4, §12.5): <c>scope</c>,
/// <c>team</c>, <c>project</c>, <c>assignee</c>, <c>status</c>, <c>priority</c>, <c>text</c> and
/// <c>limit</c> map onto a <see cref="TaskFilter"/> and <see cref="TaskQuery.Filter"/>/
/// <see cref="TaskQuery.Sort"/>, the same pure helpers the Tasks page uses (D7). Every argument is
/// read through <see cref="TaskToolText"/>'s <c>TryGet*</c> helpers, so a wrongly-typed argument is
/// refused rather than thrown (corrections-B4 D10 item 4), and <c>assignee: "me"</c>/
/// <c>"unassigned"</c> and Persona Aliases are resolved before filtering (corrections-B4 D10 item
/// 3). Unlike the mutating Task tools (10.2, 10.5, 10.6), this tool never calls
/// <see cref="TaskToolText.ResolveActorAsync"/> unless <c>assignee: "me"</c> is actually asked
/// for, so an unknown caller never blocks a query that doesn't need its identity.
/// </summary>
internal sealed class ListTasksTool(
    TaskStore store,
    ITeamDirectory directory,
    PersonaStore personas,
    ISprintCatalog sprints,
    IPromptSource prompts,
    string callerAgentId) : IAppTool
{
    /// <summary>The default <c>limit</c> when the argument is omitted (Spec §11.4).</summary>
    private const int DefaultLimit = 50;

    /// <summary>The smallest accepted <c>limit</c> (Spec §11.4).</summary>
    private const int MinLimit = 1;

    /// <summary>The largest accepted <c>limit</c> (Spec §11.4).</summary>
    private const int MaxLimit = 200;

    /// <inheritdoc />
    public string Name => "list_tasks";

    /// <inheritdoc />
    public string Description => prompts.Render("tool.listTasks.description");

    /// <inheritdoc />
    public JsonObject InputSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["scope"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray { "active", "closed" } },
            ["team"] = new JsonObject { ["type"] = "string" },
            ["project"] = new JsonObject { ["type"] = "string" },
            ["assignee"] = new JsonObject { ["type"] = "string" },
            ["status"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" } },
            ["priority"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" } },
            ["text"] = new JsonObject { ["type"] = "string" },
            ["limit"] = new JsonObject { ["type"] = "integer" },
            ["sprint"] = new JsonObject { ["type"] = "string" },
        },
    };

    /// <inheritdoc />
    public async Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!TryParseScope(arguments, out ViewScope scope, out string scopeRefusal))
        {
            return scopeRefusal;
        }

        if (!TryReadTeamAndProject(arguments, out string? team, out string? project, out string teamProjectRefusal))
        {
            return teamProjectRefusal;
        }

        (IReadOnlyList<string> assignees, string assigneeRefusal) = await this.ResolveAssigneeAsync(arguments, cancellationToken).ConfigureAwait(false);
        if (assigneeRefusal.Length > 0)
        {
            return assigneeRefusal;
        }

        if (!TryParseStates(arguments, out IReadOnlyList<TaskState> states, out string statesRefusal))
        {
            return statesRefusal;
        }

        if (!TryParsePriorities(arguments, out IReadOnlyList<TaskPriority> priorities, out string prioritiesRefusal))
        {
            return prioritiesRefusal;
        }

        if (!TaskToolText.TryGetString(arguments, "text", out string? text, out string textRefusal))
        {
            return textRefusal;
        }

        if (!TryReadLimit(arguments, out int limit, out string limitRefusal))
        {
            return limitRefusal;
        }

        if (!TryResolveSprintFilter(arguments, sprints, out IReadOnlyList<string> sprintValues, out string sprintRefusal))
        {
            return sprintRefusal;
        }

        TaskFilter filter = new()
        {
            Teams = team is null ? [] : [team],
            Projects = team is not null && project is not null ? [new ProjectRef(team, project)] : [],
            Assignees = assignees,
            States = states,
            Priorities = priorities,
            Sprints = sprintValues,
        };

        string humanName = await ResolveHumanNameAsync(directory, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<TaskItem> filtered = TaskQuery.Filter(store.All, scope, filter, text, humanName, sprints.Current?.Id);
        IReadOnlyList<TaskItem> sorted = TaskQuery.Sort(filtered, []);

        return Render(sorted, limit);
    }

    /// <summary>Reads the <c>team</c> and <c>project</c> arguments; a project is only meaningful with a team.</summary>
    private static bool TryReadTeamAndProject(JsonObject arguments, out string? team, out string? project, out string refusal)
    {
        project = null;
        if (!TaskToolText.TryGetString(arguments, "team", out team, out refusal))
        {
            return false;
        }

        if (!TaskToolText.TryGetString(arguments, "project", out project, out refusal))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(project) && string.IsNullOrEmpty(team))
        {
            refusal = "project needs team";
            return false;
        }

        refusal = string.Empty;
        return true;
    }

    /// <summary>Reads the <c>limit</c> argument (Spec §11.4): defaulted when absent, and refused outside the accepted range.</summary>
    private static bool TryReadLimit(JsonObject arguments, out int limit, out string refusal)
    {
        if (!TaskToolText.TryGetInt(arguments, "limit", DefaultLimit, out limit, out refusal))
        {
            return false;
        }

        if (limit is < MinLimit or > MaxLimit)
        {
            refusal = "'limit' must be between 1 and 200.";
            return false;
        }

        refusal = string.Empty;
        return true;
    }

    /// <summary>
    /// Resolves the <c>sprint</c> argument (Sprints spec §10.2) into the one-value <see cref="TaskFilter.Sprints"/>:
    /// <c>none</c> becomes <c>@none</c>, anything else is resolved by <see cref="TaskToolText.TryResolveSprint"/> to its id.
    /// An absent or empty argument adds no filter.
    /// </summary>
    private static bool TryResolveSprintFilter(JsonObject arguments, ISprintCatalog catalog, out IReadOnlyList<string> values, out string refusal)
    {
        values = [];
        if (!TaskToolText.TryGetString(arguments, "sprint", out string? text, out refusal))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (string.Equals(text.Trim(), "none", StringComparison.OrdinalIgnoreCase))
        {
            values = ["@none"];
            return true;
        }

        if (!TaskToolText.TryResolveSprint(catalog, text, out SprintId? sprint, out refusal))
        {
            return false;
        }

        values = [sprint.Value.ToString()];
        return true;
    }

    /// <summary>Parses the <c>scope</c> argument (Spec §11.4): absent defaults to Active; anything but "active"/"closed" is refused.</summary>
    private static bool TryParseScope(JsonObject arguments, out ViewScope scope, out string refusal)
    {
        if (!TaskToolText.TryGetString(arguments, "scope", out string? text, out string typeRefusal))
        {
            scope = ViewScope.Active;
            refusal = typeRefusal;
            return false;
        }

        if (text is null || string.Equals(text, "active", StringComparison.OrdinalIgnoreCase))
        {
            scope = ViewScope.Active;
            refusal = string.Empty;
            return true;
        }

        if (string.Equals(text, "closed", StringComparison.OrdinalIgnoreCase))
        {
            scope = ViewScope.Closed;
            refusal = string.Empty;
            return true;
        }

        scope = ViewScope.Active;
        refusal = "'scope' must be 'active' or 'closed'.";
        return false;
    }

    /// <summary>
    /// Resolves the <c>assignee</c> argument (Spec §11.4, corrections-B4 D10 item 3): <c>"me"</c> to
    /// the caller's own Name (resolving the caller only now, so an unrelated query never pays for an
    /// unknown caller), <c>"unassigned"</c> to <c>"@unassigned"</c>, and anything else through
    /// <see cref="PersonaStore.ResolveByNameOrAlias"/> so a Persona Alias also matches.
    /// </summary>
    private async Task<(IReadOnlyList<string> Assignees, string Refusal)> ResolveAssigneeAsync(
        JsonObject arguments, CancellationToken cancellationToken)
    {
        if (!TaskToolText.TryGetString(arguments, "assignee", out string? assignee, out string typeRefusal))
        {
            return ([], typeRefusal);
        }

        if (assignee is null)
        {
            return ([], string.Empty);
        }

        if (string.Equals(assignee, "me", StringComparison.OrdinalIgnoreCase))
        {
            (TaskActor? actor, string actorRefusal) = await TaskToolText.ResolveActorAsync(directory, callerAgentId, cancellationToken).ConfigureAwait(false);
            return actor is null ? ([], actorRefusal) : ([actor.Name], string.Empty);
        }

        if (string.Equals(assignee, "unassigned", StringComparison.OrdinalIgnoreCase))
        {
            return (["@unassigned"], string.Empty);
        }

        PersonaEntry? persona = personas.ResolveByNameOrAlias(assignee);
        return ([persona?.Name ?? assignee], string.Empty);
    }

    /// <summary>Parses the <c>status</c> argument into <see cref="TaskState"/> values, refusing an unknown wire name.</summary>
    private static bool TryParseStates(JsonObject arguments, out IReadOnlyList<TaskState> states, out string refusal)
    {
        if (!TaskToolText.TryGetStringList(arguments, "status", out IReadOnlyList<string>? values, out string typeRefusal))
        {
            states = [];
            refusal = typeRefusal;
            return false;
        }

        if (values is null)
        {
            states = [];
            refusal = string.Empty;
            return true;
        }

        List<TaskState> parsed = new(values.Count);
        foreach (string value in values)
        {
            if (!TaskStates.TryParse(value, out TaskState state))
            {
                states = [];
                refusal = TaskToolText.UnknownStatusRefusal(value);
                return false;
            }

            parsed.Add(state);
        }

        states = parsed;
        refusal = string.Empty;
        return true;
    }

    /// <summary>Parses the <c>priority</c> argument into <see cref="TaskPriority"/> values, refusing an unknown wire name.</summary>
    private static bool TryParsePriorities(JsonObject arguments, out IReadOnlyList<TaskPriority> priorities, out string refusal)
    {
        if (!TaskToolText.TryGetStringList(arguments, "priority", out IReadOnlyList<string>? values, out string typeRefusal))
        {
            priorities = [];
            refusal = typeRefusal;
            return false;
        }

        if (values is null)
        {
            priorities = [];
            refusal = string.Empty;
            return true;
        }

        List<TaskPriority> parsed = new(values.Count);
        foreach (string value in values)
        {
            if (!TaskPriorities.TryParse(value, out TaskPriority priority))
            {
                priorities = [];
                refusal = $"'priority' has an unknown value '{value}'.";
                return false;
            }

            parsed.Add(priority);
        }

        priorities = parsed;
        refusal = string.Empty;
        return true;
    }

    /// <summary>
    /// The real Human's Name (corrections-B4 D10 item 3: "pass the real Human name as humanName"),
    /// read from <paramref name="teamDirectory"/> rather than hard-coded, since the Human's display
    /// name is configurable (<see cref="Agency.Huddle.App.TeamOptions.HumanName"/>) and the directory
    /// is this tool's own source of truth for every other Name it resolves.
    /// </summary>
    private static async Task<string> ResolveHumanNameAsync(ITeamDirectory teamDirectory, CancellationToken cancellationToken)
    {
        User? human = await teamDirectory.GetUserAsync(KnownIds.Human, cancellationToken).ConfigureAwait(false);
        return human?.Name ?? "the Human";
    }

    /// <summary>Renders the §11.1 line per Task, cut off at <paramref name="limit"/> with the §11.4 trailer, or the §11.4 empty-result text.</summary>
    private static string Render(IReadOnlyList<TaskItem> sorted, int limit)
    {
        if (sorted.Count == 0)
        {
            return "No tasks match.";
        }

        List<string> lines = sorted.Take(limit).Select(TaskToolText.Line).ToList();
        if (sorted.Count > limit)
        {
            lines.Add($"Showing {limit} of {sorted.Count}; narrow the filters or raise limit.");
        }

        return string.Join('\n', lines);
    }
}

namespace Agency.Huddle.App.Acp.Tools;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Tasks;

/// <summary>
/// Changes one or more fields of an existing Task (Spec §11.5). Every optional argument is parsed
/// through <see cref="TaskToolText"/>'s never-throwing helpers, assembled into a <see cref="TaskPatch"/>,
/// and always applied through <see cref="TaskService.Update"/> with <c>baseVersion: null</c> - a tool
/// call never tracks a stale draft version, so §9.3's merge/conflict path is not reachable from here
/// (Spec §11.1: "A Conflict can't happen here, because tools pass baseVersion: null").
/// </summary>
internal sealed class UpdateTaskTool(
    TaskService tasks,
    TaskStore store,
    TaskTriggerService triggers,
    ITeamDirectory directory,
    ISprintCatalog sprints,
    IPromptSource prompts,
    string callerAgentId) : IAppTool
{
    private static readonly IReadOnlyDictionary<string, string> NoValues = new Dictionary<string, string>();

    /// <inheritdoc />
    public string Name => "update_task";

    /// <inheritdoc />
    public string Description => prompts.Render("tool.updateTask.description", NoValues);

    /// <inheritdoc />
    public JsonObject InputSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["taskId"] = new JsonObject { ["type"] = "string" },
            ["title"] = new JsonObject { ["type"] = "string" },
            ["description"] = new JsonObject { ["type"] = "string" },
            ["status"] = new JsonObject { ["type"] = "string" },
            ["priority"] = new JsonObject { ["type"] = "string" },
            ["assignee"] = new JsonObject { ["type"] = "string" },
            ["team"] = new JsonObject { ["type"] = "string" },
            ["project"] = new JsonObject { ["type"] = "string" },
            ["parent"] = new JsonObject { ["type"] = "string" },
            ["blocked_by"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" } },
            ["duplicate_of"] = new JsonObject { ["type"] = "string" },
            ["tags"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" } },
            ["start_date"] = new JsonObject { ["type"] = "string" },
            ["due_date"] = new JsonObject { ["type"] = "string" },
            ["reason"] = new JsonObject { ["type"] = "string" },
            ["sprint"] = new JsonObject { ["type"] = "string" },
            ["human_gate"] = new JsonObject { ["type"] = "boolean" },
            ["action"] = new JsonObject { ["type"] = "string" },
        },
        ["required"] = new JsonArray { "taskId" },
    };

    /// <inheritdoc />
    public async Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!TaskToolText.TryGetString(arguments, "taskId", out string? taskIdText, out string taskIdRefusal))
        {
            return taskIdRefusal;
        }

        if (!TaskToolText.TryResolve(store, taskIdText, out TaskItem? current, out string resolveRefusal))
        {
            return resolveRefusal;
        }

        (TaskActor? actor, string actorRefusal) = await TaskToolText.ResolveActorAsync(directory, callerAgentId, cancellationToken).ConfigureAwait(false);
        if (actor is null)
        {
            return actorRefusal;
        }

        if (!TryBuildPatch(actor, sprints, arguments, out TaskPatch? patch, out string patchRefusal))
        {
            return patchRefusal;
        }

        if (patch.IsEmpty)
        {
            return "Nothing to change: pass at least one field besides taskId.";
        }

        if (!TaskToolText.TryRun(() => tasks.Update(current.Id, patch, null, actor), out TaskResult? result, out string runRefusal))
        {
            return runRefusal;
        }

        return result switch
        {
            TaskResult.Saved saved => this.FormatSaved(saved, actor),
            TaskResult.Unchanged unchanged => $"{unchanged.Task.Id} already has those values; nothing changed.",
            TaskResult.Refused refused => string.Join('\n', refused.Problems),
            TaskResult.NotFound notFound => $"Unknown task '{notFound.Id}'.",
            TaskResult.GateRefused gateRefused => gateRefused.Message,

            // Spec §11.1: "A Conflict can't happen here, because tools pass baseVersion: null" - this
            // tool always does (above), so TaskService.Update's only remaining Conflict route (a
            // stale baseVersion) never triggers from here.
            _ => throw new UnreachableException("update_task always passes baseVersion: null, so TaskService.Update never returns Conflict for it (Spec §11.1)."),
        };
    }

    /// <summary>Formats the Spec §11.5 success text: the Change log summary, then the notify clause from <see cref="TaskTriggerService.Preview"/>.</summary>
    private string FormatSaved(TaskResult.Saved saved, TaskActor actor)
    {
        string summary = TaskDiff.Summarise(saved.Change.Changes);
        WakePreview preview = triggers.Preview(saved.Change.Before, saved.Task, actor);
        string notify = TaskToolText.NotifyClause(preview, actor.Name);
        return $"Updated {saved.Task.Id}: {summary}. {notify}";
    }

    /// <summary>
    /// Parses every optional Spec §11.5 argument through <see cref="TaskToolText"/>'s never-throwing
    /// helpers into a <see cref="TaskPatch"/>: an absent argument leaves its field unset
    /// (<see cref="Optional{T}.IsSet"/> false), an empty string clears a field whose "clear" state
    /// is itself <see langword="null"/> (assignee, parent, project, duplicate_of, the dates), and
    /// <c>assignee: "me"</c> resolves to <paramref name="actor"/>'s own Name.
    /// </summary>
    /// <param name="actor">The caller, whose Name resolves <c>assignee: "me"</c>.</param>
    /// <param name="sprints">The read-only catalogue a <c>sprint</c> argument is resolved against; <c>""</c> clears the Sprint.</param>
    /// <param name="arguments">The tool's JSON arguments.</param>
    /// <param name="patch">The assembled patch, or <see langword="null"/> when this returns <see langword="false"/>.</param>
    /// <param name="refusal">The refusal text when this returns <see langword="false"/>; otherwise <see cref="string.Empty"/>.</param>
    private static bool TryBuildPatch(TaskActor actor, ISprintCatalog sprints, JsonObject arguments, [NotNullWhen(true)] out TaskPatch? patch, out string refusal)
    {
        patch = null;
        if (!TaskToolText.TryGetString(arguments, "title", out string? title, out refusal)
            || !TaskToolText.TryGetString(arguments, "description", out string? description, out refusal)
            || !TryReadStatus(arguments, out TaskState? status, out refusal)
            || !TryReadPriority(arguments, out TaskPriority? priority, out refusal)
            || !TryReadAssignee(arguments, actor, out Optional<string?> assignee, out refusal)
            || !TaskToolText.TryGetString(arguments, "team", out string? team, out refusal)
            || !TryReadProject(arguments, out Optional<string?> project, out refusal)
            || !TryReadOptionalId(arguments, "parent", out Optional<TaskId?> parent, out refusal)
            || !TryReadBlockedBy(arguments, out IReadOnlyList<TaskId>? blockedBy, out refusal)
            || !TryReadOptionalId(arguments, "duplicate_of", out Optional<TaskId?> duplicateOf, out refusal)
            || !TaskToolText.TryGetStringList(arguments, "tags", out IReadOnlyList<string>? tags, out refusal)
            || !TryReadOptionalDate(arguments, "start_date", out Optional<DateOnly?> startDate, out refusal)
            || !TryReadOptionalDate(arguments, "due_date", out Optional<DateOnly?> dueDate, out refusal)
            || !TaskToolText.TryGetString(arguments, "reason", out string? reason, out refusal)
            || !TryReadSprint(arguments, sprints, out Optional<SprintId?> sprint, out refusal)
            || !TryReadHumanGate(arguments, out Optional<bool> humanGate, out refusal)
            || !TryReadAction(arguments, out Optional<string?> action, out refusal))
        {
            return false;
        }

        patch = new TaskPatch
        {
            Title = title,
            Status = status,
            Priority = priority,
            Assignee = assignee,
            Parent = parent,
            BlockedBy = blockedBy,
            DuplicateOf = duplicateOf,
            Tags = tags,
            StartDate = startDate,
            DueDate = dueDate,
            Description = description,
            Team = team,
            Project = project,
            Reason = reason,
            Sprint = sprint,
            HumanGate = humanGate,
            Action = action,
        };
        refusal = string.Empty;
        return true;
    }

    /// <summary>Reads the optional <c>human_gate</c> argument; absent leaves it unset, and <see langword="false"/> is passed on so the service can refuse it for an Agent.</summary>
    /// <param name="arguments">The tool's JSON arguments.</param>
    /// <param name="humanGate">The gate change, unset when the argument is absent.</param>
    /// <param name="refusal">The refusal text when this returns <see langword="false"/>.</param>
    private static bool TryReadHumanGate(JsonObject arguments, out Optional<bool> humanGate, out string refusal)
    {
        humanGate = default;
        if (!TaskToolText.TryGetBool(arguments, "human_gate", false, out bool value, out refusal))
        {
            return false;
        }

        if (arguments.ContainsKey("human_gate"))
        {
            humanGate = Optional<bool>.Set(value);
        }

        return true;
    }

    /// <summary>Reads the optional <c>action</c> argument; an empty string clears the Action.</summary>
    /// <param name="arguments">The tool's JSON arguments.</param>
    /// <param name="action">The Action change, unset when the argument is absent.</param>
    /// <param name="refusal">The refusal text when this returns <see langword="false"/>.</param>
    private static bool TryReadAction(JsonObject arguments, out Optional<string?> action, out string refusal)
    {
        action = default;
        if (!TaskToolText.TryGetString(arguments, "action", out string? text, out refusal))
        {
            return false;
        }

        if (text is not null)
        {
            action = Optional<string?>.Set(text.Trim().Length == 0 ? null : text);
        }

        return true;
    }

    /// <summary>Reads the optional <c>status</c> argument; absent leaves it unset, an unknown wire name is refused.</summary>
    /// <param name="arguments">The tool's JSON arguments.</param>
    /// <param name="status">The parsed status, or <see langword="null"/> when absent.</param>
    /// <param name="refusal">The refusal text when this returns <see langword="false"/>.</param>
    private static bool TryReadStatus(JsonObject arguments, out TaskState? status, out string refusal)
    {
        status = null;
        if (!TaskToolText.TryGetString(arguments, "status", out string? statusText, out refusal))
        {
            return false;
        }

        if (statusText is not null)
        {
            if (!TaskStates.TryParse(statusText, out TaskState parsedStatus))
            {
                refusal = $"'status' has an unknown value '{statusText}'.";
                return false;
            }

            status = parsedStatus;
        }

        return true;
    }

    /// <summary>Reads the optional <c>priority</c> argument; absent leaves it unset, an unknown wire name is refused.</summary>
    /// <param name="arguments">The tool's JSON arguments.</param>
    /// <param name="priority">The parsed priority, or <see langword="null"/> when absent.</param>
    /// <param name="refusal">The refusal text when this returns <see langword="false"/>.</param>
    private static bool TryReadPriority(JsonObject arguments, out TaskPriority? priority, out string refusal)
    {
        priority = null;
        if (!TaskToolText.TryGetString(arguments, "priority", out string? priorityText, out refusal))
        {
            return false;
        }

        if (priorityText is not null)
        {
            if (!TaskPriorities.TryParse(priorityText, out TaskPriority parsedPriority))
            {
                refusal = $"'priority' has an unknown value '{priorityText}'.";
                return false;
            }

            priority = parsedPriority;
        }

        return true;
    }

    /// <summary>Reads the optional <c>assignee</c> argument: <c>"me"</c> becomes the caller's Name and an empty string clears the assignee.</summary>
    /// <param name="arguments">The tool's JSON arguments.</param>
    /// <param name="actor">The caller, whose Name resolves <c>"me"</c>.</param>
    /// <param name="assignee">The assignee change, unset when the argument is absent.</param>
    /// <param name="refusal">The refusal text when this returns <see langword="false"/>.</param>
    private static bool TryReadAssignee(JsonObject arguments, TaskActor actor, out Optional<string?> assignee, out string refusal)
    {
        assignee = default;
        if (!TaskToolText.TryGetString(arguments, "assignee", out string? assigneeText, out refusal))
        {
            return false;
        }

        if (assigneeText is not null)
        {
            string? resolvedAssignee = string.Equals(assigneeText, "me", StringComparison.Ordinal) ? actor.Name : assigneeText;
            assignee = assigneeText.Length == 0
                ? Optional<string?>.Set(null)
                : Optional<string?>.Set(resolvedAssignee);
        }

        return true;
    }

    /// <summary>Reads the optional <c>project</c> argument; an empty string clears the project.</summary>
    /// <param name="arguments">The tool's JSON arguments.</param>
    /// <param name="project">The project change, unset when the argument is absent.</param>
    /// <param name="refusal">The refusal text when this returns <see langword="false"/>.</param>
    private static bool TryReadProject(JsonObject arguments, out Optional<string?> project, out string refusal)
    {
        project = default;
        if (!TaskToolText.TryGetString(arguments, "project", out string? projectText, out refusal))
        {
            return false;
        }

        if (projectText is not null)
        {
            project = Optional<string?>.Set(projectText.Length == 0 ? null : projectText);
        }

        return true;
    }

    /// <summary>Reads an optional task-id argument (<c>parent</c> or <c>duplicate_of</c>); an empty string clears the field.</summary>
    /// <param name="arguments">The tool's JSON arguments.</param>
    /// <param name="argumentName">The argument's name.</param>
    /// <param name="id">The id change, unset when the argument is absent.</param>
    /// <param name="refusal">The refusal text when this returns <see langword="false"/>.</param>
    private static bool TryReadOptionalId(JsonObject arguments, string argumentName, out Optional<TaskId?> id, out string refusal)
    {
        id = default;
        if (!TaskToolText.TryGetString(arguments, argumentName, out string? text, out refusal))
        {
            return false;
        }

        if (text is not null)
        {
            if (!TryParseOptionalId(text, out TaskId? parsedId, out refusal))
            {
                return false;
            }

            id = Optional<TaskId?>.Set(parsedId);
        }

        return true;
    }

    /// <summary>Reads the optional <c>blocked_by</c> list of task ids.</summary>
    /// <param name="arguments">The tool's JSON arguments.</param>
    /// <param name="blockedBy">The parsed ids, or <see langword="null"/> when the argument is absent.</param>
    /// <param name="refusal">The refusal text when this returns <see langword="false"/>.</param>
    private static bool TryReadBlockedBy(JsonObject arguments, out IReadOnlyList<TaskId>? blockedBy, out string refusal)
    {
        blockedBy = null;
        if (!TaskToolText.TryGetStringList(arguments, "blocked_by", out IReadOnlyList<string>? blockedByText, out refusal))
        {
            return false;
        }

        if (blockedByText is not null)
        {
            List<TaskId> ids = new(blockedByText.Count);
            foreach (string item in blockedByText)
            {
                if (!TaskId.TryParse(item, out TaskId parsedId))
                {
                    refusal = $"'{item}' is not a task id; ids look like PLAT-0042.";
                    return false;
                }

                ids.Add(parsedId);
            }

            blockedBy = ids;
        }

        return true;
    }

    /// <summary>Reads an optional date argument (<c>start_date</c> or <c>due_date</c>); an empty string clears the field.</summary>
    /// <param name="arguments">The tool's JSON arguments.</param>
    /// <param name="argumentName">The argument's name.</param>
    /// <param name="date">The date change, unset when the argument is absent.</param>
    /// <param name="refusal">The refusal text when this returns <see langword="false"/>.</param>
    private static bool TryReadOptionalDate(JsonObject arguments, string argumentName, out Optional<DateOnly?> date, out string refusal)
    {
        date = default;
        if (!TaskToolText.TryGetString(arguments, argumentName, out string? text, out refusal))
        {
            return false;
        }

        if (text is not null)
        {
            if (!TryParseOptionalDate(argumentName, text, out DateOnly? parsedDate, out refusal))
            {
                return false;
            }

            date = Optional<DateOnly?>.Set(parsedDate);
        }

        return true;
    }

    /// <summary>Reads the optional <c>sprint</c> argument; a blank value clears the Sprint, anything else is resolved against the catalogue.</summary>
    /// <param name="arguments">The tool's JSON arguments.</param>
    /// <param name="sprints">The read-only catalogue the argument is resolved against.</param>
    /// <param name="sprint">The Sprint change, unset when the argument is absent.</param>
    /// <param name="refusal">The refusal text when this returns <see langword="false"/>.</param>
    private static bool TryReadSprint(JsonObject arguments, ISprintCatalog sprints, out Optional<SprintId?> sprint, out string refusal)
    {
        sprint = default;
        if (!TaskToolText.TryGetString(arguments, "sprint", out string? sprintText, out refusal))
        {
            return false;
        }

        if (sprintText is not null)
        {
            if (sprintText.Trim().Length == 0)
            {
                sprint = Optional<SprintId?>.Set(null);
            }
            else if (TaskToolText.TryResolveSprint(sprints, sprintText, out SprintId? resolved, out refusal))
            {
                sprint = Optional<SprintId?>.Set(resolved);
            }
            else
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Parses a task-id-shaped argument that also accepts <see cref="string.Empty"/> to mean "clear this field".</summary>
    private static bool TryParseOptionalId(string value, out TaskId? id, out string refusal)
    {
        if (value.Length == 0)
        {
            id = null;
            refusal = string.Empty;
            return true;
        }

        if (!TaskId.TryParse(value, out TaskId parsed))
        {
            id = null;
            refusal = $"'{value}' is not a task id; ids look like PLAT-0042.";
            return false;
        }

        id = parsed;
        refusal = string.Empty;
        return true;
    }

    /// <summary>
    /// Parses a <c>yyyy-MM-dd</c> date argument that also accepts <see cref="string.Empty"/> to mean
    /// "clear this field", refusing with the shared D10 wording <c>create_task</c> already pins
    /// (settled to match across both tools).
    /// </summary>
    /// <param name="argumentName">The argument's name, named in the refusal.</param>
    /// <param name="value">The argument's text.</param>
    /// <param name="date">The parsed date, or <see langword="null"/> for a clearing empty string, or when this returns <see langword="false"/>.</param>
    /// <param name="refusal">The refusal text when this returns <see langword="false"/>; otherwise <see cref="string.Empty"/>.</param>
    private static bool TryParseOptionalDate(string argumentName, string value, out DateOnly? date, out string refusal)
    {
        if (value.Length == 0)
        {
            date = null;
            refusal = string.Empty;
            return true;
        }

        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly parsed))
        {
            date = null;
            refusal = $"'{argumentName}' must be a date in the form yyyy-MM-dd.";
            return false;
        }

        date = parsed;
        refusal = string.Empty;
        return true;
    }
}

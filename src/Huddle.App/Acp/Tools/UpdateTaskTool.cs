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

        (TaskActor? actor, string actorRefusal) = await TaskToolText.ResolveActorAsync(directory, callerAgentId, cancellationToken);
        if (actor is null)
        {
            return actorRefusal;
        }

        if (!TryBuildPatch(actor, arguments, out TaskPatch? patch, out string patchRefusal))
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
    /// <param name="arguments">The tool's JSON arguments.</param>
    /// <param name="patch">The assembled patch, or <see langword="null"/> when this returns <see langword="false"/>.</param>
    /// <param name="refusal">The refusal text when this returns <see langword="false"/>; otherwise <see cref="string.Empty"/>.</param>
    private static bool TryBuildPatch(TaskActor actor, JsonObject arguments, [NotNullWhen(true)] out TaskPatch? patch, out string refusal)
    {
        if (!TaskToolText.TryGetString(arguments, "title", out string? title, out refusal))
        {
            patch = null;
            return false;
        }

        if (!TaskToolText.TryGetString(arguments, "description", out string? description, out refusal))
        {
            patch = null;
            return false;
        }

        if (!TaskToolText.TryGetString(arguments, "status", out string? statusText, out refusal))
        {
            patch = null;
            return false;
        }

        TaskState? status = null;
        if (statusText is not null)
        {
            if (!TaskStates.TryParse(statusText, out TaskState parsedStatus))
            {
                patch = null;
                refusal = $"'status' has an unknown value '{statusText}'.";
                return false;
            }

            status = parsedStatus;
        }

        if (!TaskToolText.TryGetString(arguments, "priority", out string? priorityText, out refusal))
        {
            patch = null;
            return false;
        }

        TaskPriority? priority = null;
        if (priorityText is not null)
        {
            if (!TaskPriorities.TryParse(priorityText, out TaskPriority parsedPriority))
            {
                patch = null;
                refusal = $"'priority' has an unknown value '{priorityText}'.";
                return false;
            }

            priority = parsedPriority;
        }

        if (!TaskToolText.TryGetString(arguments, "assignee", out string? assigneeText, out refusal))
        {
            patch = null;
            return false;
        }

        Optional<string?> assignee = default;
        if (assigneeText is not null)
        {
            assignee = assigneeText.Length == 0
                ? Optional<string?>.Set(null)
                : Optional<string?>.Set(string.Equals(assigneeText, "me", StringComparison.Ordinal) ? actor.Name : assigneeText);
        }

        if (!TaskToolText.TryGetString(arguments, "team", out string? team, out refusal))
        {
            patch = null;
            return false;
        }

        if (!TaskToolText.TryGetString(arguments, "project", out string? projectText, out refusal))
        {
            patch = null;
            return false;
        }

        Optional<string?> project = default;
        if (projectText is not null)
        {
            project = Optional<string?>.Set(projectText.Length == 0 ? null : projectText);
        }

        if (!TaskToolText.TryGetString(arguments, "parent", out string? parentText, out refusal))
        {
            patch = null;
            return false;
        }

        Optional<TaskId?> parent = default;
        if (parentText is not null)
        {
            if (!TryParseOptionalId(parentText, out TaskId? parsedParent, out refusal))
            {
                patch = null;
                return false;
            }

            parent = Optional<TaskId?>.Set(parsedParent);
        }

        if (!TaskToolText.TryGetStringList(arguments, "blocked_by", out IReadOnlyList<string>? blockedByText, out refusal))
        {
            patch = null;
            return false;
        }

        IReadOnlyList<TaskId>? blockedBy = null;
        if (blockedByText is not null)
        {
            List<TaskId> ids = new(blockedByText.Count);
            foreach (string item in blockedByText)
            {
                if (!TaskId.TryParse(item, out TaskId parsedId))
                {
                    patch = null;
                    refusal = $"'{item}' is not a task id; ids look like PLAT-0042.";
                    return false;
                }

                ids.Add(parsedId);
            }

            blockedBy = ids;
        }

        if (!TaskToolText.TryGetString(arguments, "duplicate_of", out string? duplicateOfText, out refusal))
        {
            patch = null;
            return false;
        }

        Optional<TaskId?> duplicateOf = default;
        if (duplicateOfText is not null)
        {
            if (!TryParseOptionalId(duplicateOfText, out TaskId? parsedDuplicateOf, out refusal))
            {
                patch = null;
                return false;
            }

            duplicateOf = Optional<TaskId?>.Set(parsedDuplicateOf);
        }

        if (!TaskToolText.TryGetStringList(arguments, "tags", out IReadOnlyList<string>? tags, out refusal))
        {
            patch = null;
            return false;
        }

        if (!TaskToolText.TryGetString(arguments, "start_date", out string? startDateText, out refusal))
        {
            patch = null;
            return false;
        }

        Optional<DateOnly?> startDate = default;
        if (startDateText is not null)
        {
            if (!TryParseOptionalDate("start_date", startDateText, out DateOnly? parsedStart, out refusal))
            {
                patch = null;
                return false;
            }

            startDate = Optional<DateOnly?>.Set(parsedStart);
        }

        if (!TaskToolText.TryGetString(arguments, "due_date", out string? dueDateText, out refusal))
        {
            patch = null;
            return false;
        }

        Optional<DateOnly?> dueDate = default;
        if (dueDateText is not null)
        {
            if (!TryParseOptionalDate("due_date", dueDateText, out DateOnly? parsedDue, out refusal))
            {
                patch = null;
                return false;
            }

            dueDate = Optional<DateOnly?>.Set(parsedDue);
        }

        if (!TaskToolText.TryGetString(arguments, "reason", out string? reason, out refusal))
        {
            patch = null;
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
        };
        refusal = string.Empty;
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

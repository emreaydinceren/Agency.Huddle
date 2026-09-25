namespace Agency.Huddle.App.Acp.Tools;

using System.Globalization;
using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Tasks;

/// <summary>
/// Creates a Task (Spec §11.2): validates its arguments in the §11.2 order - required fields, an
/// <c>originRoomId</c> the caller must be a Member of, a status that isn't a Won't do state, and
/// dates in <c>yyyy-MM-dd</c> - then hands the rest to <see cref="TaskService.Create"/>, and reports
/// who will be notified via <see cref="TaskTriggerService.Preview"/>. Every expected failure is
/// text; this never throws (corrections-B4 D10 items 2, 4 and 5).
/// </summary>
internal sealed class CreateTaskTool(
    TaskService tasks,
    TaskTriggerService triggers,
    ITeamDirectory directory,
    IPromptSource prompts,
    string callerAgentId) : IAppTool
{
    private static readonly IReadOnlyDictionary<string, string> NoValues = new Dictionary<string, string>();

    /// <inheritdoc />
    public string Name => "create_task";

    /// <inheritdoc />
    public string Description => prompts.Render("tool.createTask.description", NoValues);

    /// <inheritdoc />
    public JsonObject InputSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["title"] = new JsonObject { ["type"] = "string" },
            ["team"] = new JsonObject { ["type"] = "string" },
            ["project"] = new JsonObject { ["type"] = "string" },
            ["description"] = new JsonObject { ["type"] = "string" },
            ["status"] = new JsonObject
            {
                ["type"] = "string",
                ["enum"] = new JsonArray { "Backlog", "To Do", "In Progress", "Review", "Done" },
            },
            ["priority"] = new JsonObject
            {
                ["type"] = "string",
                ["enum"] = new JsonArray { "Low", "Medium", "High", "Urgent" },
            },
            ["assignee"] = new JsonObject { ["type"] = "string" },
            ["originRoomId"] = new JsonObject { ["type"] = "string" },
            ["parent"] = new JsonObject { ["type"] = "string" },
            ["blocked_by"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "string" },
            },
            ["tags"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "string" },
            },
            ["start_date"] = new JsonObject { ["type"] = "string" },
            ["due_date"] = new JsonObject { ["type"] = "string" },
        },
        ["required"] = new JsonArray { "title", "team" },
    };

    /// <summary>Runs the §11.2 checks, in order, then <see cref="TaskService.Create"/>. Every expected failure is returned as text, never thrown.</summary>
    /// <param name="arguments">The tool's JSON arguments.</param>
    /// <param name="cancellationToken">Propagated to every awaited call.</param>
    public async Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!TaskToolText.TryGetString(arguments, "title", out string? titleText, out string titleTypeRefusal))
        {
            return titleTypeRefusal;
        }

        if (string.IsNullOrWhiteSpace(titleText))
        {
            return "'title' is a required argument.";
        }

        if (!TaskToolText.TryGetString(arguments, "team", out string? teamText, out string teamTypeRefusal))
        {
            return teamTypeRefusal;
        }

        if (string.IsNullOrWhiteSpace(teamText))
        {
            return "'team' is a required argument.";
        }

        if (!TaskToolText.TryGetString(arguments, "project", out string? projectText, out string projectTypeRefusal))
        {
            return projectTypeRefusal;
        }

        if (!TaskToolText.TryGetString(arguments, "description", out string? descriptionText, out string descriptionTypeRefusal))
        {
            return descriptionTypeRefusal;
        }

        if (!TaskToolText.TryGetString(arguments, "status", out string? statusText, out string statusTypeRefusal))
        {
            return statusTypeRefusal;
        }

        if (!TaskToolText.TryGetString(arguments, "priority", out string? priorityText, out string priorityTypeRefusal))
        {
            return priorityTypeRefusal;
        }

        if (!TaskToolText.TryGetString(arguments, "assignee", out string? assigneeText, out string assigneeTypeRefusal))
        {
            return assigneeTypeRefusal;
        }

        if (!TaskToolText.TryGetString(arguments, "originRoomId", out string? originRoomIdText, out string originRoomIdTypeRefusal))
        {
            return originRoomIdTypeRefusal;
        }

        if (!TaskToolText.TryGetString(arguments, "parent", out string? parentText, out string parentTypeRefusal))
        {
            return parentTypeRefusal;
        }

        if (!TaskToolText.TryGetStringList(arguments, "blocked_by", out IReadOnlyList<string>? blockedByText, out string blockedByTypeRefusal))
        {
            return blockedByTypeRefusal;
        }

        if (!TaskToolText.TryGetStringList(arguments, "tags", out IReadOnlyList<string>? tagsText, out string tagsTypeRefusal))
        {
            return tagsTypeRefusal;
        }

        if (!TaskToolText.TryGetString(arguments, "start_date", out string? startDateText, out string startDateTypeRefusal))
        {
            return startDateTypeRefusal;
        }

        if (!TaskToolText.TryGetString(arguments, "due_date", out string? dueDateText, out string dueDateTypeRefusal))
        {
            return dueDateTypeRefusal;
        }

        if (!string.IsNullOrWhiteSpace(originRoomIdText))
        {
            string? membershipRefusal = await this.CheckOriginRoomMembershipAsync(originRoomIdText, cancellationToken).ConfigureAwait(false);
            if (membershipRefusal is not null)
            {
                return membershipRefusal;
            }
        }

        TaskState status = TaskState.Backlog;
        if (!string.IsNullOrWhiteSpace(statusText))
        {
            if (!TaskStates.TryParse(statusText, out status))
            {
                return string.Create(CultureInfo.InvariantCulture, $"'status' has an unknown value '{statusText}'.");
            }

            if (status.IsWontDo())
            {
                return "Create the task first; to mark it Cancelled, Duplicate or Rejected, call update_task.";
            }
        }

        TaskPriority priority = TaskPriority.Medium;
        if (!string.IsNullOrWhiteSpace(priorityText) && !TaskPriorities.TryParse(priorityText, out priority))
        {
            return string.Create(CultureInfo.InvariantCulture, $"'priority' has an unknown value '{priorityText}'.");
        }

        if (!TryParseDate(startDateText, "start_date", out DateOnly? startDate, out string startDateRefusal))
        {
            return startDateRefusal;
        }

        if (!TryParseDate(dueDateText, "due_date", out DateOnly? dueDate, out string dueDateRefusal))
        {
            return dueDateRefusal;
        }

        if (!TryParseTaskId(parentText, out TaskId? parentId, out string parentRefusal))
        {
            return parentRefusal;
        }

        if (!TryParseTaskIds(blockedByText, out List<TaskId>? blockedByIds, out string blockedByRefusal))
        {
            return blockedByRefusal;
        }

        (TaskActor? actor, string? actorRefusal) = await TaskToolText.ResolveActorAsync(directory, callerAgentId, cancellationToken).ConfigureAwait(false);
        if (actor is null)
        {
            return actorRefusal ?? string.Create(CultureInfo.InvariantCulture, $"Could not identify caller '{callerAgentId}' as a Teammate.");
        }

        string? resolvedAssignee = string.IsNullOrEmpty(assigneeText)
            ? null
            : string.Equals(assigneeText, "me", StringComparison.Ordinal) ? actor.Name : assigneeText;

        TaskDraft draft = new(
            titleText,
            teamText,
            string.IsNullOrEmpty(projectText) ? null : projectText,
            status,
            priority,
            resolvedAssignee,
            originRoomIdText,
            parentId,
            blockedByIds,
            tagsText,
            startDate,
            dueDate,
            descriptionText ?? "");

        if (!TaskToolText.TryRun(() => tasks.Create(draft, actor), out TaskResult? result, out string runRefusal))
        {
            return runRefusal;
        }

        return result switch
        {
            TaskResult.Saved saved => this.BuildSuccessText(saved, actor),
            TaskResult.Refused refused => string.Join('\n', refused.Problems),
            _ => "Could not save the task.",
        };
    }

    /// <summary>Spec §11.2 check 2: the Room named by <c>originRoomId</c> must exist, and the caller must be a Member of it.</summary>
    /// <param name="originRoomId">The Room id the caller passed.</param>
    /// <param name="cancellationToken">Propagated to <see cref="ITeamDirectory"/>.</param>
    private async Task<string?> CheckOriginRoomMembershipAsync(string originRoomId, CancellationToken cancellationToken)
    {
        Room? room = await directory.GetRoomAsync(originRoomId, cancellationToken).ConfigureAwait(false);
        if (room is null)
        {
            return string.Create(CultureInfo.InvariantCulture, $"Unknown room '{originRoomId}'.");
        }

        IReadOnlyList<User> members = await directory.GetRoomMembersAsync(originRoomId, cancellationToken).ConfigureAwait(false);
        if (members.Any(member => string.Equals(member.Id, callerAgentId, StringComparison.Ordinal)))
        {
            return null;
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"You are not a member of room '{room.Name}' (id {room.Id}); pass an originRoomId only for a Room you belong to, or omit it.");
    }

    /// <summary>Builds the §11.2 success text: what was created, its assignee, and who is notified.</summary>
    /// <param name="saved">The result <see cref="TaskService.Create"/> returned.</param>
    /// <param name="actor">The caller, as resolved by <see cref="TaskToolText.ResolveActorAsync"/>.</param>
    private string BuildSuccessText(TaskResult.Saved saved, TaskActor actor)
    {
        TaskItem task = saved.Task;
        string location = task.Location.Project is null
            ? task.Location.Team
            : string.Create(CultureInfo.InvariantCulture, $"{task.Location.Team}/{task.Location.Project}");

        string assigneeClause = task.Assignee switch
        {
            null or "" => "unassigned",
            { } name when string.Equals(name, actor.Name, StringComparison.Ordinal) => "assigned to you",
            { } name => string.Create(CultureInfo.InvariantCulture, $"assigned to {name}"),
        };

        WakePreview preview = triggers.Preview(saved.Change.Before, saved.Change.After, actor);
        string notify = TaskToolText.NotifyClause(preview, actor.Name);

        return string.Create(CultureInfo.InvariantCulture, $"Created {task.Id} \"{task.Title}\" in {location}, {assigneeClause}. {notify}");
    }

    /// <summary>Parses an optional <c>yyyy-MM-dd</c> date argument (Spec §11.2 check 4).</summary>
    /// <param name="text">The argument's text, or <see langword="null"/> when absent.</param>
    /// <param name="argumentName">The argument's name, named in the refusal.</param>
    /// <param name="value">The parsed date, or <see langword="null"/> when absent.</param>
    /// <param name="refusal">The refusal text when this returns <see langword="false"/>.</param>
    private static bool TryParseDate(string? text, string argumentName, out DateOnly? value, out string refusal)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = null;
            refusal = string.Empty;
            return true;
        }

        if (!DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly parsed))
        {
            value = null;
            refusal = string.Create(CultureInfo.InvariantCulture, $"'{argumentName}' must be a date in the form yyyy-MM-dd.");
            return false;
        }

        value = parsed;
        refusal = string.Empty;
        return true;
    }

    /// <summary>Parses an optional Task id argument, with the §11.1 id-refusal wording.</summary>
    /// <param name="text">The argument's text, or <see langword="null"/> when absent.</param>
    /// <param name="value">The parsed id, or <see langword="null"/> when absent.</param>
    /// <param name="refusal">The refusal text when this returns <see langword="false"/>.</param>
    private static bool TryParseTaskId(string? text, out TaskId? value, out string refusal)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = null;
            refusal = string.Empty;
            return true;
        }

        if (!TaskId.TryParse(text, out TaskId parsed))
        {
            value = null;
            refusal = string.Create(CultureInfo.InvariantCulture, $"'{text}' is not a task id; ids look like PLAT-0042.");
            return false;
        }

        value = parsed;
        refusal = string.Empty;
        return true;
    }

    /// <summary>Parses an optional list of Task id arguments, with the §11.1 id-refusal wording for the first one that doesn't parse.</summary>
    /// <param name="texts">The argument's items, or <see langword="null"/> when absent.</param>
    /// <param name="value">The parsed ids, or <see langword="null"/> when <paramref name="texts"/> is <see langword="null"/>.</param>
    /// <param name="refusal">The refusal text when this returns <see langword="false"/>.</param>
    private static bool TryParseTaskIds(IReadOnlyList<string>? texts, out List<TaskId>? value, out string refusal)
    {
        if (texts is null)
        {
            value = null;
            refusal = string.Empty;
            return true;
        }

        List<TaskId> ids = new(texts.Count);
        foreach (string text in texts)
        {
            if (!TaskId.TryParse(text, out TaskId parsed))
            {
                value = null;
                refusal = string.Create(CultureInfo.InvariantCulture, $"'{text}' is not a task id; ids look like PLAT-0042.");
                return false;
            }

            ids.Add(parsed);
        }

        value = ids;
        refusal = string.Empty;
        return true;
    }
}

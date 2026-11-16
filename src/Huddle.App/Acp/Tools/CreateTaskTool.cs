namespace Agency.Huddle.App.Acp.Tools;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;

/// <summary>
/// Creates a Task (Spec §11.2): validates its arguments in the §11.2 order - required fields, an
/// <c>originRoomId</c> the caller must be a Member of, a status that isn't a Won't do state, and
/// dates in <c>yyyy-MM-dd</c> - then hands the rest to <see cref="TaskService.Create"/>, and reports
/// who will be notified via <see cref="TaskTriggerService.Preview"/>. Every expected failure is
/// text; this never throws (corrections-B4 D10 items 2, 4 and 5).
/// </summary>
/// <remarks>
/// With no <c>originRoomId</c> the origin is the Room of the caller's one running Turn, when the caller is a Member of it
/// (<see cref="TurnActivity.SoleRoom"/>). Models leave the argument out, and a Task with no origin is woken in a
/// {Human, assignee} Team chat rather than in the Room it was asked for in.
/// <para>
/// With no <c>project</c> (omitted, empty or blank) the Task is filed in the first Project that applies: the origin Room's, the
/// <c>parent</c> Task's, or that of the Task the caller's Turn was woken for (<see cref="TurnActivity.TaskInHand"/>). A source counts
/// only when it is in the Task's own Team and the Team's catalog still lists the Project; otherwise the Task stays at the Team root.
/// An explicit <c>project</c> is never overridden. When a Project was defaulted the result says where it came from.
/// </para>
/// </remarks>
internal sealed class CreateTaskTool(
    TaskService tasks,
    TaskTriggerService triggers,
    ITeamDirectory directory,
    ISprintCatalog sprints,
    IPromptSource prompts,
    string callerAgentId,
    TurnActivity? turnActivity = null,
    NeedsYouLevels? levels = null,
    ProcessGateService? gates = null,
    ITeamCatalog? catalog = null,
    TaskStore? store = null) : IAppTool
{
    /// <summary>The optional string arguments read straight after <c>team</c>, in check order.</summary>
    private static readonly string[] LeadingNames = ["project", "description", "status", "priority", "assignee", "originRoomId", "parent"];

    /// <summary>The optional string arguments read after the two lists, in check order.</summary>
    private static readonly string[] TrailingNames = ["start_date", "due_date", "sprint"];

    /// <inheritdoc />
    public string Name => "create_task";

    /// <inheritdoc />
    public string Description => prompts.Render("tool.createTask.description");

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
            ["sprint"] = new JsonObject { ["type"] = "string" },
            ["human_gate"] = new JsonObject { ["type"] = "boolean" },
            ["action"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "The one outward step, such as merging or sending, done only after the Human accepts in Needs You. Never use it for hand-offs or status: put those in a Message or the Task's description.",
            },
        },
        ["required"] = new JsonArray { "title", "team" },
    };

    /// <summary>Runs the §11.2 checks, in order, then <see cref="TaskService.Create"/>. Every expected failure is returned as text, never thrown.</summary>
    /// <param name="arguments">The tool's JSON arguments.</param>
    /// <param name="cancellationToken">Propagated to every awaited call.</param>
    public async Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!TryReadRawArguments(arguments, out RawArguments? raw, out string rawRefusal))
        {
            return rawRefusal;
        }

        if (!string.IsNullOrWhiteSpace(raw.OriginRoomId))
        {
            string? membershipRefusal = await this.CheckOriginRoomMembershipAsync(raw.OriginRoomId, cancellationToken).ConfigureAwait(false);
            if (membershipRefusal is not null)
            {
                return membershipRefusal;
            }
        }
        else if (await this.TurnRoomAsync(cancellationToken).ConfigureAwait(false) is { } turnRoomId)
        {
            raw = raw with { OriginRoomId = turnRoomId };
        }

        if (!this.TryParseValues(raw, out ParsedValues? values, out string valuesRefusal))
        {
            return valuesRefusal;
        }

        (TaskActor? actor, string actorRefusal) = await TaskToolText.ResolveActorAsync(directory, callerAgentId, cancellationToken).ConfigureAwait(false);
        if (actor is null)
        {
            return actorRefusal;
        }

        ProjectDefault? filed = string.IsNullOrWhiteSpace(raw.Project)
            ? await this.DefaultProjectAsync(raw, values, cancellationToken).ConfigureAwait(false)
            : null;
        if (filed is not null)
        {
            raw = raw with { Project = filed.Project };
        }

        if (!TaskToolText.TryGetBool(arguments, "human_gate", false, out bool humanGate, out string gateRefusal)
            || !TaskToolText.TryGetString(arguments, "action", out string? action, out gateRefusal))
        {
            return gateRefusal;
        }

        TaskDraft draft = BuildDraft(raw, values, actor) with
        {
            HumanGate = humanGate,
            Action = string.IsNullOrWhiteSpace(action) ? null : action,
        };

        if (!TaskToolText.TryRun(() => tasks.Create(draft, actor), out TaskResult? result, out string runRefusal))
        {
            return runRefusal;
        }

        return this.DescribeResult(result, actor, filed);
    }

    /// <summary>
    /// The Project a Task with no <c>project</c> is filed in: the first of the origin Room's, the parent's and the Task in hand's
    /// that is in the Task's Team and still listed by the catalog. <see langword="null"/> means the Team root.
    /// </summary>
    /// <param name="raw">The argument texts, with the origin Room already defaulted.</param>
    /// <param name="values">The parsed values, which carry the parent.</param>
    /// <param name="cancellationToken">Propagated to <see cref="ITeamDirectory"/>.</param>
    private async Task<ProjectDefault?> DefaultProjectAsync(RawArguments raw, ParsedValues values, CancellationToken cancellationToken)
    {
        if (catalog?.Find(raw.Team) is not { } team)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(raw.OriginRoomId)
            && await directory.GetRoomAsync(raw.OriginRoomId, cancellationToken).ConfigureAwait(false) is { } room
            && Listed(team, room.Team, room.Project) is { } fromRoom)
        {
            return new ProjectDefault(fromRoom, string.Create(CultureInfo.InvariantCulture, $"from Room \"{room.Name}\", where this Task was asked for"));
        }

        if (values.ParentId is { } parentId && this.ListedFor(team, parentId) is { } fromParent)
        {
            return new ProjectDefault(fromParent, string.Create(CultureInfo.InvariantCulture, $"from {parentId}, its parent"));
        }

        if (turnActivity?.SoleRoom(callerAgentId) is { } turnRoom
            && turnActivity.TaskInHand(callerAgentId, turnRoom) is { } inHand
            && this.ListedFor(team, inHand) is { } fromInHand)
        {
            return new ProjectDefault(fromInHand, string.Create(CultureInfo.InvariantCulture, $"from {inHand}, the Task you were woken for"));
        }

        return null;
    }

    /// <summary>The Project of Task <paramref name="id"/>, in the catalog's spelling, when the Task is in <paramref name="team"/> and the Team lists it.</summary>
    /// <param name="team">The Task's Team.</param>
    /// <param name="id">The Task to read the Project of.</param>
    private string? ListedFor(TeamSummary team, TaskId id) =>
        store?.Get(id) is { } source ? Listed(team, source.Location.Team, source.Location.Project) : null;

    /// <summary>The catalog's spelling of <paramref name="project"/> when <paramref name="sourceTeam"/> is <paramref name="team"/> (ignoring case) and the Team lists it.</summary>
    /// <param name="team">The Task's Team.</param>
    /// <param name="sourceTeam">The Team the default would come from.</param>
    /// <param name="project">The Project the default would come from.</param>
    private static string? Listed(TeamSummary team, string? sourceTeam, string? project) =>
        string.IsNullOrWhiteSpace(project) || !string.Equals(sourceTeam, team.Name, StringComparison.OrdinalIgnoreCase)
            ? null
            : team.Projects.FirstOrDefault(name => string.Equals(name, project, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Reads every argument's text in the order the checks run: <c>title</c> and <c>team</c> first (required), then the
    /// optional strings, the two lists and the remaining strings. The first wrong-typed value is the refusal.
    /// </summary>
    /// <param name="arguments">The tool's JSON arguments.</param>
    /// <param name="raw">The argument texts when this returns <see langword="true"/>.</param>
    /// <param name="refusal">The refusal text when this returns <see langword="false"/>.</param>
    private static bool TryReadRawArguments(JsonObject arguments, [NotNullWhen(true)] out RawArguments? raw, out string refusal)
    {
        raw = null;
        if (!TryReadRequired(arguments, out string title, out string team, out refusal))
        {
            return false;
        }

        if (!TryGetStrings(arguments, LeadingNames, out string?[] leading, out refusal))
        {
            return false;
        }

        if (!TaskToolText.TryGetStringList(arguments, "blocked_by", out IReadOnlyList<string>? blockedBy, out refusal))
        {
            return false;
        }

        if (!TaskToolText.TryGetStringList(arguments, "tags", out IReadOnlyList<string>? tags, out refusal))
        {
            return false;
        }

        if (!TryGetStrings(arguments, TrailingNames, out string?[] trailing, out refusal))
        {
            return false;
        }

        raw = new RawArguments(
            title,
            team,
            leading[0],
            leading[1],
            leading[2],
            leading[3],
            leading[4],
            leading[5],
            leading[6],
            blockedBy,
            tags,
            trailing[0],
            trailing[1],
            trailing[2]);
        refusal = string.Empty;
        return true;
    }

    /// <summary>Reads the two required arguments, <c>title</c> then <c>team</c>, each checked for type and then for blank.</summary>
    /// <param name="arguments">The tool's JSON arguments.</param>
    /// <param name="title">The title text when this returns <see langword="true"/>.</param>
    /// <param name="team">The team text when this returns <see langword="true"/>.</param>
    /// <param name="refusal">The refusal text when this returns <see langword="false"/>.</param>
    private static bool TryReadRequired(JsonObject arguments, out string title, out string team, out string refusal)
    {
        title = string.Empty;
        team = string.Empty;
        if (!TaskToolText.TryGetString(arguments, "title", out string? titleText, out refusal))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(titleText))
        {
            refusal = "'title' is a required argument.";
            return false;
        }

        if (!TaskToolText.TryGetString(arguments, "team", out string? teamText, out refusal))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(teamText))
        {
            refusal = "'team' is a required argument.";
            return false;
        }

        title = titleText;
        team = teamText;
        refusal = string.Empty;
        return true;
    }

    /// <summary>Reads several optional string arguments in order, stopping at the first one that is not a string.</summary>
    /// <param name="arguments">The tool's JSON arguments.</param>
    /// <param name="names">The argument names, in the order they are checked.</param>
    /// <param name="values">One text per name (null when absent) when this returns <see langword="true"/>.</param>
    /// <param name="refusal">The refusal text when this returns <see langword="false"/>.</param>
    private static bool TryGetStrings(JsonObject arguments, string[] names, out string?[] values, out string refusal)
    {
        values = new string?[names.Length];
        for (int index = 0; index < names.Length; index++)
        {
            if (!TaskToolText.TryGetString(arguments, names[index], out values[index], out refusal))
            {
                return false;
            }
        }

        refusal = string.Empty;
        return true;
    }

    /// <summary>
    /// Parses what the argument texts mean, in the §11.2 order: <c>status</c> (never a Won't do state), <c>priority</c>,
    /// the two dates, the sprint, the parent id and the blocking ids.
    /// </summary>
    /// <param name="raw">The argument texts.</param>
    /// <param name="values">The parsed values when this returns <see langword="true"/>.</param>
    /// <param name="refusal">The refusal text when this returns <see langword="false"/>.</param>
    private bool TryParseValues(RawArguments raw, [NotNullWhen(true)] out ParsedValues? values, out string refusal)
    {
        values = null;
        TaskState status = TaskState.Backlog;
        if (!string.IsNullOrWhiteSpace(raw.Status))
        {
            if (!TaskStates.TryParse(raw.Status, out status))
            {
                refusal = string.Create(CultureInfo.InvariantCulture, $"'status' has an unknown value '{raw.Status}'.");
                return false;
            }

            if (status.IsWontDo())
            {
                refusal = "Create the task first; to mark it Cancelled, Duplicate or Rejected, call update_task.";
                return false;
            }
        }

        TaskPriority priority = TaskPriority.Medium;
        if (!string.IsNullOrWhiteSpace(raw.Priority) && !TaskPriorities.TryParse(raw.Priority, out priority))
        {
            refusal = string.Create(CultureInfo.InvariantCulture, $"'priority' has an unknown value '{raw.Priority}'.");
            return false;
        }

        if (!TryParseDate(raw.StartDate, "start_date", out DateOnly? startDate, out refusal)
            || !TryParseDate(raw.DueDate, "due_date", out DateOnly? dueDate, out refusal))
        {
            return false;
        }

        SprintId? sprintId = null;
        if (!string.IsNullOrWhiteSpace(raw.Sprint) && !TaskToolText.TryResolveSprint(sprints, raw.Sprint, out sprintId, out refusal))
        {
            return false;
        }

        if (!TryParseTaskId(raw.Parent, out TaskId? parentId, out refusal)
            || !TryParseTaskIds(raw.BlockedBy, out List<TaskId>? blockedByIds, out refusal))
        {
            return false;
        }

        values = new ParsedValues(status, priority, startDate, dueDate, sprintId, parentId, blockedByIds);
        refusal = string.Empty;
        return true;
    }

    /// <summary>Assembles the draft; an assignee of <c>me</c> becomes the caller's name.</summary>
    /// <param name="raw">The argument texts.</param>
    /// <param name="values">The parsed values.</param>
    /// <param name="actor">The caller, as resolved by <see cref="TaskToolText.ResolveActorAsync"/>.</param>
    private static TaskDraft BuildDraft(RawArguments raw, ParsedValues values, TaskActor actor)
    {
        string? resolvedAssignee = null;
        if (!string.IsNullOrEmpty(raw.Assignee))
        {
            resolvedAssignee = string.Equals(raw.Assignee, "me", StringComparison.Ordinal) ? actor.Name : raw.Assignee;
        }

        return new TaskDraft(
            raw.Title,
            raw.Team,
            string.IsNullOrEmpty(raw.Project) ? null : raw.Project,
            values.Status,
            values.Priority,
            resolvedAssignee,
            raw.OriginRoomId,
            values.ParentId,
            values.BlockedByIds,
            raw.Tags,
            values.StartDate,
            values.DueDate,
            raw.Description ?? "",
            values.SprintId);
    }

    /// <summary>Words the outcome of <see cref="TaskService.Create"/> for the model.</summary>
    /// <param name="result">The service result.</param>
    /// <param name="actor">The caller.</param>
    /// <param name="filed">Where the Project came from when it was defaulted, else <see langword="null"/>.</param>
    private string DescribeResult(TaskResult? result, TaskActor actor, ProjectDefault? filed)
    {
        return result switch
        {
            TaskResult.Saved saved => this.BuildSuccessText(saved, actor, filed),
            TaskResult.Refused refused => string.Join('\n', refused.Problems),
            TaskResult.GateRefused gateRefused => gateRefused.Message,

            // CreateCore (TaskService.cs) returns only Refused, GateRefused or Saved - or throws, caught above by
            // TryRun - so Unchanged, Conflict and NotFound are all impossible from Create.
            _ => throw new UnreachableException("create_task's TaskService.Create returns only Refused or Saved; every other TaskResult case is impossible here."),
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

    /// <summary>The Room of the caller's one running Turn, when the caller is a Member of it; otherwise <see langword="null"/> and the origin stays empty.</summary>
    /// <param name="cancellationToken">Propagated to <see cref="ITeamDirectory"/>.</param>
    private async Task<string?> TurnRoomAsync(CancellationToken cancellationToken)
    {
        if (turnActivity?.SoleRoom(callerAgentId) is not { } roomId)
        {
            return null;
        }

        return await this.CheckOriginRoomMembershipAsync(roomId, cancellationToken).ConfigureAwait(false) is null ? roomId : null;
    }

    /// <summary>Builds the §11.2 success text: what was created, its assignee, and who is notified.</summary>
    /// <param name="saved">The result <see cref="TaskService.Create"/> returned.</param>
    /// <param name="actor">The caller, as resolved by <see cref="TaskToolText.ResolveActorAsync"/>.</param>
    /// <param name="filed">Where the Project came from when it was defaulted, else <see langword="null"/>.</param>
    private string BuildSuccessText(TaskResult.Saved saved, TaskActor actor, ProjectDefault? filed)
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

        string filedClause = filed is null
            ? string.Empty
            : string.Create(CultureInfo.InvariantCulture, $"Filed in project \"{filed.Project}\" ({filed.Reason}). Pass project to file it elsewhere. ");

        return string.Create(CultureInfo.InvariantCulture, $"Created {task.Id} \"{task.Title}\" in {location}, {assigneeClause}. {filedClause}{notify}{TaskActionNote.For(task, levels, gates, prompts)}");
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

    /// <summary>The argument texts exactly as the model sent them, once each has been checked to be the right JSON type.</summary>
    /// <param name="Title">The non-blank title.</param>
    /// <param name="Team">The non-blank team.</param>
    /// <param name="Project">The project, if any.</param>
    /// <param name="Description">The description, if any.</param>
    /// <param name="Status">The status text, if any.</param>
    /// <param name="Priority">The priority text, if any.</param>
    /// <param name="Assignee">The assignee, if any.</param>
    /// <param name="OriginRoomId">The origin Room id, if any.</param>
    /// <param name="Parent">The parent id text, if any.</param>
    /// <param name="BlockedBy">The blocking id texts, if any.</param>
    /// <param name="Tags">The tags, if any.</param>
    /// <param name="StartDate">The start date text, if any.</param>
    /// <param name="DueDate">The due date text, if any.</param>
    /// <param name="Sprint">The sprint text, if any.</param>
    private sealed record RawArguments(
        string Title,
        string Team,
        string? Project,
        string? Description,
        string? Status,
        string? Priority,
        string? Assignee,
        string? OriginRoomId,
        string? Parent,
        IReadOnlyList<string>? BlockedBy,
        IReadOnlyList<string>? Tags,
        string? StartDate,
        string? DueDate,
        string? Sprint);

    /// <summary>A Project chosen for a Task that named none, and where it came from.</summary>
    /// <param name="Project">The Project, in the catalog's spelling.</param>
    /// <param name="Reason">The source, worded to follow <c>Filed in project "P" (</c>.</param>
    private sealed record ProjectDefault(string Project, string Reason);

    /// <summary>The values parsed out of <see cref="RawArguments"/>.</summary>
    /// <param name="Status">The initial status.</param>
    /// <param name="Priority">The priority.</param>
    /// <param name="StartDate">The start date, if any.</param>
    /// <param name="DueDate">The due date, if any.</param>
    /// <param name="SprintId">The sprint, if any.</param>
    /// <param name="ParentId">The parent Task, if any.</param>
    /// <param name="BlockedByIds">The blocking Tasks, if any.</param>
    private sealed record ParsedValues(
        TaskState Status,
        TaskPriority Priority,
        DateOnly? StartDate,
        DateOnly? DueDate,
        SprintId? SprintId,
        TaskId? ParentId,
        List<TaskId>? BlockedByIds);
}

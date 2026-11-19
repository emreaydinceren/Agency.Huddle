using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Nodes;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Goldfish;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.HumanGate;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Acp.Tools;

/// <summary>
/// Shared text and argument-parsing helpers every App Tool over Tasks (D10, Spec §11.1) reuses,
/// so 10.2-10.6 build against one surface rather than each tool re-deriving the same wording:
/// rendering a Task as one line, resolving a task id argument, resolving the calling Agent's
/// <see cref="TaskActor"/>, phrasing the notify clause from a <see cref="WakePreview"/>, reading a
/// JSON argument without ever throwing, and turning a <see cref="TaskService"/> exception into the
/// §9.1 refusal text (corrections-B4 D10 items 2, 4 and 5).
/// </summary>
internal static class TaskToolText
{
    /// <summary>The refusal for a status text Huddle cannot parse: one message that teaches the new model when it names the removed Review state (D-13).</summary>
    /// <param name="statusText">The status text the call carried.</param>
    public static string UnknownStatusRefusal(string statusText)
    {
        ArgumentNullException.ThrowIfNull(statusText);

        return string.Equals(statusText.Trim(), "Review", StringComparison.OrdinalIgnoreCase)
            ? ProcessGateService.ReviewStateRefusal
            : string.Create(CultureInfo.InvariantCulture, $"'status' has an unknown value '{statusText}'.");
    }

    /// <summary>
    /// Indents every non-blank line after the first by two spaces, so a multi-line comment stays one readable block under its
    /// heading line. A blank line stays empty rather than carrying trailing spaces.
    /// </summary>
    /// <param name="text">The text, with any line ending.</param>
    public static string IndentContinuation(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        string[] lines = text.ReplaceLineEndings("\n").Split('\n');
        for (int index = 1; index < lines.Length; index++)
        {
            if (lines[index].Length > 0)
            {
                lines[index] = "  " + lines[index];
            }
        }

        return string.Join('\n', lines);
    }

    /// <summary>
    /// Renders <paramref name="task"/> as the one-line summary every listing tool shows (Spec
    /// §11.1): id, status, priority, <c>rank N</c> (only when the Task has a rank), assignee (or "unassigned"), <c>Team[/Project]</c> (with
    /// "(closed)" appended when Closed), and title, separated by " | ".
    /// </summary>
    /// <param name="task">The Task to render.</param>
    public static string Line(TaskItem task)
    {
        ArgumentNullException.ThrowIfNull(task);

        string assignee = string.IsNullOrEmpty(task.Assignee) ? "unassigned" : task.Assignee;
        string location = task.Location.Project is null
            ? task.Location.Team
            : $"{task.Location.Team}/{task.Location.Project}";
        if (task.Location.Closed)
        {
            location = $"{location} (closed)";
        }

        string rank = task.Rank is { } value ? string.Create(CultureInfo.InvariantCulture, $" | rank {value}") : string.Empty;
        return $"{task.Id} | {task.Status.ToWire()} | {task.Priority.ToWire()}{rank} | {assignee} | {location} | {task.Title}";
    }

    /// <summary>
    /// Renders one open Proof item as <c>label (owner): detail</c>, the owner and the detail only when it has them: the one format
    /// <c>get_task</c> and <c>add_evidence</c> both print.
    /// </summary>
    /// <param name="item">The open Proof item.</param>
    public static string OpenProofItem(ProofItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        string owner = item.Owner is null ? string.Empty : $" ({item.Owner})";
        string detail = item.Detail is null ? string.Empty : $": {item.Detail}";
        return string.Create(CultureInfo.InvariantCulture, $"{item.Label}{owner}{detail}");
    }

    /// <summary>
    /// Resolves a <c>taskId</c>-shaped argument against <paramref name="store"/> (Spec §11.1): text
    /// that doesn't parse as a <see cref="TaskId"/>, or that parses but names no known Task, is
    /// refused with the §11.1 wording rather than thrown.
    /// </summary>
    /// <param name="store">The Task store to look the id up in.</param>
    /// <param name="text">The argument text, or <see langword="null"/>.</param>
    /// <param name="task">The resolved Task, or <see langword="null"/> when this returns <see langword="false"/>.</param>
    /// <param name="refusal">The refusal text when this returns <see langword="false"/>; otherwise <see cref="string.Empty"/>.</param>
    public static bool TryResolve(TaskStore store, string? text, [NotNullWhen(true)] out TaskItem? task, out string refusal)
    {
        ArgumentNullException.ThrowIfNull(store);

        if (!TaskId.TryParse(text, out TaskId id))
        {
            task = null;
            refusal = $"'{text}' is not a task id; ids look like PLAT-0042.";
            return false;
        }

        TaskItem? found = store.Get(id);
        if (found is null)
        {
            task = null;
            refusal = store.FindArchived(id) is TaskItem archived
                ? $"Task {id} is in an archived Team. Unarchive {archived.Location.Team} to change it."
                : $"Unknown task '{id}'.";
            return false;
        }

        task = found;
        refusal = string.Empty;
        return true;
    }

    /// <summary>
    /// Like <see cref="TryResolve"/> for a tool that only reads (Spec 8.5): an id from an archived Team resolves to its
    /// <c>Archive/</c> file, read-only.
    /// </summary>
    /// <param name="store">The Task store to look the id up in.</param>
    /// <param name="text">The argument text, or <see langword="null"/>.</param>
    /// <param name="task">The resolved Task, or <see langword="null"/> when this returns <see langword="false"/>.</param>
    /// <param name="refusal">The refusal text when this returns <see langword="false"/>; otherwise <see cref="string.Empty"/>.</param>
    public static bool TryResolveForRead(TaskStore store, string? text, [NotNullWhen(true)] out TaskItem? task, out string refusal)
    {
        ArgumentNullException.ThrowIfNull(store);

        if (TryResolve(store, text, out task, out refusal))
        {
            return true;
        }

        if (TaskId.TryParse(text, out TaskId id) && store.FindArchived(id) is TaskItem archived)
        {
            task = archived;
            refusal = string.Empty;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves a <c>sprint</c> argument against <paramref name="catalog"/> (Sprints spec §10.1), in a fixed
    /// order: <c>current</c> (case-insensitive) is the catalogue's current Sprint, then a Sprint id, then a
    /// Sprint name (case-insensitive), and anything else is refused with the known Sprints. §6.1 guarantees no
    /// name can be mistaken for <c>current</c> or an id, so the order cannot change an answer.
    /// </summary>
    /// <param name="catalog">The read-only Sprint catalogue; a tool never holds the store that can create one.</param>
    /// <param name="text">The argument text.</param>
    /// <param name="sprint">The resolved Sprint's id, or <see langword="null"/> when this returns <see langword="false"/>.</param>
    /// <param name="refusal">The refusal text when this returns <see langword="false"/>; otherwise <see cref="string.Empty"/>.</param>
    public static bool TryResolveSprint(ISprintCatalog catalog, string text, [NotNullWhen(true)] out SprintId? sprint, out string refusal)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(text);

        string trimmed = text.Trim();
        Sprint? found;
        if (string.Equals(trimmed, "current", StringComparison.OrdinalIgnoreCase))
        {
            found = catalog.Current;
            if (found is null)
            {
                sprint = null;
                refusal = "There is no current sprint; no sprints exist yet.";
                return false;
            }
        }
        else if (SprintId.TryParse(trimmed, out SprintId id))
        {
            found = catalog.Get(id);
        }
        else
        {
            found = catalog.FindByName(trimmed);
        }

        if (found is null)
        {
            IReadOnlyList<Sprint> known = catalog.All;
            string knownText = known.Count == 0
                ? "There are no sprints yet."
                : "Known sprints: " + string.Join(", ", known.Select(static each => $"{each.Id} {each.Name}")) + ".";
            sprint = null;
            refusal = $"Unknown sprint '{text}'. {knownText}";
            return false;
        }

        sprint = found.Id;
        refusal = string.Empty;
        return true;
    }

    /// <summary>
    /// Resolves the calling Agent's <see cref="TaskActor"/> from <see cref="ITeamDirectory.GetUserAsync"/>
    /// (Spec §11.1, corrections-B4 D10 item 2). An unknown caller is refused; the raw
    /// <paramref name="callerAgentId"/> is never used as the Actor's Name.
    /// </summary>
    /// <param name="directory">Resolves the caller's <see cref="User"/> row.</param>
    /// <param name="callerAgentId">The calling Agent's user id.</param>
    /// <param name="cancellationToken">Propagated to <see cref="ITeamDirectory.GetUserAsync"/>.</param>
    /// <param name="goldfish">When given and the caller is possessed, the Actor's Name carries the goldfish stamp; the id never does.</param>
    /// <returns>
    /// The resolved <see cref="TaskActor"/> paired with an empty <c>Refusal</c>, or a
    /// <see langword="null"/> Actor paired with a non-empty <c>Refusal</c> when the caller is
    /// unknown - never both null and never both set, so a caller need only branch on
    /// <c>Actor is null</c>.
    /// </returns>
    public static async Task<(TaskActor? Actor, string Refusal)> ResolveActorAsync(
        ITeamDirectory directory, string callerAgentId, CancellationToken cancellationToken, GoldfishGuard? goldfish = null)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(callerAgentId);

        User? user = await directory.GetUserAsync(callerAgentId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return (null, $"Could not identify caller '{callerAgentId}' as an Agent.");
        }

        string name = goldfish?.PossessionOf(callerAgentId) is not null ? GoldfishStamp.Apply(user.Name) : user.Name;
        return (new TaskActor(TaskActorKind.Agent, name, callerAgentId), string.Empty);
    }

    /// <summary>
    /// The opening every tool that acts on one existing Task runs, in order: read the required
    /// <c>taskId</c> argument, resolve it against <paramref name="store"/> (<see cref="TryResolve"/>),
    /// then resolve the caller (<see cref="ResolveActorAsync"/>). The first step that fails decides
    /// the refusal.
    /// </summary>
    /// <param name="arguments">The tool's JSON arguments.</param>
    /// <param name="store">The Task store the id is looked up in.</param>
    /// <param name="directory">Resolves the caller's <see cref="User"/> row.</param>
    /// <param name="callerAgentId">The calling Agent's user id.</param>
    /// <param name="cancellationToken">Propagated to <see cref="ITeamDirectory.GetUserAsync"/>.</param>
    /// <param name="goldfish">Passed to <see cref="ResolveActorAsync"/>.</param>
    /// <returns>The target with an empty <c>Refusal</c>, or a <see langword="null"/> target with the refusal text.</returns>
    public static async Task<(TaskToolTarget? Target, string Refusal)> StartAsync(
        JsonObject arguments, TaskStore store, ITeamDirectory directory, string callerAgentId, CancellationToken cancellationToken, GoldfishGuard? goldfish = null)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!TryGetString(arguments, "taskId", out string? taskIdText, out string taskIdRefusal))
        {
            return (null, taskIdRefusal);
        }

        if (!TryResolve(store, taskIdText, out TaskItem? task, out string resolveRefusal))
        {
            return (null, resolveRefusal);
        }

        (TaskActor? actor, string actorRefusal) = await ResolveActorAsync(directory, callerAgentId, cancellationToken, goldfish).ConfigureAwait(false);
        return actor is null ? (null, actorRefusal) : (new TaskToolTarget(task, actor), string.Empty);
    }

    /// <summary>The input schema of a tool whose only argument is the required <c>taskId</c>.</summary>
    public static JsonObject TaskIdSchema() => SingleStringSchema("taskId");

    /// <summary>The input schema of a tool whose only argument is one required string; a fresh object on every call, because a JSON node has one parent.</summary>
    /// <param name="name">The argument's name.</param>
    public static JsonObject SingleStringSchema(string name) => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            [name] = new JsonObject { ["type"] = "string" },
        },
        ["required"] = new JsonArray { name },
    };

    /// <summary>
    /// Reads a string argument that must be present and not blank, never throwing: a wrongly-typed
    /// value is refused as <see cref="TryGetString"/> refuses it, and an absent or blank one with
    /// <c>'name' is a required argument.</c>
    /// </summary>
    /// <param name="arguments">The tool's JSON arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <param name="value">The value, untrimmed, or <see langword="null"/> when this returns <see langword="false"/>.</param>
    /// <param name="refusal">The refusal text, or <see cref="string.Empty"/> when this returns <see langword="true"/>.</param>
    public static bool TryGetRequiredString(JsonObject arguments, string name, [NotNullWhen(true)] out string? value, out string refusal)
    {
        if (!TryGetString(arguments, name, out value, out refusal))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            value = null;
            refusal = $"'{name}' is a required argument.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Words a <see cref="TaskService"/> write that was not saved: the problems of a
    /// <see cref="TaskResult.Refused"/>, the message of a <see cref="TaskResult.GateRefused"/>, and the
    /// §11.1 texts for <see cref="TaskResult.Conflict"/> and <see cref="TaskResult.NotFound"/>.
    /// </summary>
    /// <param name="result">The write's result.</param>
    /// <param name="refusal">The refusal text, or <see cref="string.Empty"/> when the result is <see cref="TaskResult.Saved"/> or <see cref="TaskResult.Unchanged"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="result"/> is a refusal.</returns>
    public static bool TryDescribeRefusal(TaskResult result, out string refusal)
    {
        ArgumentNullException.ThrowIfNull(result);

        refusal = result switch
        {
            TaskResult.Refused refused => string.Join('\n', refused.Problems),
            TaskResult.GateRefused gateRefused => gateRefused.Message,
            TaskResult.Conflict conflict => $"{conflict.Current.Id} was changed by someone else; please try again.",
            TaskResult.NotFound notFound => $"Unknown task '{notFound.Id}'.",
            _ => string.Empty,
        };
        return refusal.Length > 0;
    }

    /// <summary>
    /// <see cref="TryDescribeRefusal"/> for a tool that has already handled every result it can
    /// succeed with: a <see cref="TaskResult.Saved"/> or <see cref="TaskResult.Unchanged"/> reaching
    /// here is a bug in the caller, not a refusal.
    /// </summary>
    /// <param name="result">The write's result.</param>
    /// <param name="toolName">The tool's name, for the exception message.</param>
    public static string Refusal(TaskResult result, string toolName)
    {
        if (!TryDescribeRefusal(result, out string refusal))
        {
            throw new UnreachableException($"{toolName} handles every {result.GetType().Name} result before it asks for a refusal.");
        }

        return refusal;
    }

    /// <summary>
    /// Phrases who will be notified of a Task change from <paramref name="preview"/> (DM table,
    /// corrections-B4 D10 item 2): the assignee's Name when nothing blocks the wake, and a clause
    /// naming the guard otherwise.
    /// </summary>
    /// <param name="preview">The resolved <see cref="WakePreview"/> for the change.</param>
    /// <param name="callerName">The caller's Name, validated but otherwise unused: the table's wording never distinguishes on it.</param>
    public static string NotifyClause(WakePreview preview, string callerName)
    {
        ArgumentNullException.ThrowIfNull(preview);
        ArgumentException.ThrowIfNullOrWhiteSpace(callerName);

        return preview.Block switch
        {
            WakeBlock.None => $"{preview.AssigneeName} will be notified.",
            WakeBlock.AssigneeIsHuman => "You aren't notified; the Human sees it on the Tasks page.",
            WakeBlock.BudgetPaused => "No one is notified: wake-ups for this task are paused.",
            WakeBlock.Disabled => "No one is notified: wake-ups are off.",
            _ => "No one is notified.",
        };
    }

    /// <summary>
    /// Reads a string argument, never throwing (corrections-B4 D10 item 4): an absent argument
    /// yields <see langword="null"/> with no refusal (the caller decides whether it was required),
    /// and a present but wrongly-typed argument is refused, naming the argument and the expected
    /// type.
    /// </summary>
    /// <param name="arguments">The tool's JSON arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <param name="value">The parsed value, or <see langword="null"/> when absent or refused.</param>
    /// <param name="refusal">The refusal text, or <see cref="string.Empty"/> when this returns <see langword="true"/>.</param>
    public static bool TryGetString(JsonObject arguments, string name, out string? value, out string refusal)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        JsonNode? node = arguments[name];
        if (node is null)
        {
            value = null;
            refusal = string.Empty;
            return true;
        }

        if (node is JsonValue jsonValue && jsonValue.TryGetValue(out string? text))
        {
            value = text;
            refusal = string.Empty;
            return true;
        }

        value = null;
        refusal = $"'{name}' must be text.";
        return false;
    }

    /// <summary>
    /// Reads an integer argument, never throwing (corrections-B4 D10 item 4): an absent argument
    /// falls back to <paramref name="defaultValue"/> when one is supplied, or is refused as
    /// required when it is not; a present but wrongly-typed argument is refused, naming the
    /// argument and the expected type.
    /// </summary>
    /// <param name="arguments">The tool's JSON arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <param name="defaultValue">The value to use when the argument is absent, or <see langword="null"/> when it is required.</param>
    /// <param name="value">The parsed or defaulted value; <c>0</c> when this returns <see langword="false"/>.</param>
    /// <param name="refusal">The refusal text, or <see cref="string.Empty"/> when this returns <see langword="true"/>.</param>
    public static bool TryGetInt(JsonObject arguments, string name, int? defaultValue, out int value, out string refusal)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        JsonNode? node = arguments[name];
        if (node is null)
        {
            if (defaultValue is null)
            {
                value = 0;
                refusal = $"'{name}' is a required argument.";
                return false;
            }

            value = defaultValue.Value;
            refusal = string.Empty;
            return true;
        }

        if (node is JsonValue jsonValue && jsonValue.TryGetValue(out int parsed))
        {
            value = parsed;
            refusal = string.Empty;
            return true;
        }

        value = defaultValue ?? 0;
        refusal = $"'{name}' must be a whole number.";
        return false;
    }

    /// <summary>
    /// Reads a boolean argument, never throwing (corrections-B4 D10 item 4): an absent argument
    /// falls back to <paramref name="defaultValue"/> when one is supplied, or is refused as
    /// required when it is not; a present but wrongly-typed argument is refused, naming the
    /// argument and the expected type.
    /// </summary>
    /// <param name="arguments">The tool's JSON arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <param name="defaultValue">The value to use when the argument is absent, or <see langword="null"/> when it is required.</param>
    /// <param name="value">The parsed or defaulted value; <see langword="false"/> when this returns <see langword="false"/>.</param>
    /// <param name="refusal">The refusal text, or <see cref="string.Empty"/> when this returns <see langword="true"/>.</param>
    public static bool TryGetBool(JsonObject arguments, string name, bool? defaultValue, out bool value, out string refusal)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        JsonNode? node = arguments[name];
        if (node is null)
        {
            if (defaultValue is null)
            {
                value = false;
                refusal = $"'{name}' is a required argument.";
                return false;
            }

            value = defaultValue.Value;
            refusal = string.Empty;
            return true;
        }

        if (node is JsonValue jsonValue && jsonValue.TryGetValue(out bool parsed))
        {
            value = parsed;
            refusal = string.Empty;
            return true;
        }

        value = defaultValue ?? false;
        refusal = $"'{name}' must be true or false.";
        return false;
    }

    /// <summary>
    /// Reads a list-of-strings argument, never throwing (corrections-B4 D10 item 4): an absent
    /// argument yields <see langword="null"/> with no refusal (the caller decides whether that
    /// means "unspecified" or "clear it"), and an argument that isn't an array, or whose items
    /// aren't all strings, is refused, naming the argument and the expected type.
    /// </summary>
    /// <param name="arguments">The tool's JSON arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <param name="value">The parsed items, or <see langword="null"/> when absent or refused.</param>
    /// <param name="refusal">The refusal text, or <see cref="string.Empty"/> when this returns <see langword="true"/>.</param>
    public static bool TryGetStringList(JsonObject arguments, string name, out IReadOnlyList<string>? value, out string refusal)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        JsonNode? node = arguments[name];
        if (node is null)
        {
            value = null;
            refusal = string.Empty;
            return true;
        }

        if (node is not JsonArray array)
        {
            value = null;
            refusal = $"'{name}' must be a list of text.";
            return false;
        }

        List<string> items = new(array.Count);
        foreach (JsonNode? item in array)
        {
            if (item is JsonValue itemValue && itemValue.TryGetValue(out string? text))
            {
                items.Add(text);
            }
            else
            {
                value = null;
                refusal = $"'{name}' must be a list of text.";
                return false;
            }
        }

        value = items;
        refusal = string.Empty;
        return true;
    }

    /// <summary>The §9.1 wording for a save that failed with <paramref name="exception"/>'s message.</summary>
    /// <param name="exception">The exception a <see cref="TaskService"/> call threw.</param>
    public static string SaveFailed(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return $"Could not save the task: {exception.Message}";
    }

    /// <summary>
    /// Runs a <see cref="TaskService"/> mutation, mapping the R4 "Create collision" exceptions
    /// (<see cref="InvalidOperationException"/>, an allocator invariant break, and
    /// <see cref="IOException"/>, a racing file) to <see cref="SaveFailed"/>'s text rather than
    /// letting them propagate: every mutating App Tool (10.2-10.6) never throws.
    /// </summary>
    /// <param name="operation">The <see cref="TaskService"/> call to run.</param>
    /// <param name="result">The operation's result, or <see langword="null"/> when this returns <see langword="false"/>.</param>
    /// <param name="refusal">The refusal text, or <see cref="string.Empty"/> when this returns <see langword="true"/>.</param>
    public static bool TryRun(Func<TaskResult> operation, [NotNullWhen(true)] out TaskResult? result, out string refusal)
    {
        ArgumentNullException.ThrowIfNull(operation);

        try
        {
            result = operation();
            refusal = string.Empty;
            return true;
        }
        catch (InvalidOperationException exception)
        {
            result = null;
            refusal = SaveFailed(exception);
            return false;
        }
        catch (IOException exception)
        {
            result = null;
            refusal = SaveFailed(exception);
            return false;
        }
    }
}

using System.Text.Json.Nodes;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Tasks;

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
    /// <summary>
    /// Renders <paramref name="task"/> as the one-line summary every listing tool shows (Spec
    /// §11.1): id, status, priority, assignee (or "unassigned"), <c>Team[/Project]</c> (with
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

        return $"{task.Id} | {task.Status.ToWire()} | {task.Priority.ToWire()} | {assignee} | {location} | {task.Title}";
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
    public static bool TryResolve(TaskStore store, string? text, out TaskItem? task, out string refusal)
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
            refusal = $"Unknown task '{id}'.";
            return false;
        }

        task = found;
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
    /// <returns>The resolved <see cref="TaskActor"/>, or a refusal when the caller is unknown.</returns>
    public static async Task<(TaskActor? Actor, string? Refusal)> ResolveActorAsync(
        ITeamDirectory directory, string callerAgentId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(callerAgentId);

        User? user = await directory.GetUserAsync(callerAgentId, cancellationToken);
        if (user is null)
        {
            return (null, $"Could not identify caller '{callerAgentId}' as a Teammate.");
        }

        return (new TaskActor(TaskActorKind.Agent, user.Name, callerAgentId), null);
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
    public static bool TryRun(Func<TaskResult> operation, out TaskResult? result, out string refusal)
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

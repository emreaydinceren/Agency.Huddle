namespace Agency.Huddle.App.Acp.Tools;

using System.Globalization;
using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Tasks;

/// <summary>
/// Reads one Task's full text (Spec §11.3): the §11.1 line, a <c>key: value</c> line for every
/// non-empty field, a blank line, the description, and - only when <c>include_change_log</c> asks
/// for it - the Change log's last 50 entries. Read-only, unlike the other five App Tools (D10): it
/// takes no <c>callerAgentId</c> and no <see cref="TaskService"/> (corrections-B4 D10 item 1),
/// because reading a Task changes nothing, so there is no actor to resolve and no notify clause to
/// build.
/// </summary>
internal sealed class GetTaskTool(TaskStore store, IPromptSource prompts) : IAppTool
{
    private static readonly IReadOnlyDictionary<string, string> NoValues = new Dictionary<string, string>();

    /// <inheritdoc />
    public string Name => "get_task";

    /// <inheritdoc />
    public string Description => prompts.Render("tool.getTask.description", NoValues);

    /// <inheritdoc />
    public JsonObject InputSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["taskId"] = new JsonObject { ["type"] = "string" },
            ["include_change_log"] = new JsonObject { ["type"] = "boolean" },
        },
        ["required"] = new JsonArray { "taskId" },
    };

    /// <summary>
    /// Resolves <c>taskId</c> against <see cref="TaskStore"/> and renders it (Spec §11.3). Every
    /// expected failure - a missing or wrongly-typed argument, or an id that doesn't resolve - is
    /// returned as text, never thrown.
    /// </summary>
    /// <param name="arguments">The tool's JSON arguments.</param>
    /// <param name="cancellationToken">Unused: every lookup this tool makes is synchronous.</param>
    public Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        _ = cancellationToken;

        if (!TaskToolText.TryGetString(arguments, "taskId", out string? taskIdText, out string taskIdTypeRefusal))
        {
            return Task.FromResult(taskIdTypeRefusal);
        }

        if (string.IsNullOrWhiteSpace(taskIdText))
        {
            return Task.FromResult("'taskId' is a required argument.");
        }

        if (!TaskToolText.TryResolve(store, taskIdText, out TaskItem? task, out string idRefusal) || task is null)
        {
            return Task.FromResult(idRefusal);
        }

        if (!TaskToolText.TryGetBool(arguments, "include_change_log", false, out bool includeChangeLog, out string includeChangeLogRefusal))
        {
            return Task.FromResult(includeChangeLogRefusal);
        }

        return Task.FromResult(this.Render(task, includeChangeLog));
    }

    /// <summary>Builds the §11.3 output for <paramref name="task"/>.</summary>
    /// <param name="task">The Task to render.</param>
    /// <param name="includeChangeLog">Whether to append the last 50 Change log entries.</param>
    private string Render(TaskItem task, bool includeChangeLog)
    {
        List<string> lines = [TaskToolText.Line(task)];

        if (task.Creator.Length > 0)
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"creator: {task.Creator}"));
        }

        if (task.Parent is { } parent)
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"parent: {this.FormatReference(parent)}"));
        }

        if (task.BlockedBy.Count > 0)
        {
            string blockers = string.Join(", ", task.BlockedBy.Select(this.FormatReference));
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"blocked_by: {blockers}"));
        }

        if (task.DuplicateOf is { } duplicateOf)
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"duplicate_of: {this.FormatReference(duplicateOf)}"));
        }

        if (task.Tags.Count > 0)
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"tags: {string.Join(", ", task.Tags)}"));
        }

        if (task.StartDate is { } startDate)
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"start_date: {startDate:yyyy-MM-dd}"));
        }

        if (task.DueDate is { } dueDate)
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"due_date: {dueDate:yyyy-MM-dd}"));
        }

        string header = string.Join('\n', lines);
        string body = string.Create(CultureInfo.InvariantCulture, $"{header}\n\n{task.Description}");

        if (!includeChangeLog || task.ChangeLog.Count == 0)
        {
            return body;
        }

        IEnumerable<ChangeLogEntry> lastFifty = task.ChangeLog.Count > 50
            ? task.ChangeLog.Skip(task.ChangeLog.Count - 50)
            : task.ChangeLog;
        string entriesText = string.Join('\n', lastFifty.Select(FormatEntry));

        return string.Create(CultureInfo.InvariantCulture, $"{body}\n\nChange log:\n{entriesText}");
    }

    /// <summary>Renders a Task reference with its current status (Spec §11.3), or just the id when the reference no longer resolves.</summary>
    /// <param name="id">The referenced Task's id.</param>
    private string FormatReference(TaskId id)
    {
        TaskItem? referenced = store.Get(id);
        return referenced is null
            ? id.ToString()
            : string.Create(CultureInfo.InvariantCulture, $"{id} ({referenced.Status.ToWire()})");
    }

    /// <summary>Formats one Change log entry.</summary>
    /// <param name="entry">The entry to format.</param>
    private static string FormatEntry(ChangeLogEntry entry) =>
        string.Create(CultureInfo.InvariantCulture, $"{entry.At:yyyy-MM-dd HH:mm} {entry.Actor}: {entry.Summary}");
}

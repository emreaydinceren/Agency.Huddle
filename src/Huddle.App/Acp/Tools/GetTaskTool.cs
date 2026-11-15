namespace Agency.Huddle.App.Acp.Tools;

using System.Globalization;
using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.HumanGate;

/// <summary>
/// Reads one Task's full text (Spec §11.3): the §11.1 line, a <c>key: value</c> line for every
/// non-empty field, a blank line, the description, and a "Timeline:" block of the last 50 entries,
/// oldest first: the comments and Evidence, plus the events (the Change log) only when
/// <c>include_change_log</c> asks for them. Read-only, unlike the other five App Tools (D10): it
/// takes no <c>callerAgentId</c> and no <see cref="TaskService"/> (corrections-B4 D10 item 1),
/// because reading a Task changes nothing, so there is no actor to resolve and no notify clause to
/// build.
/// </summary>
internal sealed class GetTaskTool(TaskStore store, ISprintCatalog sprints, IPromptSource prompts, IProofSource? proofs = null) : IAppTool
{
    /// <inheritdoc />
    public string Name => "get_task";

    /// <inheritdoc />
    public string Description => prompts.Render("tool.getTask.description");

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

        if (!TaskToolText.TryGetRequiredString(arguments, "taskId", out string? taskIdText, out string taskIdRefusal))
        {
            return Task.FromResult(taskIdRefusal);
        }

        if (!TaskToolText.TryResolveForRead(store, taskIdText, out TaskItem? task, out string idRefusal))
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
    /// <param name="includeChangeLog">Whether the timeline block also carries the events (the Change log).</param>
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

        if (task.Sprint is { } sprint)
        {
            string sprintName = sprints.Get(sprint)?.Name ?? "missing";
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"sprint: {sprint} ({sprintName})"));
        }

        AddHumanGateLines(lines, task, proofs);
        string header = string.Join('\n', lines);
        string body = string.Create(CultureInfo.InvariantCulture, $"{header}\n\n{task.Description}");

        List<TimelineEntry> entries = [.. task.Timeline.Where(entry => includeChangeLog || entry.Kind != TimelineEntryKind.Event)];
        if (entries.Count == 0)
        {
            return body;
        }

        IEnumerable<TimelineEntry> lastFifty = entries.Count > 50 ? entries.Skip(entries.Count - 50) : entries;
        string entriesText = string.Join('\n', lastFifty.Select(FormatEntry));

        return string.Create(CultureInfo.InvariantCulture, $"{body}\n\nTimeline:\n{entriesText}");
    }

    /// <summary>
    /// Adds the Human gate lines (Human gate Spec §6.10): <c>human_gate: true</c>, the <c>action</c>, the Proof as
    /// <c>proof: met/total</c> with one <c>- label (owner): detail</c> line per open item (the owner and the detail only when it has them), and the acceptance. Each is
    /// omitted when it does not apply.
    /// </summary>
    /// <param name="lines">The header lines built so far.</param>
    /// <param name="task">The Task being rendered.</param>
    /// <param name="proofSource">The Proof source, or <see langword="null"/> when none is wired.</param>
    private static void AddHumanGateLines(List<string> lines, TaskItem task, IProofSource? proofSource)
    {
        if (!task.HumanGate)
        {
            return;
        }

        lines.Add("human_gate: true");
        if (!string.IsNullOrEmpty(task.Action))
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"action: {task.Action}"));
        }

        Proof? proof = proofSource?.For(task);
        if (proof is not null && proof.Total > 0)
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"proof: {proof.MetCount}/{proof.Total}"));
            foreach (ProofItem item in proof.Items.Where(static candidate => !candidate.Met))
            {
                string owner = item.Owner is null ? string.Empty : $" ({item.Owner})";
                string detail = item.Detail is null ? string.Empty : $": {item.Detail}";
                lines.Add(string.Create(CultureInfo.InvariantCulture, $"- {item.Label}{owner}{detail}"));
            }
        }

        AcceptanceInfo acceptance = Acceptance.Of(task);
        if (acceptance.State == AcceptanceState.Valid && acceptance.At is { } at)
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"accepted: {at.UtcDateTime:yyyy-MM-dd'T'HH:mm:ss'Z'} by {acceptance.By}"));
        }
        else if (acceptance.State == AcceptanceState.Stale)
        {
            lines.Add("acceptance: stale");
        }
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

    /// <summary>
    /// Formats one timeline entry: <c>time author comment: text</c> or <c>time author evidence: text</c> (a multi-line text has its
    /// continuation lines indented), and an event as <c>time actor: summary</c>, as the Change log has always printed it.
    /// </summary>
    /// <param name="entry">The entry to format.</param>
    private static string FormatEntry(TimelineEntry entry) => entry.Kind switch
    {
        TimelineEntryKind.Comment => string.Create(CultureInfo.InvariantCulture, $"{entry.At:yyyy-MM-dd HH:mm} {entry.Author} comment: {TaskToolText.IndentContinuation(entry.Text)}"),
        TimelineEntryKind.Evidence => string.Create(CultureInfo.InvariantCulture, $"{entry.At:yyyy-MM-dd HH:mm} {entry.Author} evidence: {TaskToolText.IndentContinuation(entry.Text)}"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{entry.At:yyyy-MM-dd HH:mm} {entry.Author}: {entry.Text}"),
    };
}

using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.App.Teams;

/// <summary>One Task that changed since the cutoff, as the retro sees it.</summary>
/// <param name="Id">The Task id.</param>
/// <param name="Title">The Task title.</param>
/// <param name="Status">The Task's current status, by its wire name.</param>
/// <param name="Assignee">The assignee, or <see langword="null"/> when unassigned.</param>
internal sealed record TaskRecord(string Id, string Title, string Status, string? Assignee);

/// <summary>One Request (a Task with Request fields) that changed since the cutoff.</summary>
/// <param name="Id">The Task id.</param>
/// <param name="Title">The Request title.</param>
/// <param name="Kind">The Request kind (Bug, Feature, Feedback or Question).</param>
/// <param name="FromTeam">The requesting Team.</param>
/// <param name="Triage">The triage state (None, NeedsInfo, Accepted or Declined).</param>
/// <param name="Status">The Task status, by its wire name.</param>
internal sealed record RequestRecord(string Id, string Title, string Kind, string FromTeam, string Triage, string Status);

/// <summary>One notable event, with a link back to its source.</summary>
/// <param name="Time">When it happened.</param>
/// <param name="TaskId">The Task it concerns.</param>
/// <param name="Member">Who made the change.</param>
/// <param name="Summary">The Change log summary, verbatim.</param>
/// <param name="Link">The Task id, or a log path with a line number.</param>
internal sealed record RecordEvent(DateTimeOffset Time, string TaskId, string Member, string Summary, string Link);

/// <summary>Tool calls of one member and tool since the cutoff.</summary>
/// <param name="Member">The member whose log was read.</param>
/// <param name="Tool">The tool name.</param>
/// <param name="Calls">How many calls were logged.</param>
/// <param name="Failures">How many of them failed.</param>
internal sealed record ToolCallSummary(string Member, string Tool, int Calls, int Failures);

/// <summary>The compact evidence a retro starts from (RT-3, RT-8, RT-9).</summary>
/// <param name="Team">The Team's display name.</param>
/// <param name="Since">The cutoff; nothing older is included.</param>
/// <param name="Tasks">Tasks of the Team with a change since the cutoff.</param>
/// <param name="ReviewBounces">Review to In Progress or To Do changes.</param>
/// <param name="BudgetPauses">Change log entries that mention a spent budget.</param>
/// <param name="WakeLimitsHit">Change log entries that mention a wake limit.</param>
/// <param name="Escalations">Declines and escalations.</param>
/// <param name="SpendByMember">Each member's stored spend.</param>
/// <param name="Requests">Requests with a change since the cutoff.</param>
/// <param name="ToolCalls">The tool-call log, aggregated.</param>
internal sealed record RecordsBundle(
    string Team,
    DateTimeOffset Since,
    IReadOnlyList<TaskRecord> Tasks,
    IReadOnlyList<RecordEvent> ReviewBounces,
    IReadOnlyList<RecordEvent> BudgetPauses,
    IReadOnlyList<RecordEvent> WakeLimitsHit,
    IReadOnlyList<RecordEvent> Escalations,
    IReadOnlyDictionary<string, IReadOnlyList<StoredSpend>> SpendByMember,
    IReadOnlyList<RequestRecord> Requests,
    IReadOnlyList<ToolCallSummary> ToolCalls)
{
    /// <summary>How many tool-call log lines or files could not be read and were skipped.</summary>
    public int Unreadable { get; init; }
}

/// <summary>Builds a <see cref="RecordsBundle"/> from Task change logs, the tool-call log and the spend store, and renders it as compact text.</summary>
/// <param name="tasks">The Task index.</param>
/// <param name="teams">Resolves a Team's members.</param>
/// <param name="spend">The per-Persona spend store.</param>
/// <param name="options">Supplies the data folder holding <c>logs/tool-calls</c>.</param>
internal sealed class RecordsBundleBuilder(TaskStore tasks, ITeamCatalog teams, PersonaSpendStore spend, IOptions<TeamOptions> options)
{
    private const string Arrow = "→";

    private static readonly string[] BounceMarkers = [$"status: Review {Arrow} To Do", $"status: Review {Arrow} In Progress"];

    private static readonly string[] WakeLimitMarkers = ["wake limit", "wake-up limit"];

    private static readonly string[] EscalationMarkers = [$"{Arrow} declined", "escalat"];

    /// <summary>Collects the Team's records since <paramref name="since"/>.</summary>
    /// <param name="team">The Team name (any case).</param>
    /// <param name="since">The cutoff; older changes and log lines are left out.</param>
    /// <returns>The bundle; empty sections for a Team with nothing in the window.</returns>
    public RecordsBundle Build(string team, DateTimeOffset since)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(team);

        TeamSummary? summary = teams.Find(team);
        string name = summary?.Name ?? team;
        IReadOnlyList<string> members = summary?.Members ?? [];

        List<TaskItem> changed = [.. tasks.All
            .Where(task => string.Equals(task.Location.Team, name, StringComparison.OrdinalIgnoreCase)
                && task.ChangeLog.Any(entry => entry.At >= since))
            .OrderBy(static task => task.Id.ToString(), StringComparer.Ordinal)];

        List<TaskRecord> taskRecords = [];
        List<RequestRecord> requests = [];
        List<RecordEvent> bounces = [];
        List<RecordEvent> budgets = [];
        List<RecordEvent> wakeLimits = [];
        List<RecordEvent> escalations = [];
        foreach (TaskItem task in changed)
        {
            string id = task.Id.ToString();
            string status = task.Status.ToWire();
            taskRecords.Add(new TaskRecord(id, task.Title, status, task.Assignee));
            if (RequestFields.TryRead(task, out RequestFields? fields) && fields is not null)
            {
                requests.Add(new RequestRecord(id, task.Title, fields.Kind.ToString(), fields.FromTeam, fields.Triage.ToString(), status));
            }

            foreach (ChangeLogEntry entry in task.ChangeLog.Where(entry => entry.At >= since))
            {
                RecordEvent recorded = new(entry.At, id, entry.Actor, entry.Summary, id);
                AddIfMatches(bounces, recorded, BounceMarkers, StringComparison.Ordinal);
                AddIfMatches(budgets, recorded, ["budget"], StringComparison.OrdinalIgnoreCase);
                AddIfMatches(wakeLimits, recorded, WakeLimitMarkers, StringComparison.OrdinalIgnoreCase);
                AddIfMatches(escalations, recorded, EscalationMarkers, StringComparison.OrdinalIgnoreCase);
            }
        }

        Dictionary<string, IReadOnlyList<StoredSpend>> spendByMember = new(StringComparer.Ordinal);
        foreach (string member in members)
        {
            IReadOnlyList<StoredSpend> stored = spend.ForPersona(member);
            if (stored.Count > 0)
            {
                spendByMember[member] = stored;
            }
        }

        (List<ToolCallSummary> toolCalls, int unreadable) = this.ReadToolCalls(members, since);

        return new RecordsBundle(name, since, taskRecords, bounces, budgets, wakeLimits, escalations, spendByMember, requests, toolCalls)
        {
            Unreadable = unreadable,
        };
    }

    /// <summary>Renders the bundle as one line per event: counts first, then events newest first with their links.</summary>
    /// <param name="bundle">The bundle to render.</param>
    /// <param name="maxCharacters">The length cap; surplus events are replaced by a count line.</param>
    /// <returns>The text, within the cap unless the counts alone exceed it.</returns>
    public static string RenderText(RecordsBundle bundle, int maxCharacters = 12000)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        int calls = bundle.ToolCalls.Sum(static c => c.Calls);
        int failures = bundle.ToolCalls.Sum(static c => c.Failures);
        string spendText = bundle.SpendByMember.Count == 0
            ? "none"
            : string.Join("; ", bundle.SpendByMember.OrderBy(static pair => pair.Key, StringComparer.Ordinal).Select(static pair =>
                string.Concat(pair.Key, " ", string.Join(", ", pair.Value.Select(static s => string.Create(CultureInfo.InvariantCulture, $"{s.Amount:0.00} {s.Currency}"))))));

        List<string> header =
        [
            string.Create(CultureInfo.InvariantCulture, $"Records for {bundle.Team} since {bundle.Since.UtcDateTime:yyyy-MM-dd}"),
            string.Create(CultureInfo.InvariantCulture, $"tasks: {bundle.Tasks.Count}"),
            string.Create(CultureInfo.InvariantCulture, $"review bounces: {bundle.ReviewBounces.Count}"),
            string.Create(CultureInfo.InvariantCulture, $"budget pauses: {bundle.BudgetPauses.Count}"),
            string.Create(CultureInfo.InvariantCulture, $"wake limits hit: {bundle.WakeLimitsHit.Count}"),
            string.Create(CultureInfo.InvariantCulture, $"escalations: {bundle.Escalations.Count}"),
            string.Create(CultureInfo.InvariantCulture, $"requests: {bundle.Requests.Count}"),
            string.Create(CultureInfo.InvariantCulture, $"tool calls: {calls} ({failures} failed)"),
            $"spend: {spendText}",
            "events (newest first):",
        ];

        List<(DateTimeOffset Time, string Line)> events = [];
        AddLines(events, "bounce", bundle.ReviewBounces);
        AddLines(events, "budget", bundle.BudgetPauses);
        AddLines(events, "wake-limit", bundle.WakeLimitsHit);
        AddLines(events, "escalation", bundle.Escalations);
        List<string> ordered = [.. events.OrderByDescending(static e => e.Time).ThenBy(static e => e.Line, StringComparer.Ordinal).Select(static e => e.Line)];

        string unreadable = bundle.Unreadable > 0 ? string.Create(CultureInfo.InvariantCulture, $"unreadable: {bundle.Unreadable}") : string.Empty;
        int reserved = MoreLine(ordered.Count).Length + 1 + (unreadable.Length > 0 ? unreadable.Length + 1 : 0);

        StringBuilder text = new(string.Join('\n', header));
        int shown = 0;
        foreach (string line in ordered)
        {
            if (text.Length + 1 + line.Length + reserved > maxCharacters)
            {
                break;
            }

            _ = text.Append('\n').Append(line);
            shown++;
        }

        if (shown < ordered.Count)
        {
            _ = text.Append('\n').Append(MoreLine(ordered.Count - shown));
        }

        if (unreadable.Length > 0)
        {
            _ = text.Append('\n').Append(unreadable);
        }

        return text.ToString();
    }

    private static string MoreLine(int count) =>
        string.Create(CultureInfo.InvariantCulture, $"... {count} more, ask for a narrower window");

    private static void AddLines(List<(DateTimeOffset Time, string Line)> target, string kind, IReadOnlyList<RecordEvent> events)
    {
        foreach (RecordEvent e in events)
        {
            target.Add((e.Time, string.Create(CultureInfo.InvariantCulture, $"{kind} {e.Time.UtcDateTime:yyyy-MM-dd HH:mm} {e.Member} {e.TaskId} {e.Summary} [{e.Link}]")));
        }
    }

    private static void AddIfMatches(List<RecordEvent> target, RecordEvent recorded, string[] markers, StringComparison comparison)
    {
        foreach (string marker in markers)
        {
            if (recorded.Summary.Contains(marker, comparison))
            {
                target.Add(recorded);
                return;
            }
        }
    }

    private (List<ToolCallSummary> Calls, int Unreadable) ReadToolCalls(IReadOnlyList<string> members, DateTimeOffset since)
    {
        string firstDay = since.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        Dictionary<(string Member, string Tool), (int Calls, int Failures)> totals = [];
        int unreadable = 0;
        foreach (string member in members)
        {
            string folder = Path.Combine(options.Value.DataDir, TeamPaths.ToolCallLogDir, ToolCallLogWriter.SanitiseName(member));
            if (!Directory.Exists(folder))
            {
                continue;
            }

            foreach (string file in Directory.GetFiles(folder, "*.jsonl").Where(file => string.CompareOrdinal(Path.GetFileNameWithoutExtension(file), firstDay) >= 0))
            {
                string[] lines;
                try
                {
                    using FileStream stream = new(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using StreamReader reader = new(stream, Encoding.UTF8);
                    lines = reader.ReadToEnd().Split('\n');
                }
                catch (IOException)
                {
                    unreadable++;
                    continue;
                }

                foreach (string line in lines.Where(static l => !string.IsNullOrWhiteSpace(l)))
                {
                    if (!TryParseLine(line, out DateTimeOffset time, out string tool, out bool failed))
                    {
                        unreadable++;
                    }
                    else if (time >= since)
                    {
                        (int calls, int failures) = totals.GetValueOrDefault((member, tool));
                        totals[(member, tool)] = (calls + 1, failures + (failed ? 1 : 0));
                    }
                }
            }
        }

        List<ToolCallSummary> summaries = [.. totals
            .OrderBy(static pair => pair.Key.Member, StringComparer.Ordinal)
            .ThenBy(static pair => pair.Key.Tool, StringComparer.Ordinal)
            .Select(static pair => new ToolCallSummary(pair.Key.Member, pair.Key.Tool, pair.Value.Calls, pair.Value.Failures))];
        return (summaries, unreadable);
    }

    private static bool TryParseLine(string line, out DateTimeOffset time, out string tool, out bool failed)
    {
        time = default;
        tool = string.Empty;
        failed = false;
        try
        {
            using JsonDocument document = JsonDocument.Parse(line);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("time", out JsonElement timeElement)
                || timeElement.ValueKind != JsonValueKind.String
                || !timeElement.TryGetDateTimeOffset(out time)
                || !root.TryGetProperty("tool", out JsonElement toolElement)
                || toolElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            tool = toolElement.GetString() ?? string.Empty;
            failed = root.TryGetProperty("status", out JsonElement statusElement)
                && statusElement.ValueKind == JsonValueKind.String
                && string.Equals(statusElement.GetString(), "failed", StringComparison.Ordinal);
            return tool.Length > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

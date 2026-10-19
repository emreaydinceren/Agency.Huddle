using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Agency.Huddle.App.Teams;

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
    /// <param name="rowsToVerify">The register rows due for verification (RT-7), or <see langword="null"/> when the Team has no register; they lead the text.</param>
    /// <returns>The text, within the cap unless the counts alone exceed it.</returns>
    public static string RenderText(RecordsBundle bundle, int maxCharacters = 12000, IReadOnlyList<VerificationPrompt>? rowsToVerify = null)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        int calls = bundle.ToolCalls.Sum(static c => c.Calls);
        int failures = bundle.ToolCalls.Sum(static c => c.Failures);
        string spendText = bundle.SpendByMember.Count == 0
            ? "none"
            : string.Join("; ", bundle.SpendByMember.OrderBy(static pair => pair.Key, StringComparer.Ordinal).Select(static pair =>
                string.Concat(pair.Key, " ", string.Join(", ", pair.Value.Select(static s => string.Create(CultureInfo.InvariantCulture, $"{s.Amount:0.00} {s.Currency}"))))));

        List<string> header = VerifyLines(rowsToVerify);
        header.AddRange(
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
        ]);

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

    private static List<string> VerifyLines(IReadOnlyList<VerificationPrompt>? rowsToVerify)
    {
        List<string> lines = [];
        if (rowsToVerify is null)
        {
            return lines;
        }

        if (rowsToVerify.Count == 0)
        {
            lines.Add("Rows to verify: none");
            return lines;
        }

        lines.Add("Rows to verify:");
        foreach (VerificationPrompt prompt in rowsToVerify)
        {
            string line = $"{prompt.RowId}: {prompt.Finding} | action: {prompt.Action} | check: {prompt.EvidenceNextCheck}";
            lines.Add(line);
        }

        return lines;
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

    /// <summary>The most bytes of one tool-call log that are read; only the tail counts.</summary>
    private const long MaxLogBytes = 1024 * 1024;

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
                if (!TryReadLogLines(file, out string[] lines))
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

    /// <summary>Reads the tail of one tool-call log, at most <see cref="MaxLogBytes"/> bytes.</summary>
    /// <param name="file">The log file.</param>
    /// <param name="lines">The lines read; empty when the file could not be read.</param>
    /// <returns><see langword="false"/> when the file could not be read.</returns>
    private static bool TryReadLogLines(string file, out string[] lines)
    {
        try
        {
            using FileStream stream = new(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            bool cut = stream.Length > MaxLogBytes;
            if (cut)
            {
                _ = stream.Seek(-MaxLogBytes, SeekOrigin.End);
            }

            using StreamReader reader = new(stream, Encoding.UTF8);
            string[] read = reader.ReadToEnd().Split('\n');

            // A tail read starts mid-line: drop the cut first line so it is not counted as corrupt.
            lines = cut ? read[1..] : read;
            return true;
        }
        catch (IOException)
        {
            lines = [];
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            lines = [];
            return false;
        }
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

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.App.Tasks;

/// <summary>
/// Parses, composes and version-hashes a Task file: the frontmatter and the description-plus-timeline
/// body in Spec §7 and ADR-0086 (the timeline half is in <c>TaskFileFormat.Timeline.cs</c>). This is
/// the only place that reads or writes a Task file's text; <c>TaskStore</c> is the only class that
/// touches the file system for Task files.
/// </summary>
internal static partial class TaskFileFormat
{
    /// <summary>The Markdown heading, matched case-insensitively, that starts a Task file's Change log section.</summary>
    public const string ChangeLogHeading = "## Change log";

    /// <summary>The Markdown heading, matched case-insensitively, that starts a Task file's append-only Evidence section.</summary>
    public const string EvidenceHeading = "## Evidence";

    /// <summary>The fence line <see cref="Compose"/> writes at both ends of the frontmatter.</summary>
    private const string DefaultFence = "---";

    /// <summary>The Spec §7.2 frontmatter keys this parser recognises, compared case-insensitively.</summary>
    private static readonly HashSet<string> KnownKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "id", "title", "status", "priority", "creator", "assignee", "origin",
        "parent", "blocked_by", "duplicate_of", "tags", "start_date", "due_date",
        "sprint", "human_gate", "action",
    };

    /// <summary>The longest <c>action</c> value a Task file may carry (Human gate Spec §6.1).</summary>
    private const int MaxActionLength = 200;

    private static readonly char[] LineBreakCharacters = ['\n', '\r'];

    /// <summary>
    /// Parses a Task file's text into a <see cref="TaskItem"/> (Spec §7.2-§7.3). Every known key is
    /// validated per Spec §7.2; the first violation is returned as <paramref name="error"/>. The body
    /// is split at the last <c>## Timeline</c> heading: the text before it is
    /// <see cref="TaskItem.Description"/>, the lines after it are the comments, Evidence and events
    /// (Spec §7.7, ADR-0086). A body without that heading is an older file and is read by Spec §7.7.3:
    /// its Change log lines are the events and its <c>## Evidence</c> sections are Evidence entries,
    /// not description.
    /// </summary>
    /// <param name="text">The Task file's raw text.</param>
    /// <param name="path">The Task file's absolute path, passed through unchanged.</param>
    /// <param name="location">Where the Task lives in the folder structure, passed through unchanged.</param>
    /// <param name="task">The parsed Task, when parsing succeeds.</param>
    /// <param name="error">The first validation failure, or "" when parsing succeeds.</param>
    public static bool TryParse(string text, string path, TaskLocation location, [NotNullWhen(true)] out TaskItem? task, out string error)
    {
        if (!HasFrontmatterBlock(text))
        {
            task = null;
            error = "has no frontmatter block between --- lines";
            return false;
        }

        (IReadOnlyList<PersonaFrontmatterField> fields, string body) = PersonaFrontmatter.Parse(text);

        if (!TryCheckNoDuplicateKeys(fields, out error))
        {
            task = null;
            return false;
        }

        if (!TryParseRequired(fields, out RequiredFields? required, out error))
        {
            task = null;
            return false;
        }

        if (!TryParseRelations(fields, required.Id, required.Status, out Relations? relations, out error))
        {
            task = null;
            return false;
        }

        string? assignee = TryGetField(fields, "assignee", out string? assigneeRaw) ? NullIfEmpty(assigneeRaw) : null;
        string? originRoomId = TryGetField(fields, "origin", out string? originRaw) ? NullIfEmpty(originRaw) : null;
        ParsedBody parsedBody = ParseBodyOf(body, required);
        task = new TaskItem
        {
            Id = required.Id,
            Title = required.Title,
            Status = required.Status,
            Priority = required.Priority,
            Creator = required.Creator,
            Assignee = assignee,
            OriginRoomId = originRoomId,
            Parent = relations.Parent,
            BlockedBy = relations.BlockedBy,
            DuplicateOf = relations.DuplicateOf,
            Tags = relations.Tags,
            StartDate = relations.StartDate,
            DueDate = relations.DueDate,
            Sprint = relations.Sprint,
            HumanGate = relations.HumanGate,
            Action = relations.Action,
            Description = parsedBody.Description,
            Location = location,
            ChangeLog = parsedBody.ChangeLog,
            Comments = parsedBody.Comments,
            UnknownFields = UnknownFieldsOf(fields),
            Path = path,
            Version = ComputeVersion(text),
            ClosedAt = ComputeClosedAt(parsedBody.ChangeLog, location.Closed),
        };
        error = "";
        return true;
    }

    /// <summary>The keys a Task file must carry, validated per Spec §7.2.</summary>
    private sealed record RequiredFields(TaskId Id, string Title, TaskState Status, TaskPriority Priority, string Creator, bool StoredReview);

    /// <summary>The wire text of the Review state Huddle no longer has; a file that still says it reads as In Progress (D-6).</summary>
    private const string LegacyReviewWire = "Review";

    /// <summary>The Huddle line a loaded <c>status: Review</c> Task carries on its Timeline (D-6).</summary>
    private const string LegacyReviewLine = "status: Review → In Progress (Huddle has no Review state; a review is a Task tagged review)";

    /// <summary>Parses a stored status; a stored Review reads as In Progress and is reported in <paramref name="storedReview"/> (D-6).</summary>
    private static bool TryParseStatus(string raw, out TaskState status, out bool storedReview)
    {
        storedReview = string.Equals(raw.Trim(), LegacyReviewWire, StringComparison.OrdinalIgnoreCase);
        status = TaskState.InProgress;
        return storedReview || TaskStates.TryParse(raw, out status);
    }

    /// <summary>
    /// Parses the body; when the file stored the Review status, adds the Huddle line that says it was read as In Progress,
    /// dated at the Task's last entry so the Timeline order holds.
    /// </summary>
    private static ParsedBody ParseBodyOf(string body, RequiredFields required)
    {
        ParsedBody parsed = ParseBody(body);
        if (!required.StoredReview)
        {
            return parsed;
        }

        DateTimeOffset at = TimelineMerge.LastAt(parsed.Comments, parsed.ChangeLog) ?? DateTimeOffset.UnixEpoch;
        return parsed with { ChangeLog = [.. parsed.ChangeLog, new ChangeLogEntry(at, "Huddle", LegacyReviewLine)] };
    }

    /// <summary>The optional keys that point at other records or carry dates, validated per Spec §7.2.</summary>
    private sealed record Relations(TaskId? Parent, IReadOnlyList<TaskId> BlockedBy, TaskId? DuplicateOf, IReadOnlyList<string> Tags, DateOnly? StartDate, DateOnly? DueDate, SprintId? Sprint, bool HumanGate, string? Action);

    /// <summary>Validates <c>id</c>, <c>title</c>, <c>status</c>, <c>priority</c> and <c>creator</c>; the first violation is <paramref name="error"/>.</summary>
    private static bool TryParseRequired(IReadOnlyList<PersonaFrontmatterField> fields, [NotNullWhen(true)] out RequiredFields? required, out string error)
    {
        required = null;
        if (!TryGetField(fields, "id", out string? idRaw))
        {
            error = "missing required key: id";
            return false;
        }

        if (!TaskId.TryParse(idRaw, out TaskId id))
        {
            error = string.Create(CultureInfo.InvariantCulture, $"invalid id: '{idRaw}'");
            return false;
        }

        if (!TryGetField(fields, "title", out string? titleRaw))
        {
            error = "missing required key: title";
            return false;
        }

        string title = titleRaw.Trim();
        if (title.Length is < 1 or > 200)
        {
            error = string.Create(CultureInfo.InvariantCulture, $"title must be 1-200 characters: '{title}'");
            return false;
        }

        if (!TryGetField(fields, "status", out string? statusRaw))
        {
            error = "missing required key: status";
            return false;
        }

        if (!TryParseStatus(statusRaw, out TaskState status, out bool storedReview))
        {
            error = string.Create(CultureInfo.InvariantCulture, $"invalid status: '{statusRaw}'");
            return false;
        }

        if (!TryGetField(fields, "priority", out string? priorityRaw))
        {
            error = "missing required key: priority";
            return false;
        }

        if (!TaskPriorities.TryParse(priorityRaw, out TaskPriority priority))
        {
            error = string.Create(CultureInfo.InvariantCulture, $"invalid priority: '{priorityRaw}'");
            return false;
        }

        if (!TryGetField(fields, "creator", out string? creatorRaw) || creatorRaw.Trim().Length == 0)
        {
            error = "missing required key: creator";
            return false;
        }

        required = new RequiredFields(id, title, status, priority, creatorRaw.Trim(), storedReview);
        error = "";
        return true;
    }

    /// <summary>Validates <c>parent</c>, <c>blocked_by</c>, <c>duplicate_of</c>, <c>tags</c>, both dates and <c>sprint</c>; the first violation is <paramref name="error"/>.</summary>
    private static bool TryParseRelations(IReadOnlyList<PersonaFrontmatterField> fields, TaskId id, TaskState status, [NotNullWhen(true)] out Relations? relations, out string error)
    {
        relations = null;
        if (!TryParseParent(fields, id, out TaskId? parent, out error)
            || !TryParseBlockedBy(fields, id, out IReadOnlyList<TaskId> blockedBy, out error)
            || !TryParseDuplicateOf(fields, id, status, out TaskId? duplicateOf, out error)
            || !TryParseTags(fields, out IReadOnlyList<string> tags, out error)
            || !TryParseDate(fields, "start_date", out DateOnly? startDate, out error)
            || !TryParseDate(fields, "due_date", out DateOnly? dueDate, out error)
            || !TryParseSprint(fields, out SprintId? sprint, out error)
            || !TryParseAction(fields, out string? action, out error))
        {
            return false;
        }

        bool humanGate = TryGetField(fields, "human_gate", out string? humanGateRaw)
            && string.Equals(humanGateRaw?.Trim(), "true", StringComparison.OrdinalIgnoreCase);
        relations = new Relations(parent, blockedBy, duplicateOf, tags, startDate, dueDate, sprint, humanGate, action);
        return true;
    }

    /// <summary>The frontmatter fields whose keys this parser does not recognise, in file order.</summary>
    private static List<KeyValuePair<string, string>> UnknownFieldsOf(IReadOnlyList<PersonaFrontmatterField> fields)
    {
        List<KeyValuePair<string, string>> unknownFields = [];
        foreach (PersonaFrontmatterField field in fields)
        {
            if (!KnownKeys.Contains(field.Key))
            {
                unknownFields.Add(new(field.Key, field.Value));
            }
        }

        return unknownFields;
    }

    /// <summary>Composes a Task's canonical file text (Spec §7.4): the §7.2 keys in order, then
    /// any unknown keys, then the description, a blank line, the Timeline heading and every
    /// timeline entry. Line endings are '\n' only.</summary>
    /// <param name="task">The Task to compose.</param>
    public static string Compose(TaskItem task)
    {
        ArgumentNullException.ThrowIfNull(task);

        List<string> lines = ComposeHeadLines(task);
        lines.Add(TimelineHeading);
        foreach (TimelineEntry entry in task.Timeline)
        {
            lines.Add(FormatTimelineEntry(entry));
        }

        return string.Join('\n', lines);
    }

    /// <summary>
    /// Composes <paramref name="after"/>'s frontmatter and description (Spec §7.4), then keeps the
    /// text of <paramref name="currentText"/> from its last fence-aware <see cref="TimelineHeading"/>
    /// line onward, byte for byte (Settled corrections-B2 D6 item 1: never recompose a whole
    /// existing file for an update - a hand-edited timeline line must survive untouched, and a
    /// description write must never reach an Evidence entry, #586). A file written before the
    /// timeline is converted first (<see cref="Upgrade"/>), so it has a heading to keep from. The
    /// result has '\n' line endings.
    /// </summary>
    /// <param name="currentText">The Task file's current raw text on disk.</param>
    /// <param name="after">The Task as it should be after the update, for its frontmatter and description.</param>
    public static string ReplaceHead(string currentText, TaskItem after)
    {
        ArgumentNullException.ThrowIfNull(currentText);
        ArgumentNullException.ThrowIfNull(after);

        string[] rawLines = Upgrade(currentText).Split('\n');
        int headingIndex = FindHeadingLineIndex(rawLines, TimelineHeading);
        (string opening, string closing) = ExistingFences(rawLines);
        List<string> headLines = ComposeHeadLines(after, opening, closing);

        return headingIndex < 0
            ? string.Join('\n', headLines)
            : string.Join('\n', headLines.Concat(rawLines[headingIndex..]));
    }

    /// <summary>
    /// The opening and closing fence lines of an existing Task file, as written: a BOM, surrounding spaces or a <c>...</c> closer
    /// are kept, so rewriting the head of a hand-edited file changes only its field lines. The line break is dropped.
    /// </summary>
    /// <param name="rawLines">The file's lines, split on <c>\n</c>.</param>
    /// <returns>The two fence lines; <c>---</c> for both when the file has no closed block.</returns>
    private static (string Opening, string Closing) ExistingFences(string[] rawLines)
    {
        if (rawLines.Length < 2 || !FrontmatterFence.IsOpening(rawLines[0]))
        {
            return (DefaultFence, DefaultFence);
        }

        int closingIndex = FrontmatterFence.FindClosing(rawLines, 1);
        return closingIndex < 0
            ? (DefaultFence, DefaultFence)
            : (rawLines[0].TrimEnd('\r'), rawLines[closingIndex].TrimEnd('\r'));
    }

    /// <summary>The frontmatter and description lines common to <see cref="Compose"/> and <see cref="ReplaceHead"/>, ending with the blank line before the Change log heading.</summary>
    private static List<string> ComposeHeadLines(TaskItem task, string opening = DefaultFence, string closing = DefaultFence)
    {
        List<string> lines = [opening, string.Create(CultureInfo.InvariantCulture, $"id: {task.Id}")];
        lines.Add(ComposeScalarLine("title", task.Title));
        lines.Add(ComposeScalarLine("status", task.Status.ToWire()));
        lines.Add(ComposeScalarLine("priority", task.Priority.ToWire()));
        lines.Add(ComposeScalarLine("creator", task.Creator));

        if (task.Assignee is { Length: > 0 } assignee)
        {
            lines.Add(ComposeScalarLine("assignee", assignee));
        }

        if (task.OriginRoomId is { Length: > 0 } origin)
        {
            lines.Add(ComposeScalarLine("origin", origin));
        }

        AddRelationLines(lines, task);
        foreach (KeyValuePair<string, string> field in task.UnknownFields)
        {
            lines.Add(ComposeScalarLine(field.Key, field.Value));
        }

        lines.Add(closing);
        lines.Add(task.Description);
        lines.Add("");

        return lines;
    }

    /// <summary>Adds the <c>parent</c>, <c>blocked_by</c>, <c>duplicate_of</c>, <c>tags</c>, date and <c>sprint</c> lines a Task has, in Spec §7.4 order.</summary>
    private static void AddRelationLines(List<string> lines, TaskItem task)
    {
        if (task.Parent is { } parent)
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"parent: {parent}"));
        }

        if (task.BlockedBy.Count > 0)
        {
            lines.Add("blocked_by:");
            foreach (TaskId blocker in task.BlockedBy)
            {
                lines.Add(string.Create(CultureInfo.InvariantCulture, $"  - {blocker}"));
            }
        }

        if (task.DuplicateOf is { } duplicateOf)
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"duplicate_of: {duplicateOf}"));
        }

        if (task.Tags.Count > 0)
        {
            lines.Add("tags:");
            foreach (string tag in task.Tags)
            {
                lines.Add(string.Create(CultureInfo.InvariantCulture, $"  - {tag}"));
            }
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
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"sprint: {sprint}"));
        }

        if (task.HumanGate)
        {
            lines.Add("human_gate: true");
        }

        if (task.Action is { } action)
        {
            lines.Add(ComposeScalarLine("action", action));
        }
    }

    /// <summary>Writes one frontmatter scalar line, quoting the value when Spec §7.4 requires it.</summary>
    private static string ComposeScalarLine(string key, string value) =>
        string.Create(CultureInfo.InvariantCulture, $"{key}: {ComposeScalarValue(value)}");

    /// <summary>Quotes a scalar value with '' escaping when it's unsafe to write plain (Spec §7.4).</summary>
    private static string ComposeScalarValue(string value) =>
        NeedsQuoting(value) ? QuoteScalar(value) : value;

    /// <summary>The characters that force a scalar to be quoted, per Spec §7.4.</summary>
    private static readonly char[] UnsafeScalarCharacters = [':', '#', '\'', '"', ',', '[', ']', '{', '}', '&'];

    /// <summary>
    /// True when <paramref name="value"/> is empty after trimming, starts or ends with a space,
    /// equals a YAML block scalar indicator (<c>&gt;</c>, <c>&gt;-</c>, <c>&gt;+</c>, <c>|</c>,
    /// <c>|-</c>, <c>|+</c>), or contains an <see cref="UnsafeScalarCharacters"/> character.
    /// </summary>
    private static bool NeedsQuoting(string value)
    {
        if (value.Trim().Length == 0)
        {
            return true;
        }

        if (value[0] == ' ' || value[^1] == ' ')
        {
            return true;
        }

        if (value is ">" or ">-" or ">+" or "|" or "|-" or "|+")
        {
            return true;
        }

        foreach (char c in value)
        {
            if (Array.IndexOf(UnsafeScalarCharacters, c) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Single-quotes a scalar value, doubling any embedded single quote (matches <c>PersonaFrontmatter.QuoteScalar</c>).</summary>
    private static string QuoteScalar(string value) =>
        string.Create(CultureInfo.InvariantCulture, $"'{value.Replace("'", "''", StringComparison.Ordinal)}'");

    /// <summary>Formats one event as its timeline file line (Spec §7.4, §7.7.1): <c>- {at} | {actor} | event | {summary}</c>.</summary>
    /// <param name="entry">The entry to format.</param>
    public static string FormatEntry(ChangeLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return FormatTimelineEntry(new TimelineEntry(entry.At, entry.Actor, TimelineEntryKind.Event, entry.Summary));
    }

    /// <summary>
    /// Appends a formatted event to <paramref name="fileText"/>, converting an older file first
    /// (<see cref="Upgrade"/>) and adding the <see cref="TimelineHeading"/> when the file has none
    /// outside a fenced code block. Existing lines are never rewritten.
    /// </summary>
    /// <param name="fileText">The Task file's raw text.</param>
    /// <param name="entry">The entry to append.</param>
    public static string AppendEntry(string fileText, ChangeLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(fileText);
        ArgumentNullException.ThrowIfNull(entry);

        return AppendTimelineEntry(fileText, new TimelineEntry(entry.At, entry.Actor, TimelineEntryKind.Event, entry.Summary));
    }

    /// <summary>
    /// The version hash of a Task file's text: the first 16 hex characters of the SHA-256 of the
    /// text after CRLF has been normalised to '\n' (Spec §7.6).
    /// </summary>
    /// <param name="fileText">The Task file's raw text.</param>
    public static string ComputeVersion(string fileText)
    {
        ArgumentNullException.ThrowIfNull(fileText);
        byte[] normalized = Encoding.UTF8.GetBytes(fileText.ReplaceLineEndings("\n"));
        byte[] hash = SHA256.HashData(normalized);
        return Convert.ToHexStringLower(hash)[..16];
    }

    /// <summary>
    /// True when <paramref name="text"/>'s first line opens a frontmatter block and some later line
    /// closes it, by the shared <see cref="FrontmatterFence"/> rule (a BOM, surrounding spaces and a
    /// <c>...</c> closer are all accepted). Normalises line endings first, since the file may arrive
    /// with CRLF.
    /// </summary>
    private static bool HasFrontmatterBlock(string text)
    {
        string normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        string[] lines = normalized.Split('\n');
        return lines.Length >= 2 && FrontmatterFence.IsOpening(lines[0]) && FrontmatterFence.FindClosing(lines, 1) >= 0;
    }

    /// <summary>Rejects the file when the same key (case-insensitive) appears more than once.</summary>
    private static bool TryCheckNoDuplicateKeys(IReadOnlyList<PersonaFrontmatterField> fields, out string error)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (PersonaFrontmatterField field in fields)
        {
            if (!seen.Add(field.Key))
            {
                error = string.Create(CultureInfo.InvariantCulture, $"duplicate key: {field.Key}");
                return false;
            }
        }

        error = "";
        return true;
    }

    /// <summary>Finds a field by key, compared case-insensitively.</summary>
    private static bool TryGetField(IReadOnlyList<PersonaFrontmatterField> fields, string key, [NotNullWhen(true)] out string? value)
    {
        foreach (PersonaFrontmatterField field in fields)
        {
            if (string.Equals(field.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = field.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    /// <summary>Trims a raw field value, returning null when it's empty afterward.</summary>
    private static string? NullIfEmpty(string raw)
    {
        string trimmed = raw.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    /// <summary>Splits a joined list field on "; ", trimming and dropping empty items.</summary>
    private static string[] SplitList(string raw) =>
        raw.Split("; ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Parses the optional "parent" key: a <see cref="TaskId"/> that isn't the Task itself.</summary>
    private static bool TryParseParent(IReadOnlyList<PersonaFrontmatterField> fields, TaskId id, out TaskId? parent, out string error)
    {
        parent = null;
        if (!TryGetField(fields, "parent", out string? parentRaw) || NullIfEmpty(parentRaw) is null)
        {
            error = "";
            return true;
        }

        if (!TaskId.TryParse(parentRaw, out TaskId parentId))
        {
            error = string.Create(CultureInfo.InvariantCulture, $"invalid parent: '{parentRaw}'");
            return false;
        }

        if (parentId == id)
        {
            error = "parent cannot reference the Task itself";
            return false;
        }

        parent = parentId;
        error = "";
        return true;
    }

    /// <summary>Parses the optional "blocked_by" key: a list of <see cref="TaskId"/>, no duplicates, none referencing the Task itself.</summary>
    private static bool TryParseBlockedBy(IReadOnlyList<PersonaFrontmatterField> fields, TaskId id, out IReadOnlyList<TaskId> blockedBy, out string error)
    {
        List<TaskId> items = [];
        if (TryGetField(fields, "blocked_by", out string? blockedByRaw))
        {
            foreach (string item in SplitList(blockedByRaw))
            {
                if (!TaskId.TryParse(item, out TaskId blockerId))
                {
                    blockedBy = [];
                    error = string.Create(CultureInfo.InvariantCulture, $"invalid blocked_by: '{item}'");
                    return false;
                }

                if (blockerId == id)
                {
                    blockedBy = [];
                    error = "blocked_by cannot reference the Task itself";
                    return false;
                }

                if (items.Contains(blockerId))
                {
                    blockedBy = [];
                    error = string.Create(CultureInfo.InvariantCulture, $"duplicate blocked_by: '{item}'");
                    return false;
                }

                items.Add(blockerId);
            }
        }

        blockedBy = items;
        error = "";
        return true;
    }

    /// <summary>
    /// Parses the optional "duplicate_of" key: a <see cref="TaskId"/> that isn't the Task itself,
    /// required exactly when <paramref name="status"/> is <see cref="TaskState.Duplicate"/>.
    /// </summary>
    private static bool TryParseDuplicateOf(IReadOnlyList<PersonaFrontmatterField> fields, TaskId id, TaskState status, out TaskId? duplicateOf, out string error)
    {
        duplicateOf = null;
        if (TryGetField(fields, "duplicate_of", out string? duplicateOfRaw) && NullIfEmpty(duplicateOfRaw) is not null)
        {
            if (!TaskId.TryParse(duplicateOfRaw, out TaskId duplicateOfId))
            {
                error = string.Create(CultureInfo.InvariantCulture, $"invalid duplicate_of: '{duplicateOfRaw}'");
                return false;
            }

            if (duplicateOfId == id)
            {
                error = "duplicate_of cannot reference the Task itself";
                return false;
            }

            duplicateOf = duplicateOfId;
        }

        if (status == TaskState.Duplicate && duplicateOf is null)
        {
            error = "duplicate_of is required when status is Duplicate";
            return false;
        }

        if (status != TaskState.Duplicate && duplicateOf is not null)
        {
            error = "duplicate_of is only valid when status is Duplicate";
            return false;
        }

        error = "";
        return true;
    }

    /// <summary>Parses the optional "tags" key: each item 1-40 characters, with no ',', ';' or line break.</summary>
    private static bool TryParseTags(IReadOnlyList<PersonaFrontmatterField> fields, out IReadOnlyList<string> tags, out string error)
    {
        List<string> items = [];
        if (TryGetField(fields, "tags", out string? tagsRaw))
        {
            foreach (string tag in SplitList(tagsRaw))
            {
                bool valid = tag.Length is >= 1 and <= 40
                    && !tag.Contains(',', StringComparison.Ordinal)
                    && !tag.Contains(';', StringComparison.Ordinal)
                    && tag.IndexOfAny(LineBreakCharacters) < 0;

                if (!valid)
                {
                    tags = [];
                    error = string.Create(CultureInfo.InvariantCulture, $"invalid tags: '{tag}'");
                    return false;
                }

                items.Add(tag);
            }
        }

        tags = items;
        error = "";
        return true;
    }

    /// <summary>Parses the optional "sprint" key: absent or empty is none, a value that is not a <see cref="SprintId"/> rejects the file (Sprints spec §9.1).</summary>
    private static bool TryParseSprint(IReadOnlyList<PersonaFrontmatterField> fields, out SprintId? sprint, out string error)
    {
        sprint = null;
        if (TryGetField(fields, "sprint", out string? raw) && NullIfEmpty(raw) is { } trimmed)
        {
            if (!SprintId.TryParse(trimmed, out SprintId parsed))
            {
                error = string.Create(CultureInfo.InvariantCulture, $"invalid sprint: '{raw}'");
                return false;
            }

            sprint = parsed;
        }

        error = "";
        return true;
    }

    /// <summary>Parses the optional "action" key: trimmed, empty is none, more than <see cref="MaxActionLength"/> characters rejects the file (Human gate Spec §6.1).</summary>
    private static bool TryParseAction(IReadOnlyList<PersonaFrontmatterField> fields, out string? action, out string error)
    {
        action = null;
        if (TryGetField(fields, "action", out string? raw) && NullIfEmpty(raw) is { } trimmed)
        {
            if (trimmed.Length > MaxActionLength)
            {
                error = string.Create(CultureInfo.InvariantCulture, $"invalid action: longer than {MaxActionLength} characters");
                return false;
            }

            action = trimmed;
        }

        error = "";
        return true;
    }

    /// <summary>Parses an optional "yyyy-MM-dd" date key.</summary>
    private static bool TryParseDate(IReadOnlyList<PersonaFrontmatterField> fields, string key, out DateOnly? date, out string error)
    {
        date = null;
        if (TryGetField(fields, key, out string? raw) && NullIfEmpty(raw) is { } trimmed)
        {
            if (!DateOnly.TryParseExact(trimmed, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly parsed))
            {
                error = string.Create(CultureInfo.InvariantCulture, $"invalid {key}: '{raw}'");
                return false;
            }

            date = parsed;
        }

        error = "";
        return true;
    }

    /// <summary>
    /// Parses one Change log line (Spec §7.3 step 4). A line that doesn't match the grammar, or
    /// whose timestamp doesn't parse, is not an error - it's kept in the file but ignored.
    /// </summary>
    private static bool TryParseLogLine(string line, [NotNullWhen(true)] out ChangeLogEntry? entry)
    {
        Match match = LogLineRegex().Match(line);
        if (!match.Success)
        {
            entry = null;
            return false;
        }

        if (!DateTimeOffset.TryParseExact(
                match.Groups[1].Value,
                "yyyy-MM-dd'T'HH:mm:ss'Z'",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out DateTimeOffset at))
        {
            entry = null;
            return false;
        }

        string actor = UnescapeEntryField(match.Groups[2].Value);
        string summary = UnescapeEntryField(match.Groups[3].Value);
        entry = new ChangeLogEntry(at, actor, summary);
        return true;
    }

    /// <summary>The Change log entry line grammar: "- {timestamp} | {actor} | {summary}".</summary>
    [GeneratedRegex(@"\A- (\S+) \| (.*?) \| (.*)\z", RegexOptions.CultureInvariant)]
    private static partial Regex LogLineRegex();

    /// <summary>Escapes an entry field for writing: '\' becomes '\\', '|' becomes '\|', and any line break becomes a single space.</summary>
    private static string EscapeEntryField(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\n', ' ')
            .Replace('\r', ' ');

    /// <summary>
    /// Reverses <see cref="EscapeEntryField"/> in a single left-to-right pass, so an escaped
    /// backslash immediately followed by an escaped pipe (<c>\\\|</c>) is not mis-decoded the way
    /// chained <see cref="string.Replace(string, string)"/> calls would.
    /// </summary>
    private static string UnescapeEntryField(string raw)
    {
        StringBuilder builder = new(raw.Length);
        int i = 0;
        while (i < raw.Length)
        {
            if (raw[i] == '\\' && i + 1 < raw.Length)
            {
                char next = raw[i + 1];
                if (next is '\\' or '|')
                {
                    builder.Append(next);
                    i += 2;
                    continue;
                }

                if (next == 'n')
                {
                    builder.Append('\n');
                    i += 2;
                    continue;
                }
            }

            builder.Append(raw[i]);
            i++;
        }

        return builder.ToString();
    }

    /// <summary>
    /// The <c>At</c> of the last "closed" entry not followed by a "reopened" entry, when
    /// <paramref name="locationClosed"/> is true; otherwise null (Spec §7.4).
    /// </summary>
    private static DateTimeOffset? ComputeClosedAt(IReadOnlyList<ChangeLogEntry> changeLog, bool locationClosed)
    {
        if (!locationClosed)
        {
            return null;
        }

        DateTimeOffset? lastClosed = null;
        bool followedByReopened = false;
        foreach (ChangeLogEntry entry in changeLog)
        {
            if (string.Equals(entry.Summary, "closed", StringComparison.Ordinal))
            {
                lastClosed = entry.At;
                followedByReopened = false;
            }
            else if (string.Equals(entry.Summary, "reopened", StringComparison.Ordinal))
            {
                followedByReopened = true;
            }
        }

        return followedByReopened ? null : lastClosed;
    }
}

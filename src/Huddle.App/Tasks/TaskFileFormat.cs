using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Agency.Huddle.App.Acp;

namespace Agency.Huddle.App.Tasks;

/// <summary>
/// Parses, composes and version-hashes a Task file: the frontmatter and Change log grammar in
/// Spec §7. This is the only place that reads or writes a Task file's text; <c>TaskStore</c> is
/// the only class that touches the file system for Task files.
/// </summary>
internal static partial class TaskFileFormat
{
    /// <summary>The Markdown heading, matched case-insensitively, that starts a Task file's Change log section.</summary>
    public const string ChangeLogHeading = "## Change log";

    /// <summary>The Spec §7.2 frontmatter keys this parser recognises, compared case-insensitively.</summary>
    private static readonly HashSet<string> KnownKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "id", "title", "status", "priority", "creator", "assignee", "origin",
        "parent", "blocked_by", "duplicate_of", "tags", "start_date", "due_date",
    };

    private static readonly char[] LineBreakCharacters = ['\n', '\r'];

    /// <summary>
    /// Parses a Task file's text into a <see cref="TaskItem"/> (Spec §7.2-§7.3 steps 1-2). Every
    /// known key is validated per Spec §7.2; the first violation is returned as
    /// <paramref name="error"/>. <see cref="TaskItem.Description"/> is the whole body for now -
    /// splitting out the Change log is Task 2.2.
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

        if (!TryGetField(fields, "id", out string? idRaw))
        {
            task = null;
            error = "missing required key: id";
            return false;
        }

        if (!TaskId.TryParse(idRaw, out TaskId id))
        {
            task = null;
            error = string.Create(CultureInfo.InvariantCulture, $"invalid id: '{idRaw}'");
            return false;
        }

        if (!TryGetField(fields, "title", out string? titleRaw))
        {
            task = null;
            error = "missing required key: title";
            return false;
        }

        string title = titleRaw.Trim();
        if (title.Length is < 1 or > 200)
        {
            task = null;
            error = string.Create(CultureInfo.InvariantCulture, $"title must be 1-200 characters: '{title}'");
            return false;
        }

        if (!TryGetField(fields, "status", out string? statusRaw))
        {
            task = null;
            error = "missing required key: status";
            return false;
        }

        if (!TaskStates.TryParse(statusRaw, out TaskState status))
        {
            task = null;
            error = string.Create(CultureInfo.InvariantCulture, $"invalid status: '{statusRaw}'");
            return false;
        }

        if (!TryGetField(fields, "priority", out string? priorityRaw))
        {
            task = null;
            error = "missing required key: priority";
            return false;
        }

        if (!TaskPriorities.TryParse(priorityRaw, out TaskPriority priority))
        {
            task = null;
            error = string.Create(CultureInfo.InvariantCulture, $"invalid priority: '{priorityRaw}'");
            return false;
        }

        if (!TryGetField(fields, "creator", out string? creatorRaw) || creatorRaw.Trim().Length == 0)
        {
            task = null;
            error = "missing required key: creator";
            return false;
        }

        string creator = creatorRaw.Trim();

        string? assignee = TryGetField(fields, "assignee", out string? assigneeRaw) ? NullIfEmpty(assigneeRaw) : null;
        string? originRoomId = TryGetField(fields, "origin", out string? originRaw) ? NullIfEmpty(originRaw) : null;

        if (!TryParseParent(fields, id, out TaskId? parent, out error))
        {
            task = null;
            return false;
        }

        if (!TryParseBlockedBy(fields, id, out IReadOnlyList<TaskId> blockedBy, out error))
        {
            task = null;
            return false;
        }

        if (!TryParseDuplicateOf(fields, id, status, out TaskId? duplicateOf, out error))
        {
            task = null;
            return false;
        }

        if (!TryParseTags(fields, out IReadOnlyList<string> tags, out error))
        {
            task = null;
            return false;
        }

        if (!TryParseDate(fields, "start_date", out DateOnly? startDate, out error))
        {
            task = null;
            return false;
        }

        if (!TryParseDate(fields, "due_date", out DateOnly? dueDate, out error))
        {
            task = null;
            return false;
        }

        List<KeyValuePair<string, string>> unknownFields = [];
        foreach (PersonaFrontmatterField field in fields)
        {
            if (!KnownKeys.Contains(field.Key))
            {
                unknownFields.Add(new(field.Key, field.Value));
            }
        }

        (string description, IReadOnlyList<ChangeLogEntry> changeLog) = SplitChangeLog(body);

        task = new TaskItem
        {
            Id = id,
            Title = title,
            Status = status,
            Priority = priority,
            Creator = creator,
            Assignee = assignee,
            OriginRoomId = originRoomId,
            Parent = parent,
            BlockedBy = blockedBy,
            DuplicateOf = duplicateOf,
            Tags = tags,
            StartDate = startDate,
            DueDate = dueDate,
            Description = description,
            Location = location,
            ChangeLog = changeLog,
            UnknownFields = unknownFields,
            Path = path,
            Version = ComputeVersion(text),
            ClosedAt = ComputeClosedAt(changeLog, location.Closed),
        };
        error = "";
        return true;
    }

    /// <summary>Composes a Task's canonical file text (Spec §7.4): the §7.2 keys in order, then
    /// any unknown keys, then the description, a blank line, the Change log heading and its
    /// entries. Line endings are '\n' only.</summary>
    /// <param name="task">The Task to compose.</param>
    public static string Compose(TaskItem task)
    {
        ArgumentNullException.ThrowIfNull(task);

        List<string> lines = ["---", string.Create(CultureInfo.InvariantCulture, $"id: {task.Id}")];
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

        lines.Add(
            task.DuplicateOf is { } duplicateOf
                ? string.Create(CultureInfo.InvariantCulture, $"duplicate_of: {duplicateOf}")
                : "duplicate_of:");

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

        foreach (KeyValuePair<string, string> field in task.UnknownFields)
        {
            lines.Add(ComposeScalarLine(field.Key, field.Value));
        }

        lines.Add("---");
        lines.Add(task.Description);
        lines.Add("");
        lines.Add(ChangeLogHeading);
        foreach (ChangeLogEntry entry in task.ChangeLog)
        {
            lines.Add(FormatEntry(entry));
        }

        return string.Join('\n', lines);
    }

    /// <summary>True when <paramref name="description"/> has a line that trims (case-insensitively) to <see cref="ChangeLogHeading"/> outside a fenced code block.</summary>
    /// <param name="description">The text to search.</param>
    public static bool ContainsChangeLogHeading(string description)
    {
        ArgumentNullException.ThrowIfNull(description);

        string normalized = description.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
        return FindChangeLogHeadingLineIndex(normalized.Split('\n')) >= 0;
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

    /// <summary>Formats one Change log entry as its file line (Spec §7.4).</summary>
    /// <param name="entry">The entry to format.</param>
    public static string FormatEntry(ChangeLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        string at = entry.At.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        string actor = EscapeEntryField(entry.Actor);
        string summary = EscapeEntryField(entry.Summary);
        return string.Create(CultureInfo.InvariantCulture, $"- {at} | {actor} | {summary}");
    }

    /// <summary>
    /// Appends a formatted Change log entry to <paramref name="fileText"/>, adding the
    /// <see cref="ChangeLogHeading"/> first when the file has none outside a fenced code block.
    /// Existing log lines are never rewritten.
    /// </summary>
    /// <param name="fileText">The Task file's raw text.</param>
    /// <param name="entry">The entry to append.</param>
    public static string AppendEntry(string fileText, ChangeLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(fileText);
        ArgumentNullException.ThrowIfNull(entry);

        string formatted = FormatEntry(entry);
        string normalized = fileText.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
        string[] lines = normalized.Split('\n');
        int headingIndex = FindChangeLogHeadingLineIndex(lines);

        string trimmed = normalized.TrimEnd('\n');
        return headingIndex < 0
            ? trimmed + "\n\n" + ChangeLogHeading + "\n" + formatted + "\n"
            : trimmed + "\n" + formatted + "\n";
    }

    /// <summary>
    /// The version hash of a Task file's text: the first 16 hex characters of the SHA-256 of the
    /// text after CRLF has been normalised to '\n' (Spec §7.6).
    /// </summary>
    /// <param name="fileText">The Task file's raw text.</param>
    // TODO(2.4): stub until Task 2.4 implements the real SHA-256 based hash.
    public static string ComputeVersion(string fileText)
    {
        ArgumentNullException.ThrowIfNull(fileText);
        return "";
    }

    /// <summary>
    /// True when <paramref name="text"/>'s first line trims to "---" and some later line also
    /// trims to "---", closing the frontmatter block. Normalises line endings first, since the
    /// file may arrive with CRLF.
    /// </summary>
    private static bool HasFrontmatterBlock(string text)
    {
        string normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
        string[] lines = normalized.Split('\n');
        if (lines.Length < 2 || lines[0].Trim() != "---")
        {
            return false;
        }

        for (int i = 1; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "---")
            {
                return true;
            }
        }

        return false;
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
    /// Splits a Task file's body at the last line that trims (case-insensitively) to
    /// <see cref="ChangeLogHeading"/> and is outside a fenced code block. Everything before that
    /// line, trailing whitespace trimmed, is the description; everything after it is log lines
    /// (Spec §7.3 step 3).
    /// </summary>
    private static (string Description, IReadOnlyList<ChangeLogEntry> ChangeLog) SplitChangeLog(string body)
    {
        string[] lines = body.Split('\n');
        int headingIndex = FindChangeLogHeadingLineIndex(lines);

        if (headingIndex < 0)
        {
            return (body.TrimEnd(), []);
        }

        string description = string.Join('\n', lines[..headingIndex]).TrimEnd();
        List<ChangeLogEntry> entries = [];
        for (int i = headingIndex + 1; i < lines.Length; i++)
        {
            if (TryParseLogLine(lines[i], out ChangeLogEntry? entry))
            {
                entries.Add(entry);
            }
        }

        return (description, entries);
    }

    /// <summary>The index of the last line that trims to <see cref="ChangeLogHeading"/> outside a fenced code block, or -1.</summary>
    private static int FindChangeLogHeadingLineIndex(string[] lines)
    {
        bool inFence = false;
        int found = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].Trim();
            if (trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal))
            {
                inFence = !inFence;
                continue;
            }

            if (!inFence && string.Equals(trimmed, ChangeLogHeading, StringComparison.OrdinalIgnoreCase))
            {
                found = i;
            }
        }

        return found;
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
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal);

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

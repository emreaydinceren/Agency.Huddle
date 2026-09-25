using System.Globalization;

namespace Agency.Huddle.App.Tasks;

/// <summary>
/// A field-by-field diff between two versions of a <see cref="TaskItem"/> (Spec §7.5), and the
/// exact summary vocabulary for a Change log entry (Spec §7.4). The trigger message, the Change
/// log entry, the conflict check and the watcher's outside-edit path all use this one type.
/// </summary>
internal static class TaskDiff
{
    private const string EmDash = "—";
    private const string MinusSign = "−";
    private const string ListSeparator = "; ";

    private static readonly Dictionary<TaskField, string> WireKeys = new()
    {
        { TaskField.Title, "title" },
        { TaskField.Status, "status" },
        { TaskField.Priority, "priority" },
        { TaskField.Assignee, "assignee" },
        { TaskField.Origin, "origin" },
        { TaskField.Parent, "parent" },
        { TaskField.BlockedBy, "blocked_by" },
        { TaskField.DuplicateOf, "duplicate_of" },
        { TaskField.Tags, "tags" },
        { TaskField.StartDate, "start_date" },
        { TaskField.DueDate, "due_date" },
    };

    /// <summary>
    /// The short, Human-facing field label used by the conflict UI (Spec §13.7): the same wire key
    /// <see cref="WireKeys"/> already carries for a scalar or list field (matching what a Change log
    /// summary shows), "team/project" for <see cref="TaskField.Location"/> (Settled by the delivery
    /// manager, J55: "location" means nothing to a Human, who only ever sees Team and Project as two
    /// separate fields - <see cref="Summarise"/> keeps its own "moved: X → Y" wording for that field,
    /// unrelated to this one), or the lowercase enum name for a field - Description, Unknown - that has
    /// no wire key of its own, since neither is ever set directly by name on the wire. Exposed so
    /// callers reuse this one map instead of duplicating it (corrections-B7 "14.3" - Settled by the
    /// delivery manager, J50).
    /// </summary>
    /// <param name="field">The field to label.</param>
    public static string FieldLabel(TaskField field) => field switch
    {
        TaskField.Location => "team/project",
        _ => WireKeys.TryGetValue(field, out string? key) ? key : field.ToString().ToLowerInvariant(),
    };

    /// <summary>What changed, field by field, ignoring Path, Version and ChangeLog.</summary>
    public static IReadOnlyList<FieldChange> Compare(TaskItem before, TaskItem after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        List<FieldChange> changes = [];

        AddIfDifferent(changes, TaskField.Title, before.Title, after.Title);
        AddIfDifferent(
            changes, TaskField.Status,
            before.Status == after.Status ? null : before.Status.ToWire(),
            before.Status == after.Status ? null : after.Status.ToWire());
        AddIfDifferent(
            changes, TaskField.Priority,
            before.Priority == after.Priority ? null : before.Priority.ToWire(),
            before.Priority == after.Priority ? null : after.Priority.ToWire());
        AddIfDifferent(changes, TaskField.Assignee, before.Assignee, after.Assignee);
        AddIfDifferent(changes, TaskField.Origin, before.OriginRoomId, after.OriginRoomId);
        AddIfDifferent(changes, TaskField.Parent, before.Parent?.ToString(), after.Parent?.ToString());

        if (!SetEquals(before.BlockedBy, after.BlockedBy))
        {
            changes.Add(new FieldChange(TaskField.BlockedBy, Join(before.BlockedBy), Join(after.BlockedBy)));
        }

        AddIfDifferent(changes, TaskField.DuplicateOf, before.DuplicateOf?.ToString(), after.DuplicateOf?.ToString());

        if (!TagSetEquals(before.Tags, after.Tags))
        {
            changes.Add(new FieldChange(TaskField.Tags, string.Join(ListSeparator, before.Tags), string.Join(ListSeparator, after.Tags)));
        }

        AddIfDifferent(changes, TaskField.StartDate, FormatDate(before.StartDate), FormatDate(after.StartDate));
        AddIfDifferent(changes, TaskField.DueDate, FormatDate(before.DueDate), FormatDate(after.DueDate));

        if (!string.Equals(before.Description, after.Description, StringComparison.Ordinal))
        {
            changes.Add(new FieldChange(TaskField.Description, null, null));
        }

        string beforeLocation = LocationText(before.Location);
        string afterLocation = LocationText(after.Location);
        if (!LocationEquals(before.Location, after.Location))
        {
            changes.Add(new FieldChange(TaskField.Location, beforeLocation, afterLocation));
        }

        foreach (string key in UnknownKeysInOrder(before, after))
        {
            string? beforeValue = LastValue(before.UnknownFields, key);
            string? afterValue = LastValue(after.UnknownFields, key);
            if (!string.Equals(beforeValue, afterValue, StringComparison.Ordinal))
            {
                changes.Add(new FieldChange(TaskField.Unknown, key, null));
            }
        }

        return changes;
    }

    /// <summary>The Spec §7.4 summary vocabulary: one text per change, joined with "; ".</summary>
    public static string Summarise(IReadOnlyList<FieldChange> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);

        if (changes.Count == 0)
        {
            return "";
        }

        List<string> parts = new(changes.Count);
        foreach (FieldChange change in changes)
        {
            parts.Add(SummariseOne(change));
        }

        return string.Join(ListSeparator, parts);
    }

    private static string SummariseOne(FieldChange change) => change.Field switch
    {
        TaskField.Description => "description edited",
        TaskField.Location => $"moved: {change.Old} → {change.New}",
        TaskField.Unknown => $"{change.Old} edited",
        TaskField.BlockedBy or TaskField.Tags => SummariseListChange(change),
        _ => SummariseScalarChange(change),
    };

    private static string SummariseScalarChange(FieldChange change)
    {
        string key = WireKeys[change.Field];
        return $"{key}: {Dash(change.Old)} → {Dash(change.New)}";
    }

    private static string SummariseListChange(FieldChange change)
    {
        string key = WireKeys[change.Field];
        string[] oldItems = Split(change.Old);
        string[] newItems = Split(change.New);
        HashSet<string> oldSet = new(oldItems, StringComparer.Ordinal);
        HashSet<string> newSet = new(newItems, StringComparer.Ordinal);

        List<string> added = [.. newItems.Where(item => !oldSet.Contains(item)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        List<string> removed = [.. oldItems.Where(item => !newSet.Contains(item)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

        List<string> parts = new(added.Count + removed.Count);
        parts.AddRange(added.Select(item => $"+{item}"));
        parts.AddRange(removed.Select(item => $"{MinusSign}{item}"));

        return $"{key}: {string.Join(", ", parts)}";
    }

    private static void AddIfDifferent(List<FieldChange> changes, TaskField field, string? oldValue, string? newValue)
    {
        if (!string.Equals(oldValue, newValue, StringComparison.Ordinal))
        {
            changes.Add(new FieldChange(field, oldValue, newValue));
        }
    }

    private static bool SetEquals(IReadOnlyList<TaskId> before, IReadOnlyList<TaskId> after) =>
        new HashSet<TaskId>(before).SetEquals(after);

    private static bool TagSetEquals(IReadOnlyList<string> before, IReadOnlyList<string> after) =>
        new HashSet<string>(before, StringComparer.Ordinal).SetEquals(new HashSet<string>(after, StringComparer.Ordinal));

    private static string Join(IReadOnlyList<TaskId> ids) =>
        string.Join(ListSeparator, ids.Select(id => id.ToString()));

    private static string[] Split(string? value) =>
        string.IsNullOrEmpty(value) ? [] : value.Split(ListSeparator, StringSplitOptions.None);

    private static string Dash(string? value) => string.IsNullOrEmpty(value) ? EmDash : value;

    private static string? FormatDate(DateOnly? date) =>
        date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string LocationText(TaskLocation location) =>
        location.Project is null ? location.Team : $"{location.Team}/{location.Project}";

    private static bool LocationEquals(TaskLocation before, TaskLocation after) =>
        string.Equals(before.Team, after.Team, StringComparison.Ordinal) &&
        string.Equals(before.Project, after.Project, StringComparison.Ordinal);

    private static List<string> UnknownKeysInOrder(TaskItem before, TaskItem after)
    {
        List<string> keys = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        foreach (KeyValuePair<string, string> field in before.UnknownFields)
        {
            if (seen.Add(field.Key))
            {
                keys.Add(field.Key);
            }
        }

        foreach (KeyValuePair<string, string> field in after.UnknownFields)
        {
            if (seen.Add(field.Key))
            {
                keys.Add(field.Key);
            }
        }

        return keys;
    }

    private static string? LastValue(IReadOnlyList<KeyValuePair<string, string>> fields, string key)
    {
        string? value = null;
        foreach (KeyValuePair<string, string> field in fields)
        {
            if (string.Equals(field.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = field.Value;
            }
        }

        return value;
    }
}

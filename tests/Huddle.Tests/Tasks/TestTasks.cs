using System.Globalization;
using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>Shared test builder for TaskItem and ChangeLogEntry.</summary>
internal static class TestTasks
{
    /// <summary>Creates a TaskItem with sensible defaults for testing.</summary>
    /// <param name="id">Task ID. Default: "PLAT-0001"</param>
    /// <param name="title">Task title. Default: "T"</param>
    /// <param name="status">Task status. Default: ToDo</param>
    /// <param name="priority">Task priority. Default: Medium</param>
    /// <param name="creator">Creator name. Default: "Human"</param>
    /// <param name="assignee">Assignee name. Default: null</param>
    /// <param name="originRoomId">Origin room ID. Default: null</param>
    /// <param name="parent">Parent task ID. Default: null</param>
    /// <param name="blockedBy">Task IDs blocking this task. Default: []</param>
    /// <param name="duplicateOf">Task ID this is a duplicate of. Default: null</param>
    /// <param name="tags">Task tags. Default: []</param>
    /// <param name="startDate">Start date. Default: null</param>
    /// <param name="dueDate">Due date. Default: null</param>
    /// <param name="description">Task description. Default: ""</param>
    /// <param name="location">Task location. Default: new("Platform", null, false)</param>
    /// <param name="changeLog">Change log entries. Default: []</param>
    /// <param name="unknownFields">Unknown fields. Default: []</param>
    /// <param name="path">File path. Default: ""</param>
    /// <param name="version">Version hash. Default: ""</param>
    /// <param name="closedAt">Close date. Default: null</param>
    public static TaskItem Make(
        string id = "PLAT-0001",
        string title = "T",
        TaskState status = TaskState.ToDo,
        TaskPriority priority = TaskPriority.Medium,
        string creator = "Human",
        string? assignee = null,
        string? originRoomId = null,
        TaskId? parent = null,
        IReadOnlyList<TaskId>? blockedBy = null,
        TaskId? duplicateOf = null,
        IReadOnlyList<string>? tags = null,
        DateOnly? startDate = null,
        DateOnly? dueDate = null,
        string? description = null,
        TaskLocation? location = null,
        IReadOnlyList<ChangeLogEntry>? changeLog = null,
        IReadOnlyList<KeyValuePair<string, string>>? unknownFields = null,
        string? path = null,
        string? version = null,
        DateTimeOffset? closedAt = null)
    {
        _ = TaskId.TryParse(id, out TaskId taskId);

        return new()
        {
            Id = taskId,
            Title = title,
            Status = status,
            Priority = priority,
            Creator = creator,
            Assignee = assignee,
            OriginRoomId = originRoomId,
            Parent = parent,
            BlockedBy = blockedBy ?? [],
            DuplicateOf = duplicateOf,
            Tags = tags ?? [],
            StartDate = startDate,
            DueDate = dueDate,
            Description = description ?? "",
            Location = location ?? new("Platform", null, false),
            ChangeLog = changeLog ?? [],
            UnknownFields = unknownFields ?? [],
            Path = path ?? "",
            Version = version ?? "",
            ClosedAt = closedAt,
        };
    }

    /// <summary>Creates a ChangeLogEntry with a parsed ISO 8601 timestamp.</summary>
    /// <param name="at">ISO 8601 timestamp, e.g. "2026-10-01T09:00:00Z"</param>
    /// <param name="actor">Actor name</param>
    /// <param name="summary">Change summary</param>
    public static ChangeLogEntry Entry(string at, string actor, string summary)
    {
        DateTimeOffset timestamp = DateTimeOffset.Parse(at, CultureInfo.InvariantCulture);
        return new(timestamp, actor, summary);
    }
}

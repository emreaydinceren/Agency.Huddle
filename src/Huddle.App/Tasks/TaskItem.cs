namespace Agency.Huddle.App.Tasks;

/// <summary>Who made a change. Names are Persona Names, or the Human's Name.</summary>
public sealed record TaskActor(TaskActorKind Kind, string Name, string? UserId);

/// <summary>The kind of actor who made a change to a Task.</summary>
public enum TaskActorKind
{
    /// <summary>The human user.</summary>
    Human,

    /// <summary>An AI agent.</summary>
    Agent,

    /// <summary>Someone outside Huddle who edited the file.</summary>
    OutsideHuddle,
}

/// <summary>Where a Task lives. Project is null for a Task directly under its Team.</summary>
public sealed record TaskLocation(string Team, string? Project, bool Closed);

/// <summary>One entry in a Task's change log.</summary>
public sealed record ChangeLogEntry(DateTimeOffset At, string Actor, string Summary);

/// <summary>A work item that the Human and AI Teammates share.</summary>
public sealed record TaskItem
{
    /// <summary>The Task's unique identifier.</summary>
    public required TaskId Id { get; init; }

    /// <summary>The Task's title.</summary>
    public required string Title { get; init; }

    /// <summary>The current state of the Task.</summary>
    public required TaskState Status { get; init; }

    /// <summary>The priority level of the Task.</summary>
    public required TaskPriority Priority { get; init; }

    /// <summary>The name of the person who created the Task.</summary>
    public required string Creator { get; init; }

    /// <summary>The name of the person assigned to the Task, or null if unassigned.</summary>
    public string? Assignee { get; init; }

    /// <summary>The Room in which the Task was created, or null if created outside a Room.</summary>
    public string? OriginRoomId { get; init; }

    /// <summary>The parent Task, if this Task is a subtask.</summary>
    public TaskId? Parent { get; init; }

    /// <summary>Tasks that must be completed before this Task can be done.</summary>
    public IReadOnlyList<TaskId> BlockedBy { get; init; } = [];

    /// <summary>If this Task's status is Duplicate, the Task it is a duplicate of.</summary>
    public TaskId? DuplicateOf { get; init; }

    /// <summary>User-defined labels for the Task.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>The date the Task should start, or null if not set.</summary>
    public DateOnly? StartDate { get; init; }

    /// <summary>The date the Task is due, or null if not set.</summary>
    public DateOnly? DueDate { get; init; }

    /// <summary>The Task's description in markdown format.</summary>
    public string Description { get; init; } = "";

    /// <summary>Where the Task lives in the folder structure.</summary>
    public required TaskLocation Location { get; init; }

    /// <summary>The log of all changes made to this Task.</summary>
    public IReadOnlyList<ChangeLogEntry> ChangeLog { get; init; } = [];

    /// <summary>Fields from the Task file that the parser did not recognize.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> UnknownFields { get; init; } = [];

    /// <summary>The absolute file path to the Task file.</summary>
    public required string Path { get; init; }

    /// <summary>The version hash of the Task file for conflict detection.</summary>
    public required string Version { get; init; }

    /// <summary>The date and time the Task was closed, or null if not closed.</summary>
    public DateTimeOffset? ClosedAt { get; init; }

    /// <summary>The creation date, derived from the first ChangeLog entry.</summary>
    public DateTimeOffset? Created => this.ChangeLog.Count > 0 ? this.ChangeLog[0].At : null;

    /// <summary>The last update date, derived from the last ChangeLog entry.</summary>
    public DateTimeOffset? Updated => this.ChangeLog.Count > 0 ? this.ChangeLog[^1].At : null;
}

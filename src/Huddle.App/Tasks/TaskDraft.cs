namespace Agency.Huddle.App.Tasks;

/// <summary>
/// A new Task, as offered to <see cref="TaskService.Create(TaskDraft, TaskActor)"/> (Spec §9.1).
/// <see cref="Team"/> is required; <see cref="Project"/> is optional; every other field defaults.
/// Public (Settled corrections-B5 D6 decision A): a Razor component builds one directly as a
/// <c>TaskDetail</c>/<c>TaskDetailDialog</c> parameter, so a flat positional record with no
/// behaviour needs no interface to hide behind (CA1000/CA1034 don't apply to a record with no
/// static members or nested generic types).
/// </summary>
/// <param name="Title">The Task's title, 1-200 characters, one line.</param>
/// <param name="Team">The Team the Task belongs to: a known Team label or an existing Team folder.</param>
/// <param name="Project">The Project the Task belongs to, or <see langword="null"/> for none.</param>
/// <param name="Status">The Task's initial state. Defaults to <see cref="TaskState.Backlog"/>.</param>
/// <param name="Priority">The Task's initial priority. Defaults to <see cref="TaskPriority.Medium"/>.</param>
/// <param name="Assignee">The Task's assignee: a known Persona Name or Alias, or the Human's Name; <see langword="null"/> for unassigned.</param>
/// <param name="OriginRoomId">The Room the Task was created in, or <see langword="null"/> when created outside a Room.</param>
/// <param name="Parent">The Task's parent, or <see langword="null"/> for none.</param>
/// <param name="BlockedBy">The Tasks that block this one; <see langword="null"/> is treated as none.</param>
/// <param name="Tags">The Task's labels; <see langword="null"/> is treated as none.</param>
/// <param name="StartDate">The date the Task should start, or <see langword="null"/>.</param>
/// <param name="DueDate">The date the Task is due, or <see langword="null"/>.</param>
/// <param name="Description">The Task's description in markdown format. Must not contain a Change log heading.</param>
public sealed record TaskDraft(
    string Title,
    string Team,
    string? Project,
    TaskState Status = TaskState.Backlog,
    TaskPriority Priority = TaskPriority.Medium,
    string? Assignee = null,
    string? OriginRoomId = null,
    TaskId? Parent = null,
    IReadOnlyList<TaskId>? BlockedBy = null,
    IReadOnlyList<string>? Tags = null,
    DateOnly? StartDate = null,
    DateOnly? DueDate = null,
    string Description = "");

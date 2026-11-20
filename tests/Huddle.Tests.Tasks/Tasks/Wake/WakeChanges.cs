using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.HumanGate;

namespace Agency.Huddle.Tests.Tasks.Wake;

/// <summary>Builders for the <see cref="TaskChange"/> values the wake relevance tests judge, so each test names only what it varies.</summary>
internal static class WakeChanges
{
    private static readonly DateTimeOffset Noon = new(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A Platform Task in <paramref name="status"/>, in the open folder unless <paramref name="closed"/>.</summary>
    /// <param name="status">The Task's status.</param>
    /// <param name="closed">Whether the Task sits in the closed folder.</param>
    public static TaskItem Task(TaskState status = TaskState.ToDo, bool closed = false) =>
        TestTasks.Make(status: status, location: new TaskLocation("Platform", null, closed));

    /// <summary>A change raised by <paramref name="actor"/> with the given kind, fields, decision and comment.</summary>
    /// <param name="before">The Task before, or <see langword="null"/> for a Create.</param>
    /// <param name="after">The Task after.</param>
    /// <param name="changes">The field changes.</param>
    /// <param name="actor">The kind of actor who made the change.</param>
    /// <param name="kind">What the change is, for a change with no fields.</param>
    /// <param name="decision">The Human's decision, when the change records one.</param>
    /// <param name="comment">The comment text, for a comment.</param>
    public static TaskChange Make(
        TaskItem? before,
        TaskItem after,
        IReadOnlyList<FieldChange> changes,
        TaskActorKind actor = TaskActorKind.Human,
        TaskChangeKind kind = TaskChangeKind.Edit,
        TaskDecision? decision = null,
        string? comment = null) =>
        new(
            before,
            after,
            changes,
            new TaskActor(actor, "Someone", null),
            new ChangeLogEntry(Noon, "Someone", "changed"),
            decision,
            Kind: kind,
            Comment: comment);

    /// <summary>An edit of one Task (before and after are the same Task) whose only fields are <paramref name="changes"/>.</summary>
    /// <param name="actor">The kind of actor who made the change.</param>
    /// <param name="changes">The field changes.</param>
    public static TaskChange Edit(TaskActorKind actor, params FieldChange[] changes) =>
        Make(Task(), Task(), changes, actor);

    /// <summary>An edit whose only field is a Status change written the way the Task file writes it.</summary>
    /// <param name="from">The old status.</param>
    /// <param name="to">The new status.</param>
    /// <param name="actor">The kind of actor who made the change.</param>
    public static TaskChange StatusMove(TaskState from, TaskState to, TaskActorKind actor = TaskActorKind.Agent) =>
        Make(Task(from), Task(from), [new FieldChange(TaskField.Status, from.ToWire(), to.ToWire())], actor);

    /// <summary>A change with no fields of the given <paramref name="kind"/> on a Task in <paramref name="status"/>.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="status">The Task's status.</param>
    public static TaskChange Bare(TaskChangeKind kind, TaskState status = TaskState.InProgress) =>
        Make(Task(status), Task(status), [], TaskActorKind.Agent, kind);

    /// <summary>A close (<paramref name="closing"/>) or reopen of a Task in <paramref name="status"/>: the closed flag flips and no field changes.</summary>
    /// <param name="status">The Task's status, which a close or reopen leaves alone.</param>
    /// <param name="closing"><see langword="true"/> to close, <see langword="false"/> to reopen.</param>
    /// <param name="kind">The kind the producer put on the change.</param>
    /// <param name="actor">The kind of actor who made the change.</param>
    public static TaskChange CloseOrReopen(TaskState status, bool closing, TaskChangeKind kind, TaskActorKind actor = TaskActorKind.Human) =>
        Make(Task(status, closed: !closing), Task(status, closed: closing), [], actor, kind);
}

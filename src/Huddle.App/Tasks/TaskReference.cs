namespace Agency.Huddle.App.Tasks;

/// <summary>The slice of a Task that a rendered link needs: its id, title and whether it is Closed.</summary>
/// <param name="Id">The Task's id.</param>
/// <param name="Title">The Task's title, used as the link's <c>title</c> attribute.</param>
/// <param name="Closed">Whether the Task is Closed, which adds the struck-through <c>task-ref-closed</c> class.</param>
/// <param name="Status">The Task's state, which gives the link its status dot, or <see langword="null"/> for no dot.</param>
public sealed record TaskReference(TaskId Id, string Title, bool Closed, TaskState? Status = null);

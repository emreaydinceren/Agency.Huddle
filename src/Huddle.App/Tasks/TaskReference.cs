namespace Agency.Huddle.App.Tasks;

/// <summary>
/// Resolves a candidate <see cref="TaskId"/> found in rendered Markdown to the Task it names, so
/// <see cref="Agency.Huddle.App.Services.MarkdownRenderer"/> can turn it into a link (Spec §13.13.2).
/// Called once per candidate token while rendering.
/// </summary>
public interface ITaskReferenceResolver
{
    /// <summary>The Task with this id, or <see langword="null"/> when no Task carries it.</summary>
    /// <param name="id">The candidate id parsed out of the rendered text.</param>
    TaskReference? Resolve(TaskId id);
}

/// <summary>The slice of a Task that a rendered link needs: its id, title and whether it is Closed.</summary>
/// <param name="Id">The Task's id.</param>
/// <param name="Title">The Task's title, used as the link's <c>title</c> attribute.</param>
/// <param name="Closed">Whether the Task is Closed, which adds the struck-through <c>task-ref-closed</c> class.</param>
public sealed record TaskReference(TaskId Id, string Title, bool Closed);

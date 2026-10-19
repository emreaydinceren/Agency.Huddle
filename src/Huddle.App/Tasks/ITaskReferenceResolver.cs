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

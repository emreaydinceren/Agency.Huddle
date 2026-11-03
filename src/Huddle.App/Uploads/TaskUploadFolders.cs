using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Uploads;

/// <summary>
/// Resolves the folder a Task's uploaded files live in (Second Brain Spec 6.1, correction C4):
/// <c>teams:/&lt;Team&gt;[/&lt;Project&gt;]/raw/Tasks/&lt;ID&gt;</c>, under the Team and Project the Task is filed in.
/// The Task id never changes, so closing a Task never moves it.
/// </summary>
/// <param name="resolver">Resolves the path inside the Teams root.</param>
/// <param name="tasks">Finds the Task, and so its Team and Project.</param>
internal sealed class TaskUploadFolders(LibraryPathResolver resolver, TaskStore tasks)
{
    /// <summary>The folder under a <c>raw</c> folder that holds every Task's folder.</summary>
    internal const string FolderName = "Tasks";

    private const string TeamsRootId = "teams";

    /// <summary>The upload folder of <paramref name="taskId"/>, whether or not it exists yet.</summary>
    /// <param name="taskId">The Task id, such as <c>GROW-0002</c>.</param>
    /// <returns>The resolved folder, or <see langword="null"/> when the id is not a valid Task id, no Task has it, or the Teams root is not configured.</returns>
    public LibraryPath? For(string taskId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);

        if (!TaskId.TryParse(taskId, out TaskId id) || LibrarySegments.Check(taskId) is not null)
        {
            return null;
        }

        TaskItem? task = tasks.Get(id);

        if (task is null)
        {
            return null;
        }

        string scope = task.Location.Project is { } project
            ? $"{task.Location.Team}/{project}"
            : task.Location.Team;

        return resolver.TryResolve(TeamsRootId, $"{scope}/{TeamNames.RawFolder}/{TaskUploadFolders.FolderName}/{taskId}", out LibraryPath? folder, out _)
            ? folder
            : null;
    }
}

using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.App.Uploads;

/// <summary>
/// Resolves the folder a Task's uploaded files live in (TaskAttachments spec 6.1): <c>raw:/Tasks/&lt;ID&gt;</c>.
/// It is keyed by the Task's id, which never changes, so closing or re-filing a Task never moves it.
/// </summary>
/// <param name="resolver">Resolves the path inside the Uploads root.</param>
internal sealed class TaskUploadFolders(LibraryPathResolver resolver)
{
    /// <summary>The folder under the Uploads root that holds every Task's folder. Room folders are 32 hex digits, so it cannot collide.</summary>
    internal const string FolderName = "Tasks";

    private const string RawRootId = "raw";

    /// <summary>The upload folder of <paramref name="taskId"/>, whether or not it exists yet.</summary>
    /// <param name="taskId">The Task id, such as <c>GROW-0002</c>.</param>
    /// <returns>The resolved folder, or <see langword="null"/> when the Uploads root is not configured or the id is not a valid Task id and a safe path segment.</returns>
    public LibraryPath? For(string taskId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);

        if (!TaskId.TryParse(taskId, out _) || LibrarySegments.Check(taskId) is not null)
        {
            return null;
        }

        return resolver.TryResolve(RawRootId, $"{TaskUploadFolders.FolderName}/{taskId}", out LibraryPath? folder, out _)
            ? folder
            : null;
    }
}

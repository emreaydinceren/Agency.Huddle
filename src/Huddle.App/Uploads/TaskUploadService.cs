using System.Globalization;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Uploads;

/// <summary>
/// Saves a file the Human attaches to a Task into <c>uploads:/Tasks/&lt;ID&gt;</c> (TaskAttachments spec 6.2),
/// through the same byte writer and refusals as a chat upload.
/// </summary>
/// <param name="tasks">Says whether the Task still exists.</param>
/// <param name="folders">Resolves the Task's upload folder.</param>
/// <param name="files">The Library's byte writer.</param>
/// <param name="options">Supplies <see cref="LibraryOptions.MaxUploadBytes"/>.</param>
/// <param name="baselines">Records the saved file's size and modified time for <c>brain-check</c> (Spec 7.6).</param>
/// <param name="timeProvider">The clock a pasted image's name is stamped from; the system clock when <see langword="null"/>.</param>
/// <param name="logger">Logs a baseline that could not be recorded; nothing is logged when <see langword="null"/>.</param>
internal sealed class TaskUploadService(
    ITaskReferenceResolver tasks,
    TaskUploadFolders folders,
    LibraryFileService files,
    IOptions<TeamOptions> options,
    RawBaselineStore baselines,
    TimeProvider? timeProvider = null,
    ILogger<TaskUploadService>? logger = null)
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    /// <summary>Cleans <paramref name="browserName"/> and saves <paramref name="content"/> into the Task's upload folder.</summary>
    /// <param name="taskId">The Task the file is attached to.</param>
    /// <param name="browserName">The name the browser gave the file.</param>
    /// <param name="content">The bytes to save.</param>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>The saved file with its kind and size, or the refusal text.</returns>
    public async Task<LibraryResult<LibraryUpload>> SaveAsync(string taskId, string? browserName, Stream content, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        ArgumentNullException.ThrowIfNull(content);

        string name = UploadNames.Clean(browserName, this.clock);
        if (!TaskId.TryParse(taskId, out TaskId id) || tasks.Resolve(id) is null)
        {
            return LibraryResult<LibraryUpload>.Fail(string.Create(CultureInfo.InvariantCulture, $"Couldn't attach {name}: this Task no longer exists."));
        }

        LibraryPath? folder = folders.For(taskId);
        if (folder is null)
        {
            return LibraryResult<LibraryUpload>.Fail(string.Create(CultureInfo.InvariantCulture, $"Couldn't save {name}."));
        }

        LibraryResult<LibraryUpload> result = await files.CreateFromStreamAsync(folder, name, content, options.Value.Library.MaxUploadBytes, ct).ConfigureAwait(false);
        if (result.Value is not null)
        {
            RawBaselineRecorder.Record(baselines, options.Value.DataDir, result.Value.File.FullPath, logger);
        }

        return result;
    }

    /// <summary>Deletes a saved upload whose path line was never saved into the description.</summary>
    /// <param name="file">The saved file.</param>
    /// <param name="ct">Cancels the delete.</param>
    /// <returns>A task that completes once the file is gone.</returns>
    public Task DeleteUnsentAsync(LibraryPath file, CancellationToken ct) => files.DeleteUnsentUploadAsync(file, ct);
}

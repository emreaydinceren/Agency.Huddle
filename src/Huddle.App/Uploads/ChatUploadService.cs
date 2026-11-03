using System.Globalization;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Uploads;

/// <summary>
/// Saves a file the Human attaches in a chat into the Library (ChatUploads spec 5 and 6.4): a root Room's
/// files go to <c>uploads:/&lt;roomId&gt;</c>, a Team Room's to <c>teams:/&lt;Team&gt;/Uploads/&lt;roomId&gt;</c>.
/// </summary>
/// <param name="directory">Loads the Room, whose Scope picks the folder.</param>
/// <param name="files">The Library's byte writer.</param>
/// <param name="folders">Resolves the Room's upload folders.</param>
/// <param name="options">Supplies <see cref="LibraryOptions.MaxUploadBytes"/>.</param>
/// <param name="baselines">Records the saved file's size and modified time for <c>brain-check</c> (Spec 7.6).</param>
/// <param name="timeProvider">The clock a pasted image's name is stamped from; the system clock when <see langword="null"/>.</param>
/// <param name="logger">Logs a baseline that could not be recorded; nothing is logged when <see langword="null"/>.</param>
internal sealed class ChatUploadService(
    ITeamDirectory directory,
    LibraryFileService files,
    IRoomUploadFolders folders,
    IOptions<TeamOptions> options,
    RawBaselineStore baselines,
    TimeProvider? timeProvider = null,
    ILogger<ChatUploadService>? logger = null)
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    /// <summary>Cleans <paramref name="browserName"/> and saves <paramref name="content"/> into the Room's upload folder.</summary>
    /// <param name="roomId">The Room the file is attached in.</param>
    /// <param name="browserName">The name the browser gave the file.</param>
    /// <param name="content">The bytes to save.</param>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>The saved file with its kind and size, or the refusal text.</returns>
    public async Task<LibraryResult<LibraryUpload>> SaveAsync(string roomId, string? browserName, Stream content, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        ArgumentNullException.ThrowIfNull(content);

        string name = UploadNames.Clean(browserName, this.clock);
        string couldNotSave = string.Create(CultureInfo.InvariantCulture, $"Couldn't save {name}.");

        Room? room = await directory.GetRoomAsync(roomId, ct).ConfigureAwait(false);
        if (room is null)
        {
            return LibraryResult<LibraryUpload>.Fail(couldNotSave);
        }

        IReadOnlyList<LibraryPath> candidates = folders.For(room);
        int expected = room.Team is null ? 1 : 2;
        if (candidates.Count != expected)
        {
            return LibraryResult<LibraryUpload>.Fail(couldNotSave);
        }

        LibraryResult<LibraryUpload> result = await files.CreateFromStreamAsync(candidates[^1], name, content, options.Value.Library.MaxUploadBytes, ct).ConfigureAwait(false);
        if (result.Value is not null)
        {
            RawBaselineRecorder.Record(baselines, options.Value.DataDir, result.Value.File.FullPath, logger);
        }

        return result;
    }

    /// <summary>Deletes a saved upload that was never sent.</summary>
    /// <param name="file">The saved file.</param>
    /// <param name="ct">Cancels the delete.</param>
    /// <returns>A task that completes once the file is gone.</returns>
    public Task DeleteUnsentAsync(LibraryPath file, CancellationToken ct) => files.DeleteUnsentUploadAsync(file, ct);
}

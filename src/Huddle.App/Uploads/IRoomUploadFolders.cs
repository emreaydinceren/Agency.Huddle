using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.App.Uploads;

/// <summary>
/// Says where a Room's chat uploads live in the Library (ChatUploads spec 6.9): always
/// <c>uploads:/&lt;roomId&gt;</c>, and for a Room scoped to a Team also <c>teams:/&lt;Team&gt;/Uploads/&lt;roomId&gt;</c>.
/// </summary>
internal interface IRoomUploadFolders
{
    /// <summary>
    /// The upload folders of <paramref name="room"/>, whether or not they exist yet: the Uploads-root folder first,
    /// then the Team folder when the Room has a Team. A folder whose path the Library refuses is left out.
    /// </summary>
    /// <param name="room">The Room to look up.</param>
    /// <returns>One or two resolved folders.</returns>
    IReadOnlyList<LibraryPath> For(Room room);

    /// <summary>Counts the files in <paramref name="room"/>'s upload folders, nested ones included (one enumeration).</summary>
    /// <param name="room">The Room to count.</param>
    /// <returns>The file count; 0 when no folder exists or one cannot be read.</returns>
    int CountFiles(Room room);

    /// <summary>
    /// Sends each existing upload folder of <paramref name="room"/> to the Recycle Bin through the Library.
    /// Best effort: a refusal is reported, never thrown, and the remaining folders are still tried.
    /// </summary>
    /// <param name="room">The Room whose uploads to recycle.</param>
    /// <param name="ct">Cancels the recycle.</param>
    /// <returns>One refusal text per folder that could not be recycled; empty when all went to the bin.</returns>
    Task<IReadOnlyList<string>> RecycleAsync(Room room, CancellationToken ct);
}

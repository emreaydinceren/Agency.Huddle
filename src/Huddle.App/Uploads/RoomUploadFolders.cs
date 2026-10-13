using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Uploads;

/// <summary>Resolves a Room's upload folders through the Library's path resolver.</summary>
/// <param name="resolver">Resolves <c>uploads:/&lt;roomId&gt;</c> and <c>teams:/&lt;Team&gt;/Uploads/&lt;roomId&gt;</c>.</param>
/// <param name="files">Recycles a folder; the Library never hard-deletes.</param>
internal sealed class RoomUploadFolders(LibraryPathResolver resolver, LibraryFileService files) : IRoomUploadFolders
{
    private const string UploadsRootId = "uploads";
    private const string TeamsRootId = "teams";

    /// <inheritdoc />
    public IReadOnlyList<LibraryPath> For(Room room)
    {
        ArgumentNullException.ThrowIfNull(room);

        List<LibraryPath> folders = [];
        if (resolver.TryResolve(UploadsRootId, room.Id, out LibraryPath? rootFolder, out _))
        {
            folders.Add(rootFolder);
        }

        if (room.Team is not null
            && LibrarySegments.Check(room.Team) is null
            && resolver.TryResolve(TeamsRootId, $"{room.Team}/{TeamNames.UploadsFolder}/{room.Id}", out LibraryPath? teamFolder, out _))
        {
            folders.Add(teamFolder);
        }

        return folders;
    }

    /// <inheritdoc />
    public int CountFiles(Room room)
    {
        int count = 0;
        foreach (LibraryPath folder in this.For(room))
        {
            try
            {
                if (Directory.Exists(folder.FullPath))
                {
                    count += Directory.EnumerateFiles(folder.FullPath, "*", SearchOption.AllDirectories).Count();
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The count only decorates a confirmation; an unreadable folder counts as empty.
            }
        }

        return count;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> RecycleAsync(Room room, CancellationToken ct)
    {
        List<string> failures = [];
        foreach (LibraryPath folder in this.For(room))
        {
            if (!Directory.Exists(folder.FullPath))
            {
                continue;
            }

            LibraryResult<LibraryPath> result = await files.RecycleAsync(folder, ct);
            if (result.Error is not null)
            {
                failures.Add(result.Error);
            }
        }

        return failures;
    }
}

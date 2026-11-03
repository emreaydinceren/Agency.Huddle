using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.SecondBrain;

/// <summary>
/// The crash-safe folder move behind archive and unarchive (corrections-12 items 4, 7, 8, 9, 15, 16). A plain rename is
/// tried first. When the file system says the destination is on another volume, or the destination already holds entries
/// (a Team whose Projects were archived on their own), the folder is copied into a staging folder beside the destination,
/// verified by file count and bytes, renamed into place, and only then is the source renamed to a deleting folder and deleted.
/// A crash at any step leaves either the whole source or the whole destination plus a dot folder that
/// <see cref="Recover"/> removes at the next start.
/// </summary>
internal static class ArchiveMove
{
    /// <summary>Prefix of the staging folder an archive copies into.</summary>
    public const string ArchivingPrefix = ".huddle-archiving-";

    /// <summary>Prefix of the staging folder an unarchive copies into.</summary>
    public const string UnarchivingPrefix = ".huddle-unarchiving-";

    /// <summary>Prefix of the folder a moved source is renamed to before it is deleted.</summary>
    public const string DeletingPrefix = ".huddle-deleting-";

    private const int RenameAttempts = 10;

    private static readonly string[] DotPrefixes = [ArchivingPrefix, UnarchivingPrefix, DeletingPrefix];

    /// <summary>Moves a folder to a destination that may already hold entries.</summary>
    /// <param name="source">The folder to move.</param>
    /// <param name="destination">The new folder path; an existing non-empty folder is merged into.</param>
    /// <param name="stagingPrefix">The dot prefix for the staging folder beside the destination.</param>
    /// <param name="files">The disk operations.</param>
    /// <returns><see cref="ArchiveMoveResult.Moved"/>, or <see cref="ArchiveMoveResult.InUse"/> when a program kept a file open through every retry; nothing has changed then.</returns>
    /// <exception cref="IOException">The copy or its verification failed; the staging folder is removed and the source is whole.</exception>
    public static ArchiveMoveResult Move(string source, string destination, string stagingPrefix, IArchiveFileSystem files)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(stagingPrefix);
        ArgumentNullException.ThrowIfNull(files);

        files.CreateDirectory(ParentOf(destination));
        bool merge = files.ListEntries(destination).Count > 0;
        if (!merge)
        {
            // An empty destination (left by hand or an earlier run) would make a whole-folder rename fail on Windows.
            files.DeleteEmptyFolder(destination);
            ArchiveMoveResult renamed = Rename(source, destination, files);
            if (renamed != ArchiveMoveResult.CrossVolume)
            {
                return renamed;
            }
        }

        return Staged(source, destination, stagingPrefix, merge, files);
    }

    /// <summary>Deletes the dot folders an interrupted move left in both roots; any other dot folder (a user's <c>.git</c>) stays.</summary>
    /// <param name="archiveRoot">The Archive root.</param>
    /// <param name="teamsRoot">The Teams root.</param>
    /// <param name="files">The disk operations.</param>
    public static void Recover(string archiveRoot, string teamsRoot, IArchiveFileSystem files)
    {
        ArgumentNullException.ThrowIfNull(archiveRoot);
        ArgumentNullException.ThrowIfNull(teamsRoot);
        ArgumentNullException.ThrowIfNull(files);

        foreach (string root in new[] { archiveRoot, teamsRoot })
        {
            foreach (string entry in files.ListEntries(root).Where(static e => DotPrefixes.Any(p => e.StartsWith(p, StringComparison.Ordinal))))
            {
                files.DeleteTree(Path.Combine(root, entry));
            }
        }
    }

    private static ArchiveMoveResult Staged(string source, string destination, string stagingPrefix, bool merge, IArchiveFileSystem files)
    {
        string leaf = Path.GetFileName(destination);
        string staging = Path.Combine(ParentOf(destination), stagingPrefix + leaf);
        string deleting = Path.Combine(ParentOf(source), DeletingPrefix + Path.GetFileName(source));
        DeleteIfPresent(staging, files);
        DeleteIfPresent(deleting, files);
        CopyAndVerify(source, staging, files);

        ArchiveMoveResult placed = merge ? MergeIn(staging, destination, files) : Rename(staging, destination, files);
        if (placed != ArchiveMoveResult.Moved)
        {
            files.DeleteTree(staging);
            return ArchiveMoveResult.InUse;
        }

        if (Rename(source, deleting, files) != ArchiveMoveResult.Moved)
        {
            // The destination is a complete copy but the source cannot be retired: take the copy back out so nothing looks archived.
            Unplace(source, destination, merge, files);
            return ArchiveMoveResult.InUse;
        }

        try
        {
            files.DeleteTree(deleting);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The move is done; a file the delete could not remove is finished by Recover at the next start.
        }

        return ArchiveMoveResult.Moved;
    }

    private static void CopyAndVerify(string source, string staging, IArchiveFileSystem files)
    {
        try
        {
            files.CopyTree(source, staging);
            (int Files, long Bytes) want = files.Measure(source);
            (int Files, long Bytes) got = files.Measure(staging);
            if (want != got)
            {
                throw new IOException($"The copy of {Path.GetFileName(source)} does not match the original: {got.Files} of {want.Files} files, {got.Bytes} of {want.Bytes} bytes.");
            }
        }
        catch (IOException)
        {
            files.DeleteTree(staging);
            throw;
        }
    }

    /// <summary>Moves the staging folder's entries into the existing destination, <c>_team.md</c> last so a half merge never makes the Team look archived.</summary>
    private static ArchiveMoveResult MergeIn(string staging, string destination, IArchiveFileSystem files)
    {
        List<string> entries = [.. files.ListEntries(staging).OrderBy(static e => string.Equals(e, TeamPaths.TeamFileName, StringComparison.OrdinalIgnoreCase) ? 1 : 0)];
        List<string> placed = [];
        foreach (string entry in entries)
        {
            if (Rename(Path.Combine(staging, entry), Path.Combine(destination, entry), files) != ArchiveMoveResult.Moved)
            {
                RemoveFromDestination(placed, destination, files);
                return ArchiveMoveResult.InUse;
            }

            placed.Add(entry);
        }

        files.DeleteEmptyFolder(staging);
        return ArchiveMoveResult.Moved;
    }

    private static void Unplace(string source, string destination, bool merge, IArchiveFileSystem files)
    {
        if (merge)
        {
            RemoveFromDestination(files.ListEntries(source), destination, files);
        }
        else
        {
            files.DeleteTree(destination);
        }
    }

    private static void RemoveFromDestination(IReadOnlyList<string> entries, string destination, IArchiveFileSystem files)
    {
        foreach (string entry in entries.OrderByDescending(static e => string.Equals(e, TeamPaths.TeamFileName, StringComparison.OrdinalIgnoreCase)))
        {
            string path = Path.Combine(destination, entry);
            if (files.FileExists(path))
            {
                files.DeleteFile(path);
            }
            else
            {
                files.DeleteTree(path);
            }
        }
    }

    private static void DeleteIfPresent(string path, IArchiveFileSystem files)
    {
        if (files.ListEntries(ParentOf(path)).Contains(Path.GetFileName(path), StringComparer.Ordinal))
        {
            files.DeleteTree(path);
        }
    }

    /// <summary>One rename, retried with a growing wait while the answer is <see cref="ArchiveMoveResult.InUse"/>.</summary>
    private static ArchiveMoveResult Rename(string source, string destination, IArchiveFileSystem files)
    {
        for (int attempt = 1; ; attempt++)
        {
            ArchiveMoveResult result = files.Move(source, destination);
            if (result != ArchiveMoveResult.InUse || attempt >= RenameAttempts)
            {
                return result;
            }

            // Synchronous on purpose: the caller runs under the provisioner's folder gate, a plain lock that cannot be
            // held across an await, and the whole wait is bounded (30 ms times 1 to 9, as AtomicFile waits).
            Thread.Sleep(TimeSpan.FromMilliseconds(30 * attempt));
        }
    }

    private static string ParentOf(string path) => Path.GetDirectoryName(path) ?? path;
}

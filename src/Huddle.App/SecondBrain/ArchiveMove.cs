using System.Text.Json;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.SecondBrain;

/// <summary>
/// The crash-safe folder move behind archive and unarchive (corrections-12 items 4, 7, 8, 9, 15, 16). A plain rename is
/// tried first. When the file system says the destination is on another volume, or the destination already holds entries
/// (a Team whose Projects were archived on their own), the result is built in a staging folder beside the destination (for a merge:
/// the archived Projects plus the live Team), verified by file count and bytes, and swapped into place; only then is the source
/// renamed to a deleting folder and deleted. Before every step a small journal, <c>Archive/.huddle-move-&lt;T&gt;.json</c>, records
/// where the move is, so <see cref="Recover"/> finishes or undoes it from any crash point.
/// </summary>
internal static class ArchiveMove
{
    /// <summary>Prefix of the staging folder an archive copies into.</summary>
    public const string ArchivingPrefix = ".huddle-archiving-";

    /// <summary>Prefix of the staging folder an unarchive copies into.</summary>
    public const string UnarchivingPrefix = ".huddle-unarchiving-";

    /// <summary>Prefix of the folder a moved source is renamed to before it is deleted.</summary>
    public const string DeletingPrefix = ".huddle-deleting-";

    /// <summary>Prefix of the folder the old archive folder is set aside to while a merged one is swapped in.</summary>
    public const string ReplacedPrefix = ".huddle-replaced-";

    /// <summary>Prefix of the journal file in the Archive root.</summary>
    public const string JournalPrefix = ".huddle-move-";

    private const int RenameAttempts = 10;
    private const string JournalSuffix = ".json";
    private const string ArchiveDirection = "archive";
    private const string UnarchiveDirection = "unarchive";
    private const string RenamingStep = "renaming";
    private const string StagingStep = "staging";
    private const string VerifiedStep = "verified";
    private const string PlacingStep = "placing";
    private const string PlacedStep = "placed";
    private const string UnplacingStep = "unplacing";
    private const string RetiredStep = "retired";

    private static readonly string[] DotPrefixes = [ArchivingPrefix, UnarchivingPrefix, DeletingPrefix, ReplacedPrefix];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Moves a folder to a destination that may already hold entries.</summary>
    /// <param name="source">The folder to move.</param>
    /// <param name="destination">The new folder path; an existing non-empty folder is merged into.</param>
    /// <param name="stagingPrefix">The dot prefix for the staging folder beside the destination; it also says which direction the move goes.</param>
    /// <param name="files">The disk operations.</param>
    /// <param name="teamFileCreated">Whether <c>_team.md</c> in the source was created for this move (a label-only Team); recovery deletes it again when the move is undone.</param>
    /// <returns><see cref="ArchiveMoveResult.Moved"/>, or <see cref="ArchiveMoveResult.InUse"/> when a program kept a file open through every retry; nothing has changed then.</returns>
    /// <exception cref="IOException">The copy, its verification or a rename failed for a reason other than an open file; the source is whole.</exception>
    /// <exception cref="UnauthorizedAccessException">The same, with access denied.</exception>
    public static ArchiveMoveResult Move(string source, string destination, string stagingPrefix, IArchiveFileSystem files, bool teamFileCreated = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(stagingPrefix);
        ArgumentNullException.ThrowIfNull(files);

        bool archiving = string.Equals(stagingPrefix, ArchivingPrefix, StringComparison.Ordinal);
        string archiveRoot = ParentOf(archiving ? destination : source);
        files.CreateDirectory(archiveRoot);
        files.CreateDirectory(ParentOf(destination));
        bool merge = files.ListEntries(destination).Count > 0;
        Context context = new(source, destination, stagingPrefix, files, merge, Path.Combine(archiveRoot, JournalPrefix + Path.GetFileName(destination) + JournalSuffix))
        {
            Journal = new Journal(archiving ? ArchiveDirection : UnarchiveDirection, RenamingStep, Path.GetFileName(source), Path.GetFileName(destination), merge, teamFileCreated),
        };
        context.Write(merge ? StagingStep : RenamingStep);
        if (!merge)
        {
            ArchiveMoveResult renamed;
            try
            {
                // An empty destination (left by hand or an earlier run) would make a whole-folder rename fail on Windows.
                files.DeleteEmptyFolder(destination);
                renamed = RenameRetrying(source, destination, files);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                files.DeleteFile(context.JournalPath);
                throw;
            }

            if (renamed != ArchiveMoveResult.CrossVolume)
            {
                files.DeleteFile(context.JournalPath);
                return renamed;
            }
        }

        return Staged(context);
    }

    /// <summary>Opens the journal before anything else changes (the roster write), so a crash before <see cref="Move"/> is recoverable.</summary>
    /// <param name="source">The folder that will move.</param>
    /// <param name="destination">Where it will move to.</param>
    /// <param name="stagingPrefix">The same prefix <see cref="Move"/> will get.</param>
    /// <param name="files">The disk operations.</param>
    /// <param name="teamFileCreated">Whether <c>_team.md</c> in the source is created for this move.</param>
    public static void Begin(string source, string destination, string stagingPrefix, IArchiveFileSystem files, bool teamFileCreated)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(stagingPrefix);
        ArgumentNullException.ThrowIfNull(files);

        bool archiving = string.Equals(stagingPrefix, ArchivingPrefix, StringComparison.Ordinal);
        files.CreateDirectory(ParentOf(archiving ? destination : source));
        Journal journal = new(archiving ? ArchiveDirection : UnarchiveDirection, RenamingStep, Path.GetFileName(source), Path.GetFileName(destination), false, teamFileCreated);
        files.WriteTextAtomic(JournalPathOf(source, destination, stagingPrefix), JsonSerializer.Serialize(journal, JsonOptions));
    }

    /// <summary>Removes the journal <see cref="Begin"/> opened when the move is given up before <see cref="Move"/> runs.</summary>
    /// <param name="source">The folder that would have moved.</param>
    /// <param name="destination">Where it would have moved to.</param>
    /// <param name="stagingPrefix">The prefix given to <see cref="Begin"/>.</param>
    /// <param name="files">The disk operations.</param>
    public static void Abandon(string source, string destination, string stagingPrefix, IArchiveFileSystem files)
    {
        ArgumentNullException.ThrowIfNull(files);

        files.DeleteFile(JournalPathOf(source, destination, stagingPrefix));
    }

    private static string JournalPathOf(string source, string destination, string stagingPrefix) =>
        Path.Combine(ParentOf(string.Equals(stagingPrefix, ArchivingPrefix, StringComparison.Ordinal) ? destination : source), JournalPrefix + Path.GetFileName(destination) + JournalSuffix);

    /// <summary>Finishes or undoes every journalled move, then deletes the dot folders an interrupted move left in both roots; any other dot folder (a user's <c>.git</c>) stays.</summary>
    /// <param name="archiveRoot">The Archive root.</param>
    /// <param name="teamsRoot">The Teams root.</param>
    /// <param name="files">The disk operations.</param>
    public static void Recover(string archiveRoot, string teamsRoot, IArchiveFileSystem files)
    {
        ArgumentNullException.ThrowIfNull(archiveRoot);
        ArgumentNullException.ThrowIfNull(teamsRoot);
        ArgumentNullException.ThrowIfNull(files);

        bool settled = true;
        foreach (string entry in files.ListEntries(archiveRoot).Where(static e => e.StartsWith(JournalPrefix, StringComparison.Ordinal)))
        {
            settled &= RecoverJournal(Path.Combine(archiveRoot, entry), archiveRoot, teamsRoot, files);
        }

        if (!settled)
        {
            // A journalled move could not be finished (a file is open); its folders are the only copy of something, so they stay for the next start.
            return;
        }

        foreach (string root in new[] { archiveRoot, teamsRoot })
        {
            foreach (string entry in files.ListEntries(root).Where(static e => DotPrefixes.Any(p => e.StartsWith(p, StringComparison.Ordinal))))
            {
                files.DeleteTree(Path.Combine(root, entry));
            }
        }
    }

    private static bool RecoverJournal(string path, string archiveRoot, string teamsRoot, IArchiveFileSystem files)
    {
        if (!path.EndsWith(JournalSuffix, StringComparison.Ordinal))
        {
            files.DeleteFile(path);
            return true;
        }

        Journal? journal = ReadJournal(files.ReadText(path));
        try
        {
            if (journal is not null)
            {
                bool archive = string.Equals(journal.Direction, ArchiveDirection, StringComparison.Ordinal);
                Finish(journal, archive ? teamsRoot : archiveRoot, archive ? archiveRoot : teamsRoot, files);
            }

            files.DeleteFile(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A program holds a file open, so the journal stays and the next start tries again.
            return false;
        }
    }

    private static Journal? ReadJournal(string? text)
    {
        try
        {
            return text is null ? null : JsonSerializer.Deserialize<Journal>(text, JsonOptions);
        }
        catch (JsonException)
        {
            // An unreadable journal is dropped; the dot-folder sweep still removes the leftovers, and a Team in both roots stays as it is.
            return null;
        }
    }

    /// <summary>Brings the move the journal describes to its end: the whole move, or none of it.</summary>
    private static void Finish(Journal journal, string sourceRoot, string destinationRoot, IArchiveFileSystem files)
    {
        string prefix = string.Equals(journal.Direction, ArchiveDirection, StringComparison.Ordinal) ? ArchivingPrefix : UnarchivingPrefix;
        string source = Path.Combine(sourceRoot, journal.SourceLeaf);
        string destination = Path.Combine(destinationRoot, journal.DestinationLeaf);
        string staging = Path.Combine(destinationRoot, prefix + journal.DestinationLeaf);
        string aside = Path.Combine(destinationRoot, ReplacedPrefix + journal.DestinationLeaf);
        string deleting = Path.Combine(sourceRoot, DeletingPrefix + journal.SourceLeaf);
        switch (journal.Step)
        {
            case RenamingStep when !Exists(source, files):
            case PlacedStep:
            case RetiredStep:
                Retire(source, deleting, aside, files);
                break;
            case PlacingStep when !Exists(staging, files):
                Retire(source, deleting, aside, files);
                break;
            case PlacingStep when journal.Merge && !Exists(destination, files) && Exists(aside, files):
                MoveIntoPlace(staging, destination, files);
                Retire(source, deleting, aside, files);
                break;
            case UnplacingStep:
                Unplace(destination, staging, aside, journal.Merge, files);
                DeleteCreatedTeamFile(journal, source, files);
                break;
            default:
                files.DeleteTree(staging);
                DeleteCreatedTeamFile(journal, source, files);
                break;
        }
    }

    private static void DeleteCreatedTeamFile(Journal journal, string source, IArchiveFileSystem files)
    {
        if (journal.TeamFileCreated && Exists(source, files))
        {
            files.DeleteFile(Path.Combine(source, TeamPaths.TeamFileName));
        }
    }

    private static void MoveIntoPlace(string staging, string destination, IArchiveFileSystem files)
    {
        if (files.Move(staging, destination) != ArchiveMoveResult.Moved)
        {
            throw new IOException($"{Path.GetFileName(destination)} could not be put in place.");
        }
    }

    private static void Retire(string source, string deleting, string aside, IArchiveFileSystem files)
    {
        if (Exists(source, files))
        {
            files.DeleteTree(deleting);
            if (files.Move(source, deleting) != ArchiveMoveResult.Moved)
            {
                throw new IOException($"{Path.GetFileName(source)} could not be retired.");
            }
        }

        files.DeleteTree(deleting);
        files.DeleteTree(aside);
    }

    private static ArchiveMoveResult Staged(Context c)
    {
        IArchiveFileSystem files = c.Files;
        DeleteIfPresent(c.Staging, files);
        DeleteIfPresent(c.Deleting, files);
        DeleteIfPresent(c.Aside, files);
        c.Write(StagingStep);
        CopyAndVerify(c);
        c.Write(VerifiedStep);
        c.Write(PlacingStep);

        ArchiveMoveResult placed = Place(c);
        if (placed != ArchiveMoveResult.Moved)
        {
            files.DeleteTree(c.Staging);
            files.DeleteFile(c.JournalPath);
            return ArchiveMoveResult.InUse;
        }

        c.Write(PlacedStep);
        if (RenameRetrying(c.Source, c.Deleting, files) != ArchiveMoveResult.Moved)
        {
            // The destination is a complete copy but the source cannot be retired: take the copy back out so nothing looks archived.
            c.Write(UnplacingStep);
            Unplace(c.Destination, c.Staging, c.Aside, c.Merge, files);
            files.DeleteFile(c.JournalPath);
            return ArchiveMoveResult.InUse;
        }

        c.Write(RetiredStep);
        try
        {
            files.DeleteTree(c.Deleting);
            files.DeleteTree(c.Aside);
            files.DeleteFile(c.JournalPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The move is done; the journal stays at "retired" and Recover finishes the delete at the next start.
        }

        return ArchiveMoveResult.Moved;
    }

    /// <summary>Puts the staged result in place: a plain rename, or for a merge the old archive folder set aside and the staged one renamed over it.</summary>
    private static ArchiveMoveResult Place(Context c)
    {
        try
        {
            if (!c.Merge)
            {
                return RenameRetrying(c.Staging, c.Destination, c.Files);
            }

            if (RenameRetrying(c.Destination, c.Aside, c.Files) != ArchiveMoveResult.Moved)
            {
                return ArchiveMoveResult.InUse;
            }

            if (RenameRetrying(c.Staging, c.Destination, c.Files) != ArchiveMoveResult.Moved)
            {
                _ = RenameRetrying(c.Aside, c.Destination, c.Files);
                return ArchiveMoveResult.InUse;
            }

            return ArchiveMoveResult.Moved;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Undo whatever half of the swap happened, then let the caller refuse; the journal says "placing" so Recover would do the same.
            c.Files.DeleteTree(c.Staging);
            if (c.Merge && !Exists(c.Destination, c.Files) && Exists(c.Aside, c.Files))
            {
                _ = c.Files.Move(c.Aside, c.Destination);
            }

            c.Files.DeleteFile(c.JournalPath);
            throw;
        }
    }

    private static void CopyAndVerify(Context c)
    {
        try
        {
            (int Files, long Bytes) want = c.Files.Measure(c.Source);
            if (c.Merge)
            {
                // The staged result is the archived Projects plus the live Team; the live archive is not touched until the swap.
                c.Files.CopyTree(c.Destination, c.Staging);
                (int Files, long Bytes) held = c.Files.Measure(c.Destination);
                want = (want.Files + held.Files, want.Bytes + held.Bytes);
            }

            c.Files.CopyTree(c.Source, c.Staging);
            (int Files, long Bytes) got = c.Files.Measure(c.Staging);
            if (want != got)
            {
                throw new IOException($"The copy of {Path.GetFileName(c.Source)} does not match the original: {got.Files} of {want.Files} files, {got.Bytes} of {want.Bytes} bytes.");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            c.Files.DeleteTree(c.Staging);
            c.Files.DeleteFile(c.JournalPath);
            throw;
        }
    }

    /// <summary>Undoes a placement: removes the new destination and, for a merge, puts the old archive folder back from where it was set aside.</summary>
    private static void Unplace(string destination, string staging, string aside, bool merge, IArchiveFileSystem files)
    {
        files.DeleteTree(staging);
        if (merge && Exists(aside, files))
        {
            files.DeleteTree(destination);
            if (RenameRetrying(aside, destination, files) != ArchiveMoveResult.Moved)
            {
                throw new IOException($"{Path.GetFileName(destination)} could not be put back.");
            }
        }
        else if (!merge)
        {
            files.DeleteTree(destination);
        }
    }

    private static bool Exists(string path, IArchiveFileSystem files) =>
        files.ListEntries(ParentOf(path)).Contains(Path.GetFileName(path), StringComparer.Ordinal);

    private static void DeleteIfPresent(string path, IArchiveFileSystem files)
    {
        if (Exists(path, files))
        {
            files.DeleteTree(path);
        }
    }

    /// <summary>
    /// The Team rename's folder rename: <see cref="RenameRetrying"/> that refuses a destination that already exists and writes no
    /// journal (the <c>team_renames</c> row is the journal).
    /// </summary>
    /// <param name="source">The folder to rename.</param>
    /// <param name="destination">The new path; it must not exist.</param>
    /// <param name="files">The disk operations.</param>
    /// <returns><see cref="ArchiveMoveResult.Moved"/>, or <see cref="ArchiveMoveResult.InUse"/> after every retry; nothing has changed then.</returns>
    /// <exception cref="IOException">The destination already exists.</exception>
    internal static ArchiveMoveResult Rename(string source, string destination, IArchiveFileSystem files)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(files);

        if (Exists(destination, files))
        {
            throw new IOException($"'{destination}' already exists.");
        }

        return RenameRetrying(source, destination, files);
    }

    /// <summary>One rename, retried with a growing wait while the answer is <see cref="ArchiveMoveResult.InUse"/>.</summary>
    private static ArchiveMoveResult RenameRetrying(string source, string destination, IArchiveFileSystem files)
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

    /// <summary>What the journal file holds: the direction, the last step begun, the folder names and what recovery must know to undo.</summary>
    private sealed record Journal(string Direction, string Step, string SourceLeaf, string DestinationLeaf, bool Merge, bool TeamFileCreated);

    /// <summary>The paths and flags of one move, and the journal that follows it.</summary>
    private sealed class Context
    {
        public Context(string source, string destination, string stagingPrefix, IArchiveFileSystem files, bool merge, string journalPath)
        {
            this.Source = source;
            this.Destination = destination;
            this.Files = files;
            this.Merge = merge;
            this.JournalPath = journalPath;
            this.Staging = Path.Combine(ParentOf(destination), stagingPrefix + Path.GetFileName(destination));
            this.Deleting = Path.Combine(ParentOf(source), DeletingPrefix + Path.GetFileName(source));
            this.Aside = Path.Combine(ParentOf(destination), ReplacedPrefix + Path.GetFileName(destination));
        }

        public string Source { get; }

        public string Destination { get; }

        public IArchiveFileSystem Files { get; }

        public bool Merge { get; }

        public string JournalPath { get; }

        public string Staging { get; }

        public string Deleting { get; }

        public string Aside { get; }

        public required Journal Journal { get; init; }

        /// <summary>Records that the move has reached a step, atomically.</summary>
        public void Write(string step)
        {
            this.Files.WriteTextAtomic(this.JournalPath, JsonSerializer.Serialize(this.Journal with { Step = step }, JsonOptions));
        }
    }
}

namespace Agency.Huddle.App.FileChanges;

/// <summary>The kind of change <see cref="FileStateDiff"/> found between two snapshots of a file.</summary>
internal enum FileChangeKind
{
    /// <summary>The file exists in the later snapshot only.</summary>
    Added,

    /// <summary>The file exists in both snapshots, with a different size or modified time.</summary>
    Changed,

    /// <summary>The file exists in the earlier snapshot only.</summary>
    Deleted,
}

/// <summary>One file's size and modified time, as last observed by a scan.</summary>
/// <param name="Size">The file's length in bytes, from <see cref="FileInfo.Length"/>.</param>
/// <param name="ModifiedUtc">The file's last write time, from <see cref="FileSystemInfo.LastWriteTimeUtc"/>.</param>
internal sealed record FileEntry(long Size, DateTimeOffset ModifiedUtc);

/// <summary>One folder's files, keyed by path relative to the folder root.</summary>
/// <param name="Files">The folder's files, keyed by relative path.</param>
internal sealed record FolderSnapshot(IReadOnlyDictionary<string, FileEntry> Files)
{
    /// <summary>
    /// The comparer relative paths are keyed by: <see cref="StringComparer.OrdinalIgnoreCase"/> on
    /// Windows and macOS, matching the file system, and <see cref="StringComparer.Ordinal"/> on Linux.
    /// </summary>
    internal static StringComparer PathComparer => OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>An empty snapshot, keyed by <see cref="PathComparer"/>.</summary>
    internal static FolderSnapshot Empty { get; } = new(new Dictionary<string, FileEntry>(PathComparer));
}

/// <summary>This Agent's last own write to a file: the Room whose Turn touched it, and the file as that left it.</summary>
/// <param name="RoomId">The id of the Room whose Turn last wrote the file.</param>
/// <param name="Entry">The file as that write left it.</param>
internal sealed record FileWriter(string RoomId, FileEntry Entry);

/// <summary>What this Agent was last shown in one Room.</summary>
/// <param name="Folders">The Agent's Watched Folders, keyed by entry, never by full path.</param>
internal sealed record RoomBaseline(IReadOnlyDictionary<string, FolderSnapshot> Folders);

/// <summary>What one Agent last saw, per Room. Saved as <c>{DataDir}/file-state/&lt;Name&gt;.json</c>.</summary>
/// <param name="Subscribed">Entries added by <c>watch_folder</c>, in call order.</param>
/// <param name="Rooms">This Agent's last-shown baseline, keyed by Room id.</param>
/// <param name="Writers">This Agent's own last writes, keyed by entry then by relative path.</param>
internal sealed record FileState(
    IReadOnlyList<string> Subscribed,
    IReadOnlyDictionary<string, RoomBaseline> Rooms,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, FileWriter>> Writers)
{
    /// <summary>An empty <see cref="FileState"/>, with every dictionary keyed per FC §6.4's key comparison rules.</summary>
    internal static FileState Empty { get; } = new(
        [],
        new Dictionary<string, RoomBaseline>(StringComparer.Ordinal),
        new Dictionary<string, IReadOnlyDictionary<string, FileWriter>>(StringComparer.OrdinalIgnoreCase));
}

/// <summary>One file's change between two snapshots, as reported to an Agent.</summary>
/// <param name="Kind">Whether the file was added, changed or deleted.</param>
/// <param name="FullPath">The file's full path.</param>
/// <param name="ByYouRoomName">
/// The current name of the Room whose Turn last wrote this file, when this Agent's own last
/// recorded write to it still matches the file exactly as it is now, and that Room is not the one
/// this Turn is running in (FC §6.15). <see langword="null"/> when no such match applies - the
/// file was last written by someone else, the match no longer holds (E-19), or that Room no
/// longer exists (E-20).
/// </param>
internal sealed record FileChange(FileChangeKind Kind, string FullPath, string? ByYouRoomName = null);

/// <summary>The File Changes block for one Turn: already-capped changes, plus what the cap left out.</summary>
/// <param name="Changes">The changes to report, already capped.</param>
/// <param name="NotListed">How many changes the cap left out.</param>
/// <param name="Unchecked">Full paths of folders over the file cap, so they were not scanned.</param>
/// <param name="MaxFilesPerFolder">
/// The cap in force, carried only so the pure <c>BuildPrompt</c> can render <c>turn.folderUnchecked</c>'s
/// <c>{{max}}</c> placeholder.
/// </param>
internal sealed record FileChangesReport(
    IReadOnlyList<FileChange> Changes,
    int NotListed,
    IReadOnlyList<string> Unchecked,
    int MaxFilesPerFolder = 0)
{
    /// <summary>An empty report: no changes, nothing left out, nothing unchecked.</summary>
    internal static FileChangesReport Empty { get; } = new([], 0, []);

    /// <summary><see langword="true"/> when there is nothing to report: no changes, nothing left out, nothing unchecked.</summary>
    internal bool IsEmpty => this.Changes.Count == 0 && this.NotListed == 0 && this.Unchecked.Count == 0;
}

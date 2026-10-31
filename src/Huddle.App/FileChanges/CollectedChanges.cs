namespace Agency.Huddle.App.FileChanges;

/// <summary>
/// The start scan of every Watched Folder for one Turn, carried from <see cref="FileChangeTracker.CollectAsync"/>
/// to <see cref="FileChangeTracker.CommitAsync"/> so a Turn scans each folder once, not twice, per FC §6.7.
/// </summary>
/// <param name="Report">The File Changes block for this Turn, already capped.</param>
/// <param name="Folders">
/// Every resolved Watched Folder, in order: own Work Dir, then declared, then subscribed. A plan
/// addition beyond FC §6.7's own listing: the commit needs each entry's full path.
/// </param>
/// <param name="Scans">Each folder's start scan, keyed by entry, compared <see cref="StringComparer.OrdinalIgnoreCase"/>.</param>
internal sealed record CollectedChanges(
    FileChangesReport Report,
    IReadOnlyList<WatchedFolder> Folders,
    IReadOnlyDictionary<string, ScanResult> Scans);

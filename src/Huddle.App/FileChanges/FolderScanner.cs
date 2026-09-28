namespace Agency.Huddle.App.FileChanges;

/// <summary>What a scan of one folder found, per FC §6.4.</summary>
internal enum ScanOutcome
{
    /// <summary>The folder exists and was walked within <see cref="FileChangesOptions.MaxFilesPerFolder"/>.</summary>
    Scanned,

    /// <summary>The folder does not exist. Not a failure: its files are reported as added once it appears.</summary>
    Missing,

    /// <summary>The folder holds more than <see cref="FileChangesOptions.MaxFilesPerFolder"/> files and was not fully walked.</summary>
    TooLarge,
}

/// <summary>The result of one <see cref="FolderScanner.Scan(string, FileChangesOptions, bool)"/> call.</summary>
/// <param name="Outcome">Whether the folder was scanned, is missing, or is too large.</param>
/// <param name="Snapshot"><see cref="FolderSnapshot.Empty"/> for <see cref="ScanOutcome.Missing"/> and <see cref="ScanOutcome.TooLarge"/>.</param>
internal sealed record ScanResult(ScanOutcome Outcome, FolderSnapshot Snapshot);

/// <summary>
/// Scans one folder into a <see cref="FolderSnapshot"/>, pruning ignored directories, skipping
/// reparse points, and stopping once the file count exceeds a cap, per FC §6.4. Nothing is read
/// or hashed: each file contributes only its length and last-write time.
/// </summary>
internal static class FolderScanner
{
    /// <summary>
    /// The <see cref="EnumerationOptions.AttributesToSkip"/> this scanner uses. The default value
    /// of that property is <see cref="FileAttributes.Hidden"/> | <see cref="FileAttributes.System"/>,
    /// which would silently drop hidden files; setting it to only <see cref="FileAttributes.ReparsePoint"/>
    /// is deliberate, so a hidden file is scanned and a reparse point is not followed.
    /// </summary>
    private static readonly EnumerationOptions EnumerationOptions = new()
    {
        RecurseSubdirectories = false,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
    };

    /// <summary>
    /// Scans <paramref name="fullPath"/> into a <see cref="FolderSnapshot"/>. Recurses by hand, one
    /// level at a time, so an ignored directory is pruned before its contents are ever enumerated.
    /// </summary>
    /// <param name="fullPath">The folder's full path.</param>
    /// <param name="options">Supplies <see cref="FileChangesOptions.EffectiveIgnore"/> and <see cref="FileChangesOptions.MaxFilesPerFolder"/>.</param>
    /// <param name="pruneUnderscore">
    /// When <see langword="true"/>, per Spec §6.13, any <c>_</c>-prefixed sub-folder (at any depth)
    /// is pruned before its contents are enumerated, alongside <see cref="FileChangesOptions.EffectiveIgnore"/>.
    /// </param>
    internal static ScanResult Scan(string fullPath, FileChangesOptions options, bool pruneUnderscore = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        ArgumentNullException.ThrowIfNull(options);

        if (!Directory.Exists(fullPath))
        {
            return new ScanResult(ScanOutcome.Missing, FolderSnapshot.Empty);
        }

        Dictionary<string, FileEntry> files = new(FolderSnapshot.PathComparer);

        if (!Walk(fullPath, fullPath, options, pruneUnderscore, files))
        {
            return new ScanResult(ScanOutcome.TooLarge, FolderSnapshot.Empty);
        }

        return new ScanResult(ScanOutcome.Scanned, new FolderSnapshot(files));
    }

    /// <summary>
    /// Walks <paramref name="directory"/> one level at a time, adding its files to
    /// <paramref name="files"/> and recursing into every non-ignored subdirectory.
    /// </summary>
    /// <returns><see langword="false"/> once <paramref name="files"/> exceeds the cap, to unwind the recursion.</returns>
    private static bool Walk(string root, string directory, FileChangesOptions options, bool pruneUnderscore, Dictionary<string, FileEntry> files)
    {
        foreach (string file in Directory.EnumerateFiles(directory, "*", EnumerationOptions))
        {
            if (files.Count >= options.MaxFilesPerFolder)
            {
                return false;
            }

            FileInfo info = new(file);
            string relativePath = Path.GetRelativePath(root, file);
            files[relativePath] = new FileEntry(info.Length, new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero));
        }

        foreach (string subdirectory in Directory.EnumerateDirectories(directory, "*", EnumerationOptions))
        {
            string name = Path.GetFileName(subdirectory);
            if (options.EffectiveIgnore.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (pruneUnderscore && name.StartsWith('_'))
            {
                continue;
            }

            if (!Walk(root, subdirectory, options, pruneUnderscore, files))
            {
                return false;
            }
        }

        return true;
    }
}

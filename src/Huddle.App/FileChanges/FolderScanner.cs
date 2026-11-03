using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.FileChanges;

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
    /// <param name="teamsRoot">
    /// The Teams root, when known. Per Chat uploads spec 6.8, a <c>Teams/&lt;Team&gt;/Uploads</c>
    /// folder (two levels under it, compared with <see cref="TeamNames.RawFolder"/>, ordinal
    /// ignoring case) is pruned before its contents are enumerated, so its files never count toward
    /// <see cref="FileChangesOptions.MaxFilesPerFolder"/>. <see langword="null"/> prunes nothing.
    /// </param>
    internal static ScanResult Scan(string fullPath, FileChangesOptions options, bool pruneUnderscore = false, string? teamsRoot = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        ArgumentNullException.ThrowIfNull(options);

        if (!Directory.Exists(fullPath))
        {
            return new ScanResult(ScanOutcome.Missing, FolderSnapshot.Empty);
        }

        Dictionary<string, FileEntry> files = new(FolderSnapshot.PathComparer);

        if (!Walk(fullPath, fullPath, options, pruneUnderscore, teamsRoot, files))
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
    private static bool Walk(string root, string directory, FileChangesOptions options, bool pruneUnderscore, string? teamsRoot, Dictionary<string, FileEntry> files)
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

            if (teamsRoot is not null && FolderScanner.IsTeamUploads(subdirectory, name, teamsRoot))
            {
                continue;
            }

            if (!Walk(root, subdirectory, options, pruneUnderscore, teamsRoot, files))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>True when <paramref name="subdirectory"/> is named Uploads and sits directly under a Team folder, itself directly under <paramref name="teamsRoot"/>.</summary>
    private static bool IsTeamUploads(string subdirectory, string name, string teamsRoot)
    {
        if (!string.Equals(name, TeamNames.RawFolder, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string? teamFolder = Path.GetDirectoryName(subdirectory);
        string? parentOfTeam = teamFolder is null ? null : Path.GetDirectoryName(teamFolder);
        return parentOfTeam is not null && FolderSnapshot.PathComparer.Equals(parentOfTeam, Path.GetFullPath(teamsRoot));
    }
}

namespace Agency.Huddle.App.FileChanges;

/// <summary>
/// Pure comparison of two <see cref="FolderSnapshot"/> instances into the added, deleted and
/// changed files between them, per FC §6.5.
/// </summary>
internal static class FileStateDiff
{
    /// <summary>
    /// Compares <paramref name="before"/> and <paramref name="after"/> and returns the files
    /// added, deleted or changed, sorted by relative path with <see cref="StringComparer.Ordinal"/>.
    /// </summary>
    /// <param name="before">The earlier snapshot.</param>
    /// <param name="after">The later snapshot.</param>
    /// <param name="folderFullPath">The folder's full path, combined with each relative path to build <see cref="FileChange.FullPath"/>.</param>
    /// <returns>The changes between the two snapshots, sorted by relative path, ordinal.</returns>
    internal static IReadOnlyList<FileChange> Compare(FolderSnapshot before, FolderSnapshot after, string folderFullPath)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentException.ThrowIfNullOrWhiteSpace(folderFullPath);

        List<string> relativePaths = [.. before.Files.Keys.Concat(after.Files.Keys).Distinct(FolderSnapshot.PathComparer)];
        relativePaths.Sort(StringComparer.Ordinal);

        List<FileChange> changes = [];
        foreach (string relativePath in relativePaths)
        {
            string fullPath = Path.Combine(folderFullPath, relativePath);

            if (before.Files.TryGetValue(relativePath, out FileEntry? beforeEntry) && after.Files.TryGetValue(relativePath, out FileEntry? afterEntry))
            {
                if (beforeEntry.Size != afterEntry.Size || beforeEntry.ModifiedUtc != afterEntry.ModifiedUtc)
                {
                    changes.Add(new FileChange(FileChangeKind.Changed, fullPath));
                }
            }
            else if (after.Files.ContainsKey(relativePath))
            {
                changes.Add(new FileChange(FileChangeKind.Added, fullPath));
            }
            else
            {
                changes.Add(new FileChange(FileChangeKind.Deleted, fullPath));
            }
        }

        return changes;
    }
}

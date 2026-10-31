namespace Agency.Huddle.App.FileChanges;

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

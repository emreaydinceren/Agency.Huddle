namespace Agency.Huddle.App.Library;

/// <summary>
/// The one answer to "is this path inside that folder?" and the one way to show a path relative to a folder.
/// Both sides are made full and the root loses its trailing separator, so a trailing separator, a mixed-separator
/// root or a drive root never changes the answer, and a sibling that merely shares a name prefix (<c>C:\a\bc</c>
/// against <c>C:\a\b</c>) is not inside.
/// </summary>
internal static class PathContainment
{
    /// <summary>The comparison for paths on this machine: ignoring case on Windows and macOS, exact elsewhere.</summary>
    internal static StringComparison Comparison =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>Whether <paramref name="path"/> lies inside <paramref name="root"/>, compared with <see cref="Comparison"/>.</summary>
    /// <param name="path">The path to test; made full first.</param>
    /// <param name="root">The folder; made full first, and a trailing separator is ignored.</param>
    /// <param name="includeRoot">Whether the root itself counts as inside.</param>
    /// <returns><see langword="true"/> when the path is below the root, or is the root and <paramref name="includeRoot"/> is set.</returns>
    internal static bool IsInside(string path, string root, bool includeRoot)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(root);

        string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        string top = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (string.Equals(full, top, Comparison))
        {
            return includeRoot;
        }

        string prefix = Path.EndsInDirectorySeparator(top) ? top : top + Path.DirectorySeparatorChar;
        return full.Length > prefix.Length && full.StartsWith(prefix, Comparison);
    }

    /// <summary>The path relative to the root with forward slashes, the form shown to a Human and stored in logs.</summary>
    /// <param name="root">The folder the result is relative to.</param>
    /// <param name="path">The path to express.</param>
    /// <returns>The relative path with <c>/</c> separators.</returns>
    internal static string RelativeSlashPath(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');
}

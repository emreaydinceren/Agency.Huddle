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

    /// <summary>
    /// Whether <paramref name="path"/> lies inside <paramref name="root"/>. The comparison is always the caller's
    /// choice, because the safe answer depends on what the check guards: a guard that <i>allows</i> a path passes
    /// <see cref="Comparison"/>, so a differently cased folder on a case-sensitive filesystem is not mistaken for the
    /// allowed one; a guard that <i>refuses</i> a folder may pass <see cref="StringComparison.OrdinalIgnoreCase"/>,
    /// where matching too much is the safe direction (an Uploads folder is refused whichever case it is typed in).
    /// </summary>
    /// <param name="path">The path to test; made full first.</param>
    /// <param name="root">The folder; made full first, and a trailing separator is ignored.</param>
    /// <param name="includeRoot">Whether the root itself counts as inside.</param>
    /// <param name="comparison">How the two paths are compared.</param>
    /// <returns><see langword="true"/> when the path is below the root, or is the root and <paramref name="includeRoot"/> is set.</returns>
    internal static bool IsInside(string path, string root, bool includeRoot, StringComparison comparison)
    {
        if (Below(path, root, comparison) is { } below)
        {
            return below.Length > 0 || includeRoot;
        }

        return false;
    }

    /// <summary>
    /// The part of <paramref name="path"/> below <paramref name="root"/>, sliced with the same comparison that decided
    /// containment. <see cref="Path.GetRelativePath(string, string)"/> is not a substitute here: it always uses the
    /// filesystem's own case rule, so on Linux it turns a case-insensitive match into a <c>..</c> path.
    /// </summary>
    /// <param name="path">The path to slice; made full first.</param>
    /// <param name="root">The folder; made full first, and a trailing separator is ignored.</param>
    /// <param name="comparison">How the two paths are compared.</param>
    /// <returns>The remainder with the platform separator and no leading separator, an empty string for the root itself, or <see langword="null"/> when the path is not inside the root.</returns>
    internal static string? Below(string path, string root, StringComparison comparison)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(root);

        string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        string top = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (string.Equals(full, top, comparison))
        {
            return string.Empty;
        }

        string prefix = Path.EndsInDirectorySeparator(top) ? top : top + Path.DirectorySeparatorChar;
        return full.Length > prefix.Length && full.StartsWith(prefix, comparison) ? full[prefix.Length..] : null;
    }

    /// <summary>The path relative to the root with forward slashes, the form shown to a Human and stored in logs.</summary>
    /// <param name="root">The folder the result is relative to.</param>
    /// <param name="path">The path to express.</param>
    /// <returns>The relative path with <c>/</c> separators.</returns>
    internal static string RelativeSlashPath(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');
}

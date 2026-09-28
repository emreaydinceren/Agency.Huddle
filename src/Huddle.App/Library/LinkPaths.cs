namespace Agency.Huddle.App.Library;

/// <summary>
/// Shared reparse-point resolution (Spec §6.1 step 4, corrections-B3 item 26): follows a link to its
/// final target, treating a reparse point with a <see langword="null"/> <c>LinkTarget</c> (a OneDrive
/// placeholder, a dedup entry) as an ordinary entry rather than a link. Used by
/// <see cref="LibraryPathResolver"/> to resolve containment along a walked path, and by
/// <see cref="WindowsRecycleBin"/> to check the real target's drive (corrections-B4 item 30).
/// </summary>
internal static class LinkPaths
{
    /// <summary>
    /// When <paramref name="path"/> exists and is a reparse point with a non-null <c>LinkTarget</c>,
    /// resolves its final target; a reparse point with a <see langword="null"/> <c>LinkTarget</c>
    /// is an ordinary entry.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <returns>The resolved final target, or <see langword="null"/> when <paramref name="path"/> is not a link.</returns>
    internal static string? ResolveIfLink(string path)
    {
        if (Directory.Exists(path))
        {
            DirectoryInfo info = new(path);
            return info.Attributes.HasFlag(FileAttributes.ReparsePoint) ? ResolveLinkTarget(info) : null;
        }

        if (File.Exists(path))
        {
            FileInfo info = new(path);
            return info.Attributes.HasFlag(FileAttributes.ReparsePoint) ? ResolveLinkTarget(info) : null;
        }

        return null;
    }

    /// <summary>Resolves <paramref name="entry"/>'s final link target, or <see langword="null"/> when its <c>LinkTarget</c> is itself null.</summary>
    /// <param name="entry">The reparse-point entry to resolve.</param>
    /// <returns>The final target's full path, or <see langword="null"/> when <paramref name="entry"/> is not itself a link.</returns>
    internal static string? ResolveLinkTarget(FileSystemInfo entry) =>
        entry.LinkTarget is not null && entry.ResolveLinkTarget(returnFinalTarget: true) is FileSystemInfo resolved
            ? resolved.FullName
            : null;
}

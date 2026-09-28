namespace Agency.Huddle.App.Library;

using FileChanges;

/// <summary>Restrictions on renaming, moving, or deleting an item in the Library.</summary>
/// <param name="CanRename">Whether the item can be renamed.</param>
/// <param name="CanMove">Whether the item can be moved.</param>
/// <param name="CanDelete">Whether the item can be deleted.</param>
/// <param name="Reason">If any action is forbidden, a user-facing reason (e.g., "Team folders are managed from Tasks").</param>
internal sealed record LibraryProtection(bool CanRename, bool CanMove, bool CanDelete, string? Reason)
{
    /// <summary>
    /// Returns protection rules for a path based on its role. Scope protection is UI-only
    /// (corrections-B4 item 2): when a path equals the scope, it is protected like a root.
    /// </summary>
    /// <param name="path">The Library path to check.</param>
    /// <param name="scope">Optional scope folder; if it equals the path, that path is protected like a root.</param>
    /// <returns>The protection rules for this path.</returns>
    internal static LibraryProtection For(LibraryPath path, LibraryPath? scope = null)
    {
        ArgumentNullException.ThrowIfNull(path);

        // Check if this path equals the scope (UI-only protection). Compare using FolderSnapshot.PathComparer.
        if (scope is not null && PathEquals(path, scope))
        {
            return new LibraryProtection(false, false, false, "A Library root can't be renamed, moved or deleted.");
        }

        return path.Role switch
        {
            LibraryNodeRole.Root => new LibraryProtection(false, false, false, "A Library root can't be renamed, moved or deleted."),
            LibraryNodeRole.TeamFolder or LibraryNodeRole.ProjectFolder => new LibraryProtection(false, false, false, "Team and Project folders can't be renamed, moved or deleted here."),
            LibraryNodeRole.TeammateFolder or LibraryNodeRole.TeammateDefinition or LibraryNodeRole.WorkDir => new LibraryProtection(false, false, false, "Teammate folders can't be renamed, moved or deleted here."),
            LibraryNodeRole.Folder or LibraryNodeRole.File => new LibraryProtection(true, true, true, null),
            _ => throw new InvalidOperationException($"Unknown role: {path.Role}"),
        };
    }

    /// <summary>
    /// Compares two paths for equality using the path comparer (case-insensitive on Windows,
    /// case-sensitive on Linux).
    /// </summary>
    private static bool PathEquals(LibraryPath left, LibraryPath right)
    {
        if (left.Root.Id != right.Root.Id)
        {
            return false;
        }

        return FolderSnapshot.PathComparer.Equals(left.RelativePath, right.RelativePath);
    }
}

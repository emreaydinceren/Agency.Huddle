using Agency.Huddle.App.FileChanges;

namespace Agency.Huddle.App.Library;

/// <summary>
/// The single hidden-folder rule shared by <see cref="LibraryFileService.ListAsync"/> and
/// <see cref="WikiLinkIndex"/>'s walk (corrections-B5 item 5), so the two can never drift.
/// </summary>
internal static class LibraryHiddenFolders
{
    /// <summary>Folder names hidden everywhere in the Library tree (Spec §6.4), beyond <see cref="FileChangesOptions.EffectiveIgnore"/>.</summary>
    private static readonly string[] AlwaysHidden = [".obsidian", ".trash", ".git"];

    /// <summary>Whether a folder name is hidden: always-hidden, in the effective ignore list, or (Teams root only) underscore-prefixed.</summary>
    /// <param name="name">The folder's name.</param>
    /// <param name="ignoredFolders">The configured <see cref="FileChangesOptions.EffectiveIgnore"/> list.</param>
    /// <param name="hideUnderscoreFolders">Whether underscore-prefixed folders are hidden (Spec §6.16, the Teams root only).</param>
    internal static bool IsHidden(string name, IReadOnlyList<string> ignoredFolders, bool hideUnderscoreFolders)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(ignoredFolders);

        if (Array.Exists(AlwaysHidden, hidden => string.Equals(hidden, name, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (hideUnderscoreFolders && name.StartsWith('_'))
        {
            return true;
        }

        foreach (string ignored in ignoredFolders)
        {
            if (string.Equals(ignored, name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.App.Components.Library;

/// <summary>
/// Compares <see cref="LibraryPath"/> values by identity only: <see cref="LibraryRoot.Id"/> and
/// <see cref="LibraryPath.RelativePath"/> (Task 12.1, corrections-B6 item 22). <see cref="LibraryTree"/>
/// binds <c>MudTreeView&lt;LibraryPath&gt;</c> with this comparer rather than <see cref="LibraryEntry"/>
/// or <see cref="LibraryPath"/>'s own record equality (which includes <see cref="LibraryPath.Root"/>'s
/// <see cref="LibraryRoot.DisplayName"/>, so a pinned root rename would otherwise drop selection and
/// expansion). <see cref="LibraryPath.RelativePath"/> is compared with <see cref="FolderSnapshot.PathComparer"/>
/// (a real file-system path), and <see cref="LibraryRoot.Id"/> is compared with <see cref="StringComparer.Ordinal"/>
/// (an identifier).
/// </summary>
internal sealed class LibraryPathComparer : IEqualityComparer<LibraryPath>
{
    /// <summary>The shared instance; this comparer holds no state.</summary>
    internal static readonly LibraryPathComparer Instance = new();

    private LibraryPathComparer()
    {
    }

    /// <inheritdoc/>
    public bool Equals(LibraryPath? x, LibraryPath? y)
    {
        if (x is null || y is null)
        {
            return ReferenceEquals(x, y);
        }

        return string.Equals(x.Root.Id, y.Root.Id, StringComparison.Ordinal)
            && FolderSnapshot.PathComparer.Equals(x.RelativePath, y.RelativePath);
    }

    /// <inheritdoc/>
    public int GetHashCode(LibraryPath obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        return HashCode.Combine(
            StringComparer.Ordinal.GetHashCode(obj.Root.Id),
            FolderSnapshot.PathComparer.GetHashCode(obj.RelativePath));
    }
}

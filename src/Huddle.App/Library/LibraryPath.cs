namespace Agency.Huddle.App.Library;

/// <summary>A path proven to be inside a Library Root. By convention only <c>LibraryPathResolver</c> creates one.</summary>
/// <param name="Root">The Library Root.</param>
/// <param name="RelativePath">The relative path from the root, forward-slash, no leading slash, empty string for root.</param>
/// <param name="FullPath">The absolute file system path.</param>
/// <param name="Role">The node role in the Library tree structure.</param>
public sealed record LibraryPath(LibraryRoot Root, string RelativePath, string FullPath, LibraryNodeRole Role)
{
    /// <summary>
    /// Whether this node is a file rather than a folder. Two roles are files: an ordinary
    /// <see cref="LibraryNodeRole.File"/>, and a Teammate's own <c>&lt;Name&gt;.md</c>, which the
    /// resolver marks <see cref="LibraryNodeRole.TeammateDefinition"/>. A check on
    /// <see cref="LibraryNodeRole.File"/> alone misses the second, so ask this instead.
    /// </summary>
    public bool IsFile => this.Role is LibraryNodeRole.File or LibraryNodeRole.TeammateDefinition;
}

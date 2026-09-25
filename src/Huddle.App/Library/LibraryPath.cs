namespace Agency.Huddle.App.Library;

/// <summary>A path proven to be inside a Library Root. By convention only <c>LibraryPathResolver</c> creates one.</summary>
/// <param name="Root">The Library Root.</param>
/// <param name="RelativePath">The relative path from the root, forward-slash, no leading slash, empty string for root.</param>
/// <param name="FullPath">The absolute file system path.</param>
/// <param name="Role">The node role in the Library tree structure.</param>
public sealed record LibraryPath(LibraryRoot Root, string RelativePath, string FullPath, LibraryNodeRole Role);

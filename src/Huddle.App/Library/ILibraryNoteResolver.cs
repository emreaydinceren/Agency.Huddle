namespace Agency.Huddle.App.Library;

/// <summary>Adds relative-Markdown-link resolution to <see cref="ILibraryReferenceResolver"/> (corrections-B5
/// item 22), for links written inside a Library note as opposed to a wikilink or an absolute chat path.</summary>
internal interface ILibraryNoteResolver : ILibraryReferenceResolver
{
    /// <summary>The Library link for a relative Markdown link (<c>./a.md</c>, <c>../b.md</c>, <c>sub/c.md</c>)
    /// written in the note at <paramref name="from"/>, or <see langword="null"/> when <paramref name="url"/>
    /// is not a relative file link (an absolute URL, a <c>mailto:</c> link, a bare heading anchor) or escapes
    /// the Library root.</summary>
    /// <param name="from">The note the link appears in.</param>
    /// <param name="url">The link's raw, possibly URL-encoded target.</param>
    LibraryReference? ResolveRelative(LibraryPath from, string url);
}

namespace Agency.Huddle.App.Library;

/// <summary>Resolves wikilinks and paths to their target Library references.</summary>
public interface ILibraryReferenceResolver
{
    /// <summary>The Library link for an absolute path inside a Library Root, or null.</summary>
    LibraryReference? ResolvePath(string absolutePath);

    /// <summary>The Library link for a wikilink written in the note at <paramref name="from"/>, or null.</summary>
    LibraryReference? ResolveWikiLink(LibraryPath from, WikiLink link);
}

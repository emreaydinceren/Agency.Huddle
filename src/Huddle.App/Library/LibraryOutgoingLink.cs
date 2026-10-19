namespace Agency.Huddle.App.Library;

/// <summary>An outgoing wikilink of a note, for the Linked documents panel.</summary>
/// <param name="Link">The parsed link as written in the note.</param>
/// <param name="ResolvedPath">The linked note's root-relative path, or <see langword="null"/> when it does not exist.</param>
internal sealed record LibraryOutgoingLink(WikiLink Link, string? ResolvedPath);

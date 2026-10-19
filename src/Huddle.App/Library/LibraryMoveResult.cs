namespace Agency.Huddle.App.Library;

/// <summary>The result of moving or renaming an item and updating wikilinks in notes.</summary>
/// <param name="NewPath">The new path of the moved item.</param>
/// <param name="RewrittenNotes">The relative paths of notes whose links were updated.</param>
/// <param name="FailedNotes">The notes that couldn't be updated, with reasons.</param>
/// <param name="LinksNotUpdated">Whether the destination root's wikilink index was unavailable
/// (corrections-B5 D8 item 8), so no rewrite could be attempted at all.</param>
internal sealed record LibraryMoveResult(LibraryPath NewPath, IReadOnlyList<string> RewrittenNotes, IReadOnlyList<LibraryNoteFailure> FailedNotes, bool LinksNotUpdated);

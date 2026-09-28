namespace Agency.Huddle.App.Library;

/// <summary>The result of moving or renaming an item and updating wikilinks in notes.</summary>
/// <param name="NewPath">The new path of the moved item.</param>
/// <param name="RewrittenNotes">The relative paths of notes whose links were updated.</param>
/// <param name="FailedNotes">The notes that couldn't be updated, with reasons.</param>
/// <param name="LinksNotUpdated">Whether the destination root's wikilink index was unavailable
/// (corrections-B5 D8 item 8), so no rewrite could be attempted at all.</param>
internal sealed record LibraryMoveResult(LibraryPath NewPath, IReadOnlyList<string> RewrittenNotes, IReadOnlyList<LibraryNoteFailure> FailedNotes, bool LinksNotUpdated);

/// <summary>A note that failed to be updated during a move or rename.</summary>
/// <param name="RelativePath">The relative path of the note in the Library root.</param>
/// <param name="Error">The reason the update failed.</param>
internal sealed record LibraryNoteFailure(string RelativePath, string Error);

/// <summary>A preview of the wikilink rewrite a rename or move would perform (corrections-B5 D8 item 8),
/// computed without touching disk.</summary>
/// <param name="Links">The number of wikilinks that resolve to <c>item</c> or, for a folder, to any file
/// inside it.</param>
/// <param name="Notes">The number of distinct notes those links live in.</param>
/// <param name="IndexAvailable">Whether the item's Library Root's wikilink index is available
/// (<see langword="false"/> when the root has too many files to index).</param>
internal sealed record LibraryLinkPreview(int Links, int Notes, bool IndexAvailable);

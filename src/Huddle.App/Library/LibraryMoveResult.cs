namespace Agency.Huddle.App.Library;

/// <summary>The result of moving or renaming an item and updating wikilinks in notes.</summary>
/// <param name="NewPath">The new path of the moved item.</param>
/// <param name="RewrittenNotes">The relative paths of notes whose links were updated.</param>
/// <param name="FailedNotes">The notes that couldn't be updated, with reasons.</param>
internal sealed record LibraryMoveResult(LibraryPath NewPath, IReadOnlyList<string> RewrittenNotes, IReadOnlyList<LibraryNoteFailure> FailedNotes);

/// <summary>A note that failed to be updated during a move or rename.</summary>
/// <param name="RelativePath">The relative path of the note in the Library root.</param>
/// <param name="Error">The reason the update failed.</param>
internal sealed record LibraryNoteFailure(string RelativePath, string Error);

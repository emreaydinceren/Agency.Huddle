namespace Agency.Huddle.App.Library;

/// <summary>A note that failed to be updated during a move or rename.</summary>
/// <param name="RelativePath">The relative path of the note in the Library root.</param>
/// <param name="Error">The reason the update failed.</param>
internal sealed record LibraryNoteFailure(string RelativePath, string Error);

namespace Agency.Huddle.App.Library;

/// <summary>
/// A completed Library file operation (Task 12.5, corrections-B6 item 21): raised by
/// <see cref="Agency.Huddle.App.Components.Library.LibraryFileOps"/> after a rename, move, delete or
/// create so the host (the explorer or an open document) can re-point or refresh itself, and stale
/// saves against a moved or deleted file are avoided.
/// </summary>
/// <param name="OldPath">The item's previous path, or <see langword="null"/> for a create.</param>
/// <param name="NewPath">The item's new path, or <see langword="null"/> for a delete.</param>
/// <param name="RewrittenNotes">The relative paths of notes whose wikilinks were rewritten.</param>
public sealed record LibraryChange(LibraryPath? OldPath, LibraryPath? NewPath, IReadOnlyList<string> RewrittenNotes);

namespace Agency.Huddle.App.Library;

/// <summary>A Library file or folder that was renamed or moved.</summary>
/// <param name="From">The item before the move.</param>
/// <param name="To">The item after the move.</param>
internal sealed record LibraryItemMoved(LibraryPath From, LibraryPath To);

using Agency.Huddle.App.Library;

namespace Agency.Huddle.App.Components.Library;

/// <summary>
/// An action raised by a <see cref="LibraryTree"/> row's menu (Task 12.1; corrections-B6 item 20).
/// </summary>
/// <param name="Kind">Which action was chosen.</param>
/// <param name="Path">The node the menu was opened on.</param>
/// <param name="Name">
/// For <see cref="LibraryTreeActionKind.NewNote"/> raised from a missing wikilink's create offer,
/// the note name to create; <see langword="null"/> otherwise.
/// </param>
public sealed record LibraryTreeAction(LibraryTreeActionKind Kind, LibraryPath Path, string? Name);

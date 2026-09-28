namespace Agency.Huddle.App.Library;

/// <summary>A row in the Backlinks panel (Spec §6.5): the linking note's path, line number and text.</summary>
/// <param name="Note">The note containing the backlink, resolved to its root-relative path.</param>
/// <param name="Line">The 1-based line number that contains the link.</param>
/// <param name="LineText">The full text of that line.</param>
public record LibraryBacklinkRow(LibraryPath Note, int Line, string LineText);

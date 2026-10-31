namespace Agency.Huddle.App.Library;

/// <summary>A backlink into <see cref="LibraryBacklink.NotePath"/>, one row per line that holds a
/// resolved link to the queried note (Spec §6.5 <i>Backlinks</i>).</summary>
/// <param name="NotePath">The linking note's root-relative path.</param>
/// <param name="Line">The 1-based line number the link (or links) appear on.</param>
/// <param name="LineText">The full text of that line.</param>
internal sealed record LibraryBacklink(string NotePath, int Line, string LineText);

namespace Agency.Huddle.App.Library;

/// <summary>One top-level front matter entry of a note (front matter properties spec D-1).</summary>
/// <param name="Key">The key exactly as written.</param>
/// <param name="Kind">The type the entry is shown as.</param>
/// <param name="Shape">How the value was written.</param>
/// <param name="Value">The scalar's text without quotes, as written; empty for any other shape.</param>
/// <param name="Items">The list's strings without quotes; empty for any other shape.</param>
/// <param name="Source">The value's own lines as written, indentation kept, shown for an Unsupported entry.</param>
/// <param name="Expected">For a Mismatch, the type the key was assigned.</param>
public sealed record NoteProperty(
    string Key,
    NotePropertyKind Kind,
    NotePropertyShape Shape,
    string Value,
    IReadOnlyList<string> Items,
    string Source,
    NotePropertyKind? Expected = null);

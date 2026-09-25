namespace Agency.Huddle.App.Library;

/// <summary>A document's content, metadata and editability state.</summary>
/// <param name="Path">The path inside a Library Root.</param>
/// <param name="Kind">The file kind (Markdown, Text, Image, etc.).</param>
/// <param name="Text">The file text, or null if binary or too large to load.</param>
/// <param name="Format">The text encoding and line ending, or null if binary.</param>
/// <param name="Editable">Whether the document can be edited in place.</param>
/// <param name="ViewOnlyReason">If not editable, why (e.g., "Too large to edit", "Unsupported encoding").</param>
/// <param name="Length">The file size in bytes.</param>
/// <param name="LastWriteUtc">The last write time in UTC.</param>
public sealed record LibraryDocumentContent(
    LibraryPath Path,
    LibraryFileKind Kind,
    string? Text,
    TextFileFormat? Format,
    bool Editable,
    string? ViewOnlyReason,
    long Length,
    DateTimeOffset LastWriteUtc);

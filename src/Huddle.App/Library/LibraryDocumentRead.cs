namespace Agency.Huddle.App.Library;

/// <summary>A document read in full.</summary>
/// <param name="Bytes">The file's bytes.</param>
internal sealed record LibraryDocumentRead(byte[] Bytes) : LibraryDocumentResult;

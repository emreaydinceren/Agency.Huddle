namespace Agency.Huddle.App.Library;

/// <summary>A document the reader would not return.</summary>
/// <param name="Reason">Why.</param>
internal sealed record LibraryDocumentRefused(LibraryDocumentRefusal Reason) : LibraryDocumentResult;

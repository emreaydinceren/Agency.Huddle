namespace Agency.Huddle.App.Library;

/// <summary>Why a document's bytes were not returned.</summary>
internal enum LibraryDocumentRefusal
{
    /// <summary>The file is larger than the bytes the Turn still allows.</summary>
    TooLarge,

    /// <summary>The file could not be read, or is not a PDF any more.</summary>
    Unreadable,
}

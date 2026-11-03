namespace Agency.Huddle.App.Library;

/// <summary>
/// What reading a document's bytes from the Library produced: the bytes, or the reason they are not
/// going to be sent. A refusal is data rather than an exception because the caller carries on with
/// the path line the file already has.
/// </summary>
internal abstract record LibraryDocumentResult;

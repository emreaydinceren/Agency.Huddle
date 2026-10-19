namespace Agency.Huddle.App.Library;

/// <summary>
/// What reading an image from the Library produced (design §6.3): its bytes, or the reason it is
/// not going to be sent. A refusal is data rather than an exception because the caller wants to carry on
/// with the path line the file already has.
/// </summary>
internal abstract record LibraryImageResult;

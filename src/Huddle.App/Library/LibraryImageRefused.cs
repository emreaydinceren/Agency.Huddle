namespace Agency.Huddle.App.Library;

/// <summary>A file that will not be sent as an image, and why.</summary>
/// <param name="Reason">The reason.</param>
internal sealed record LibraryImageRefused(LibraryImageRefusal Reason) : LibraryImageResult;

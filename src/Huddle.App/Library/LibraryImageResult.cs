namespace Agency.Huddle.App.Library;

/// <summary>
/// What reading an image from the Library produced (design §6.3): its bytes, or the reason it is
/// not going to be sent. A refusal is data rather than an exception because the caller wants to carry on
/// with the path line the file already has.
/// </summary>
internal abstract record LibraryImageResult;

/// <summary>An image that passed every check.</summary>
/// <param name="MimeType">The type its magic bytes name: <c>image/png</c>, <c>image/jpeg</c>, <c>image/gif</c> or <c>image/webp</c>.</param>
/// <param name="Bytes">The whole file.</param>
/// <param name="Width">The width in pixels, from the header.</param>
/// <param name="Height">The height in pixels, from the header.</param>
internal sealed record LibraryImageRead(string MimeType, byte[] Bytes, int Width, int Height) : LibraryImageResult;

/// <summary>A file that will not be sent as an image, and why.</summary>
/// <param name="Reason">The reason.</param>
internal sealed record LibraryImageRefused(LibraryImageRefusal Reason) : LibraryImageResult;

/// <summary>Why <see cref="LibraryFileService.ReadImageAsync"/> refused a file.</summary>
public enum LibraryImageRefusal
{
    /// <summary>The magic bytes are not a supported raster image, or the header could not be parsed. SVG lands here.</summary>
    NotAnImage,

    /// <summary>The file is over the byte cap.</summary>
    TooLarge,

    /// <summary>The longest side is over the edge cap.</summary>
    TooManyPixels,

    /// <summary>The path no longer resolves inside a Library Root, or the file could not be opened or read.</summary>
    Unreadable,
}

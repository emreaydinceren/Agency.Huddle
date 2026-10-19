namespace Agency.Huddle.App.Library;

/// <summary>An image that passed every check.</summary>
/// <param name="MimeType">The type its magic bytes name: <c>image/png</c>, <c>image/jpeg</c>, <c>image/gif</c> or <c>image/webp</c>.</param>
/// <param name="Bytes">The whole file.</param>
/// <param name="Width">The width in pixels, from the header.</param>
/// <param name="Height">The height in pixels, from the header.</param>
internal sealed record LibraryImageRead(string MimeType, byte[] Bytes, int Width, int Height) : LibraryImageResult;

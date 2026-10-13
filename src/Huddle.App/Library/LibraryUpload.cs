namespace Agency.Huddle.App.Library;

/// <summary>A file saved by <see cref="LibraryFileService.CreateFromStreamAsync"/>.</summary>
/// <param name="File">The saved file, resolved after the move.</param>
/// <param name="Kind">What the first bytes say the file is.</param>
/// <param name="Size">The number of bytes written.</param>
internal sealed record LibraryUpload(LibraryPath File, UploadKind Kind, long Size);

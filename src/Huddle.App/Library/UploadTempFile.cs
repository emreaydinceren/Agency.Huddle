namespace Agency.Huddle.App.Library;

/// <summary>
/// Removes the hidden <c>.{guid}.upload</c> file an unfinished upload leaves behind. It lives apart from
/// <see cref="LibraryFileService"/> because that class never deletes anything permanently (a test pins it);
/// a temp file was never a document the Human could see, so it is not a Recycle Bin matter.
/// </summary>
internal static class UploadTempFile
{
    /// <summary>Deletes the temp file if it is still there; a failure to delete is not worth failing the upload over.</summary>
    /// <param name="tempPath">The temp file's full path.</param>
    internal static void DeleteQuietly(string tempPath)
    {
        try
        {
            File.Delete(tempPath);
        }
        catch (IOException)
        {
            // The file is hidden from the listing and named by a Guid; a leftover is harmless.
        }
        catch (UnauthorizedAccessException)
        {
            // Same as above: nothing refers to the temp file, so leaving it is safe.
        }
    }
}

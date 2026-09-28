namespace Agency.Huddle.App.Library;

/// <summary>
/// Atomically writes bytes to a file (Spec §6.4 <c>WriteTextAsync</c>): a temp file in the same folder, then
/// <see cref="File.Move(string, string, bool)"/> with overwrite. The temp file is deleted in a
/// <c>finally</c> block, so a failed move never leaves it behind (corrections-B4 item 15).
/// </summary>
internal static class AtomicFile
{
    /// <summary>
    /// Writes <paramref name="bytes"/> to <paramref name="targetPath"/> via <c>{name}.{Guid:N}.tmp</c> in the
    /// same folder, then moves it over <paramref name="targetPath"/>. Catches only <see cref="IOException"/>
    /// and <see cref="UnauthorizedAccessException"/> (corrections-B4 item 15: a target held open without
    /// <see cref="FileShare.Delete"/>, or read-only) - the caller maps a refusal to the settled save-failure
    /// text; every other exception propagates.
    /// </summary>
    /// <param name="targetPath">The final file path.</param>
    /// <param name="bytes">The bytes to write.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns><see langword="true"/> when the move succeeded; <see langword="false"/> when it was refused.</returns>
    public static async Task<bool> TryWriteAsync(string targetPath, byte[] bytes, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        ArgumentNullException.ThrowIfNull(bytes);

        string folder = Path.GetDirectoryName(targetPath)
            ?? throw new InvalidOperationException($"'{targetPath}' has no parent folder.");
        string tempPath = Path.Combine(folder, $"{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await File.WriteAllBytesAsync(tempPath, bytes, ct);
            File.Move(tempPath, targetPath, overwrite: true);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}

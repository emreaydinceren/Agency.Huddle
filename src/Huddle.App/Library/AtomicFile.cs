using System.Text;

namespace Agency.Huddle.App.Library;

/// <summary>
/// Atomically writes a file (Spec §6.4 <c>WriteTextAsync</c>): a temp file in the same folder, then
/// <see cref="File.Move(string, string, bool)"/> with overwrite. The temp name is unique
/// (<c>{name}.{Guid:N}.tmp</c>), so two writers never share a temp file, and the temp file is deleted in a
/// <c>finally</c> block, so a failed move never leaves it behind (corrections-B4 item 15). Nothing here flushes
/// to disk; a caller that needs durability across power loss must ask for it separately.
/// </summary>
internal static class AtomicFile
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

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

        string tempPath = TempPathFor(targetPath);

        try
        {
            await File.WriteAllBytesAsync(tempPath, bytes, ct).ConfigureAwait(false);
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
            DeleteLeftover(tempPath);
        }
    }

    /// <summary>
    /// Writes <paramref name="text"/> to <paramref name="targetPath"/> as UTF-8 without a byte-order mark (the
    /// encoding <see cref="File.WriteAllText(string, string)"/> uses), through a unique temp file in the same
    /// folder and an overwriting move. Unlike <see cref="TryWriteAsync"/> this throws on an IO failure - the
    /// <see cref="IOException"/> or <see cref="UnauthorizedAccessException"/> reaches the caller unchanged - and
    /// the temp file is deleted either way. The target's folder must already exist.
    /// </summary>
    /// <param name="targetPath">The final file path.</param>
    /// <param name="text">The text to write.</param>
    public static void WriteText(string targetPath, string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        WriteBytes(targetPath, Utf8NoBom.GetBytes(text));
    }

    /// <summary>
    /// Writes <paramref name="bytes"/> to <paramref name="targetPath"/> through a unique temp file in the same
    /// folder and an overwriting move, throwing on an IO failure and always deleting the temp file.
    /// The target's folder must already exist.
    /// </summary>
    /// <param name="targetPath">The final file path.</param>
    /// <param name="bytes">The bytes to write.</param>
    public static void WriteBytes(string targetPath, byte[] bytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        ArgumentNullException.ThrowIfNull(bytes);

        string tempPath = TempPathFor(targetPath);

        try
        {
            File.WriteAllBytes(tempPath, bytes);
            File.Move(tempPath, targetPath, overwrite: true);
        }
        finally
        {
            DeleteLeftover(tempPath);
        }
    }

    /// <summary>The unique temp path <c>{folder}/{name}.{Guid:N}.tmp</c> next to <paramref name="targetPath"/>.</summary>
    private static string TempPathFor(string targetPath)
    {
        string folder = Path.GetDirectoryName(targetPath)
            ?? throw new InvalidOperationException($"'{targetPath}' has no parent folder.");
        return Path.Combine(folder, $"{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");
    }

    /// <summary>Deletes the temp file when a failed write or move left it behind; a no-op after a successful move.</summary>
    private static void DeleteLeftover(string tempPath)
    {
        if (File.Exists(tempPath))
        {
            File.Delete(tempPath);
        }
    }
}

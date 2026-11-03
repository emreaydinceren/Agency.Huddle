namespace Agency.Huddle.App.SecondBrain;

/// <summary>
/// <see cref="IArchiveFileSystem"/> over the real disk. A rename is retried with a short backoff, the way
/// <c>AtomicFile</c> retries its own, because the Library watcher or an editor can hold a file for a moment.
/// The cross-volume copy fallback (corrections-12 item 7) is Task 113's and lives here.
/// </summary>
internal sealed class PhysicalArchiveFileSystem : IArchiveFileSystem
{
    private const int MoveAttempts = 10;

    private static readonly TimeSpan MoveRetryDelay = TimeSpan.FromMilliseconds(30);

    /// <inheritdoc />
    public IReadOnlyList<string> ListEntries(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);

        return Directory.Exists(folder)
            ? [.. Directory.EnumerateFileSystemEntries(folder).Select(static e => Path.GetFileName(e))]
            : [];
    }

    /// <inheritdoc />
    public bool FileExists(string path) => File.Exists(path);

    /// <inheritdoc />
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    /// <inheritdoc />
    public void DeleteFile(string path) => File.Delete(path);

    /// <inheritdoc />
    public void DeleteEmptyFolder(string path)
    {
        if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
        {
            Directory.Delete(path);
        }
    }

    /// <inheritdoc />
    public ArchiveMoveResult Move(string source, string destination)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        for (int attempt = 1; ; attempt++)
        {
            try
            {
                if (Directory.Exists(source))
                {
                    Directory.Move(source, destination);
                }
                else
                {
                    File.Move(source, destination);
                }

                return ArchiveMoveResult.Moved;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A reader holds a file open (Windows reports it as an IO error or access denied): wait and try again.
                if (attempt >= MoveAttempts)
                {
                    return ArchiveMoveResult.InUse;
                }

                Thread.Sleep(MoveRetryDelay);
            }
        }
    }
}

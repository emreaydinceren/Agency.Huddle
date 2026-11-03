namespace Agency.Huddle.App.SecondBrain;

/// <summary>
/// <see cref="IArchiveFileSystem"/> over the real disk. A rename is one attempt: it answers
/// <see cref="ArchiveMoveResult.InUse"/> or <see cref="ArchiveMoveResult.CrossVolume"/> (corrections-12 item 7) and
/// <see cref="ArchiveMove"/> retries or copies.
/// </summary>
internal sealed class PhysicalArchiveFileSystem : IArchiveFileSystem
{
    private const int WindowsNotSameDevice = unchecked((int)0x80070011);
    private const int LinuxCrossDevice = 18;
    private const int SharingViolation = unchecked((int)0x80070020);
    private const int LockViolation = unchecked((int)0x80070021);

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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && Classify(ex, source, destination) is not null)
        {
            return Classify(ex, source, destination) ?? ArchiveMoveResult.InUse;
        }
    }

    /// <summary>Says what a failed rename means: another volume, a file held open, or neither.</summary>
    /// <param name="ex">The exception the rename threw.</param>
    /// <param name="source">The path that was being renamed.</param>
    /// <param name="destination">The path it was being renamed to.</param>
    /// <returns><see cref="ArchiveMoveResult.CrossVolume"/> when the paths have different roots (Windows drive letters or UNC shares, which
    /// <see cref="Directory.Move(string, string)"/> refuses before touching the disk with HResult 0x80131620) or the file system reported ERROR_NOT_SAME_DEVICE
    /// or EXDEV; <see cref="ArchiveMoveResult.InUse"/> for a sharing or lock violation or access denied; <see langword="null"/> for anything else,
    /// which the caller reports as a failure rather than retrying.</returns>
    internal static ArchiveMoveResult? Classify(Exception ex, string source, string destination)
    {
        ArgumentNullException.ThrowIfNull(ex);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        if (ex is UnauthorizedAccessException)
        {
            return ArchiveMoveResult.InUse;
        }

        if (ex is not IOException)
        {
            return null;
        }

        if (ex.HResult is SharingViolation or LockViolation)
        {
            return ArchiveMoveResult.InUse;
        }

        // Comparing roots is necessary but not sufficient: a mount point or a junction hides a volume change from the path.
        bool otherRoot = !string.Equals(Path.GetPathRoot(Path.GetFullPath(source)), Path.GetPathRoot(Path.GetFullPath(destination)), StringComparison.OrdinalIgnoreCase);
        return otherRoot || ex.HResult == WindowsNotSameDevice || ex.HResult == LinuxCrossDevice ? ArchiveMoveResult.CrossVolume : null;
    }

    /// <inheritdoc />
    public string? ReadText(string path) => File.Exists(path) ? File.ReadAllText(path) : null;

    /// <inheritdoc />
    public void WriteTextAtomic(string path, string text)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(text);

        string temporary = path + ".tmp";
        File.WriteAllText(temporary, text);
        File.Move(temporary, path, overwrite: true);
    }

    /// <inheritdoc />
    public void CopyTree(string source, string destination)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        _ = Directory.CreateDirectory(destination);
        foreach (string directory in Directory.EnumerateDirectories(source))
        {
            this.CopyTree(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }

        foreach (string file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: false);
        }
    }

    /// <inheritdoc />
    public (int Files, long Bytes) Measure(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);

        if (!Directory.Exists(folder))
        {
            return (0, 0);
        }

        int count = 0;
        long bytes = 0;
        foreach (string file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
        {
            count++;
            bytes += new FileInfo(file).Length;
        }

        return (count, bytes);
    }

    /// <inheritdoc />
    public void DeleteTree(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}

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
        catch (IOException ex) when (ex.HResult == WindowsNotSameDevice || ex.HResult == LinuxCrossDevice)
        {
            // The destination is on another volume: Windows ERROR_NOT_SAME_DEVICE (as an HRESULT) or Linux EXDEV.
            // The path roots are deliberately not compared: a mount point or a junction hides the volume from the path.
            return ArchiveMoveResult.CrossVolume;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A reader holds a file open (Windows reports it as an IO error or access denied); the caller retries.
            return ArchiveMoveResult.InUse;
        }
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

namespace Agency.Huddle.App.SecondBrain;

/// <summary>
/// The file operations <see cref="ArchiveService"/> needs, behind an interface so a test can simulate an
/// open file and Task 113 can add staging, verification and the cross-volume copy without touching the service.
/// </summary>
internal interface IArchiveFileSystem
{
    /// <summary>Lists the names (not paths) of the entries directly inside a folder, in any order.</summary>
    /// <param name="folder">The folder; a missing folder lists nothing.</param>
    /// <returns>The entry names as they are on disk.</returns>
    IReadOnlyList<string> ListEntries(string folder);

    /// <summary>Whether a file exists at the path.</summary>
    /// <param name="path">The file path.</param>
    bool FileExists(string path);

    /// <summary>Creates a folder and any missing parents; an existing folder is left alone.</summary>
    /// <param name="path">The folder path.</param>
    void CreateDirectory(string path);

    /// <summary>Deletes a file; a missing file is not an error.</summary>
    /// <param name="path">The file path.</param>
    void DeleteFile(string path);

    /// <summary>Deletes a folder that holds nothing; a missing or non-empty folder is left alone.</summary>
    /// <param name="path">The folder path.</param>
    void DeleteEmptyFolder(string path);

    /// <summary>Renames a file or folder once; <see cref="ArchiveMove"/> does the retrying.</summary>
    /// <param name="source">The existing path.</param>
    /// <param name="destination">The new path; it must not exist.</param>
    /// <returns><see cref="ArchiveMoveResult.Moved"/>, <see cref="ArchiveMoveResult.InUse"/> or <see cref="ArchiveMoveResult.CrossVolume"/>; nothing has moved unless <c>Moved</c>.</returns>
    ArchiveMoveResult Move(string source, string destination);

    /// <summary>Copies a folder and everything under it into a new folder.</summary>
    /// <param name="source">The folder to copy.</param>
    /// <param name="destination">The new folder.</param>
    void CopyTree(string source, string destination);

    /// <summary>Counts the files under a folder and their total size.</summary>
    /// <param name="folder">The folder to measure; a missing folder holds nothing.</param>
    /// <returns>The file count and total bytes.</returns>
    (int Files, long Bytes) Measure(string folder);

    /// <summary>Deletes a folder and everything under it; a missing folder is not an error.</summary>
    /// <param name="path">The folder to delete.</param>
    void DeleteTree(string path);
}

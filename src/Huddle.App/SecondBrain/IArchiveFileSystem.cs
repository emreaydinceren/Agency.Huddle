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

    /// <summary>Renames a file or folder, retrying briefly while another program holds a file open.</summary>
    /// <param name="source">The existing path.</param>
    /// <param name="destination">The new path; it must not exist.</param>
    /// <returns><see cref="ArchiveMoveResult.Moved"/>, or <see cref="ArchiveMoveResult.InUse"/> when the retries ran out; nothing has moved then.</returns>
    ArchiveMoveResult Move(string source, string destination);
}

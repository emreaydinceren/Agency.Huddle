namespace Agency.Huddle.App.Acp;

/// <summary>Deletes browser profile folders, which hold an Agent's sign-ins and are never followed out of.</summary>
internal static class BrowserProfileFolders
{
    /// <summary>
    /// Deletes a folder and everything in it. A junction or symlink inside, or the folder itself being one, is unlinked and never followed, so a
    /// link an Agent made to a folder outside cannot make this delete that folder's contents. Read-only files (a Chromium profile has them) are cleared first.
    /// </summary>
    /// <param name="path">The folder to delete. A folder that is not there is not an error.</param>
    internal static void DeleteTree(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        DirectoryInfo directory = new(path);
        if (directory.Exists)
        {
            DeleteTree(directory);
        }
    }

    private static void DeleteTree(DirectoryInfo directory)
    {
        if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            directory.Delete(recursive: false);
            return;
        }

        foreach (FileSystemInfo entry in directory.EnumerateFileSystemInfos())
        {
            if (entry is DirectoryInfo child)
            {
                DeleteTree(child);
            }
            else
            {
                if ((entry.Attributes & FileAttributes.ReparsePoint) == 0)
                {
                    entry.Attributes = FileAttributes.Normal;
                }

                entry.Delete();
            }
        }

        directory.Delete(recursive: false);
    }
}

namespace Agency.Huddle.App.Teams;

/// <summary>Tells a repository from a plain folder and from a path that cannot be read (Spec §6.4).</summary>
internal static class WorkspaceProbe
{
    private const string GitEntry = ".git";

    /// <summary>Classifies a workspace path: a <c>.git</c> folder or file at its root means a repository.</summary>
    /// <param name="path">The workspace path.</param>
    /// <returns>The state; <see cref="WorkspaceState.Unreadable"/> when the path does not exist, is not a folder or cannot be listed.</returns>
    internal static WorkspaceState StateOf(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return WorkspaceState.Unreadable;
        }

        try
        {
            if (!Directory.Exists(path))
            {
                return WorkspaceState.Unreadable;
            }

            // Listing proves the folder is readable; a missing permission throws here rather than later.
            using IEnumerator<string> listing = Directory.EnumerateFileSystemEntries(path).GetEnumerator();
            _ = listing.MoveNext();

            string git = Path.Combine(path, GitEntry);
            return Directory.Exists(git) || File.Exists(git) ? WorkspaceState.Repository : WorkspaceState.Folder;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return WorkspaceState.Unreadable;
        }
    }
}

namespace Agency.Huddle.App.Teams;

/// <summary>Tells a repository from a plain folder and from a path that cannot be read (Spec §6.4).</summary>
internal static class WorkspaceProbe
{
    private const string GitEntry = ".git";

    /// <summary>
    /// Whether a workspace path is the Teams folder itself. Build a team used to fill that in as the default, and it is
    /// never a Team's workspace: every Agent would be told that its work belongs among all the Teams' folders.
    /// </summary>
    /// <param name="path">The workspace path.</param>
    /// <param name="teamsRoot">The Teams folder.</param>
    /// <returns><see langword="true"/> when they name one folder, ignoring case as every refusing path check does.</returns>
    internal static bool IsTeamsRoot(string path, string teamsRoot)
    {
        try
        {
            return string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(teamsRoot)),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException)
        {
            // An unusable path is not the Teams folder; StateOf reports it as unreadable.
            return false;
        }
    }

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

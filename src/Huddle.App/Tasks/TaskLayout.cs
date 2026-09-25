namespace Agency.Huddle.App.Tasks;

/// <summary>Maps file paths to task locations and generates file paths for tasks.</summary>
internal static class TaskLayout
{
    /// <summary>The reserved folder name for closed tasks.</summary>
    internal const string ClosedFolder = "_closed";

    /// <summary>Maps a file path to a task location based on folder structure rules.</summary>
    /// <param name="root">The root directory path for tasks.</param>
    /// <param name="fullPath">The full file path to map.</param>
    /// <param name="location">The mapped task location, or null if not valid.</param>
    /// <param name="error">An error message if the path is invalid, null if ignored or valid.</param>
    /// <returns>True if the path maps to a valid location, false if invalid or ignored.</returns>
    internal static bool TryMap(string root, string fullPath, out TaskLocation? location, out string? error)
    {
        string relativePath = Path.GetRelativePath(root, fullPath);
        string[] parts = relativePath.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.None);

        // Need at least Team/file.md (2 parts)
        if (parts.Length < 2)
        {
            location = null;
            error = "is not inside a Team folder";
            return false;
        }

        // Check all folder segments (excluding filename) for reserved folders (starts with _ but not _closed)
        // This must be done at ANY depth before applying normal structure checks
        int folderCount = parts.Length - 1;
        for (int i = 0; i < folderCount; i++)
        {
            string segment = parts[i];
            if (segment.StartsWith('_') && segment != ClosedFolder)
            {
                // Reserved folder found at any level
                location = null;
                error = null;  // Ignored, not an error
                return false;
            }
        }

        string team = parts[0];

        if (folderCount == 1)
        {
            // Team/file.md
            location = new TaskLocation(team, null, false);
            error = null;
            return true;
        }

        if (folderCount == 2)
        {
            string middle = parts[1];
            if (middle == ClosedFolder)
            {
                // Team/_closed/file.md
                location = new TaskLocation(team, null, true);
                error = null;
                return true;
            }
            else
            {
                // Team/Project/file.md
                location = new TaskLocation(team, middle, false);
                error = null;
                return true;
            }
        }

        if (folderCount == 3)
        {
            string middle = parts[1];
            string last = parts[2];

            // Middle folder cannot be _closed (that would be Team/_closed/something which is too deep)
            if (middle == ClosedFolder)
            {
                location = null;
                error = "is nested too deeply; Tasks live at Team/[Project/][_closed/]";
                return false;
            }

            if (last == ClosedFolder)
            {
                // Team/Project/_closed/file.md
                location = new TaskLocation(team, middle, true);
                error = null;
                return true;
            }

            // Nested too deeply
            location = null;
            error = "is nested too deeply; Tasks live at Team/[Project/][_closed/]";
            return false;
        }

        // 4+ folder levels - nested too deep
        location = null;
        error = "is nested too deeply; Tasks live at Team/[Project/][_closed/]";
        return false;
    }

    /// <summary>Generates the file path for a task at a given location.</summary>
    /// <param name="root">The root directory path for tasks.</param>
    /// <param name="location">The task's location.</param>
    /// <param name="id">The task's unique identifier.</param>
    /// <returns>The full file path where the task should be stored.</returns>
    internal static string PathFor(string root, TaskLocation location, TaskId id)
    {
        string teamPath = Path.Combine(root, location.Team);

        if (location.Project == null)
        {
            if (location.Closed)
            {
                return Path.Combine(teamPath, ClosedFolder, $"{id}.md");
            }
            else
            {
                return Path.Combine(teamPath, $"{id}.md");
            }
        }
        else
        {
            string projectPath = Path.Combine(teamPath, location.Project);
            if (location.Closed)
            {
                return Path.Combine(projectPath, ClosedFolder, $"{id}.md");
            }
            else
            {
                return Path.Combine(projectPath, $"{id}.md");
            }
        }
    }
}

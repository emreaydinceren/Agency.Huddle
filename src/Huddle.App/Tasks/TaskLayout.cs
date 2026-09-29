using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Tasks;

/// <summary>Maps file paths to task locations and generates file paths for tasks.</summary>
internal static class TaskLayout
{
    /// <summary>The reserved folder name for a Team's or Project's Tasks.</summary>
    internal const string TasksFolder = "_tasks";

    /// <summary>The reserved folder name for closed tasks.</summary>
    internal const string ClosedFolder = "_closed";

    /// <summary>Maps a file path to a task location. A path outside the <c>_tasks/</c> layout is
    /// ignored rather than rejected: <paramref name="error"/> is always <see langword="null"/>.</summary>
    /// <param name="root">The root directory path for tasks.</param>
    /// <param name="fullPath">The full file path to map.</param>
    /// <param name="location">The mapped task location, or null when the path is not one.</param>
    /// <param name="error">Always <see langword="null"/>; kept for call-site compatibility.</param>
    /// <returns>True if the path maps to a valid location, false otherwise.</returns>
    internal static bool TryMap(string root, string fullPath, out TaskLocation? location, out string? error)
    {
        error = null;
        string[] parts = SplitRelativePath(root, fullPath);

        if (parts.Length < 3 || IsReservedFolderName(parts[0]))
        {
            location = null;
            return false;
        }

        string team = parts[0];

        if (IsTasksFolder(parts[1]))
        {
            return TryMapUnderTasksFolder(parts, 2, team, null, out location);
        }

        string project = parts[1];
        if (!TeamNames.IsReservedProjectName(project) && parts.Length >= 4 && IsTasksFolder(parts[2]))
        {
            return TryMapUnderTasksFolder(parts, 3, team, project, out location);
        }

        location = null;
        return false;
    }

    /// <summary>Generates the file path for a task at a given location.</summary>
    /// <param name="root">The root directory path for tasks.</param>
    /// <param name="location">The task's location.</param>
    /// <param name="id">The task's unique identifier.</param>
    /// <returns>The full file path where the task should be stored.</returns>
    internal static string PathFor(string root, TaskLocation location, TaskId id)
    {
        string tasksPath = location.Project is null
            ? Path.Combine(root, location.Team, TasksFolder)
            : Path.Combine(root, location.Team, location.Project, TasksFolder);

        return location.Closed
            ? Path.Combine(tasksPath, ClosedFolder, $"{id}.md")
            : Path.Combine(tasksPath, $"{id}.md");
    }

    /// <summary>True when a change at <paramref name="fullPath"/> could affect the Tasks under
    /// <paramref name="root"/>: a Team folder, a Project folder, a <c>_tasks</c>/<c>_closed</c>
    /// folder, or a Markdown file inside one of them. Pure string code: it never touches disk, so
    /// it decides directory versus file from the path's shape alone.</summary>
    /// <param name="root">The root directory path for tasks.</param>
    /// <param name="fullPath">The full path that changed.</param>
    /// <returns>True when the change could affect the Tasks store.</returns>
    internal static bool AffectsTasks(string root, string fullPath)
    {
        string[] parts = SplitRelativePath(root, fullPath);

        if (parts.Length == 0 || IsReservedFolderName(parts[0]))
        {
            return false;
        }

        if (parts.Length == 1)
        {
            return true;
        }

        if (IsTasksFolder(parts[1]))
        {
            return AffectsUnderTasksFolder(parts, 2);
        }

        string project = parts[1];
        if (IsReservedFolderName(project) || LooksLikeMarkdownFile(project))
        {
            return false;
        }

        if (parts.Length == 2)
        {
            return true;
        }

        return IsTasksFolder(parts[2]) && AffectsUnderTasksFolder(parts, 3);
    }

    /// <summary>True when <paramref name="name"/> is a reserved folder name: one starting with
    /// <c>_</c> or <c>.</c>. An ordinary name is never reserved, even with a space.</summary>
    /// <param name="name">The folder name to check.</param>
    /// <returns>True when the name is reserved.</returns>
    internal static bool IsReservedFolderName(string name) =>
        name.Length > 0 && (name[0] == '_' || name[0] == '.');

    private static bool TryMapUnderTasksFolder(string[] parts, int afterIndex, string team, string? project, out TaskLocation? location)
    {
        if (parts.Length == afterIndex + 1)
        {
            location = new TaskLocation(team, project, false);
            return true;
        }

        if (parts.Length == afterIndex + 2 && IsClosedFolder(parts[afterIndex]))
        {
            location = new TaskLocation(team, project, true);
            return true;
        }

        location = null;
        return false;
    }

    private static bool AffectsUnderTasksFolder(string[] parts, int afterIndex)
    {
        if (parts.Length == afterIndex)
        {
            return true;
        }

        string next = parts[afterIndex];
        if (IsClosedFolder(next))
        {
            return parts.Length == afterIndex + 1
                || (parts.Length == afterIndex + 2 && LooksLikeMarkdownFile(parts[afterIndex + 1]));
        }

        return parts.Length == afterIndex + 1 && LooksLikeMarkdownFile(next);
    }

    private static string[] SplitRelativePath(string root, string fullPath)
    {
        string relativePath = Path.GetRelativePath(root, fullPath);
        return relativePath.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.None);
    }

    private static bool IsTasksFolder(string name) => FolderSnapshot.PathComparer.Equals(name, TasksFolder);

    private static bool IsClosedFolder(string name) => FolderSnapshot.PathComparer.Equals(name, ClosedFolder);

    private static bool LooksLikeMarkdownFile(string name) => name.EndsWith(".md", StringComparison.Ordinal);
}

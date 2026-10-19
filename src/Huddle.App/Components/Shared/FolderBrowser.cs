namespace Agency.Huddle.App.Components.Shared;

/// <summary>Lists the sub-folders of a folder on the machine the app runs on, for the folder picker. Pure: reads the disk, keeps nothing.</summary>
internal static class FolderBrowser
{
    /// <summary>The most folders one listing returns; a larger folder says so instead of drawing thousands of rows.</summary>
    public const int MaxFolders = 500;

    /// <summary>The words shown when a path is not a folder that can be listed.</summary>
    public const string MissingMessage = "That folder does not exist.";

    private static readonly EnumerationOptions Options = new()
    {
        IgnoreInaccessible = true,
        RecurseSubdirectories = false,
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
    };

    /// <summary>Lists the folders under <paramref name="path"/>.</summary>
    /// <param name="path">A folder path, or <see langword="null"/> or blank for the top level.</param>
    /// <returns>The listing; a missing or unreadable folder is reported in the listing, never thrown.</returns>
    public static FolderListing List(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Top();
        }

        string full;
        try
        {
            full = Path.GetFullPath(path.Trim());
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return new FolderListing(null, null, [], false, MissingMessage);
        }

        if (!Directory.Exists(full))
        {
            return new FolderListing(null, null, [], false, MissingMessage);
        }

        string? parent = Directory.GetParent(full)?.FullName;
        try
        {
            List<BrowsedFolder> folders = [];
            bool truncated = false;
            foreach (string folder in Directory.EnumerateDirectories(full, "*", Options).Order(StringComparer.OrdinalIgnoreCase))
            {
                if (folders.Count == MaxFolders)
                {
                    truncated = true;
                    break;
                }

                folders.Add(new BrowsedFolder(Path.GetFileName(folder), folder));
            }

            return new FolderListing(full, parent, folders, truncated, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The folder went away, or became unreadable, between the check above and the listing.
            return new FolderListing(null, null, [], false, MissingMessage);
        }
    }

    /// <summary>The path to <paramref name="current"/> as clickable segments, outermost first: the drive or root, then each folder with its full path.</summary>
    /// <param name="current">The folder shown, or <see langword="null"/> for the top level.</param>
    /// <returns>The segments; empty for the top level.</returns>
    public static IReadOnlyList<BrowsedFolder> Crumbs(string? current)
    {
        if (string.IsNullOrWhiteSpace(current))
        {
            return [];
        }

        List<BrowsedFolder> trail = [];
        string? path = current;
        while (path is not null)
        {
            string? parent = Path.GetDirectoryName(path);
            trail.Add(new BrowsedFolder(parent is null ? path : Path.GetFileName(path), path));
            path = parent;
        }

        trail.Reverse();
        return trail;
    }

    /// <summary>The top level: every ready drive on Windows, the root folder elsewhere.</summary>
    private static FolderListing Top()
    {
        if (!OperatingSystem.IsWindows())
        {
            return List("/");
        }

        List<BrowsedFolder> drives = [.. DriveInfo.GetDrives().Where(static drive => drive.IsReady).Select(static drive => new BrowsedFolder(drive.Name, drive.Name))];
        return new FolderListing(null, null, drives, false, null);
    }
}

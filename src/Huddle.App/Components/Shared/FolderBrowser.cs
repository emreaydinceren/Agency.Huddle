namespace Agency.Huddle.App.Components.Shared;

/// <summary>One folder offered by <see cref="FolderBrowser"/>.</summary>
/// <param name="Name">The label: the folder's name, or a drive's root such as <c>C:\</c>.</param>
/// <param name="Path">The full path.</param>
internal sealed record BrowsedFolder(string Name, string Path);

/// <summary>What <see cref="FolderBrowser.List"/> found at one place.</summary>
/// <param name="Current">The folder listed, or <see langword="null"/> for the top level (the drives, or <c>/</c>).</param>
/// <param name="Parent">The folder one level up, or <see langword="null"/> when there is none (a drive root goes up to the top level, shown by <see cref="Current"/> being null).</param>
/// <param name="Folders">The sub-folders, ordered by name; hidden and system folders are left out.</param>
/// <param name="Truncated">Whether more folders exist than <see cref="FolderBrowser.MaxFolders"/>.</param>
/// <param name="Error">Why the folder could not be read, or <see langword="null"/>.</param>
internal sealed record FolderListing(string? Current, string? Parent, IReadOnlyList<BrowsedFolder> Folders, bool Truncated, string? Error)
{
    /// <summary>Whether a folder is open that can be chosen; the top level can only be walked into.</summary>
    public bool CanSelect => this.Current is not null && this.Error is null;
}

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

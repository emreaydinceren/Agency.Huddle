namespace Agency.Huddle.Seeder;

/// <summary>A folder the seeder must never wipe.</summary>
/// <param name="Path">The folder.</param>
/// <param name="BlocksDescendants">
/// <see langword="true"/> when nothing inside the folder may be wiped either (the repository);
/// <see langword="false"/> when only the folder itself and its ancestors are protected (the user profile,
/// so a seed root under the profile or the temp folder is still allowed).
/// </param>
internal sealed record ProtectedFolder(string Path, bool BlocksDescendants);

/// <summary>
/// Decides whether a folder may be wiped and then deletes it. The seeder's "clean slate" promise is
/// only safe because of this class: it deletes a folder wholesale, so it refuses everything it cannot
/// prove is a seed root the seeder made itself.
/// </summary>
internal static class SeedRootGuard
{
    /// <summary>The marker file the seeder leaves in every seed root; a wipe proceeds only where it exists.</summary>
    internal const string MarkerFileName = ".huddle-seed";

    /// <summary>
    /// Returns the reason <paramref name="root"/> must not be wiped, or <see langword="null"/> when it is safe.
    /// </summary>
    /// <param name="root">The seed root the caller wants to wipe and rebuild.</param>
    /// <param name="protectedFolders">Folders the root must not equal or contain, and, for those that block descendants, must not sit inside.</param>
    /// <returns>A human-readable refusal, or <see langword="null"/>.</returns>
    internal static string? Check(string root, IReadOnlyList<ProtectedFolder> protectedFolders)
    {
        string full = Normalize(root);
        string pathRoot = Path.GetPathRoot(full) ?? string.Empty;

        if (string.Equals(full, pathRoot, StringComparison.OrdinalIgnoreCase))
        {
            return $"'{full}' is a drive root.";
        }

        string relative = full[pathRoot.Length..];
        if (relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries).Length < 2)
        {
            return $"'{full}' is too close to the drive root; use a folder at least two levels deep, such as {Path.Combine(pathRoot, "seeds", "huddle")}.";
        }

        foreach (ProtectedFolder protectedFolder in protectedFolders)
        {
            string other = Normalize(protectedFolder.Path);
            if (IsSameOrInside(other, full) || (protectedFolder.BlocksDescendants && IsSameOrInside(full, other)))
            {
                return $"'{full}' overlaps the protected folder '{other}'.";
            }
        }

        if (!Directory.Exists(full))
        {
            return null;
        }

        if (new DirectoryInfo(full).Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            return $"'{full}' is a symbolic link or junction.";
        }

        bool empty = !Directory.EnumerateFileSystemEntries(full).Any();
        if (!empty && !File.Exists(Path.Combine(full, MarkerFileName)))
        {
            return $"'{full}' is not empty and has no {MarkerFileName} marker, so the seeder did not create it.";
        }

        string? link = FindReparsePoint(full);
        return link is null ? null : $"'{link}' inside the seed root is a symbolic link or junction.";
    }

    /// <summary>
    /// Deletes <paramref name="root"/> and everything in it, after <see cref="Check"/> has approved it.
    /// </summary>
    /// <param name="root">The seed root.</param>
    /// <param name="protectedFolders">The folders <see cref="Check"/> must keep clear of.</param>
    /// <exception cref="InvalidOperationException">The root is refused, or a file in it is still held open.</exception>
    internal static void Wipe(string root, IReadOnlyList<ProtectedFolder> protectedFolders)
    {
        string? refusal = Check(root, protectedFolders);
        if (refusal is not null)
        {
            throw new InvalidOperationException($"Refusing to wipe: {refusal}");
        }

        string full = Normalize(root);
        if (!Directory.Exists(full))
        {
            return;
        }

        try
        {
            Directory.Delete(full, recursive: true);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException($"Could not delete '{full}'. Is Huddle still running against it? {ex.Message}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidOperationException($"Could not delete '{full}'. Is Huddle still running against it? {ex.Message}", ex);
        }
    }

    /// <summary>Folders the seeder must never touch: the repository, the working directory's repository and the user profile.</summary>
    /// <returns>The protected folders that exist on this machine.</returns>
    internal static IReadOnlyList<ProtectedFolder> DefaultProtectedFolders()
    {
        List<ProtectedFolder> folders = [];
        AddIfPresent(folders, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), blocksDescendants: false);
        AddIfPresent(folders, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), blocksDescendants: false);
        AddIfPresent(folders, Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), blocksDescendants: false);
        AddIfPresent(folders, FindRepositoryRoot(AppContext.BaseDirectory), blocksDescendants: true);
        AddIfPresent(folders, FindRepositoryRoot(Environment.CurrentDirectory), blocksDescendants: true);
        return folders;
    }

    /// <summary>Walks up from <paramref name="start"/> to the folder that holds <c>Huddle.slnx</c>.</summary>
    /// <param name="start">A folder inside the repository.</param>
    /// <returns>The repository root, or <see langword="null"/> when none is found.</returns>
    internal static string? FindRepositoryRoot(string start)
    {
        DirectoryInfo? directory = new(start);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Huddle.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static void AddIfPresent(List<ProtectedFolder> folders, string? folder, bool blocksDescendants)
    {
        if (!string.IsNullOrWhiteSpace(folder))
        {
            folders.Add(new ProtectedFolder(folder, blocksDescendants));
        }
    }

    private static string Normalize(string path)
    {
        string full = Path.GetFullPath(path);
        string pathRoot = Path.GetPathRoot(full) ?? string.Empty;
        return string.Equals(full, pathRoot, StringComparison.OrdinalIgnoreCase)
            ? full
            : full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool IsSameOrInside(string candidate, string container)
    {
        if (string.Equals(candidate, container, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string prefix = container.EndsWith(Path.DirectorySeparatorChar) ? container : container + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static string? FindReparsePoint(string folder)
    {
        EnumerationOptions options = new() { RecurseSubdirectories = true, AttributesToSkip = 0, IgnoreInaccessible = true };
        foreach (string entry in Directory.EnumerateFileSystemEntries(folder, "*", options))
        {
            if (File.GetAttributes(entry).HasFlag(FileAttributes.ReparsePoint))
            {
                return entry;
            }
        }

        return null;
    }
}

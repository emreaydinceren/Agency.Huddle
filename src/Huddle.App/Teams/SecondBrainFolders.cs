namespace Agency.Huddle.App.Teams;

/// <summary>The three sub-folders every Team and Project folder carries (Spec §6.1, SB-R01).</summary>
internal static class SecondBrainFolders
{
    /// <summary>The sub-folder names, in creation order.</summary>
    internal static readonly IReadOnlyList<string> PerScope = [TeamNames.RawFolder, TeamNames.MemoryFolder, TeamNames.OutputsFolder];

    /// <summary>Creates each missing sub-folder under <paramref name="folderFullPath"/>; a folder that matches ignoring case is reused.</summary>
    /// <param name="folderFullPath">The Team or Project folder's full path; it must exist.</param>
    /// <exception cref="IOException">A sub-folder could not be created.</exception>
    /// <exception cref="UnauthorizedAccessException">The folder is not writable.</exception>
    internal static void Ensure(string folderFullPath)
    {
        string[] existing = [.. Directory.GetDirectories(folderFullPath).Select(static path => Path.GetFileName(path))];
        foreach (string name in PerScope)
        {
            if (!Array.Exists(existing, entry => string.Equals(entry, name, StringComparison.OrdinalIgnoreCase)))
            {
                Directory.CreateDirectory(Path.Combine(folderFullPath, name));
            }
        }
    }
}

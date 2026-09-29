using Agency.Huddle.App.FileChanges;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// Builds one Persona's Team Memory snapshot: the Team-wide and per-Project Memory of every Team the
/// Persona carries a label for, capped across all of them, per Spec §6.4 and §8.4.
/// </summary>
internal static class TeamMemoryIndex
{
    /// <summary>The name of the Memory folder inside a Team folder and inside a Project folder.</summary>
    private const string MemoryFolderName = "memory";

    /// <summary>
    /// Builds the Team Memory index for one Persona's Teams, capped across all of them. Every path is
    /// built from <see cref="TeamSummary.Name"/> (the catalog's folder spelling), never from a label.
    /// A label with no summary, or whose summary has no folder, gives no group. A Team folder or
    /// Project folder that is a reparse point is skipped, and a <c>memory</c> folder that is one, is
    /// a file, is missing or cannot be read counts as empty.
    /// </summary>
    /// <param name="teamsRoot">The Teams root folder.</param>
    /// <param name="teamLabels">The Persona's Team labels, in the order written.</param>
    /// <param name="teams">The catalog's Teams.</param>
    /// <param name="maxEntries">The most entries to list across every Team; zero or less lists none.</param>
    /// <returns>One group per distinct label that has a Team folder, in label order, and the total not listed.</returns>
    public static TeamMemorySnapshot Build(
        string teamsRoot,
        IReadOnlyList<string> teamLabels,
        IReadOnlyList<TeamSummary> teams,
        int maxEntries)
    {
        int remaining = Math.Max(0, maxEntries);
        int notListed = 0;
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        List<TeamMemoryGroup> groups = [];

        foreach (string label in teamLabels)
        {
            if (!seen.Add(label))
            {
                continue;
            }

            TeamSummary? summary = teams.FirstOrDefault(team => string.Equals(team.Name, label, StringComparison.OrdinalIgnoreCase));
            if (summary is null || !summary.HasFolder)
            {
                continue;
            }

            string teamFolder = Path.Combine(teamsRoot, summary.Name);
            if (IsSkipped(teamFolder))
            {
                continue;
            }

            string teamMemoryPath = Path.Combine(teamFolder, MemoryFolderName);
            MemorySnapshot teamWide = BuildScope(teamMemoryPath, remaining);
            remaining -= teamWide.Entries.Count;
            notListed += teamWide.NotListed;

            List<(string Project, MemorySnapshot Memory)> projects = [];
            foreach (string project in summary.Projects)
            {
                string projectFolder = Path.Combine(teamFolder, project);
                if (IsSkipped(projectFolder))
                {
                    continue;
                }

                MemorySnapshot memory = BuildScope(Path.Combine(projectFolder, MemoryFolderName), remaining);
                remaining -= memory.Entries.Count;
                notListed += memory.NotListed;
                projects.Add((project, memory));
            }

            groups.Add(new TeamMemoryGroup(summary.Name, teamMemoryPath, teamWide, projects));
        }

        return new TeamMemorySnapshot(groups, notListed);
    }

    /// <summary>
    /// Indexes one <c>memory</c> folder. A reparse point is never followed, and a missing folder, a
    /// file of that name or a folder that cannot be read gives an empty snapshot.
    /// </summary>
    /// <param name="memoryPath">The <c>memory</c> folder's path.</param>
    /// <param name="remaining">How many entries may still be listed.</param>
    private static MemorySnapshot BuildScope(string memoryPath, int remaining)
    {
        if (IsSkipped(memoryPath))
        {
            return new MemorySnapshot(memoryPath, [], 0);
        }

        try
        {
            (IReadOnlyList<MemoryEntry> entries, int notListed) = MemoryIndex.Build(memoryPath, remaining);
            return new MemorySnapshot(memoryPath, entries, notListed);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Build is a pure static with no logger, so an unreadable folder is treated as empty:
            // one Team's locked memory folder must not stop the Persona's session prompt (S10).
            return new MemorySnapshot(memoryPath, [], 0);
        }
    }

    /// <summary>
    /// True when <paramref name="path"/> is a reparse point (a junction or symbolic link, which
    /// <c>MemoryIndex.Build</c> would follow out of the Teams root) or its attributes cannot be read.
    /// A path that does not exist is not skipped: the caller finds nothing there.
    /// </summary>
    /// <param name="path">The folder to check.</param>
    private static bool IsSkipped(string path)
    {
        try
        {
            return File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            // Nothing is there, so there is nothing to follow; the caller treats it as empty.
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Build is a pure static with no logger, so a path whose attributes cannot be read is
            // treated as unsafe and skipped, which reads as empty (S10).
            return true;
        }
    }
}

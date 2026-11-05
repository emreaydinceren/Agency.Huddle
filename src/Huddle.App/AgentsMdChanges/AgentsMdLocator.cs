using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.AgentsMdChanges;

/// <summary>Says which <c>AGENTS.md</c> files apply to an Agent and in what order (Spec §6.1).</summary>
internal static class AgentsMdLocator
{
    /// <summary>Yields each level in composition order: Huddle-wide, then each Team, then its Projects sorted ordinally.</summary>
    /// <param name="teamsRoot">The Teams root folder.</param>
    /// <param name="teamNames">The folder names of the Agent's Teams, in the order given.</param>
    /// <returns>Every level's file, whether or not it exists.</returns>
    public static IEnumerable<AgentsMdFile> Enumerate(string teamsRoot, IReadOnlyList<string> teamNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(teamsRoot);
        ArgumentNullException.ThrowIfNull(teamNames);
        return EnumerateCore(teamsRoot, teamNames);
    }

    /// <summary>Walks the levels lazily so a Team's folder is listed only when the caller reaches it.</summary>
    /// <param name="teamsRoot">The Teams root folder.</param>
    /// <param name="teamNames">The Team folder names.</param>
    private static IEnumerable<AgentsMdFile> EnumerateCore(string teamsRoot, IReadOnlyList<string> teamNames)
    {
        yield return Make(AgentsMdLevel.HuddleWide, null, null, teamsRoot);

        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (string team in teamNames)
        {
            if (LibraryNames.Validate(team) is not null || !seen.Add(team))
            {
                continue;
            }

            string teamDir = Path.Combine(teamsRoot, team);
            yield return Make(AgentsMdLevel.Team, team, null, teamDir);
            foreach (string project in ProjectNames(teamDir))
            {
                yield return Make(AgentsMdLevel.Project, team, project, Path.Combine(teamDir, project));
            }
        }
    }

    /// <summary>Builds the file entry for a level folder.</summary>
    /// <param name="level">The level.</param>
    /// <param name="team">The Team name, if any.</param>
    /// <param name="project">The Project name, if any.</param>
    /// <param name="folder">The folder that holds the file.</param>
    private static AgentsMdFile Make(AgentsMdLevel level, string? team, string? project, string folder)
    {
        string relative = (team, project) switch
        {
            (null, _) => TeamPaths.AgentsMdFileName,
            (_, null) => $"{team}/{TeamPaths.AgentsMdFileName}",
            _ => $"{team}/{project}/{TeamPaths.AgentsMdFileName}",
        };
        return new AgentsMdFile(level, team, project, Path.Combine(folder, TeamPaths.AgentsMdFileName), relative);
    }

    /// <summary>Lists a Team's Project folder names, minus reserved names, sorted ordinally.</summary>
    /// <param name="teamDir">The Team's folder.</param>
    private static List<string> ProjectNames(string teamDir)
    {
        List<string> names = [];
        try
        {
            foreach (string dir in Directory.EnumerateDirectories(teamDir))
            {
                string name = Path.GetFileName(dir);
                if (!TeamNames.IsReservedProjectName(name))
                {
                    names.Add(name);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A missing or locked Team folder has no readable Projects; they are simply absent.
            return [];
        }

        names.Sort(StringComparer.Ordinal);
        return names;
    }
}

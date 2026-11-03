using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Library;

/// <summary>
/// Lists Team folders for the Library (Spec §6.2 first paragraph), built on
/// <see cref="TaskStore.Teams"/> so the Library and the board can never disagree about which
/// folders are orphans (corrections-B4 item 32). A Team folder the Library's
/// <see cref="LibraryPathResolver"/> refuses - a junction reaching outside the Teams root - is
/// skipped, even though the board still counts it.
/// </summary>
internal static class TeamFolderCatalog
{
    /// <summary>Lists every Team folder under <paramref name="teamsRoot"/> that the resolver accepts,
    /// ordered ordinal-ignore-case by name.</summary>
    /// <param name="resolver">Checks each Team folder against the Library's path boundary.</param>
    /// <param name="teamsRoot">The already-resolved Teams root.</param>
    /// <param name="teams">The board's current Team folders (<see cref="TaskStore.Teams"/>).</param>
    /// <returns>The board's own <see cref="TeamFolder"/> entries, filtered and re-ordered: the Library shows the same Name, Projects and orphan flag.</returns>
    internal static IReadOnlyList<TeamFolder> List(LibraryPathResolver resolver, LibraryPath teamsRoot, IReadOnlyList<TeamFolder> teams)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(teamsRoot);
        ArgumentNullException.ThrowIfNull(teams);

        List<TeamFolder> folders = [];
        foreach (TeamFolder team in teams)
        {
            if (TeamNames.IsReservedTeamName(team.Name))
            {
                continue;
            }

            DirectoryInfo directory = new(Path.Combine(teamsRoot.FullPath, team.Name));
            if (!directory.Exists || !resolver.TryResolveChild(teamsRoot, directory, out _, out _))
            {
                continue;
            }

            folders.Add(team);
        }

        return [.. folders.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)];
    }
}

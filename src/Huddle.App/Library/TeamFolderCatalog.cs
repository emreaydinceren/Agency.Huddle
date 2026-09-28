using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.App.Library;

/// <summary>
/// A Team folder as the Library lists it: <see cref="TeamFolder.Name"/> and
/// <see cref="TeamFolder.Projects"/> unchanged, plus whether it is an orphan (Spec §6.2 first
/// paragraph).
/// </summary>
/// <param name="Name">The folder's name, exactly as it appears on disk.</param>
/// <param name="IsOrphan"><see langword="true"/> when no Persona's <c>teams</c> field names this folder.</param>
/// <param name="Projects">The names of its Project sub-folders.</param>
internal sealed record LibraryTeamFolder(string Name, bool IsOrphan, IReadOnlyList<string> Projects)
{
    /// <summary>Structural equality for <see cref="Projects"/>, the same reasoning as <see cref="TeamFolder"/>'s.</summary>
    /// <param name="other">The other <see cref="LibraryTeamFolder"/> to compare against.</param>
    public bool Equals(LibraryTeamFolder? other) =>
        other is not null &&
        string.Equals(this.Name, other.Name, StringComparison.Ordinal) &&
        this.IsOrphan == other.IsOrphan &&
        this.Projects.SequenceEqual(other.Projects, StringComparer.Ordinal);

    /// <inheritdoc cref="Equals(LibraryTeamFolder)"/>
    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(this.Name, StringComparer.Ordinal);
        hash.Add(this.IsOrphan);
        foreach (string project in this.Projects)
        {
            hash.Add(project, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }
}

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
    internal static IReadOnlyList<LibraryTeamFolder> List(LibraryPathResolver resolver, LibraryPath teamsRoot, IReadOnlyList<TeamFolder> teams)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(teamsRoot);
        ArgumentNullException.ThrowIfNull(teams);

        List<LibraryTeamFolder> folders = [];
        foreach (TeamFolder team in teams)
        {
            DirectoryInfo directory = new(Path.Combine(teamsRoot.FullPath, team.Name));
            if (!directory.Exists || !resolver.TryResolveChild(teamsRoot, directory, out _, out _))
            {
                continue;
            }

            folders.Add(new LibraryTeamFolder(team.Name, team.IsOrphan, team.Projects));
        }

        return [.. folders.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)];
    }
}

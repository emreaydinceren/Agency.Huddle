namespace Agency.Huddle.App.Tasks;

/// <summary>
/// A Team folder found directly under the Tasks scan root, together with its Project sub-folders
/// (Spec §8.2). Folders are listed whether or not they hold any Task file - <see cref="TaskStore"/>
/// enumerates with <see cref="Directory.GetDirectories(string)"/>, not by deriving folders from the
/// files it parsed - and two folders differing only by case fold into one entry, the first by
/// Ordinal winning.
/// </summary>
/// <param name="Name">The folder's name, exactly as it appears on disk.</param>
/// <param name="Projects">The names of its Project sub-folders, excluding <c>_closed</c>.</param>
/// <param name="IsOrphan">
/// <see langword="true"/> when no Persona's <c>teams</c> field names this folder, compared
/// case-insensitively. Recomputed whenever <see cref="Acp.PersonaStore.PersonasChanged"/> fires,
/// without rescanning any file.
/// </param>
public sealed record TeamFolder(string Name, IReadOnlyList<string> Projects, bool IsOrphan)
{
    /// <summary>
    /// Structural equality for <see cref="Projects"/>: the compiler-generated record equality would
    /// otherwise compare it as an <see cref="IReadOnlyList{T}"/> reference (typically a
    /// <see cref="List{T}"/>, which has no value equality of its own), so two <see cref="TeamFolder"/>
    /// instances built from identical disk content in two separate scans would never compare equal.
    /// That broke <c>TaskStore.RebuildFromWatcher</c>'s "raise <c>IndexChanged</c> only when something
    /// actually changed" rule (Spec §8.4, Settled corrections-B2 D5 item 7): every debounced rebuild's
    /// freshly-scanned Team list compared unequal to the previous one on <see cref="Projects"/> alone,
    /// so the watcher republished on every rebuild, not only a real one.
    /// </summary>
    /// <param name="other">The other <see cref="TeamFolder"/> to compare against.</param>
    public bool Equals(TeamFolder? other) =>
        other is not null &&
        string.Equals(this.Name, other.Name, StringComparison.Ordinal) &&
        this.IsOrphan == other.IsOrphan &&
        this.Projects.SequenceEqual(other.Projects, StringComparer.Ordinal);

    /// <inheritdoc cref="Equals(TeamFolder)"/>
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

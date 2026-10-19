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

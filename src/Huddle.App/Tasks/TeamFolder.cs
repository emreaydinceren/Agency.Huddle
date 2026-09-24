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
public sealed record TeamFolder(string Name, IReadOnlyList<string> Projects, bool IsOrphan);

namespace Agency.Huddle.App.Tasks;

/// <summary>
/// A file under the Tasks scan root that did not become an indexed <see cref="TaskItem"/> - a file
/// outside any Team folder, one nested too deeply, a parse failure, a duplicate id, an unreadable
/// file, or one under a Team folder name that duplicates another only by case (Spec §8.1-§8.2). A
/// bad file is data, not an error: it is reported, never swallowed, following the same rule
/// <c>RejectedPersonaFile</c> gives a bad Persona file.
/// </summary>
/// <param name="Path">The rejected file's absolute path.</param>
/// <param name="Reason">Why the file was rejected, in prose a Human can read as-is.</param>
public sealed record RejectedTaskFile(string Path, string Reason);

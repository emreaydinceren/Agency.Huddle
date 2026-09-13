namespace Agency.Huddle.App.Acp;

/// <summary>
/// A file under the Teams directory that did not become a Persona: either
/// <see cref="PersonaFrontmatter.TryReadIdentity(string, out PersonaIdentity?, out string)"/> could
/// not read a valid identity out of it, or its identity collided with another file's (see
/// <see cref="PersonaIndex"/> for the collision rules). Surfaced by
/// <see cref="PersonaStore.RejectedFiles"/> so the UI can explain why a file the user expected to
/// see is missing, rather than it silently vanishing - the read path never throws for a bad file;
/// this is the alternative to throwing.
/// </summary>
/// <param name="Path">The file's absolute path.</param>
/// <param name="Reason">
/// Why the file was rejected. Names any other file(s) involved when the reason is a collision,
/// exactly the "each error naming the other's path" behaviour a duplicate Name or Alias needs to be
/// diagnosable at all.
/// </param>
public sealed record RejectedPersonaFile(string Path, string Reason);

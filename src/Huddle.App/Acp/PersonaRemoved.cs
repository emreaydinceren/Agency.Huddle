namespace Agency.Huddle.App.Acp;

/// <summary>
/// The front-matter Name of a Persona file that no longer exists - see <see cref="PersonaStore"/>'s
/// <c>PersonaRemoved</c> event, which carries one of these per detected removal.
/// </summary>
/// <param name="Name">The Name the removed Persona resolved to.</param>
public sealed record PersonaRemoved(string Name);

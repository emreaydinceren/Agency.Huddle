namespace Agency.Huddle.App.Acp;

/// <summary>
/// The old and the new front-matter Name of a Persona file whose <c>name:</c> changed in place -
/// see <see cref="PersonaStore"/>'s <c>PersonaRenamed</c> event, which carries one of these per
/// detected rename.
/// </summary>
/// <param name="OldName">The Name the Persona resolved to before this change.</param>
/// <param name="NewName">The Name the same physical file resolves to now.</param>
public sealed record PersonaRenamed(string OldName, string NewName);

namespace Agency.Huddle.App.Acp;

/// <summary>One Persona's text replacement for <see cref="PersonaStore.UpdateMany"/>.</summary>
/// <param name="Name">The Persona's current Name.</param>
/// <param name="ExpectedText">The text the file must still hold for the edit to apply.</param>
/// <param name="NewText">The text to write; it must load cleanly under the same Name.</param>
internal sealed record PersonaTextEdit(string Name, string ExpectedText, string NewText);

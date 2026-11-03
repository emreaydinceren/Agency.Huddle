namespace Agency.Huddle.App.Acp;

/// <summary>
/// "What this Agent reads" (Spec §6.5, D-18): the parts of the system prompt a Persona's next root session will be told, built by
/// the same code a session start runs. Read-only: no process starts, nothing is written, no Turn is spent.
/// </summary>
/// <param name="factory">Owns the snapshot builders and tool roster the real session start uses.</param>
internal sealed class PersonaPromptPreview(DotAcpAgentHostFactory factory)
{
    /// <summary>Builds the preview for one Persona.</summary>
    /// <param name="persona">The Persona whose next session is previewed.</param>
    /// <returns>The labelled parts in prompt order and the Adapter they were composed for.</returns>
    internal (IReadOnlyList<PromptPart> Parts, string Adapter) Build(Persona persona)
    {
        ArgumentNullException.ThrowIfNull(persona);
        return factory.PreviewParts(persona);
    }
}

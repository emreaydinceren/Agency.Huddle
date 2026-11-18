namespace Agency.Huddle.App.Acp.Sessions.TurnHooks;

/// <summary>Who a pipeline serves; computed once by PersonaSupervisor.</summary>
/// <param name="PersonaName">The Persona the pipeline serves.</param>
/// <param name="ReadsFiles">Whether the Persona's Adapter reads files.</param>
/// <param name="FileChangesOn">Whether File Changes is on for this Persona.</param>
/// <param name="DeclaredWatches">The watches the Persona declares.</param>
/// <param name="Clean">Whether the pipeline serves a goldfish possession: no hook applies.</param>
internal sealed record TurnHookScope(
    string PersonaName,
    bool ReadsFiles,
    bool FileChangesOn,
    IReadOnlyList<string> DeclaredWatches,
    bool Clean = false);

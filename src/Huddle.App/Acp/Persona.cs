namespace Agency.Huddle.App.Acp;

/// <summary>
/// A named system prompt for a Claude agent, backed by a markdown file. <paramref name="Model"/>
/// and <paramref name="Effort"/> are both stored separately, in SQLite (see
/// <see cref="Agency.Huddle.App.Data.PersonaModelStore"/> and <see cref="Agency.Huddle.App.Data.PersonaEffortStore"/>),
/// because they are mutable app state rather than part of the file-backed Persona library. A
/// <c>null</c> <paramref name="Model"/> means "use the agent's default model"; a <c>null</c>
/// <paramref name="Effort"/> means "use whatever effort that model normally uses". In both cases
/// nothing is sent on the wire at all: <see cref="Agency.Huddle.Acp.Abstractions.AgentSessionOptions"/>
/// normalises a null (or blank) value to "send no <c>session/set_config_option</c> call for this",
/// not "send an empty one".
/// </summary>
public sealed record Persona(string Name, string Text, string? Model = null, string? Effort = null);
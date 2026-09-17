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
/// not "send an empty one". <paramref name="Adapter"/>, unlike <paramref name="Model"/> and
/// <paramref name="Effort"/>, is not a separate store: it travels with the file itself (Spec §7.1,
/// §7.2), read out of the same frontmatter as <see cref="Agency.Huddle.App.Acp.PersonaIdentity.Adapter"/>.
/// Because this is a plain <see langword="record"/>, <see cref="Agency.Huddle.App.Acp.PersonaSupervisor"/>'s
/// restart check gets Adapter's contribution for free through ordinary value equality (Spec §7.3) -
/// no field-by-field comparison to keep in sync as this record grows.
/// </summary>
/// <param name="Name">The Persona's front-matter Name.</param>
/// <param name="Text">The Persona's full raw file text (frontmatter and body).</param>
/// <param name="Model">The chosen Model, or <see langword="null"/> for the agent's default.</param>
/// <param name="Effort">The chosen Effort, or <see langword="null"/> for the model's default.</param>
/// <param name="Adapter">
/// Which ACP agent runs this Persona's session (Spec §7.2), or <see langword="null"/> to run on the
/// installation's default profile.
/// </param>
public sealed record Persona(string Name, string Text, string? Model = null, string? Effort = null, string? Adapter = null);
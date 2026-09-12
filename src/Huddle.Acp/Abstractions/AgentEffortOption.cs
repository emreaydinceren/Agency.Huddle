namespace Agency.Huddle.Acp.Abstractions;

/// <summary>
/// One selectable effort (thinking/reasoning) level an agent's <c>session/new</c> or
/// <c>session/set_config_option</c> response advertised through its "thought_level" category
/// <c>configOptions</c> entry.
/// </summary>
/// <param name="Id">
/// The wire value that selects this effort level (ACP's <c>value</c> field on the option), and the
/// value to pass back as <see cref="AgentSessionOptions.Effort"/> to select it on a later session.
/// </param>
/// <param name="Name">The human-readable label for this option.</param>
/// <param name="Description">An optional human-readable description of this option.</param>
public sealed record AgentEffortOption(string Id, string Name, string? Description);

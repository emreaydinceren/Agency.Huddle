namespace Agency.Huddle.Acp.Abstractions;

/// <summary>
/// One selectable mode an agent's <c>session/new</c> or <c>session/set_config_option</c> response
/// advertised through its "mode" category <c>configOptions</c> entry: how much the agent may do
/// before it must ask.
/// </summary>
/// <param name="Id">
/// The wire value that selects this mode (ACP's <c>value</c> field on the option), and the value to
/// pass back as <see cref="AgentSessionOptions.Mode"/> to select it on a later session.
/// </param>
/// <param name="Name">The human-readable label for this option.</param>
/// <param name="Description">An optional human-readable description of this option.</param>
public sealed record AgentModeOption(string Id, string Name, string? Description);

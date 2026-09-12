namespace Agency.Huddle.Acp.Abstractions;

/// <summary>
/// One selectable model an agent's <c>session/new</c> response advertised through its "model"
/// category <c>configOptions</c> entry.
/// </summary>
/// <param name="Id">
/// The wire value that selects this model (ACP's <c>value</c> field on the option), and the value
/// to pass back as <see cref="AgentSessionOptions.Model"/> to select it on a later session.
/// </param>
/// <param name="Name">The human-readable label for this option.</param>
/// <param name="Description">An optional human-readable description of this option.</param>
public sealed record AgentModelOption(string Id, string Name, string? Description);
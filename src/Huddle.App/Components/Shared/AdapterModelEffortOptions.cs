using Agency.Huddle.Acp.Abstractions;

namespace Agency.Huddle.App.Components;

/// <summary>
/// The lists an <c>AdapterModelEffortPicker</c> has just read from its Adapter, raised after each probe so the caller can show
/// a stored id by its name elsewhere (the Agent card's read view). Public because it is a component parameter's type.
/// </summary>
/// <param name="Models">The Models the Adapter offers; empty while unread or when the probe failed.</param>
/// <param name="Efforts">The effort levels the chosen Model offers.</param>
/// <param name="WorkModes">The Work modes the Adapter offers; empty when the picker offers no Work mode.</param>
public sealed record AdapterModelEffortOptions(
    IReadOnlyList<AgentModelOption> Models,
    IReadOnlyList<AgentEffortOption> Efforts,
    IReadOnlyList<AgentModeOption> WorkModes);

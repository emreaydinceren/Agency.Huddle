using Agency.Huddle.Acp.Abstractions;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// The two catalogs one throwaway ACP session answers, read off the session
/// <see cref="IAdapterProbeRunner"/> negotiates (Spec §6.5).
/// </summary>
/// <param name="Models">The Adapter's advertised model catalog, in wire order.</param>
/// <param name="EffortLevels">
/// The Adapter's advertised effort ladder for the requested model, in wire order and BEFORE
/// <see cref="ModelCatalogProbe.WithoutAdapterDefault"/> filtering — dropping the adapter's
/// <c>"default"</c> sentinel is <see cref="ModelCatalogProbe"/>'s own responsibility, not the
/// runner's.
/// </param>
internal sealed record AdapterProbeOutcome(
    IReadOnlyList<AgentModelOption> Models,
    IReadOnlyList<AgentEffortOption> EffortLevels);

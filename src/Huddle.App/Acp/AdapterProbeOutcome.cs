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
    IReadOnlyList<AgentEffortOption> EffortLevels)
{
    /// <summary>
    /// The Adapter's advertised Work Modes (ADR-0033), in wire order and BEFORE the hidden-modes policy:
    /// hiding is <see cref="ModelCatalogProbe"/>'s job on the way out, so the cache never bakes a setting in.
    /// An init property rather than a third positional parameter, so the two-argument construction used
    /// everywhere a catalog is faked is unchanged.
    /// </summary>
    public IReadOnlyList<AgentModeOption> Modes { get; init; } = [];

    /// <summary>
    /// The id of the model the throwaway session reported running (<see cref="IAgentSession.CurrentModelId"/>),
    /// or null when the Adapter did not say. When the probe requested no model, this is the Adapter's own
    /// default. An init property for the same reason as <see cref="Modes"/>.
    /// </summary>
    public string? CurrentModel { get; init; }

    /// <summary>
    /// The id of the effort the throwaway session reported running (<see cref="IAgentSession.CurrentEffortId"/>),
    /// UNFILTERED: it may be the Adapter's <c>"default"</c> sentinel, which names no level.
    /// </summary>
    public string? CurrentEffort { get; init; }
}

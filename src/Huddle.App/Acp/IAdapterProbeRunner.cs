using Agency.Huddle.Acp.Hosting;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Runs one throwaway adapter probe: spawns the process described by an <see cref="AgentProcessOptions"/>,
/// negotiates a session requesting one model, reads back the Adapter's advertised catalogs, and
/// disposes. Extracted out of <see cref="ModelCatalogProbe"/> as the seam its caching, keying and
/// gating logic is tested through: docs/agencyteam/rules.md row 35 ("No test may reach the real
/// <see cref="ModelCatalogProbe"/>") forbids a test reaching the real probe, because it spawns a
/// real adapter process, and <see cref="ModelCatalogProbe"/> could not previously be exercised
/// without one. Internal, like <see cref="IModelCatalog"/> itself — a test seam, not a public
/// extension point. The production default, <see cref="AdapterProcessProbeRunner"/>, is
/// constructor-injected exactly like every other dependency, so normal DI registration is
/// unchanged; a test substitutes a fake runner the same way it substitutes <see cref="IModelCatalog"/>
/// itself.
/// </summary>
internal interface IAdapterProbeRunner
{
    /// <summary>
    /// Spawns, negotiates and disposes one throwaway session. Throws on failure to launch, talk to,
    /// or authenticate the adapter, or on a timeout — the same exceptions
    /// <see cref="ModelCatalogProbe"/> already catches and turns into an empty, uncached result.
    /// This seam only decides what "ran" means; the caller decides what "failed" means.
    /// </summary>
    /// <param name="processOptions">Where and how to launch the adapter process.</param>
    /// <param name="probeCwd">The working directory the throwaway session starts in.</param>
    /// <param name="model">The model id to request, or <see langword="null"/> for the adapter's own default.</param>
    /// <param name="cancellationToken">Cancels the probe, including the caller's own timeout.</param>
    /// <returns>The Adapter's advertised catalogs.</returns>
    Task<AdapterProbeOutcome> RunAsync(AgentProcessOptions processOptions, string probeCwd, string? model, CancellationToken cancellationToken);
}

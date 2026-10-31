using Microsoft.Extensions.Options;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.Hosting;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// The production <see cref="IAdapterProbeRunner"/>: actually launches the adapter process, does
/// <c>initialize</c> -&gt; <c>session/new</c>, and disposes. This is what
/// <see cref="ModelCatalogProbe"/> used to do inline before <see cref="IAdapterProbeRunner"/> was
/// extracted as a test seam — see that interface's own doc comment for why.
/// </summary>
/// <param name="options">The bound <see cref="TeamOptions"/>, read for <c>Acp.TraceWire</c>.</param>
/// <param name="loggerFactory">Handed to the launched host and its process launcher.</param>
internal sealed class AdapterProcessProbeRunner(IOptions<TeamOptions> options, ILoggerFactory loggerFactory) : IAdapterProbeRunner
{
    /// <inheritdoc/>
    public async Task<AdapterProbeOutcome> RunAsync(AgentProcessOptions processOptions, string probeCwd, string? model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(processOptions);

        AgentSessionOptions sessionOptions = new(probeCwd, new AutoApprovePermissionHandler(), model: model);
        ThrowawayAdapterSession adapter = await ThrowawayAdapterSession.StartAsync(
            processOptions, sessionOptions, options.Value.Acp.TraceWire, loggerFactory, cancellationToken).ConfigureAwait(false);
        await using var adapterScope = adapter.ConfigureAwait(false);

        return new AdapterProbeOutcome(adapter.Session.Models, adapter.Session.EffortLevels)
        {
            Modes = adapter.Session.ModeOptions,
            CurrentModel = adapter.Session.CurrentModelId,
            CurrentEffort = adapter.Session.CurrentEffortId,
        };
    }
}

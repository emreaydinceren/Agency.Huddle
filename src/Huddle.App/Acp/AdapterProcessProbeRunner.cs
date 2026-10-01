using Microsoft.Extensions.Options;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.DotAcp;
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

        DotAcpAgentHost? host = null;
        IAgentSession? session = null;
        try
        {
            var launcher = new AgentProcessLauncher(loggerFactory.CreateLogger<AgentProcessLauncher>());
            var hostOptions = new DotAcpHostOptions("Team.App", TraceWire: options.Value.Acp.TraceWire);
            host = new DotAcpAgentHost(processOptions, launcher, hostOptions, loggerFactory);
            await host.StartAsync(cancellationToken).ConfigureAwait(false);

            session = await host.StartSessionAsync(
                new AgentSessionOptions(probeCwd, new AutoApprovePermissionHandler(), model: model),
                cancellationToken).ConfigureAwait(false);

            return new AdapterProbeOutcome(session.Models, session.EffortLevels) { Modes = session.ModeOptions };
        }
        finally
        {
            if (session is not null)
            {
                await session.DisposeAsync().ConfigureAwait(false);
            }

            if (host is not null)
            {
                await host.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}

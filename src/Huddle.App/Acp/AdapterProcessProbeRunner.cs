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
    // How long a probe waits for the Adapter's first sign-in report once the session is open. A constant, not a config key.
    private static readonly TimeSpan SignInWait = TimeSpan.FromSeconds(3);

    /// <inheritdoc/>
    public async Task<AdapterProbeOutcome> RunAsync(AgentProcessOptions processOptions, string probeCwd, string? model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(processOptions);

        AgentSessionOptions sessionOptions = new(probeCwd, new AutoApprovePermissionHandler(), model: model);
        ThrowawayAdapterSession adapter = await ThrowawayAdapterSession.StartAsync(
            processOptions, sessionOptions, options.Value.Acp.TraceWire, loggerFactory, cancellationToken).ConfigureAwait(false);
        await using var adapterScope = adapter.ConfigureAwait(false);

        // Waits only for an Adapter that advertised it reports identity; the push normally precedes session/new's answer.
        AgentSignInStatus signIn = await adapter.ReadSignInStatusAsync(AdapterProcessProbeRunner.SignInWait, cancellationToken).ConfigureAwait(false);

        return new AdapterProbeOutcome(adapter.Session.Models, adapter.Session.EffortLevels)
        {
            SignIn = signIn,
            Modes = adapter.Session.ModeOptions,
            CurrentModel = adapter.Session.CurrentModelId,
            CurrentEffort = adapter.Session.CurrentEffortId,
        };
    }
}

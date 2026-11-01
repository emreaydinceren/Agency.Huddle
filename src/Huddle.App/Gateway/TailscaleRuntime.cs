using Agency.Huddle.App.Services;

namespace Agency.Huddle.App.Gateway;

/// <summary>
/// Runs the Tailscale claim and the status poller once the server has started and the managed endpoint is known (D-5). Does nothing when
/// <c>Team:Gateway:Tailscale:Mode</c> is <c>off</c>, so no test host and no plain install ever spawns <c>tailscale</c>.
/// </summary>
/// <param name="settings">The gateway settings.</param>
/// <param name="listener">The recorded listeners.</param>
/// <param name="cli">The Tailscale CLI.</param>
/// <param name="claim">The claim.</param>
/// <param name="hosts">The dynamic host list.</param>
/// <param name="logger">The logger.</param>
internal sealed class TailscaleRuntime(
    GatewaySettings settings,
    GatewayListener listener,
    ITailscaleCli cli,
    TailscaleServeClaim claim,
    GatewayDynamicHosts hosts,
    ILogger<TailscaleRuntime> logger) : IHostedLifecycleService, IDisposable
{
    private readonly StopSignal stop = new();
    private Task? work;
    private volatile TailscaleStatusPoller? poller;

    /// <summary>The latest Tailscale status and stray Serve routes the poller read, or <see langword="null"/> before the first poll or when Tailscale is off.</summary>
    public TailscaleStatusPoller.Snapshot? Snapshot => this.poller?.Current;

    /// <summary>Whether <c>Tailscale:Mode</c> is <c>serve</c> or <c>funnel</c>.</summary>
    /// <param name="options">The gateway options.</param>
    internal static bool IsOn(GatewayOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return string.Equals(options.Tailscale.Mode, "serve", StringComparison.OrdinalIgnoreCase)
            || string.Equals(options.Tailscale.Mode, "funnel", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Cancels the work and disposes the token source. The host can dispose its services before (or while) it runs
    /// <see cref="StoppingAsync"/>; <see cref="StopSignal"/> makes the two safe in either order.
    /// </summary>
    public void Dispose() => this.stop.Dispose();

    /// <inheritdoc />
    public Task StartedAsync(CancellationToken cancellationToken)
    {
        if (IsOn(settings.Options))
        {
            this.work = Task.Run(() => this.RunAsync(this.stop.Token), CancellationToken.None);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StoppingAsync(CancellationToken cancellationToken)
    {
        this.stop.Stop();
        if (this.work is not null)
        {
            await this.work.ConfigureAwait(false);
        }

        await claim.DisposeAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        GatewayOptions options = settings.Options;
        int? appPort = listener.AppEndpoints.Count > 0 && listener.AppEndpoints[0].Port > 0 ? listener.AppEndpoints[0].Port : null;
        if (listener.ManagedEndpoint is not { } managed)
        {
            logger.LogWarning("Tailscale {Mode} was requested but the managed listener is not bound; remote access over Tailscale is off.", options.Tailscale.Mode);
            return;
        }

        bool funnel = string.Equals(options.Tailscale.Mode, "funnel", StringComparison.OrdinalIgnoreCase);
        TailscaleServeClaim.ClaimRequest request = new(funnel, TailscaleHttpsPort.Resolve(options.Tailscale.HttpsPort, appPort), managed.Port, options.Auth.Mode);
        try
        {
            TailscaleServeClaim.ClaimStatus status = await claim.StartAsync(request, cancellationToken).ConfigureAwait(false);
            if (logger.IsEnabled(LogLevel.Information))
            {
                string port = request.TailscalePort.ToString(System.Globalization.CultureInfo.InvariantCulture);
                string? state = Enum.GetName(status.State);
                string mode = options.Tailscale.Mode;
                logger.LogInformation("Tailscale {Mode} claim on port {Port}: {State}.", mode, port, state);
            }
            TailscaleStatusPoller statusPoller = new(cli, hosts, claim, appPort);
            this.poller = statusPoller;
            await statusPoller.RunAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Shutdown.
        }
    }
}

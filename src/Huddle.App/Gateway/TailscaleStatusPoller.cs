using System.ComponentModel;

namespace Agency.Huddle.App.Gateway;

/// <summary>Every 30 s: status, MagicDNS name (fed to host filtering while the claim runs), owner login, stray routes (Spec 10).</summary>
/// <param name="cli">The CLI.</param>
/// <param name="hosts">The dynamic host list the MagicDNS name is fed to.</param>
/// <param name="claim">The claim whose state gates the feed.</param>
/// <param name="appPort">Huddle's app port, for stray routes; <see langword="null"/> when unknown.</param>
/// <param name="timeProvider">The clock, or <see langword="null"/> for the system clock.</param>
internal sealed class TailscaleStatusPoller(ITailscaleCli cli, GatewayDynamicHosts hosts, TailscaleServeClaim claim, int? appPort, TimeProvider? timeProvider = null)
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(10);
    private static readonly string[] ServeStatusArgs = ["serve", "status", "--json"];

    private readonly ITailscaleCli cli = cli;
    private readonly GatewayDynamicHosts hosts = hosts;
    private readonly TailscaleServeClaim claim = claim;
    private readonly int? appPort = appPort;
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;
    private volatile Snapshot? current;

    /// <summary>What the poller last saw.</summary>
    /// <param name="Status">The node status.</param>
    /// <param name="StrayRoutes">Hand-made routes that proxy to Huddle's app port.</param>
    internal sealed record Snapshot(TailscaleStatus Status, IReadOnlyList<TailscaleServeStatus.Route> StrayRoutes);

    /// <summary>The last snapshot, or <see langword="null"/> before the first refresh.</summary>
    public Snapshot? Current => this.current;

    /// <summary>Reads status and serve status once and updates the host list.</summary>
    /// <param name="cancellationToken">Cancels the refresh.</param>
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        TailscaleStatus status = await TailscaleStatus.ReadAsync(this.cli, this.timeProvider, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<TailscaleServeStatus.Route> strays = [];
        if (status.State != TailscaleState.Missing && this.appPort is int port)
        {
            try
            {
                (int exit, string stdout, _) = await this.cli.RunAsync(ServeStatusArgs, CallTimeout, cancellationToken).ConfigureAwait(false);
                if (exit == 0)
                {
                    strays = TailscaleServeStatus.Parse(stdout).StrayRoutes(port);
                }
            }
            catch (Win32Exception)
            {
                // The CLI vanished between the two calls; the next refresh reports it.
            }
            catch (TimeoutException)
            {
                // Stray routes are advisory; try again at the next refresh.
            }
        }

        this.current = new Snapshot(status, strays);
        TailscaleServeClaim.ClaimRequest? request = this.claim.Request;
        if (status.State == TailscaleState.Running
            && status.DnsName is { } dns
            && request is not null
            && this.claim.Status.State == TailscaleServeClaim.ClaimState.Running)
        {
            this.hosts.SetTailscale([dns, $"{dns}:{request.TailscalePort}"]);
        }
        else
        {
            this.hosts.SetTailscale([]);
        }
    }

    /// <summary>Refreshes at once, then every 30 s until cancelled.</summary>
    /// <param name="cancellationToken">Stops the loop.</param>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await this.RefreshAsync(cancellationToken).ConfigureAwait(false);
            await Task.Delay(Interval, this.timeProvider, cancellationToken).ConfigureAwait(false);
        }
    }
}

namespace Agency.Huddle.App.Gateway;

/// <summary>
/// Every 60 s (Spec 10): prunes stale devices (C-11), the rate limiter, expired sessions and expired pairing requests.
/// The nonce, bootstrap-token and challenge-limiter stores prune themselves on use, and last-seen is written directly by <see cref="SessionRegistry"/>.
/// </summary>
/// <param name="devices">The registry whose stale loopback and bootstrap devices are deleted.</param>
/// <param name="limiter">The auth rate limiter whose spent entries are removed.</param>
/// <param name="sessions">The sessions whose expired ones are removed.</param>
/// <param name="pairing">The pairing requests whose lapsed ones are announced to the Feed.</param>
/// <param name="logger">Where a failed duty is reported.</param>
/// <param name="timeProvider">The clock the interval runs on, or <see langword="null"/> for the system clock.</param>
internal sealed partial class GatewayHousekeeping(
    DeviceRegistry devices,
    AuthRateLimiter limiter,
    SessionRegistry sessions,
    PairingService pairing,
    ILogger<GatewayHousekeeping> logger,
    TimeProvider? timeProvider = null) : BackgroundService
{
    /// <summary>How often <see cref="RunOnce"/> runs.</summary>
    internal static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    private readonly DeviceRegistry devices = devices;
    private readonly AuthRateLimiter limiter = limiter;
    private readonly SessionRegistry sessions = sessions;
    private readonly PairingService pairing = pairing;
    private readonly ILogger<GatewayHousekeeping> logger = logger;
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;

    /// <summary>Runs the duties once per <see cref="Interval"/> until the host stops.</summary>
    /// <param name="stoppingToken">Set when the host is stopping.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(Interval, this.timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                this.RunOnce();
            }
        }
        catch (OperationCanceledException)
        {
            // The host is stopping; leaving the loop is the whole job.
        }
    }

    /// <summary>
    /// Runs the four duties. Each one is isolated, so a failure of one (a locked <c>team.db</c>, say) is logged and does not skip the others or end the loop.
    /// </summary>
    internal void RunOnce()
    {
        this.Guard("prune stale devices", this.PruneDevices);
        this.Guard("prune the rate limiter", this.limiter.Prune);
        this.Guard("prune expired sessions", () => _ = this.sessions.PruneExpired());
        this.Guard("sweep expired pairing requests", this.pairing.Sweep);
    }

    private void PruneDevices()
    {
        int removed = this.devices.PruneStale();
        if (removed > 0)
        {
            LogDevicesPruned(this.logger, removed);
        }
    }

    /// <summary>Logs how many stale loopback or bootstrap devices were deleted (C-11).</summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Removed {Count} stale loopback or bootstrap device(s) that were unseen for 30 days.")]
    private static partial void LogDevicesPruned(ILogger logger, int count);

    private void Guard(string duty, Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            // A supervisor loop must not die: the failure is logged and the next tick tries again.
            this.logger.LogWarning(exception, "Gateway housekeeping could not {Duty}; it will try again.", duty);
        }
    }
}

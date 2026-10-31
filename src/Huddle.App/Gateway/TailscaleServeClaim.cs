using System.ComponentModel;
using System.Globalization;

namespace Agency.Huddle.App.Gateway;

/// <summary>Holds the foreground <c>tailscale serve</c> or <c>funnel</c> child that routes the tailnet to the managed listener (Spec 6.3; D-11).</summary>
/// <param name="cli">The CLI.</param>
/// <param name="timeProvider">The clock, or <see langword="null"/> for the system clock.</param>
internal sealed class TailscaleServeClaim(ITailscaleCli cli, TimeProvider? timeProvider = null) : IAsyncDisposable
{
    private static readonly TimeSpan StartWindow = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan PollGap = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan StableRun = TimeSpan.FromSeconds(60);
    private static readonly string[] ServeStatusArgs = ["serve", "status", "--json"];

    private readonly ITailscaleCli cli = cli;
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;
    private readonly Lock gate = new();
    private readonly CancellationTokenSource lifetime = new();
    private ClaimStatus status = new(ClaimState.Idle);
    private ClaimRequest? request;
    private ITailscaleChild? child;
    private Task? supervisor;
    private bool disposed;

    /// <summary>Where the claim stands.</summary>
    internal enum ClaimState
    {
        /// <summary>Nothing requested yet.</summary>
        Idle,

        /// <summary>The child is starting; the route is not yet shown.</summary>
        Starting,

        /// <summary>The route shows <c>tsPort</c> to the managed listener.</summary>
        Running,

        /// <summary>The child exited unexpectedly and a restart is scheduled.</summary>
        Restarting,

        /// <summary>Tailscale port N belongs to something else; never taken over, never retried.</summary>
        TailscalePortTaken,

        /// <summary>Tailscale printed the link that enables HTTPS or Serve for the tailnet.</summary>
        ServeNeedsEnabling,

        /// <summary>Funnel without password auth: refused (Spec 6.11).</summary>
        FunnelNeedsPassword,

        /// <summary>Funnel on a port other than 443, 8443 or 10000: refused.</summary>
        FunnelPortNotAllowed,

        /// <summary>The route did not appear within 15 s.</summary>
        StartTimedOut,

        /// <summary>The Tailscale CLI is not installed.</summary>
        TailscaleMissing,

        /// <summary>Released at shutdown.</summary>
        Stopped,
    }

    /// <summary>What to hold.</summary>
    /// <param name="Funnel"><see langword="true"/> for Funnel, <see langword="false"/> for Serve.</param>
    /// <param name="TailscalePort">The Tailscale HTTPS port.</param>
    /// <param name="ManagedPort">The port of the managed loopback listener.</param>
    /// <param name="AuthMode">The configured gateway auth mode (Funnel needs <see cref="GatewayAuthMode.Password"/>).</param>
    internal sealed record ClaimRequest(bool Funnel, int TailscalePort, int ManagedPort, GatewayAuthMode AuthMode);

    /// <summary>The claim's state with its detail.</summary>
    /// <param name="State">The state.</param>
    /// <param name="EnableUrl">The enable link when <see cref="ClaimState.ServeNeedsEnabling"/>, else <see langword="null"/>.</param>
    internal sealed record ClaimStatus(ClaimState State, string? EnableUrl = null);

    /// <summary>The current state.</summary>
    public ClaimStatus Status
    {
        get
        {
            lock (this.gate)
            {
                return this.status;
            }
        }
    }

    /// <summary>The request being held, or <see langword="null"/>.</summary>
    public ClaimRequest? Request
    {
        get
        {
            lock (this.gate)
            {
                return this.request;
            }
        }
    }

    /// <summary>Starts the child and returns at the first outcome (running, refused, taken, needs enabling, timed out); the claim then keeps restarting it.</summary>
    /// <param name="request">What to hold.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    public async Task<ClaimStatus> StartAsync(ClaimRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (this.gate)
        {
            this.request = request;
        }

        if (request.Funnel && request.AuthMode != GatewayAuthMode.Password)
        {
            return this.Set(new ClaimStatus(ClaimState.FunnelNeedsPassword));
        }

        if (request.Funnel && !TailscaleHttpsPort.FunnelAllows(request.TailscalePort))
        {
            return this.Set(new ClaimStatus(ClaimState.FunnelPortNotAllowed));
        }

        ClaimStatus? precheck = await this.PrecheckAsync(request, cancellationToken).ConfigureAwait(false);
        if (precheck is not null)
        {
            return this.Set(precheck);
        }

        ClaimStatus? outcome = await this.AttemptAsync(request, cancellationToken).ConfigureAwait(false);
        if (outcome is { State: ClaimState.Running } or null)
        {
            DateTimeOffset? runningSince = outcome is null ? null : this.timeProvider.GetUtcNow();
            ClaimStatus first = outcome ?? new ClaimStatus(ClaimState.Restarting);
            lock (this.gate)
            {
                this.supervisor = this.SuperviseAsync(request, runningSince);
            }

            return this.Set(first);
        }

        return this.Set(outcome);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Task? running;
        ITailscaleChild? current;
        lock (this.gate)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            running = this.supervisor;
            current = this.child;
        }

        await this.lifetime.CancelAsync().ConfigureAwait(false);
        if (current is not null && !current.Exited.IsCompleted)
        {
            await current.KillAsync().ConfigureAwait(false);
        }

        if (running is not null)
        {
            await running.ConfigureAwait(false);
        }

        this.Set(new ClaimStatus(ClaimState.Stopped));
        this.lifetime.Dispose();
    }

    private static string[] CommandLine(ClaimRequest request)
        =>
        [
            request.Funnel ? "funnel" : "serve",
            "--bg=false",
            "--yes",
            $"--https={request.TailscalePort.ToString(CultureInfo.InvariantCulture)}",
            ManagedUrl(request),
        ];

    private static string ManagedUrl(ClaimRequest request) => $"http://127.0.0.1:{request.ManagedPort.ToString(CultureInfo.InvariantCulture)}";

    private static ClaimStatus? ReadLine(string line)
    {
        if (line.Contains("listener already exists for port", StringComparison.Ordinal))
        {
            return new ClaimStatus(ClaimState.TailscalePortTaken);
        }

        int start = line.IndexOf("https://", StringComparison.Ordinal);
        if (start >= 0 && line.Contains("tailscale.com", StringComparison.Ordinal))
        {
            string url = line[start..].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[0];
            return new ClaimStatus(ClaimState.ServeNeedsEnabling, url);
        }

        return null;
    }

    private static async Task<ClaimStatus?> WatchLinesAsync(ITailscaleChild started, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (string line in started.Lines.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                ClaimStatus? found = ReadLine(line);
                if (found is not null)
                {
                    return found;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The attempt ended before the output did.
        }

        return null;
    }

    private ClaimStatus Set(ClaimStatus next)
    {
        lock (this.gate)
        {
            this.status = next;
        }

        return next;
    }

    private async Task<ClaimStatus?> PrecheckAsync(ClaimRequest request, CancellationToken cancellationToken)
    {
        try
        {
            (int exit, string stdout, _) = await this.cli.RunAsync(ServeStatusArgs, CallTimeout, cancellationToken).ConfigureAwait(false);
            if (exit == 0
                && TailscaleServeStatus.Parse(stdout).RouteOn(request.TailscalePort) is { } route
                && !string.Equals(route.Target.TrimEnd('/'), ManagedUrl(request), StringComparison.Ordinal))
            {
                return new ClaimStatus(ClaimState.TailscalePortTaken);
            }
        }
        catch (Win32Exception)
        {
            return new ClaimStatus(ClaimState.TailscaleMissing);
        }
        catch (TimeoutException)
        {
            // The status read is advisory here; the child reports a conflict itself.
        }

        return null;
    }

    /// <summary>Starts one child and waits for its outcome: Running, a terminal status, or <see langword="null"/> when it exited without a recognised reason.</summary>
    private async Task<ClaimStatus?> AttemptAsync(ClaimRequest request, CancellationToken cancellationToken)
    {
        this.Set(new ClaimStatus(ClaimState.Starting));
        ITailscaleChild started;
        try
        {
            started = await this.cli.StartAsync(CommandLine(request), cancellationToken).ConfigureAwait(false);
        }
        catch (Win32Exception)
        {
            return new ClaimStatus(ClaimState.TailscaleMissing);
        }

        lock (this.gate)
        {
            this.child = started;
        }

        using CancellationTokenSource attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, this.lifetime.Token);
        Task<ClaimStatus?> watch = WatchLinesAsync(started, attempt.Token);
        DateTimeOffset deadline = this.timeProvider.GetUtcNow() + StartWindow;
        try
        {
            while (true)
            {
                if (started.Exited.IsCompleted)
                {
                    return await watch.ConfigureAwait(false);
                }

                if (await this.RouteShownAsync(request, attempt.Token).ConfigureAwait(false))
                {
                    return new ClaimStatus(ClaimState.Running);
                }

                if (this.timeProvider.GetUtcNow() >= deadline)
                {
                    await started.KillAsync().ConfigureAwait(false);
                    return new ClaimStatus(ClaimState.StartTimedOut);
                }

                await Task.WhenAny(Task.Delay(PollGap, this.timeProvider, attempt.Token), started.Exited, watch).ConfigureAwait(false);
                if (watch.IsCompleted && await watch.ConfigureAwait(false) is { } terminal)
                {
                    return terminal;
                }
            }
        }
        finally
        {
            await attempt.CancelAsync().ConfigureAwait(false);
        }
    }

    private async Task<bool> RouteShownAsync(ClaimRequest request, CancellationToken cancellationToken)
    {
        try
        {
            (int exit, string stdout, _) = await this.cli.RunAsync(ServeStatusArgs, CallTimeout, cancellationToken).ConfigureAwait(false);
            return exit == 0
                && TailscaleServeStatus.Parse(stdout).RouteOn(request.TailscalePort) is { } route
                && string.Equals(route.Target.TrimEnd('/'), ManagedUrl(request), StringComparison.Ordinal);
        }
        catch (Win32Exception)
        {
            return false;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    private async Task SuperviseAsync(ClaimRequest request, DateTimeOffset? runningSince)
    {
        CancellationToken stop = this.lifetime.Token;
        TimeSpan backoff = TimeSpan.FromSeconds(1);
        DateTimeOffset? since = runningSince;
        try
        {
            while (!stop.IsCancellationRequested)
            {
                ITailscaleChild? current;
                lock (this.gate)
                {
                    current = this.child;
                }

                if (current is not null)
                {
                    await current.Exited.WaitAsync(stop).ConfigureAwait(false);
                }

                if (since is { } began && this.timeProvider.GetUtcNow() - began >= StableRun)
                {
                    backoff = TimeSpan.FromSeconds(1);
                }

                this.Set(new ClaimStatus(ClaimState.Restarting));
                await Task.Delay(backoff, this.timeProvider, stop).ConfigureAwait(false);
                backoff = TimeSpan.FromTicks(Math.Min(backoff.Ticks * 2, MaxBackoff.Ticks));

                ClaimStatus? outcome = await this.AttemptAsync(request, stop).ConfigureAwait(false);
                if (outcome is { State: not ClaimState.Running and not ClaimState.StartTimedOut })
                {
                    this.Set(outcome);
                    return;
                }

                since = outcome is { State: ClaimState.Running } ? this.timeProvider.GetUtcNow() : null;
                this.Set(outcome ?? new ClaimStatus(ClaimState.Restarting));
            }
        }
        catch (OperationCanceledException)
        {
            // Disposed: the release path kills the child.
        }
    }
}

using System.Threading.Channels;
using Agency.Huddle.App.Services;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Helpers.MemoryTidy;

/// <summary>Owns Memory Tidy's passes: runs one at a time on its own task under the host's stopping token, finishes a pass a stop interrupted before the first new one, and is what the timetable, the card and the start-up switch all ask.</summary>
/// <param name="pass">The pass to run.</param>
/// <param name="recovery">Finishes the passes an earlier stop or crash left open.</param>
/// <param name="options">Supplies <c>RunAtStart</c>.</param>
/// <param name="logger">Reports a pass that threw.</param>
/// <param name="timeProvider">The clock the start-up delay runs on; the system clock when <see langword="null"/>.</param>
internal sealed class MemoryTidyRunner(
    IMemoryTidyPass pass,
    IMemoryTidyRecovery recovery,
    IOptions<TeamOptions> options,
    ILogger<MemoryTidyRunner> logger,
    TimeProvider? timeProvider = null) : BackgroundService
{
    /// <summary>How long after start-up <c>RunAtStart</c> waits.</summary>
    internal static readonly TimeSpan RunAtStartDelay = TimeSpan.FromSeconds(30);

    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private readonly Lock gate = new();
    private readonly Channel<Request> requests = Channel.CreateUnbounded<Request>();
    private bool running;
    private bool stopping;

    /// <summary>
    /// A test seam that replaces the thirty-second wait of <c>RunAtStart</c>, so a test ends it by completing a task and never waits real time. Set it
    /// before the runner is started; it is given the delay and the stopping token. Production leaves it <see langword="null"/>.
    /// </summary>
    internal Func<TimeSpan, CancellationToken, Task>? DelayForTests { get; set; }

    /// <summary>Raised when a pass starts or ends, so the card can repaint. May be raised on any thread.</summary>
    public event Action? Changed;

    /// <summary>Whether a pass is running now.</summary>
    public bool IsRunning
    {
        get
        {
            lock (this.gate)
            {
                return this.running;
            }
        }
    }

    /// <summary>Asks for a pass now.</summary>
    /// <param name="start">What is asking.</param>
    /// <returns>A task that ends with how the pass ended, or <see langword="null"/> when a pass is already running or the host is stopping.</returns>
    public Task<MemoryTidyPassEnd>? TryStart(MemoryTidyStart start)
    {
        lock (this.gate)
        {
            if (this.running || this.stopping)
            {
#pragma warning disable S4586, MA0022 // The specified contract: a null answer means a pass is running or the host is stopping, which the caller shows as "A pass is running".
                return null;
#pragma warning restore S4586, MA0022
            }

            Request request = new(start, new TaskCompletionSource<MemoryTidyPassEnd>(TaskCreationOptions.RunContinuationsAsynchronously));
            _ = this.requests.Writer.TryWrite(request);
            this.running = true;
            return request.Done.Task;
        }
    }

    /// <inheritdoc />
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        lock (this.gate)
        {
            this.stopping = true;
        }

        return base.StopAsync(cancellationToken);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Task atStart = options.Value.Memory.Tidy.RunAtStart ? this.StartAfterDelayAsync(stoppingToken) : Task.CompletedTask;
        try
        {
            await this.RecoverAsync(stoppingToken).ConfigureAwait(false);
            await this.ServeAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping; whatever is still queued is answered below.
        }
        finally
        {
            await atStart.ConfigureAwait(false);
            this.AnswerQueuedAsStopped();
        }
    }

    private async Task StartAfterDelayAsync(CancellationToken stoppingToken)
    {
        try
        {
            await (this.DelayForTests?.Invoke(RunAtStartDelay, stoppingToken) ?? Task.Delay(RunAtStartDelay, this.clock, stoppingToken)).ConfigureAwait(false);
            _ = this.TryStart(MemoryTidyStart.RunAtStart);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Stopped before the delay ran out: no pass is started.
        }
    }

    private async Task RecoverAsync(CancellationToken stoppingToken)
    {
        try
        {
            await recovery.RecoverAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Recovery is best effort: an unfinished pass is looked at again at the next start, and the runner must still serve requests.
            logger.LogError(ex, "Memory Tidy recovery failed.");
        }
    }

    private async Task ServeAsync(CancellationToken stoppingToken)
    {
        await foreach (Request request in this.requests.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            this.RaiseChanged();
            MemoryTidyPassEnd end = await this.RunOneAsync(request.Start, stoppingToken).ConfigureAwait(false);
            lock (this.gate)
            {
                this.running = false;
            }

            this.RaiseChanged();
            request.Done.SetResult(end);
        }
    }

    private async Task<MemoryTidyPassEnd> RunOneAsync(MemoryTidyStart start, CancellationToken stoppingToken)
    {
        try
        {
            return await pass.RunAsync(start, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return MemoryTidyPassEnd.Stopped;
        }
        catch (Exception ex)
        {
            // A pass that throws must not end the runner: log it, finish its list from the journal, and take the next request.
            logger.LogError(ex, "A Memory Tidy pass threw.");
            await this.RecoverAfterThrowAsync().ConfigureAwait(false);
            return MemoryTidyPassEnd.Threw;
        }
    }

    private async Task RecoverAfterThrowAsync()
    {
        try
        {
            // Not the stopping token (the throw may have come with it) and not unbounded: its own ten seconds, as the pass's finally has.
            using CancellationTokenSource finishing = new(TimeSpan.FromSeconds(10), this.clock);
            await recovery.RecoverAsync(finishing.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // The list is finished at the next start instead.
            logger.LogError(ex, "Memory Tidy could not finish the list of a pass that threw.");
        }
    }

    private void AnswerQueuedAsStopped()
    {
        lock (this.gate)
        {
            this.stopping = true;
            this.running = false;
        }

        while (this.requests.Reader.TryRead(out Request? request))
        {
            _ = request.Done.TrySetResult(MemoryTidyPassEnd.Stopped);
        }
    }

    private void RaiseChanged() => EventRaise.Isolated(this.Changed, ex => logger.LogWarning(ex, "A Memory Tidy Changed subscriber threw."));

    private sealed record Request(MemoryTidyStart Start, TaskCompletionSource<MemoryTidyPassEnd> Done);
}

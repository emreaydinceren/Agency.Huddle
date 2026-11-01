using Agency.Huddle.App.Data;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Helpers;

/// <summary>
/// Owns the queue of Helper judgements (Helpers Spec §6.3). It knows no Helper's input: it waits per key, applies the
/// enabled and Adapters-on gates, limits concurrent runs, hands the call to the Helper's <see cref="IHelperInvoker"/>, and
/// records the result on the Helper's card. It never throws to a caller and never lets a Helper take it down.
/// </summary>
internal sealed class HelperDispatcher : IHostedService, IDisposable
{
    /// <summary>The wait for further saves of one key never restarts past this many times the Helper's wait.</summary>
    internal const int MaxWaitMultiple = 6;

    private readonly Lock gate = new();
    private readonly Dictionary<string, Pending> pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IHelperInvoker> invokers = new(StringComparer.OrdinalIgnoreCase);
    private readonly HelperSettingsResolver settings;
    private readonly HelperStatusStore status;
    private readonly IOptions<TeamOptions> options;
    private readonly ILogger<HelperDispatcher> logger;
    private readonly TimeProvider clock;
    private readonly HelperFeed? feed;
    private readonly SemaphoreSlim concurrency;
    private readonly CancellationTokenSource stopping = new();
    private bool stopped;
    private bool disposed;
    private readonly HashSet<Task> running = [];

    /// <summary>Creates the dispatcher over the registered invokers.</summary>
    /// <param name="invokers">One per registered Helper.</param>
    /// <param name="settings">Resolves each Helper's settings at each fire.</param>
    /// <param name="status">Where the card's last run and last error live.</param>
    /// <param name="options">The bound options: <c>Acp.Enabled</c> and <c>HelperConcurrency</c>.</param>
    /// <param name="logger">Records each failure.</param>
    /// <param name="timeProvider">The clock; <see cref="TimeProvider.System"/> when <see langword="null"/>.</param>
    /// <param name="feed">Tells the Feed when a Helper pauses or keeps failing; <see langword="null"/> reports nothing.</param>
    public HelperDispatcher(
        IEnumerable<IHelperInvoker> invokers,
        HelperSettingsResolver settings,
        HelperStatusStore status,
        IOptions<TeamOptions> options,
        ILogger<HelperDispatcher> logger,
        TimeProvider? timeProvider = null,
        HelperFeed? feed = null)
    {
        ArgumentNullException.ThrowIfNull(invokers);
        foreach (IHelperInvoker invoker in invokers)
        {
            this.invokers.Add(invoker.Helper.Id.Value, invoker);
        }

        this.settings = settings;
        this.status = status;
        this.options = options;
        this.logger = logger;
        this.clock = timeProvider ?? TimeProvider.System;
        this.feed = feed;
        this.concurrency = new SemaphoreSlim(Math.Max(1, options.Value.HelperConcurrency));
    }

    /// <summary>Raised after a judgement ends and its result is stored, so the card can repaint. May be raised on any thread.</summary>
    public event Action? StatusChanged;

    /// <summary>Queues one judgement. Returns at once; safe to call from a synchronous event handler.</summary>
    /// <typeparam name="TInput">The Helper's input type.</typeparam>
    /// <param name="helperId">A registered Helper.</param>
    /// <param name="key">What the judgement is about, such as a Task id. One run per key at a time.</param>
    /// <param name="input">Everything the Helper needs, captured now.</param>
    public void Enqueue<TInput>(HelperId helperId, string key, TInput input)
        where TInput : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(input);
        if (!this.invokers.TryGetValue(helperId.Value, out IHelperInvoker? invoker))
        {
            throw new InvalidOperationException($"No Helper is registered with the id '{helperId.Value}'.");
        }

        int waitSeconds = this.settings.For(invoker.Helper).WaitSeconds;
        string slot = $"{helperId.Value}\n{key}";
        lock (this.gate)
        {
            if (this.stopped)
            {
                return;
            }

            if (!this.pending.TryGetValue(slot, out Pending? entry))
            {
                entry = new Pending(invoker, key);
                this.pending.Add(slot, entry);
            }

            if (entry.Running)
            {
                // A run for this key is still going: hold the newest input and wait again once it ends.
                entry.Next = input;
                entry.NextWaitSeconds = waitSeconds;
                return;
            }

            entry.Input = input;
            this.Wait(slot, entry, waitSeconds);
        }
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        Task[] inFlight;
        lock (this.gate)
        {
            // A host that stops a dispatcher the container already disposed has nothing left to stop.
            if (this.disposed)
            {
                return;
            }

            // Set under the gate so no Enqueue or timer starts a run after the snapshot below.
            this.stopped = true;
            foreach (Pending entry in this.pending.Values)
            {
                entry.Timer?.Dispose();
                entry.Timer = null;
                entry.Input = null;
                entry.Next = null;
            }

            inFlight = [.. this.running];
        }

        this.CancelUnlessDisposed();
        try
        {
            await Task.WhenAll(inFlight).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The runs were cancelled with the dispatcher; a run that ends by cancellation is the expected shutdown.
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (this.gate)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            this.stopped = true;
            foreach (Pending entry in this.pending.Values)
            {
                entry.Timer?.Dispose();
            }

            // Cancelled before it is disposed, and under the gate CancelUnlessDisposed takes, so a StopAsync racing
            // this Dispose finds either a live source to cancel or one this has already cancelled.
            this.stopping.Cancel();
            this.stopping.Dispose();
        }

        this.concurrency.Dispose();
    }

    /// <summary>
    /// Cancels the dispatcher's runs under <see cref="gate"/>. Under WebApplicationFactory the factory's stop and the
    /// host's Dispose run on two threads, so a Dispose can land after StopAsync's own check; it cancels the source
    /// before disposing it, so there is nothing left to do then (the TeamRoomReconciler race of CI run 1260).
    /// </summary>
    private void CancelUnlessDisposed()
    {
        lock (this.gate)
        {
            if (!this.disposed)
            {
                this.stopping.Cancel();
            }
        }
    }

    /// <summary>Starts or restarts the key's wait, never past <see cref="MaxWaitMultiple"/> times the Helper's wait from the first save. Called with the gate held.</summary>
    private void Wait(string slot, Pending entry, int waitSeconds)
    {
        DateTimeOffset now = this.clock.GetUtcNow();
        if (entry.Timer is null)
        {
            entry.FirstAt = now;
        }

        TimeSpan wait = TimeSpan.FromSeconds(waitSeconds);
        TimeSpan untilCap = (entry.FirstAt + (wait * MaxWaitMultiple)) - now;
        TimeSpan due = wait < untilCap ? wait : untilCap;
        if (due < TimeSpan.Zero)
        {
            due = TimeSpan.Zero;
        }

        if (entry.Timer is null)
        {
            entry.Timer = this.clock.CreateTimer(_ => this.Fire(slot), null, due, Timeout.InfiniteTimeSpan);
        }
        else
        {
            _ = entry.Timer.Change(due, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>The timer fired: take the input and run it on the thread pool.</summary>
    private void Fire(string slot)
    {
        Task task;
        lock (this.gate)
        {
            if (this.stopped || !this.pending.TryGetValue(slot, out Pending? found) || found.Input is null || found.Running)
            {
                return;
            }

            Pending entry = found;
            object input = found.Input;
            entry.Input = null;
            entry.Timer?.Dispose();
            entry.Timer = null;
            entry.Running = true;

            // The run joins the set in the same critical section that took the input, so StopAsync's snapshot cannot miss it.
            task = Task.Run(() => this.RunAsync(slot, entry, input), CancellationToken.None);
            _ = this.running.Add(task);
        }

        _ = task.ContinueWith(
            finished =>
            {
                lock (this.gate)
                {
                    _ = this.running.Remove(finished);
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    /// <summary>Runs one judgement, records its result, and starts the next wait when a newer input arrived meanwhile.</summary>
    private async Task RunAsync(string slot, Pending entry, object input)
    {
        try
        {
            await this.JudgeAsync(entry, input).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (this.stopping.IsCancellationRequested)
        {
            // Shutdown cancelled the run; the pending input is dropped on purpose.
        }
        catch (Exception ex)
        {
            // A Helper must never take the dispatcher down (Policy 4): any failure from the stores or ApplyAsync is logged and
            // shown on the card, and the Task stays as the Agent saved it.
            this.logger.LogWarning(ex, "Helper {HelperId} failed for {Key}.", entry.Invoker.Helper.Id.Value, entry.Key);
            this.TryRecordFailure(entry.Invoker.Helper.Id, ex.Message);
        }
        finally
        {
            lock (this.gate)
            {
                entry.Running = false;
                if (entry.Next is not null && !this.stopping.IsCancellationRequested)
                {
                    entry.Input = entry.Next;
                    entry.Next = null;
                    this.Wait(slot, entry, entry.NextWaitSeconds);
                }
                else
                {
                    _ = this.pending.Remove(slot);
                }
            }
        }
    }

    /// <summary>Applies the gates, runs the Helper under the concurrency limit, and stores the result.</summary>
    private async Task JudgeAsync(Pending entry, object input)
    {
        IHelperAgent helper = entry.Invoker.Helper;
        HelperSettings effective = this.settings.For(helper);
        if (!effective.Enabled || !this.options.Value.Acp.Enabled)
        {
            return;
        }

        CancellationToken ct = this.stopping.Token;
        await this.concurrency.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            HelperRunReport report = await entry.Invoker.ExecuteAsync(entry.Key, input, effective, ct).ConfigureAwait(false);
            DateTimeOffset at = this.clock.GetUtcNow();
            switch (report.Kind)
            {
                case HelperRunKind.Applied:
                    this.status.RecordSuccess(helper.Id, at, report.Summary ?? "Done");
                    this.feed?.Ran(helper, HelperRunKind.Applied, this.status.Get(helper.Id));
                    break;
                case HelperRunKind.Failed:
                    HelperStatus failed = this.status.RecordFailure(helper.Id, at, report.Error ?? "The Helper failed.");
                    this.feed?.Ran(helper, HelperRunKind.Failed, failed);
                    break;
                case HelperRunKind.OverBudget:
                    this.feed?.Paused(helper, effective);
                    break;
                default:
                    break;
            }
        }
        finally
        {
            _ = this.concurrency.Release();
        }

        this.StatusChanged?.Invoke();
    }

    /// <summary>Stores a failure without letting a store error escape the fire path.</summary>
    private void TryRecordFailure(HelperId id, string message)
    {
        try
        {
            HelperStatus failed = this.status.RecordFailure(id, this.clock.GetUtcNow(), message);
            if (this.feed is not null && this.invokers.TryGetValue(id.Value, out IHelperInvoker? invoker))
            {
                this.feed.Ran(invoker.Helper, HelperRunKind.Failed, failed);
            }

            this.StatusChanged?.Invoke();
        }
        catch (Exception ex)
        {
            // The store itself failed (a locked database, say): there is nowhere left to report to, so log it and carry on.
            this.logger.LogWarning(ex, "Helper {HelperId} could not record its failure.", id.Value);
        }
    }

    /// <summary>One key's wait and run state. Touched only with the dispatcher's gate held.</summary>
    private sealed class Pending(IHelperInvoker invoker, string key)
    {
        public IHelperInvoker Invoker { get; } = invoker;

        public string Key { get; } = key;

        public object? Input { get; set; }

        public object? Next { get; set; }

        public int NextWaitSeconds { get; set; }

        public ITimer? Timer { get; set; }

        public DateTimeOffset FirstAt { get; set; }

        public bool Running { get; set; }
    }
}

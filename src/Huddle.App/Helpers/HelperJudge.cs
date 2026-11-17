using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Helpers;

/// <summary>
/// Runs one Helper judgement now: the enabled and Adapters-on gates, the concurrency limit (<c>Team:HelperConcurrency</c>), the
/// card status and the Feed health items. The dispatcher's queue and Memory Tidy's pass both call it, so there is one limit, not
/// two. It never throws for a failed call.
/// </summary>
internal sealed class HelperJudge : IDisposable
{
    private readonly Dictionary<string, IHelperInvoker> invokers = new(StringComparer.OrdinalIgnoreCase);
    private readonly HelperSettingsResolver settings;
    private readonly HelperStatusStore status;
    private readonly IOptions<TeamOptions> options;
    private readonly ILogger<HelperJudge> logger;
    private readonly TimeProvider clock;
    private readonly HelperFeed? feed;
    private readonly SemaphoreSlim concurrency;

    /// <summary>Creates the judge over the registered invokers.</summary>
    /// <param name="invokers">One per registered Helper.</param>
    /// <param name="settings">Resolves each Helper's settings at each judgement.</param>
    /// <param name="status">Where the card's last run and last error live.</param>
    /// <param name="options">The bound options: <c>Acp.Enabled</c> and <c>HelperConcurrency</c>.</param>
    /// <param name="logger">Records each failure.</param>
    /// <param name="timeProvider">The clock; <see cref="TimeProvider.System"/> when <see langword="null"/>.</param>
    /// <param name="feed">Tells the Feed when a Helper pauses or keeps failing; <see langword="null"/> reports nothing.</param>
    public HelperJudge(
        IEnumerable<IHelperInvoker> invokers,
        HelperSettingsResolver settings,
        HelperStatusStore status,
        IOptions<TeamOptions> options,
        ILogger<HelperJudge> logger,
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

    /// <summary>Judges one input and waits for the result.</summary>
    /// <param name="helperId">A registered Helper.</param>
    /// <param name="key">What the judgement is about, such as a Task id.</param>
    /// <param name="input">Everything the Helper needs.</param>
    /// <param name="cancellationToken">Cancels the wait for a free slot and the judgement; only this cancellation is rethrown.</param>
    /// <returns>What the run came to, or <see langword="null"/> when the Helper or Adapters are off.</returns>
    /// <exception cref="InvalidOperationException">No Helper is registered with <paramref name="helperId"/>.</exception>
    public async Task<HelperRunReport?> JudgeAsync(HelperId helperId, string key, object input, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(input);
        if (!this.invokers.TryGetValue(helperId.Value, out IHelperInvoker? invoker))
        {
            throw new InvalidOperationException($"No Helper is registered with the id '{helperId.Value}'.");
        }

        try
        {
            return await this.RunGatedAsync(invoker, key, input, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // A Helper must never take its caller down (Policy 4): any failure from the stores or ApplyAsync is logged and shown
            // on the card, and the Task stays as the Agent saved it.
            this.logger.LogWarning(ex, "Helper {HelperId} failed for {Key}.", helperId.Value, key);
            this.TryRecordFailure(invoker.Helper, ex.Message);
            return new HelperRunReport(HelperRunKind.Failed, Error: ex.Message, CallRan: false);
        }
    }

    /// <inheritdoc/>
    public void Dispose() => this.concurrency.Dispose();

    /// <summary>Applies the gates, runs the Helper under the concurrency limit, stores the result and raises the event.</summary>
    private async Task<HelperRunReport?> RunGatedAsync(IHelperInvoker invoker, string key, object input, CancellationToken cancellationToken)
    {
        IHelperAgent helper = invoker.Helper;
        HelperSettings effective = this.settings.For(helper);
        if (!effective.Enabled || !this.options.Value.Acp.Enabled)
        {
            return null;
        }

        HelperRunReport report;
        await this.concurrency.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            report = await invoker.ExecuteAsync(key, input, effective, cancellationToken).ConfigureAwait(false);
            this.Record(helper, effective, report);
        }
        finally
        {
            _ = this.concurrency.Release();
        }

        this.RaiseStatusChanged();
        return report;
    }

    /// <summary>Stores the result on the card and tells the Feed.</summary>
    private void Record(IHelperAgent helper, HelperSettings effective, HelperRunReport report)
    {
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

    /// <summary>Stores a failure without letting a store error escape.</summary>
    private void TryRecordFailure(IHelperAgent helper, string message)
    {
        try
        {
            HelperStatus failed = this.status.RecordFailure(helper.Id, this.clock.GetUtcNow(), message);
            this.feed?.Ran(helper, HelperRunKind.Failed, failed);
            this.RaiseStatusChanged();
        }
        catch (Exception ex)
        {
            // The store itself failed (a locked database, say): there is nowhere left to report to, so log it and carry on.
            this.logger.LogWarning(ex, "Helper {HelperId} could not record its failure.", helper.Id.Value);
        }
    }

    /// <summary>Raises <see cref="StatusChanged"/> so one throwing subscriber neither skips the others nor fails the judgement.</summary>
    private void RaiseStatusChanged() =>
        EventRaise.Isolated(this.StatusChanged, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.StatusChanged)));
}

using System.Diagnostics.CodeAnalysis;

namespace Agency.Huddle.App.Helpers;

/// <summary>
/// The typed half of the dispatcher's work for one Helper (Helpers Spec §6.3): a per-Helper cache of <c>Ok</c> Verdicts
/// by content, then the runner, then <c>ApplyAsync</c>. A cache hit applies without a call and spends nothing.
/// </summary>
/// <typeparam name="TInput">The Helper's input.</typeparam>
/// <typeparam name="TVerdict">The Helper's Verdict.</typeparam>
/// <param name="helper">The Helper.</param>
/// <param name="runner">Makes the call.</param>
/// <param name="logger">Records an unreadable answer or a failed call with the Helper id and key.</param>
internal sealed class HelperInvoker<TInput, TVerdict>(IHelperAgent<TInput, TVerdict> helper, HelperRunner runner, ILogger logger) : IHelperInvoker
    where TVerdict : class
{
    /// <summary>How many fingerprints the cache keeps.</summary>
    internal const int CacheSize = 1000;

    private readonly Lock gate = new();
    private readonly Dictionary<string, TVerdict> cache = new(StringComparer.Ordinal);
    private readonly Queue<string> order = new();

    /// <inheritdoc/>
    public IHelperAgent Helper => helper;

    /// <inheritdoc/>
    public async Task<HelperRunReport> ExecuteAsync(string key, object input, HelperSettings settings, CancellationToken cancellationToken)
    {
        if (input is not TInput typed)
        {
            throw new ArgumentException($"Helper {helper.Id.Value} takes {typeof(TInput).Name}, not {input.GetType().Name}.", nameof(input));
        }

        string fingerprint = helper.Fingerprint(typed);
        bool caches = helper.CachesVerdicts;
        if (caches && this.TryGetCached(fingerprint, out TVerdict? cached))
        {
            return await this.ApplyAsync(key, typed, cached, false, cancellationToken).ConfigureAwait(false);
        }

        HelperOutcome<TVerdict> outcome = await runner.RunAsync(helper, settings, typed, cancellationToken).ConfigureAwait(false);
        switch (outcome)
        {
            case HelperOutcome<TVerdict>.Ok ok:
                if (caches)
                {
                    this.Remember(fingerprint, ok.Verdict);
                }

                return await this.ApplyAsync(key, typed, ok.Verdict, true, cancellationToken).ConfigureAwait(false);
            case HelperOutcome<TVerdict>.Unparseable unparseable:
                logger.LogWarning("Helper {HelperId} gave an unreadable answer for {Key}: {Problem}. Text: {Text}", helper.Id.Value, key, unparseable.Problem, unparseable.Text);
                return new HelperRunReport(HelperRunKind.Failed, Error: $"The answer wasn't the JSON the prompt asks for ({unparseable.Problem})");
            case HelperOutcome<TVerdict>.Failed failed:
                logger.LogWarning("Helper {HelperId} failed for {Key}: {Reason}", helper.Id.Value, key, failed.Reason);
                return new HelperRunReport(HelperRunKind.Failed, Error: failed.Reason, CallRan: failed.Started);
            default:
                return HelperRunReport.OverBudget;
        }
    }

    /// <summary>Applies a Verdict and maps what the Helper did to a report.</summary>
    private async Task<HelperRunReport> ApplyAsync(string key, TInput input, TVerdict verdict, bool callRan, CancellationToken cancellationToken)
    {
        HelperApplied applied = await helper.ApplyAsync(key, input, verdict, cancellationToken).ConfigureAwait(false);
        return applied switch
        {
            HelperApplied.Changed changed => new HelperRunReport(HelperRunKind.Applied, changed.Summary, CallRan: callRan),
            HelperApplied.Dropped dropped => new HelperRunReport(HelperRunKind.Applied, $"Dropped a stale verdict for {key}: {dropped.Why}", CallRan: callRan),
            HelperApplied.Refused refused => new HelperRunReport(HelperRunKind.Failed, Error: refused.Message, CallRan: callRan),
            _ => new HelperRunReport(HelperRunKind.Applied, $"Judged {key}: no change", CallRan: callRan),
        };
    }

    /// <summary>Looks a fingerprint up.</summary>
    private bool TryGetCached(string fingerprint, [MaybeNullWhen(false)] out TVerdict verdict)
    {
        lock (this.gate)
        {
            return this.cache.TryGetValue(fingerprint, out verdict);
        }
    }

    /// <summary>Stores a Verdict, keeping the most recent <see cref="CacheSize"/> fingerprints.</summary>
    private void Remember(string fingerprint, TVerdict verdict)
    {
        lock (this.gate)
        {
            if (this.cache.TryAdd(fingerprint, verdict))
            {
                this.order.Enqueue(fingerprint);
            }
            else
            {
                this.cache[fingerprint] = verdict;
            }

            while (this.order.Count > CacheSize)
            {
                _ = this.cache.Remove(this.order.Dequeue());
            }
        }
    }
}

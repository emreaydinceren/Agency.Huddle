using Agency.Huddle.App.Search;

namespace Agency.Huddle.Tests.Search;

/// <summary>
/// A test double of <see cref="ISearchProvider"/> whose every call stays pending until the test completes
/// or faults it. It deliberately ignores the call's token, so a test can finish a call after its search was
/// cancelled and prove the orchestrator drops the late result (spec §7.3 step 6).
/// </summary>
internal sealed class GatedSearchProvider(SearchFilter group, bool isExpensive) : ISearchProvider
{
    private readonly Lock gate = new();
    private readonly List<GatedCall> calls = [];
    private readonly List<(int Count, TaskCompletionSource Signal)> waiters = [];

    /// <inheritdoc/>
    public SearchFilter Group { get; } = group;

    /// <inheritdoc/>
    public bool IsExpensive { get; } = isExpensive;

    /// <summary>Every call received so far, in order.</summary>
    public IReadOnlyList<GatedCall> Calls
    {
        get
        {
            lock (this.gate)
            {
                return [.. this.calls];
            }
        }
    }

    /// <inheritdoc/>
    public Task<SearchResult> SearchAsync(SearchQuery query, CancellationToken ct)
    {
        GatedCall call = new(query, ct);
        List<TaskCompletionSource> ready = [];
        lock (this.gate)
        {
            this.calls.Add(call);
            for (int i = this.waiters.Count - 1; i >= 0; i--)
            {
                if (this.waiters[i].Count <= this.calls.Count)
                {
                    ready.Add(this.waiters[i].Signal);
                    this.waiters.RemoveAt(i);
                }
            }
        }

        foreach (TaskCompletionSource signal in ready)
        {
            signal.TrySetResult();
        }

        return call.Completion.Task;
    }

    /// <summary>Completes once at least <paramref name="count"/> calls have arrived, and returns every call.</summary>
    /// <param name="count">The number of calls to wait for.</param>
    /// <param name="ct">Fails the wait when the test is cancelled.</param>
    /// <returns>The calls received so far.</returns>
    public async Task<IReadOnlyList<GatedCall>> WaitForCallsAsync(int count, CancellationToken ct)
    {
        TaskCompletionSource signal = new(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (this.gate)
        {
            if (this.calls.Count >= count)
            {
                signal.TrySetResult();
            }
            else
            {
                this.waiters.Add((count, signal));
            }
        }

        await signal.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
        return this.Calls;
    }
}

/// <summary>One call a <see cref="GatedSearchProvider"/> received; the test decides how it ends.</summary>
/// <param name="Query">The query the orchestrator passed.</param>
/// <param name="Token">The token the orchestrator passed.</param>
internal sealed record GatedCall(SearchQuery Query, CancellationToken Token)
{
    /// <summary>The pending result the provider returned.</summary>
    public TaskCompletionSource<SearchResult> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes the call with <paramref name="result"/>.</summary>
    /// <param name="result">The batch to return; an empty one when omitted.</param>
    public void Complete(SearchResult? result = null) => this.Completion.TrySetResult(result ?? SearchResult.Empty);

    /// <summary>Faults the call with <paramref name="exception"/>.</summary>
    /// <param name="exception">The failure to throw into the orchestrator.</param>
    public void Fault(Exception exception) => this.Completion.TrySetException(exception);
}

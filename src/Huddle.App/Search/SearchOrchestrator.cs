namespace Agency.Huddle.App.Search;

/// <summary>
/// Runs one Global Search at a time (spec §7.3): fans the parsed text out to the providers the filter selects,
/// ranks and raises each batch as it completes (D-17), runs the Archived pass after every Active call (D-19),
/// drops the result of a replaced search, and turns one provider's failure into that group's failure (D-20).
/// Scoped: one per circuit. <see cref="GroupUpdated"/> is raised from a pool thread; the subscriber marshals.
/// </summary>
/// <param name="providers">Every registered provider.</param>
/// <param name="logger">Receives a provider's failure.</param>
/// <param name="timeProvider">The clock the debounce waits on; the system clock when null.</param>
internal sealed class SearchOrchestrator(
    IEnumerable<ISearchProvider> providers,
    ILogger<SearchOrchestrator> logger,
    TimeProvider? timeProvider = null) : IDisposable
{
    private readonly ISearchProvider[] registered = [.. providers];
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private readonly Lock gate = new();
    private CancellationTokenSource? current;
    private long searchId;
    private bool disposed;

    /// <summary>Raised once per provider per pass; an update whose <see cref="SearchGroupUpdate.SearchId"/> is not current is stale.</summary>
    public event Action<SearchGroupUpdate>? GroupUpdated;

    /// <summary>The id of the newest search, including one that was cancelled by an unsearchable parse.</summary>
    public long CurrentSearchId
    {
        get
        {
            lock (this.gate)
            {
                return this.searchId;
            }
        }
    }

    /// <summary>
    /// Cancels the search in flight and, when <paramref name="parse"/> is searchable, runs the new one. Never throws;
    /// completes when both passes have ended or the search was cancelled.
    /// </summary>
    /// <param name="parse">The box text as the parser read it.</param>
    /// <param name="openTeam">The Team of the focused Tab, or null.</param>
    /// <param name="purpose">Why the search runs: the box uses <see cref="SearchPurpose.Box"/>; the Composer's picker uses <see cref="SearchPurpose.Reference"/> (D-4).</param>
    /// <returns>A task that ends with the search.</returns>
    public Task SearchAsync(SearchParse parse, string? openTeam, SearchPurpose purpose = SearchPurpose.Box)
    {
        ArgumentNullException.ThrowIfNull(parse);
        CancellationToken ct;
        long id;
        lock (this.gate)
        {
            if (this.disposed)
            {
                return Task.CompletedTask;
            }

            this.CancelCurrent();
            id = ++this.searchId;
            if (!parse.IsSearchable)
            {
                return Task.CompletedTask;
            }

            this.current = new CancellationTokenSource();
            ct = this.current.Token;
        }

        return this.RunAsync(parse, openTeam, id, purpose, ct);
    }

    /// <summary>Cancels the search in flight; its late results are dropped.</summary>
    public void Cancel()
    {
        lock (this.gate)
        {
            this.CancelCurrent();
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
            this.CancelCurrent();
        }
    }

    private void CancelCurrent()
    {
        if (this.current is null)
        {
            return;
        }

        this.current.Cancel();
        this.current.Dispose();
        this.current = null;
    }

    private async Task RunAsync(SearchParse parse, string? openTeam, long id, SearchPurpose purpose, CancellationToken ct)
    {
        int maxHits = parse.Filter == SearchFilter.All ? SearchLimits.GroupCap : SearchLimits.SingleFilterCap;
        ISearchProvider[] selected = [.. this.registered.Where(provider => parse.Filter == SearchFilter.All || provider.Group == parse.Filter)];
        foreach (SearchScope scope in new[] { SearchScope.Active, SearchScope.Archived })
        {
            List<Task> pass = [];
            foreach (ISearchProvider provider in selected)
            {
                // The picker puts the Room's Members first and cuts to five itself (spec §5.3), so its Teammates
                // group is fetched at the single-filter cap: a Member the ranker listed late is not cut away here.
                int cap = purpose == SearchPurpose.Reference && provider.Group == SearchFilter.Teammates ? SearchLimits.SingleFilterCap : maxHits;
                SearchQuery query = new(parse.Phrase, parse.Filter, openTeam, scope, cap, purpose);
                pass.Add(this.RunProviderAsync(provider, query, id, scope == SearchScope.Active && provider.IsExpensive, ct));
            }

            await Task.WhenAll(pass);
            if (ct.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private async Task RunProviderAsync(ISearchProvider provider, SearchQuery query, long id, bool debounce, CancellationToken ct)
    {
        SearchGroupState state;
        try
        {
            if (debounce)
            {
                await Task.Delay(SearchLimits.Debounce, this.clock, ct);
            }

            // WaitAsync so a provider that ignores its token cannot keep a cancelled search alive.
            SearchResult result = await provider.SearchAsync(query, ct).WaitAsync(ct);
            state = new SearchGroupDone(SearchResultRanker.Rank(result, query));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // This search was replaced or closed; its result is dropped, never raised (spec §7.3 step 6).
            return;
        }
        catch (Exception ex)
        {
            // The supervisor that must not die: one provider failing empties only its own group (D-20).
#pragma warning disable HAA0601 // Boxing a value-type log argument is the cost of ILogger params object[]; accepted together with HAA0101.
            logger.LogWarning(ex, "Search provider {Group} failed.", provider.Group);
#pragma warning restore HAA0601
            state = new SearchGroupFailed();
        }

        if (!ct.IsCancellationRequested)
        {
            this.Raise(new SearchGroupUpdate(id, provider.Group, query.Scope, state));
        }
    }

    private void Raise(SearchGroupUpdate update)
    {
        try
        {
            this.GroupUpdated?.Invoke(update);
        }
        catch (Exception ex)
        {
            // A subscriber's failure must not end the search or hide the other groups.
#pragma warning disable HAA0601 // Boxing a value-type log argument is the cost of ILogger params object[]; accepted together with HAA0101.
            logger.LogWarning(ex, "A search update subscriber failed for {Group}.", update.Group);
#pragma warning restore HAA0601
        }
    }
}

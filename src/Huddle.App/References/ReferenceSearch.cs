using Agency.Huddle.App.Search;

namespace Agency.Huddle.App.References;

/// <summary>
/// The picker's own search (spec §5.2, D-5): it owns a <see cref="SearchOrchestrator"/> of its own, so a keystroke in
/// the Composer never cancels the search the Human started in the Top Bar, nor the reverse. It runs every search
/// under <see cref="SearchPurpose.Reference"/>. Transient: never scoped, so two Composers never share one.
/// </summary>
/// <param name="providers">Every registered provider; the same five the search box uses.</param>
/// <param name="loggerFactory">Creates the orchestrator's logger, which receives a provider's failure.</param>
/// <param name="timeProvider">The clock the debounce waits on; the system clock when null.</param>
internal sealed class ReferenceSearch(IEnumerable<ISearchProvider> providers, ILoggerFactory loggerFactory, TimeProvider? timeProvider = null) : IDisposable
{
    private readonly SearchOrchestrator orchestrator = new(providers, loggerFactory.CreateLogger<SearchOrchestrator>(), timeProvider);
    private bool disposed;

    /// <summary>Raised once per provider per pass, from a pool thread; the subscriber marshals.</summary>
    public event Action<SearchGroupUpdate>? GroupUpdated
    {
        add => this.orchestrator.GroupUpdated += value;
        remove => this.orchestrator.GroupUpdated -= value;
    }

    /// <summary>The id of the newest picker search.</summary>
    public long CurrentSearchId => this.orchestrator.CurrentSearchId;

    /// <summary>Runs one picker search under the Reference purpose, cancelling the picker's previous one.</summary>
    /// <param name="parse">The picker token as <see cref="References.ReferenceQueryParser"/> read it.</param>
    /// <returns>A task that ends with the search.</returns>
    public Task SearchAsync(SearchParse parse)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        return this.orchestrator.SearchAsync(parse, openTeam: null, SearchPurpose.Reference);
    }

    /// <summary>Cancels the picker's search in flight; its late results are dropped.</summary>
    public void Cancel() => this.orchestrator.Cancel();

    /// <summary>Cancels the search in flight and disposes the picker's own orchestrator.</summary>
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;
        this.orchestrator.Dispose();
        GC.SuppressFinalize(this);
    }
}

using Agency.Huddle.App.Search;

namespace Agency.Huddle.App.References;

/// <summary>
/// Makes one <see cref="ReferenceSearch"/> for a Composer. The Composer owns what it makes and disposes it; the
/// container never sees it, so a long-lived circuit does not keep one per Composer it ever rendered (a transient
/// <see cref="IDisposable"/> resolved from a scope would be held until the scope ends).
/// </summary>
/// <param name="providers">Every registered provider; the same five the search box uses.</param>
/// <param name="loggerFactory">Creates the orchestrator's logger.</param>
/// <param name="timeProvider">The clock the debounce waits on; the system clock when null.</param>
internal sealed class ReferenceSearchFactory(IEnumerable<ISearchProvider> providers, ILoggerFactory loggerFactory, TimeProvider? timeProvider = null)
{
    /// <summary>Makes a new picker search that nothing else owns.</summary>
    /// <returns>The search; the caller disposes it.</returns>
    public ReferenceSearch Create() => new(providers, loggerFactory, timeProvider);
}

namespace Agency.Huddle.App.Search;

/// <summary>One provider's batch: its hits, and whether a limit cut the search short (spec §7.1).</summary>
/// <param name="Hits">The hits, in no guaranteed order until the ranker has run.</param>
/// <param name="Truncated">Whether a limit cut the search short.</param>
public sealed record SearchResult(IReadOnlyList<SearchHit> Hits, bool Truncated)
{
    /// <summary>No hits, not truncated: what a provider returns for a scope it has no side for.</summary>
    public static SearchResult Empty { get; } = new([], false);
}

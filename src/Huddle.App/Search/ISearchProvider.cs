namespace Agency.Huddle.App.Search;

/// <summary>Searches one category (spec §7.3).</summary>
internal interface ISearchProvider
{
    /// <summary>The group this provider fills.</summary>
    SearchFilter Group { get; }

    /// <summary>Whether this provider reads from disk, and so waits for the debounce.</summary>
    bool IsExpensive { get; }

    /// <summary>
    /// Searches for <paramref name="query"/>. Returns an empty result at once for
    /// <see cref="SearchScope.Archived"/> when the category has no archived side.
    /// </summary>
    /// <param name="query">The search as the orchestrator received it.</param>
    /// <param name="ct">Cancels the search when a newer keystroke or a filter change supersedes it.</param>
    /// <returns>The provider's unranked batch.</returns>
    Task<SearchResult> SearchAsync(SearchQuery query, CancellationToken ct);
}

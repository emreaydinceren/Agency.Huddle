namespace Agency.Huddle.App.Search;

/// <summary>
/// Orders one group's hits and cuts them to the cap (spec §7.4). A pure static function with no I/O (D-22): the
/// open Team first, then the match tier, then active before archived, then the title ignoring case. Under the
/// Reference purpose with an empty phrase the provider's own browse order is kept (spec §5.2).
/// </summary>
internal static class SearchResultRanker
{
    /// <summary>Orders the hits and cuts them to <see cref="SearchQuery.MaxHits"/>.</summary>
    /// <param name="result">The provider's batch.</param>
    /// <param name="query">The query, whose open Team and cap apply.</param>
    /// <returns>The ordered, capped hits. Truncated when the provider was, or when the cap cut anything.</returns>
    public static SearchResult Rank(SearchResult result, SearchQuery query)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(query);

        if (query.Purpose == SearchPurpose.Reference && query.Text.Length == 0)
        {
            List<SearchHit> browse = [.. result.Hits.Take(query.MaxHits)];
            return new SearchResult(browse, result.Truncated || result.Hits.Count > query.MaxHits);
        }

        List<SearchHit> ordered = [.. result.Hits
            .OrderBy(hit => !IsOnOpenTeam(hit, query.OpenTeam))
            .ThenBy(hit => hit.Tier)
            .ThenBy(hit => hit.IsArchived)
            .ThenBy(hit => hit.Title, StringComparer.OrdinalIgnoreCase)];

        bool cut = ordered.Count > query.MaxHits;
        List<SearchHit> kept = [.. ordered.Take(query.MaxHits)];

        return new SearchResult(kept, result.Truncated || cut);
    }

    /// <summary>Whether a hit belongs to the open Team. An Agent counts when any of its Teams is open; a Room never does (OP-4).</summary>
    /// <param name="hit">The hit.</param>
    /// <param name="openTeam">The open Team, or null when no Team is open.</param>
    /// <returns>True when the hit ranks with the open Team.</returns>
    private static bool IsOnOpenTeam(SearchHit hit, string? openTeam)
    {
        if (openTeam is null)
        {
            return false;
        }

        return hit switch
        {
            RoomHit => false,
            AgentHit agent => agent.Teams.Any(team => string.Equals(team, openTeam, StringComparison.OrdinalIgnoreCase)),
            _ => string.Equals(hit.TeamName, openTeam, StringComparison.OrdinalIgnoreCase),
        };
    }
}

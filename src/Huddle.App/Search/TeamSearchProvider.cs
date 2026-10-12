using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Search;

/// <summary>Searches the Teams in memory (spec §8.1). Cheap, and it has no archived side.</summary>
internal sealed class TeamSearchProvider(ITeamCatalog catalog, TeamTypeCatalog types) : ISearchProvider
{
    /// <summary>The group this provider fills.</summary>
    public SearchFilter Group => SearchFilter.Teams;

    /// <summary>Teams are read from memory, so this provider does not wait for the debounce.</summary>
    public bool IsExpensive => false;

    /// <summary>
    /// Matches each Team by its name, then by its team type's display name (or its id when the type is unknown).
    /// An empty phrase lists every Team at <see cref="MatchTier.ExactName"/>; the ranker orders them.
    /// </summary>
    /// <param name="query">The search as the orchestrator received it.</param>
    /// <param name="ct">Checked once before the Teams are read.</param>
    /// <returns>The unranked hits, or an empty result for the Archived scope.</returns>
    public Task<SearchResult> SearchAsync(SearchQuery query, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (query.Scope == SearchScope.Archived)
        {
            return Task.FromResult(SearchResult.Empty);
        }

        List<SearchHit> hits = [];
        foreach (TeamSummary team in catalog.Teams)
        {
            string? typeLabel = team.TeamType is { } teamType ? types.Find(teamType.Id)?.DisplayName ?? teamType.Id : null;
            MatchTier? tier = query.Text.Length == 0
                ? MatchTier.ExactName
                : SearchMatch.FirstTier(query.Text, team.Name, typeLabel);
            if (tier is { } matched)
            {
                hits.Add(new TeamHit(team.Name, typeLabel, team.Members.Count, matched));
            }
        }

        return Task.FromResult(new SearchResult(hits, false));
    }
}

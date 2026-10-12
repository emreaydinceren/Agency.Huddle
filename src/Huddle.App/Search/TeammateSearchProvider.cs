using Agency.Huddle.App.Acp;

namespace Agency.Huddle.App.Search;

/// <summary>
/// Searches the Teammates in memory (spec §8.2). Every Persona is a Teammate result, including one
/// with no Team; the Human is never a Persona, so it is never listed (OP-8).
/// </summary>
internal sealed class TeammateSearchProvider(PersonaStore personas) : ISearchProvider
{
    /// <summary>The group this provider fills.</summary>
    public SearchFilter Group => SearchFilter.Teammates;

    /// <summary>Personas are read from memory, so this provider does not wait for the debounce.</summary>
    public bool IsExpensive => false;

    /// <summary>
    /// Matches each Persona by its Name, then its Alias, then its Title. An empty phrase lists every
    /// Persona at <see cref="MatchTier.ExactName"/>; the ranker orders them.
    /// </summary>
    /// <param name="query">The search as the orchestrator received it.</param>
    /// <param name="ct">Checked once before the Personas are read.</param>
    /// <returns>The unranked hits, or an empty result for the Archived scope.</returns>
    public Task<SearchResult> SearchAsync(SearchQuery query, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (query.Scope == SearchScope.Archived)
        {
            return Task.FromResult(SearchResult.Empty);
        }

        List<SearchHit> hits = [];
        foreach (PersonaEntry entry in personas.Entries)
        {
            MatchTier? tier = query.Text.Length == 0
                ? MatchTier.ExactName
                : SearchMatch.FirstTier(query.Text, entry.Name, entry.Alias, entry.Title);
            if (tier is { } matched)
            {
                hits.Add(new TeammateHit(entry.Name, entry.Title, entry.Alias, entry.Teams, matched));
            }
        }

        return Task.FromResult(new SearchResult(hits, false));
    }
}

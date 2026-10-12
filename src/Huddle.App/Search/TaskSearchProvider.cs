using Microsoft.Extensions.Options;
using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.App.Search;

/// <summary>
/// Searches the Tasks in memory (spec §8.4). The Active pass holds the open Tasks and the Archived
/// pass holds the Closed ones. An empty phrase finds nothing, and a disabled feature finds nothing.
/// </summary>
internal sealed class TaskSearchProvider(TaskStore store, IOptions<TeamOptions> options) : ISearchProvider
{
    /// <summary>The group this provider fills.</summary>
    public SearchFilter Group => SearchFilter.Tasks;

    /// <summary>Tasks are read from memory, so this provider does not wait for the debounce.</summary>
    public bool IsExpensive => false;

    /// <summary>
    /// Matches each Task by its title, then its id, and then, as a ContentMatch, by its assignee's Name.
    /// An exact id gets no special jump behaviour (D-14).
    /// </summary>
    /// <param name="query">The search as the orchestrator received it.</param>
    /// <param name="ct">Checked once before the Tasks are read.</param>
    /// <returns>The unranked hits, or an empty result when Tasks are disabled or the phrase is empty.</returns>
    public Task<SearchResult> SearchAsync(SearchQuery query, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!options.Value.Tasks.Enabled || query.Text.Length == 0)
        {
            return Task.FromResult(SearchResult.Empty);
        }

        bool archived = query.Scope == SearchScope.Archived;
        List<SearchHit> hits = [];
        foreach (TaskItem task in store.All)
        {
            if (task.Location.Closed != archived)
            {
                continue;
            }

            MatchTier? tier = SearchMatch.FirstTier(query.Text, task.Title, task.Id.ToString());
            if (tier is null && task.Assignee?.Contains(query.Text, StringComparison.OrdinalIgnoreCase) == true)
            {
                tier = MatchTier.ContentMatch;
            }

            if (tier is { } matched)
            {
                hits.Add(new TaskHit(task.Id, task.Title, task.Status, task.Assignee, task.Location.Team, task.Location.Closed, matched));
            }
        }

        return Task.FromResult(new SearchResult(hits, false));
    }
}

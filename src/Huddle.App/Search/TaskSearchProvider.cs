using Microsoft.Extensions.Options;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;

namespace Agency.Huddle.App.Search;

/// <summary>
/// Searches the Tasks in memory (spec §8.4). The Active pass holds the open Tasks and the Archived
/// pass holds the Closed ones. An empty phrase finds nothing under the Box purpose; under the Reference purpose
/// it browses the most recently updated Active Tasks, as the <c>#</c> picker always did (spec §5.2).
/// </summary>
internal sealed class TaskSearchProvider(TaskStore store, PersonaStore personas, IOptions<TeamOptions> options) : ISearchProvider
{
    /// <summary>The group this provider fills.</summary>
    public SearchFilter Group => SearchFilter.Tasks;

    /// <summary>Tasks are read from memory, so this provider does not wait for the debounce.</summary>
    public bool IsExpensive => false;

    /// <summary>
    /// Matches each Task by its title, then its id, and then, as a ContentMatch, by its assignee's Name.
    /// An exact id gets no special jump behaviour (D-14). A Reference browse with an empty phrase is the Active
    /// Tasks in <see cref="TaskQuery.Suggest"/> order, eight at most.
    /// </summary>
    /// <param name="query">The search as the orchestrator received it.</param>
    /// <param name="ct">Checked once before the Tasks are read.</param>
    /// <returns>The unranked hits, or an empty result when Tasks are disabled or the phrase is empty.</returns>
    public Task<SearchResult> SearchAsync(SearchQuery query, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!options.Value.Tasks.Enabled)
        {
            return Task.FromResult(SearchResult.Empty);
        }

        if (query.Purpose == SearchPurpose.Reference && query.Text.Length == 0)
        {
            if (query.Scope == SearchScope.Archived)
            {
                return Task.FromResult(SearchResult.Empty);
            }

            List<SearchHit> browse = [.. TaskQuery.Suggest(store.All, string.Empty, 8).Select<TaskItem, SearchHit>(task => Hit(task, MatchTier.ExactName))];
            return Task.FromResult(new SearchResult(browse, false));
        }

        if (query.Text.Length == 0)
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
                hits.Add(Hit(task, matched));
            }
        }

        return Task.FromResult(new SearchResult(hits, false));
    }

    /// <summary>Maps one Task to its hit, with the assignee's Persona title when one is found.</summary>
    /// <param name="task">The Task.</param>
    /// <param name="tier">How well it matched.</param>
    /// <returns>The Task hit.</returns>
    private TaskHit Hit(TaskItem task, MatchTier tier)
    {
        return new TaskHit(task.Id, task.Title, task.Status, task.Assignee, task.Location.Team, task.Location.Closed, tier, TitleOf(task.Assignee));
    }

    /// <summary>The assignee Persona's title, or null when unassigned or the Persona is gone.</summary>
    /// <param name="assignee">The assignee's Name, or null.</param>
    /// <returns>The Persona's title, or null.</returns>
    private string? TitleOf(string? assignee)
    {
        return assignee is null
            ? null
            : personas.Entries.FirstOrDefault(entry => string.Equals(entry.Name, assignee, StringComparison.OrdinalIgnoreCase))?.Title;
    }
}

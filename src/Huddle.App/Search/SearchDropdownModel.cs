namespace Agency.Huddle.App.Search;

/// <summary>Turns the search state into the dropdown's rows and strings, with no renderer (spec §6.3, §6.4, §11; D-9).</summary>
public static class SearchDropdownModel
{
    /// <summary>The cards each group shows under All.</summary>
    private const int AllCap = 5;

    /// <summary>The cards each group shows under one filter: this view is the see-all (OP-5).</summary>
    private const int SingleCap = 50;

    /// <summary>The line for a box that does not search yet.</summary>
    private const string QuietText = "Type 3 or more characters, or pick a filter.";

    /// <summary>The note under a group whose result was cut short.</summary>
    private const string TruncatedText = "Results may be incomplete.";

    /// <summary>The filters that All searches, in the order they are asked for.</summary>
    private static readonly SearchFilter[] AllGroups =
    [
        SearchFilter.Teams,
        SearchFilter.Teammates,
        SearchFilter.Library,
        SearchFilter.Tasks,
        SearchFilter.Rooms,
    ];

    /// <summary>The groups the dropdown can draw, in the fixed order of D-9.</summary>
    private static readonly SearchSlot[] Slots =
    [
        new(SearchFilter.Teams, SearchScope.Active, "Teams", "search-group-teams-active"),
        new(SearchFilter.Teammates, SearchScope.Active, "Teammates", "search-group-teammates-active"),
        new(SearchFilter.Library, SearchScope.Active, "Library", "search-group-library-active"),
        new(SearchFilter.Tasks, SearchScope.Active, "Tasks", "search-group-tasks-active"),
        new(SearchFilter.Tasks, SearchScope.Archived, "Closed Tasks", "search-group-tasks-archived"),
        new(SearchFilter.Rooms, SearchScope.Active, "Rooms", "search-group-rooms-active"),
        new(SearchFilter.Rooms, SearchScope.Archived, "Archived Rooms", "search-group-rooms-archived"),
    ];

    /// <summary>Builds the dropdown view for one search state.</summary>
    /// <param name="input">The parsed box, the group states and the sticky term.</param>
    /// <returns>The rows and the lines drawn for them.</returns>
    public static SearchDropdownView Build(SearchDropdownInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        string? header = input.BoxIsEmpty && input.StickyTerm is not null
            ? $"Search results for: '{input.StickyTerm}'"
            : null;

        if (!input.Parse.IsSearchable)
        {
            return new SearchDropdownView([], header, QuietText, null, null);
        }

        SearchFilter[] selected = SelectedGroups(input.Parse.Filter);
        List<SearchDropdownRow> rows = [];
        foreach (SearchSlot slot in Slots)
        {
            if (selected.Contains(slot.Group))
            {
                AddSlot(rows, slot, input);
            }
        }

        int hitCount = CountCards(rows);
        string? emptyLine = null;
        string? announcement = null;
        if (AllArrived(input, selected))
        {
            if (hitCount == 0)
            {
                if (input.Parse.Phrase.Length > 0)
                {
                    emptyLine = $"No results for '{input.Parse.Phrase}'.";
                }

                announcement = emptyLine ?? "0 results";
            }
            else
            {
                announcement = hitCount == 1 ? "1 result" : $"{hitCount} results";
            }
        }

        return new SearchDropdownView(rows, header, null, emptyLine, announcement);
    }

    /// <summary>The groups a filter searches: every group under All, otherwise the one filter.</summary>
    /// <param name="filter">The active filter.</param>
    /// <returns>The groups, in the order they are asked for.</returns>
    private static SearchFilter[] SelectedGroups(SearchFilter filter)
    {
        if (filter == SearchFilter.All)
        {
            return AllGroups;
        }

        return [filter];
    }

    /// <summary>Whether every expected update of the selected groups, active and archived, has arrived.</summary>
    /// <param name="input">The input whose states are checked.</param>
    /// <param name="selected">The groups the view searches.</param>
    /// <returns>True when no expected update is missing.</returns>
    private static bool AllArrived(SearchDropdownInput input, IReadOnlyList<SearchFilter> selected)
    {
        foreach (SearchFilter group in selected)
        {
            if (!input.States.ContainsKey((group, SearchScope.Active)) || !input.States.ContainsKey((group, SearchScope.Archived)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Adds one group's rows to the list, from its state, following spec §6.3.</summary>
    /// <param name="rows">The rows built so far.</param>
    /// <param name="slot">The group to draw.</param>
    /// <param name="input">The input, for its states and parse.</param>
    private static void AddSlot(List<SearchDropdownRow> rows, SearchSlot slot, SearchDropdownInput input)
    {
        input.States.TryGetValue((slot.Group, slot.Scope), out SearchGroupState? state);
        if (state is null)
        {
            if (slot.Scope == SearchScope.Active)
            {
                rows.Add(new SearchHeadingRow(slot.Id, slot.Heading, false, true));
            }

            return;
        }

        if (state is SearchGroupDone done)
        {
            AddDone(rows, slot, done.Result, input.Parse);
            return;
        }

        rows.Add(new SearchHeadingRow(slot.Id, slot.Heading, slot.Scope == SearchScope.Archived, false));
        rows.Add(new SearchNoticeRow($"Couldn't search {slot.Heading}.", SearchNoticeKind.Failed));
    }

    /// <summary>Adds a finished group: its cards, its truncation note, or the no-match line of one filter's active side.</summary>
    /// <param name="rows">The rows built so far.</param>
    /// <param name="slot">The group to draw.</param>
    /// <param name="result">The provider's batch.</param>
    /// <param name="parse">The parse, for the cap, the filter and the term.</param>
    private static void AddDone(List<SearchDropdownRow> rows, SearchSlot slot, SearchResult result, SearchParse parse)
    {
        int cap = parse.Filter == SearchFilter.All ? AllCap : SingleCap;
        List<SearchHit> shown = result.Hits.Take(cap).ToList();
        bool truncated = result.Truncated;
        bool noMatchLine = shown.Count == 0 && slot.Scope == SearchScope.Active && parse.Filter != SearchFilter.All;
        if (shown.Count == 0 && !noMatchLine)
        {
            return;
        }

        rows.Add(new SearchHeadingRow(slot.Id, slot.Heading, slot.Scope == SearchScope.Archived, false));
        if (noMatchLine)
        {
            // Browsing Teams or Teammates with an empty box has no term to name.
            string noMatch = parse.Phrase.Length == 0
                ? $"Nothing in {slot.Heading}."
                : $"Nothing in {slot.Heading} matches '{parse.Phrase}'.";
            rows.Add(new SearchNoticeRow(noMatch, SearchNoticeKind.NoMatch));
        }

        foreach (SearchHit hit in shown)
        {
            rows.Add(new SearchCardRow($"search-option-{CountCards(rows)}", slot.Id, hit));
        }

        if (truncated)
        {
            rows.Add(new SearchNoticeRow(TruncatedText, SearchNoticeKind.Truncated));
        }
    }

    /// <summary>Counts the result cards already in the rows.</summary>
    /// <param name="rows">The rows.</param>
    /// <returns>The number of cards.</returns>
    private static int CountCards(List<SearchDropdownRow> rows)
    {
        return rows.OfType<SearchCardRow>().Count();
    }

    /// <summary>One drawable group: its filter, its side, its heading and its element id.</summary>
    /// <param name="Group">The filter the group belongs to.</param>
    /// <param name="Scope">Active or Archived.</param>
    /// <param name="Heading">The heading text, as spec §6.3 names it.</param>
    /// <param name="Id">The element id of the heading.</param>
    private sealed record SearchSlot(SearchFilter Group, SearchScope Scope, string Heading, string Id);
}

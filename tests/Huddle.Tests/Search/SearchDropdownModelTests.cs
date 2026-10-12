using Agency.Huddle.App.Search;
using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Tests.Search;

/// <summary>Pins <see cref="SearchDropdownModel"/>: the rows, headings, notices and live-region strings of spec §6.3 and §11, in the fixed group order (D-9).</summary>
public sealed class SearchDropdownModelTests
{
    private const string QuietText = "Type 3 or more characters, or pick a filter.";

    /// <summary>Builds the parse of a box text, as the parser would give it.</summary>
    /// <param name="filter">The filter the box is set to.</param>
    /// <param name="phrase">The phrase after any prefix.</param>
    /// <param name="isSearchable">Whether the parser says the phrase searches.</param>
    /// <returns>The parse for that box text.</returns>
    private static SearchParse Parse(SearchFilter filter, string phrase, bool isSearchable = true)
    {
        return new(filter, phrase, isSearchable);
    }

    /// <summary>A Team hit whose Title is the name.</summary>
    /// <param name="name">The Team's name.</param>
    /// <returns>The hit.</returns>
    private static TeamHit TeamNamed(string name)
    {
        return new TeamHit(name, null, 0, MatchTier.NamePrefix);
    }

    /// <summary>A Teammate hit whose Title is the name.</summary>
    /// <param name="name">The Teammate's name.</param>
    /// <returns>The hit.</returns>
    private static TeammateHit TeammateNamed(string name)
    {
        return new TeammateHit(name, "Engineer", "mia", Array.Empty<string>(), MatchTier.NamePrefix);
    }

    /// <summary>A Library hit for a file, whose Title is its last path segment.</summary>
    /// <param name="relativePath">The file's path under its root.</param>
    /// <returns>The hit.</returns>
    private static LibraryHit FileNamed(string relativePath)
    {
        return new LibraryHit("root-1", "Library", relativePath, string.Empty, false, null, null, MatchTier.NamePrefix);
    }

    /// <summary>A Task hit whose Title is the task's title.</summary>
    /// <param name="title">The task's title.</param>
    /// <returns>The hit.</returns>
    private static TaskHit TaskNamed(string title)
    {
        return new TaskHit(new TaskId("HUD", 1), title, default, null, "Teams", false, MatchTier.NamePrefix);
    }

    /// <summary>A Room hit whose Title is the room's name.</summary>
    /// <param name="name">The Room's name.</param>
    /// <returns>The hit.</returns>
    private static RoomHit RoomNamed(string name)
    {
        return new RoomHit("room-1", name, 1, null, null, null, null, false, MatchTier.NamePrefix);
    }

    /// <summary>A group that finished with the given hits and was not truncated.</summary>
    /// <param name="hits">The hits.</param>
    /// <returns>The state.</returns>
    private static SearchGroupDone Done(params SearchHit[] hits)
    {
        return new SearchGroupDone(new SearchResult(hits, false));
    }

    /// <summary>A group that finished with the given hits and a limit cut the search short.</summary>
    /// <param name="hits">The hits.</param>
    /// <returns>The state.</returns>
    private static SearchGroupDone Cut(params SearchHit[] hits)
    {
        return new SearchGroupDone(new SearchResult(hits, true));
    }

    /// <summary>A group whose provider threw.</summary>
    /// <returns>The state.</returns>
    private static SearchGroupFailed Failed()
    {
        return new SearchGroupFailed();
    }

    /// <summary>Every expected update of the All view has arrived, each group empty, with the given overrides applied.</summary>
    /// <param name="overrides">The states to put in place of the empty ones.</param>
    /// <returns>The states, keyed by group and side.</returns>
    private static Dictionary<(SearchFilter Group, SearchScope Scope), SearchGroupState> AllArrived(params (SearchFilter Group, SearchScope Scope, SearchGroupState State)[] overrides)
    {
        Dictionary<(SearchFilter Group, SearchScope Scope), SearchGroupState> states = new();
        foreach (SearchFilter group in new[] { SearchFilter.Teams, SearchFilter.Teammates, SearchFilter.Library, SearchFilter.Tasks, SearchFilter.Rooms })
        {
            states[(group, SearchScope.Active)] = Done();
            states[(group, SearchScope.Archived)] = Done();
        }

        foreach ((SearchFilter group, SearchScope scope, SearchGroupState state) in overrides)
        {
            states[(group, scope)] = state;
        }

        return states;
    }

    /// <summary>Builds the input of the model.</summary>
    /// <param name="parse">The parse of the box.</param>
    /// <param name="states">The group states.</param>
    /// <param name="sticky">The sticky term, or null.</param>
    /// <param name="boxEmpty">Whether the box is empty.</param>
    /// <returns>The input.</returns>
    private static SearchDropdownInput Input(SearchParse parse, IReadOnlyDictionary<(SearchFilter Group, SearchScope Scope), SearchGroupState> states, string? sticky = null, bool boxEmpty = false)
    {
        return new(parse, states, sticky, boxEmpty);
    }

    /// <summary>The text of one row: a heading's text, a card's title, or a notice's text.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The text.</returns>
    private static string Describe(SearchDropdownRow row)
    {
        return row switch
        {
            SearchHeadingRow heading => heading.Text,
            SearchCardRow card => card.Hit.Title,
            SearchNoticeRow notice => notice.Text,
            _ => throw new InvalidOperationException("Unknown dropdown row."),
        };
    }

    /// <summary>The texts of every row of a view, in order.</summary>
    /// <param name="view">The view.</param>
    /// <returns>The texts.</returns>
    private static string[] Lines(SearchDropdownView view)
    {
        return view.Rows.Select(Describe).ToArray();
    }

    /// <summary>An empty box with a non-browse filter, or a phrase under three characters, shows the quiet line and no rows.</summary>
    [Fact]
    public void NotSearchable_ShowsTheQuietLine()
    {
        SearchDropdownView view = SearchDropdownModel.Build(Input(Parse(SearchFilter.All, "ab", false), AllArrived()));

        Assert.Equal(QuietText, view.QuietLine);
        Assert.Empty(view.Rows);
        Assert.Null(view.Announcement);
    }

    /// <summary>An empty box with a sticky term shows the header with that term.</summary>
    [Fact]
    public void ReopenedWithStickyTerm_ShowsHeader()
    {
        SearchDropdownView view = SearchDropdownModel.Build(Input(Parse(SearchFilter.All, string.Empty, false), AllArrived(), "login", true));

        Assert.Equal("Search results for: 'login'", view.Header);
    }

    /// <summary>An active group with no state yet is drawn as a heading that is searching, and nothing below it.</summary>
    [Fact]
    public void ActiveGroupPending_IsSearching()
    {
        Dictionary<(SearchFilter Group, SearchScope Scope), SearchGroupState> states = new();

        SearchDropdownView view = SearchDropdownModel.Build(Input(Parse(SearchFilter.Tasks, "login"), states));

        SearchDropdownRow expected = new SearchHeadingRow("search-group-tasks-active", "Tasks", false, true);
        Assert.Equal(expected, Assert.Single(view.Rows));
        Assert.Null(view.Announcement);
    }

    /// <summary>An archived group with no state yet is not drawn at all, while its active sibling is.</summary>
    [Fact]
    public void ArchivedGroupPending_IsNotDrawn()
    {
        Dictionary<(SearchFilter Group, SearchScope Scope), SearchGroupState> states = new()
        {
            [(SearchFilter.Tasks, SearchScope.Active)] = Done(TaskNamed("login fix")),
        };
        string[] expected = ["Tasks", "login fix"];

        SearchDropdownView view = SearchDropdownModel.Build(Input(Parse(SearchFilter.Tasks, "login"), states));

        Assert.Equal(expected, Lines(view));
        Assert.Null(view.Announcement);
    }

    /// <summary>All shows the groups in the fixed order, with the archived side after its active side and the heading texts whole.</summary>
    [Fact]
    public void All_GroupsInFixedOrder()
    {
        Dictionary<(SearchFilter Group, SearchScope Scope), SearchGroupState> states = AllArrived(
            (SearchFilter.Teams, SearchScope.Active, Done(TeamNamed("login team"))),
            (SearchFilter.Teammates, SearchScope.Active, Done(TeammateNamed("login mia"))),
            (SearchFilter.Library, SearchScope.Active, Done(FileNamed("login.md"))),
            (SearchFilter.Tasks, SearchScope.Active, Done(TaskNamed("login task"))),
            (SearchFilter.Tasks, SearchScope.Archived, Done(TaskNamed("login closed"))),
            (SearchFilter.Rooms, SearchScope.Active, Done(RoomNamed("login room"))),
            (SearchFilter.Rooms, SearchScope.Archived, Done(RoomNamed("login archived"))));
        string[] expected = ["Teams", "Teammates", "Library", "Tasks", "Closed Tasks", "Rooms", "Archived Rooms"];

        SearchDropdownView view = SearchDropdownModel.Build(Input(Parse(SearchFilter.All, "login"), states));

        string[] headings = view.Rows.OfType<SearchHeadingRow>().Select(heading => heading.Text).ToArray();
        Assert.Equal(expected, headings);
    }

    /// <summary>In All, a group with no hits is not drawn at all.</summary>
    [Fact]
    public void All_EmptyGroupNotDrawn()
    {
        Dictionary<(SearchFilter Group, SearchScope Scope), SearchGroupState> states = AllArrived(
            (SearchFilter.Tasks, SearchScope.Active, Done(TaskNamed("login task"))));
        string[] expected = ["Tasks", "login task"];

        SearchDropdownView view = SearchDropdownModel.Build(Input(Parse(SearchFilter.All, "login"), states));

        Assert.Equal(expected, Lines(view));
        Assert.Equal("1 result", view.Announcement);
    }

    /// <summary>One filter with an empty active group shows its heading and the notice that names the term.</summary>
    [Fact]
    public void SingleFilter_EmptyGroupSaysNothingMatches()
    {
        string[] expected = ["Tasks", "Nothing in Tasks matches 'login'."];

        SearchDropdownView view = SearchDropdownModel.Build(Input(Parse(SearchFilter.Tasks, "login"), AllArrived()));

        Assert.Equal(expected, Lines(view));
        Assert.Equal("No results for 'login'.", view.EmptyLine);
        Assert.Equal("No results for 'login'.", view.Announcement);
    }

    /// <summary>One filter with an empty archived side draws only the active group.</summary>
    [Fact]
    public void SingleFilter_EmptyArchivedGroupNotDrawn()
    {
        Dictionary<(SearchFilter Group, SearchScope Scope), SearchGroupState> states = AllArrived(
            (SearchFilter.Rooms, SearchScope.Active, Done(RoomNamed("login room"))));
        string[] expected = ["Rooms", "login room"];

        SearchDropdownView view = SearchDropdownModel.Build(Input(Parse(SearchFilter.Rooms, "login"), states));

        Assert.Equal(expected, Lines(view));
        Assert.Equal("1 result", view.Announcement);
    }

    /// <summary>A truncated group adds the incomplete note after its cards, as a truncated notice.</summary>
    [Fact]
    public void Truncated_AddsTheNote()
    {
        Dictionary<(SearchFilter Group, SearchScope Scope), SearchGroupState> states = AllArrived(
            (SearchFilter.Library, SearchScope.Active, Cut(FileNamed("login.md"))));
        string[] expected = ["Library", "login.md", "Results may be incomplete."];

        SearchDropdownView view = SearchDropdownModel.Build(Input(Parse(SearchFilter.Library, "login"), states));

        Assert.Equal(expected, Lines(view));
        SearchNoticeRow notice = Assert.IsType<SearchNoticeRow>(view.Rows[2]);
        Assert.Equal(SearchNoticeKind.Truncated, notice.Kind);
    }

    /// <summary>A failed group shows its heading and the couldn't-search notice, as a failed notice.</summary>
    [Fact]
    public void Failed_SaysCouldntSearch()
    {
        Dictionary<(SearchFilter Group, SearchScope Scope), SearchGroupState> states = AllArrived(
            (SearchFilter.Library, SearchScope.Active, Failed()));
        string[] expected = ["Library", "Couldn't search Library."];

        SearchDropdownView view = SearchDropdownModel.Build(Input(Parse(SearchFilter.Library, "login"), states));

        Assert.Equal(expected, Lines(view));
        SearchNoticeRow notice = Assert.IsType<SearchNoticeRow>(view.Rows[1]);
        Assert.Equal(SearchNoticeKind.Failed, notice.Kind);
    }

    /// <summary>A failed archived side uses the archived heading in its notice, and is marked archived.</summary>
    [Fact]
    public void FailedArchived_UsesTheArchivedHeading()
    {
        Dictionary<(SearchFilter Group, SearchScope Scope), SearchGroupState> states = AllArrived(
            (SearchFilter.Rooms, SearchScope.Active, Done(RoomNamed("login room"))),
            (SearchFilter.Rooms, SearchScope.Archived, Failed()));
        string[] expected = ["Rooms", "login room", "Archived Rooms", "Couldn't search Archived Rooms."];

        SearchDropdownView view = SearchDropdownModel.Build(Input(Parse(SearchFilter.Rooms, "login"), states));

        Assert.Equal(expected, Lines(view));
        SearchHeadingRow archivedHeading = Assert.IsType<SearchHeadingRow>(view.Rows[2]);
        Assert.True(archivedHeading.IsArchived);
    }

    /// <summary>When every group finished with no hit, the view says no results for the term.</summary>
    [Fact]
    public void NothingAnywhere_NoResultsLine()
    {
        SearchDropdownView view = SearchDropdownModel.Build(Input(Parse(SearchFilter.All, "login"), AllArrived()));

        Assert.Empty(view.Rows);
        Assert.Equal("No results for 'login'.", view.EmptyLine);
        Assert.Equal("No results for 'login'.", view.Announcement);
    }

    /// <summary>When every expected update arrived, the announcement counts the hits in all groups.</summary>
    [Fact]
    public void AllFinished_AnnouncesCount()
    {
        Dictionary<(SearchFilter Group, SearchScope Scope), SearchGroupState> states = AllArrived(
            (SearchFilter.Teams, SearchScope.Active, Done(TeamNamed("login a"), TeamNamed("login b"))),
            (SearchFilter.Rooms, SearchScope.Active, Done(RoomNamed("login room"))));

        SearchDropdownView view = SearchDropdownModel.Build(Input(Parse(SearchFilter.All, "login"), states));

        Assert.Equal("3 results", view.Announcement);
        Assert.Null(view.EmptyLine);
    }

    /// <summary>A single hit is announced in the singular.</summary>
    [Fact]
    public void OneResult_IsSingular()
    {
        Dictionary<(SearchFilter Group, SearchScope Scope), SearchGroupState> states = AllArrived(
            (SearchFilter.Teams, SearchScope.Active, Done(TeamNamed("login a"))));

        SearchDropdownView view = SearchDropdownModel.Build(Input(Parse(SearchFilter.All, "login"), states));

        Assert.Equal("1 result", view.Announcement);
    }

    /// <summary>Card option ids count from zero in render order, across group headings.</summary>
    [Fact]
    public void OptionIds_AreSequentialInRenderOrder()
    {
        Dictionary<(SearchFilter Group, SearchScope Scope), SearchGroupState> states = AllArrived(
            (SearchFilter.Teams, SearchScope.Active, Done(TeamNamed("login a"), TeamNamed("login b"))),
            (SearchFilter.Rooms, SearchScope.Active, Done(RoomNamed("login room"))));
        string[] expected = ["search-option-0", "search-option-1", "search-option-2"];

        SearchDropdownView view = SearchDropdownModel.Build(Input(Parse(SearchFilter.All, "login"), states));

        string[] optionIds = view.Rows.OfType<SearchCardRow>().Select(card => card.OptionId).ToArray();
        Assert.Equal(expected, optionIds);
    }

    /// <summary>Each card carries the id of the group it sits under.</summary>
    [Fact]
    public void Cards_CarryTheIdOfTheirGroup()
    {
        Dictionary<(SearchFilter Group, SearchScope Scope), SearchGroupState> states = AllArrived(
            (SearchFilter.Teams, SearchScope.Active, Done(TeamNamed("login a"))),
            (SearchFilter.Rooms, SearchScope.Archived, Done(RoomNamed("login archived"))));
        string[] expected = ["search-group-teams-active", "search-group-rooms-archived"];

        SearchDropdownView view = SearchDropdownModel.Build(Input(Parse(SearchFilter.All, "login"), states));

        string[] groupIds = view.Rows.OfType<SearchCardRow>().Select(card => card.GroupId).ToArray();
        Assert.Equal(expected, groupIds);
    }

    /// <summary>All caps each group at five cards and adds no other row: no see-all row, only headings, cards and notices.</summary>
    [Fact]
    public void All_NoSeeAllRow()
    {
        Dictionary<(SearchFilter Group, SearchScope Scope), SearchGroupState> states = AllArrived(
            (SearchFilter.Teams, SearchScope.Active, Done(TeamNamed("login 1"), TeamNamed("login 2"), TeamNamed("login 3"), TeamNamed("login 4"), TeamNamed("login 5"), TeamNamed("login 6"), TeamNamed("login 7"))));
        string[] expected = ["Teams", "login 1", "login 2", "login 3", "login 4", "login 5"];

        SearchDropdownView view = SearchDropdownModel.Build(Input(Parse(SearchFilter.All, "login"), states));

        Assert.Equal(expected, Lines(view));
    }
}

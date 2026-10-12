using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Components.Layout;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Search;
using Agency.Huddle.App.Tabs;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Search;

namespace Agency.Huddle.Tests.Ui.Search;

/// <summary>
/// Pins the search box in the Top Bar and its dropdown (Global Search spec §5.1, §6, §9, §11): the chips, the
/// strings of §6.3, the sticky filter, the ARIA wiring and how a hit opens. Every group answers when the test
/// says so, through <see cref="SearchBarHarness"/>.
/// </summary>
public sealed class SearchBarTests(TeamWebApplicationFactory factory) : IClassFixture<TeamWebApplicationFactory>
{
    private const string Nova = "---\nName: Nova\nTitle: Researcher\nAlias: Scout\n---\nYou research.";

    /// <summary>The answer of a pass in which no provider found anything.</summary>
    private static readonly Dictionary<SearchFilter, SearchHit[]> NoHits = [];

    /// <summary>A Team hit.</summary>
    /// <param name="name">The Team's name.</param>
    /// <returns>The hit.</returns>
    private static TeamHit Team(string name) => new(name, null, 0, MatchTier.NamePrefix);

    /// <summary>A Teammate hit.</summary>
    /// <param name="name">The Teammate's name.</param>
    /// <returns>The hit.</returns>
    private static TeammateHit Teammate(string name) => new(name, "Researcher", "scout", ["Core"], MatchTier.NamePrefix);

    /// <summary>A Task hit.</summary>
    /// <param name="title">The Task's title.</param>
    /// <returns>The hit.</returns>
    private static TaskHit TaskTitled(string title) => new(new TaskId("HUD", 1), title, default, null, "Core", false, MatchTier.NamePrefix);

    /// <summary>A Library hit for a file.</summary>
    /// <param name="path">The file's path under its root.</param>
    /// <returns>The hit.</returns>
    private static LibraryHit File(string path) => new("root-1", "Library", path, string.Empty, false, null, null, MatchTier.NamePrefix);

    /// <summary>A Room hit.</summary>
    /// <param name="name">The Room's name.</param>
    /// <returns>The hit.</returns>
    private static RoomHit Room(string name) => new("room-1", name, 1, null, null, null, null, false, MatchTier.NamePrefix);

    /// <summary>The trimmed text of each element.</summary>
    /// <param name="elements">The elements.</param>
    /// <returns>Their texts, in order.</returns>
    private static List<string> Texts(IEnumerable<IElement> elements) => [.. elements.Select(static element => element.TextContent.Trim())];

    /// <summary>The answer of a pass in which one filter found the given hits.</summary>
    /// <param name="filter">The filter that answers.</param>
    /// <param name="hits">Its hits.</param>
    /// <returns>The answers by filter.</returns>
    private static Dictionary<SearchFilter, SearchHit[]> Only(SearchFilter filter, params SearchHit[] hits) => new() { [filter] = hits };

    /// <summary>With an empty box that has never searched, the dropdown shows its one quiet line exactly.</summary>
    [Fact]
    public async Task EmptyState_TextExactly()
    {
        await using SearchBarHarness h = new(factory);
        IRenderedComponent<ContainerFragment> cut = h.Render();

        await SearchBarHarness.OpenAsync(cut);

        Assert.Equal("Type 3 or more characters, or pick a filter.", cut.Find(".search-quiet").TextContent.Trim());
    }

    /// <summary>The chips run All, Teams, Teammates, Library, Tasks, Rooms, in that order.</summary>
    [Fact]
    public async Task Chips_InOrder()
    {
        await using SearchBarHarness h = new(factory);
        IRenderedComponent<ContainerFragment> cut = h.Render();

        await SearchBarHarness.OpenAsync(cut);

        string[] expected = ["All", "Teams", "Teammates", "Library", "Tasks", "Rooms"];
        Assert.Equal(expected, SearchBarTests.Texts(cut.FindAll("[role='radiogroup'][aria-label='Filter'] [role='radio']")));
    }

    /// <summary>Typing a prefix lights its chip, and picking a chip writes the same prefix: both light the same chip.</summary>
    [Fact]
    public async Task ChipAndPrefix_LightTheSameChip()
    {
        await using SearchBarHarness h = new(factory);
        IRenderedComponent<ContainerFragment> cut = h.Render();
        await SearchBarHarness.OpenAsync(cut);

        await SearchBarHarness.TypeAsync(cut, "teammate:");
        string typed = Assert.Single(SearchBarTests.Texts(cut.FindAll("[role='radio'][aria-checked='true']")));
        await SearchBarHarness.TypeAsync(cut, string.Empty);
        await cut.InvokeAsync(() => cut.FindAll("[role='radio']").Single(static chip => chip.TextContent.Trim() == "Tasks").ClickAsync());

        Assert.Equal("Teammates", typed);
        Assert.Equal("task:", SearchBarHarness.Input(cut).GetAttribute("value"));
        Assert.Equal("Tasks", Assert.Single(SearchBarTests.Texts(cut.FindAll("[role='radio'][aria-checked='true']"))));
    }

    /// <summary>Under All the groups come in the fixed order, each at most five cards, and there is no See all row.</summary>
    [Fact]
    public async Task All_GroupsInFixedOrder_AtMostFive_NoSeeAll()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using SearchBarHarness h = new(factory);
        IRenderedComponent<ContainerFragment> cut = h.Render();
        await SearchBarHarness.OpenAsync(cut);
        await SearchBarHarness.TypeAsync(cut, "abc");
        Dictionary<SearchFilter, SearchHit[]> hits = new()
        {
            [SearchFilter.Rooms] = [SearchBarTests.Room("abc room")],
            [SearchFilter.Tasks] = [SearchBarTests.TaskTitled("abc task")],
            [SearchFilter.Library] = [SearchBarTests.File("abc.md")],
            [SearchFilter.Teammates] = [SearchBarTests.Teammate("Abcd")],
            [SearchFilter.Teams] = [.. Enumerable.Range(1, 7).Select(static n => SearchBarTests.Team($"abc{n}"))],
        };

        await h.FinishAsync(SearchBarHarness.AllFilters, hits, ct);

        cut.WaitForAssertion(() =>
        {
            string[] headings = ["Teams", "Teammates", "Library", "Tasks", "Rooms"];
            Assert.Equal(headings, SearchBarTests.Texts(cut.FindAll(".search-group-heading")));
            Assert.Equal(9, cut.FindAll("[role='option']").Count);
            Assert.DoesNotContain(SearchBarTests.Texts(cut.FindAll("*")), static text => text.StartsWith("See all", StringComparison.OrdinalIgnoreCase));
        });
    }

    /// <summary>One filter draws only its group.</summary>
    [Fact]
    public async Task SingleFilter_ShowsOneGroup()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using SearchBarHarness h = new(factory);
        IRenderedComponent<ContainerFragment> cut = h.Render();
        await SearchBarHarness.OpenAsync(cut);
        await SearchBarHarness.TypeAsync(cut, "task:abc");

        await h.FinishAsync([SearchFilter.Tasks], SearchBarTests.Only(SearchFilter.Tasks, SearchBarTests.TaskTitled("abc task")), ct);

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(["Tasks"], SearchBarTests.Texts(cut.FindAll(".search-group-heading")));
            Assert.Single(cut.FindAll("[role='option']"));
        });
    }

    /// <summary>A provider that threw says so under its heading and the rest of the dropdown is unaffected.</summary>
    [Fact]
    public async Task FailedGroup_SaysCouldntSearchLibrary()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using SearchBarHarness h = new(factory);
        IRenderedComponent<ContainerFragment> cut = h.Render();
        await SearchBarHarness.OpenAsync(cut);
        await SearchBarHarness.TypeAsync(cut, "lib:abc");

        (await h.NextOpenCallAsync(SearchFilter.Library, SearchScope.Active, ct)).Fault(new InvalidOperationException("boom"));
        (await h.NextOpenCallAsync(SearchFilter.Library, SearchScope.Archived, ct)).Complete();

        cut.WaitForAssertion(() => Assert.Equal(["Couldn't search Library."], SearchBarTests.Texts(cut.FindAll(".search-notice"))));
    }

    /// <summary>A truncated result says "Results may be incomplete." under its cards.</summary>
    [Fact]
    public async Task TruncatedGroup_SaysIncomplete()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using SearchBarHarness h = new(factory);
        IRenderedComponent<ContainerFragment> cut = h.Render();
        await SearchBarHarness.OpenAsync(cut);
        await SearchBarHarness.TypeAsync(cut, "team:abc");

        (await h.NextOpenCallAsync(SearchFilter.Teams, SearchScope.Active, ct)).Complete(new SearchResult([SearchBarTests.Team("abc")], true));
        (await h.NextOpenCallAsync(SearchFilter.Teams, SearchScope.Archived, ct)).Complete();

        cut.WaitForAssertion(() => Assert.Equal(["Results may be incomplete."], SearchBarTests.Texts(cut.FindAll(".search-notice"))));
    }

    /// <summary>One filter with no hit says "Nothing in Tasks matches 'xyz'." under the group heading.</summary>
    [Fact]
    public async Task EmptySingleFilter_SaysNothingInTasksMatches()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using SearchBarHarness h = new(factory);
        IRenderedComponent<ContainerFragment> cut = h.Render();
        await SearchBarHarness.OpenAsync(cut);
        await SearchBarHarness.TypeAsync(cut, "task:xyz");

        await h.FinishAsync([SearchFilter.Tasks], SearchBarTests.NoHits, ct);

        cut.WaitForAssertion(() => Assert.Equal(["Nothing in Tasks matches 'xyz'."], SearchBarTests.Texts(cut.FindAll(".search-notice"))));
    }

    /// <summary>Browsing Teams with an empty box and no Team says "Nothing in Teams." with no term.</summary>
    [Fact]
    public async Task Browse_NoTeams_SaysNothingInTeamsWithoutATerm()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using SearchBarHarness h = new(factory);
        IRenderedComponent<ContainerFragment> cut = h.Render();
        await SearchBarHarness.OpenAsync(cut);
        await cut.InvokeAsync(() => cut.FindAll("[role='radio']").Single(static chip => chip.TextContent.Trim() == "Teams").ClickAsync());

        await h.FinishAsync([SearchFilter.Teams], SearchBarTests.NoHits, ct);

        cut.WaitForAssertion(() => Assert.Equal(["Nothing in Teams."], SearchBarTests.Texts(cut.FindAll(".search-notice"))));
    }

    /// <summary>When every group finished with nothing, the dropdown says "No results for 'abc'." and the live region says it too.</summary>
    [Fact]
    public async Task NoResults_Exactly()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using SearchBarHarness h = new(factory);
        IRenderedComponent<ContainerFragment> cut = h.Render();
        await SearchBarHarness.OpenAsync(cut);
        await SearchBarHarness.TypeAsync(cut, "abc");

        await h.FinishAsync(SearchBarHarness.AllFilters, SearchBarTests.NoHits, ct);

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("No results for 'abc'.", cut.Find(".search-empty").TextContent.Trim());
            Assert.Equal("No results for 'abc'.", cut.Find("[aria-live='polite']").TextContent.Trim());
        });
    }

    /// <summary>Closing keeps the filter and the phrase; reopening shows an empty box, the filter, the header, and runs the phrase again.</summary>
    [Fact]
    public async Task Reopen_EmptyBoxLastFilterHeaderAndReRuns()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using SearchBarHarness h = new(factory);
        IRenderedComponent<ContainerFragment> cut = h.Render();
        await SearchBarHarness.OpenAsync(cut);
        await SearchBarHarness.TypeAsync(cut, "task:login");
        await h.FinishAsync([SearchFilter.Tasks], SearchBarTests.Only(SearchFilter.Tasks, SearchBarTests.TaskTitled("login page")), ct);
        await SearchBarHarness.PressAsync(cut, "Escape");

        await SearchBarHarness.OpenAsync(cut);

        GatedCall rerun = await h.NextOpenCallAsync(SearchFilter.Tasks, SearchScope.Active, ct);
        Assert.Equal("login", rerun.Query.Text);
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(string.Empty, SearchBarHarness.Input(cut).GetAttribute("value") ?? string.Empty);
            Assert.Equal("Tasks", Assert.Single(SearchBarTests.Texts(cut.FindAll("[role='radio'][aria-checked='true']"))));
            Assert.Equal("Search results for: 'login'", cut.Find(".search-header").TextContent.Trim());
        });
    }

    /// <summary>The box is a combobox whose expanded state follows the dropdown and whose active descendant follows Down.</summary>
    [Fact]
    public async Task Aria_ComboboxExpandedAndActivedescendantFollowsDown()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using SearchBarHarness h = new(factory);
        IRenderedComponent<ContainerFragment> cut = h.Render();
        string closed = SearchBarHarness.Input(cut).GetAttribute("aria-expanded") ?? string.Empty;
        await SearchBarHarness.OpenAsync(cut);
        string opened = SearchBarHarness.Input(cut).GetAttribute("aria-expanded") ?? string.Empty;
        await SearchBarHarness.TypeAsync(cut, "team:abc");
        await h.FinishAsync([SearchFilter.Teams], SearchBarTests.Only(SearchFilter.Teams, SearchBarTests.Team("abc1"), SearchBarTests.Team("abc2")), ct);
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("[role='option']").Count));

        await SearchBarHarness.PressAsync(cut, "ArrowDown");
        string first = SearchBarHarness.Input(cut).GetAttribute("aria-activedescendant") ?? string.Empty;
        await SearchBarHarness.PressAsync(cut, "ArrowDown");
        string second = SearchBarHarness.Input(cut).GetAttribute("aria-activedescendant") ?? string.Empty;

        Assert.Equal("false", closed);
        Assert.Equal("true", opened);
        Assert.Equal("search-option-0", first);
        Assert.Equal("search-option-1", second);
        Assert.Equal("true", cut.Find("#search-option-1").GetAttribute("aria-selected"));
        Assert.Equal("search-listbox", SearchBarHarness.Input(cut).GetAttribute("aria-controls"));
    }

    /// <summary>The results are one listbox of labelled groups of options.</summary>
    [Fact]
    public async Task Aria_GroupAndOptionRoles()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using SearchBarHarness h = new(factory);
        IRenderedComponent<ContainerFragment> cut = h.Render();
        await SearchBarHarness.OpenAsync(cut);
        await SearchBarHarness.TypeAsync(cut, "team:abc");

        await h.FinishAsync([SearchFilter.Teams], SearchBarTests.Only(SearchFilter.Teams, SearchBarTests.Team("abc1")), ct);

        cut.WaitForAssertion(() =>
        {
            IElement listbox = cut.Find("[role='listbox']");
            Assert.Equal("search-listbox", listbox.Id);
            IElement group = Assert.Single(listbox.QuerySelectorAll("[role='group']"));
            Assert.Equal("search-group-teams-active", group.GetAttribute("aria-labelledby"));
            Assert.Equal("Teams", cut.Find("#search-group-teams-active").TextContent.Trim());
            IElement option = Assert.Single(group.QuerySelectorAll("[role='option']"));
            Assert.Equal("search-option-0", option.Id);
        });
    }

    /// <summary>The Top Bar's middle cell holds the search box, filled through the Search slot.</summary>
    [Fact]
    public async Task TopBar_SearchCellHoldsTheSearchBox()
    {
        await using SearchBarHarness h = new(factory);
        _ = h.Tabs.Open("/rooms/ab12");
        RenderFragment slot = static builder =>
        {
            builder.OpenComponent<Agency.Huddle.App.Components.Search.SearchBar>(0);
            builder.CloseComponent();
        };

        IRenderedComponent<ContainerFragment> cut = h.Ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TopBar>(0);
            builder.AddAttribute(1, nameof(TopBar.Search), slot);
            builder.CloseComponent();
        });

        Assert.NotNull(cut.Find(".top-bar-center input[role='combobox']"));
        Assert.Empty(cut.FindAll(".top-bar-start input[role='combobox']"));
    }

    /// <summary>Enter on a Team hit navigates the focused Tab in place.</summary>
    [Fact]
    public async Task Enter_NavigatesTheFocusedTabInPlace()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using SearchBarHarness h = new(factory);
        Tab room = h.Tabs.Open("/rooms/ab12");
        h.Tabs.Keep(room.Id);
        IRenderedComponent<ContainerFragment> cut = await SearchBarTests.RenderWithTeamHitAsync(h, ct);

        await SearchBarHarness.PressAsync(cut, "ArrowDown");
        await SearchBarHarness.PressAsync(cut, "Enter");

        Tab only = Assert.Single(h.Tabs.Tabs);
        Assert.Equal("/teams/Core", only.Url);
        Assert.Equal(room.Id, only.Id);
        Assert.Empty(cut.FindAll(".search-dropdown"));
    }

    /// <summary>Ctrl+Enter on a Team hit opens a new, permanent Tab.</summary>
    [Fact]
    public async Task CtrlEnter_OpensAPermanentTab()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using SearchBarHarness h = new(factory);
        Tab room = h.Tabs.Open("/rooms/ab12");
        h.Tabs.Keep(room.Id);
        IRenderedComponent<ContainerFragment> cut = await SearchBarTests.RenderWithTeamHitAsync(h, ct);

        await SearchBarHarness.PressAsync(cut, "ArrowDown");
        await SearchBarHarness.PressAsync(cut, "Enter", ctrl: true);

        Assert.Equal(["/rooms/ab12", "/teams/Core"], h.Tabs.Tabs.Select(static tab => tab.Url).ToList());
        Tab? active = h.Tabs.Active;
        Assert.NotNull(active);
        Assert.Equal("/teams/Core", active.Url);
        Assert.False(active.IsPreview);
    }

    /// <summary>Ctrl+Enter on a hit that is already open only focuses that Tab.</summary>
    [Fact]
    public async Task CtrlEnter_AlreadyOpen_OnlyFocusesIt()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using SearchBarHarness h = new(factory);
        Tab team = h.Tabs.Open("/teams/Core");
        h.Tabs.Keep(team.Id);
        Tab room = h.Tabs.Open("/rooms/ab12");
        h.Tabs.Keep(room.Id);
        IRenderedComponent<ContainerFragment> cut = await SearchBarTests.RenderWithTeamHitAsync(h, ct);

        await SearchBarHarness.PressAsync(cut, "ArrowDown");
        await SearchBarHarness.PressAsync(cut, "Enter", ctrl: true);

        Assert.Equal(2, h.Tabs.Tabs.Count);
        Assert.Equal(team.Id, h.Tabs.Active?.Id);
    }

    /// <summary>Enter on a Teammate hit opens that Teammate's card dialog and changes no Tab.</summary>
    [Fact]
    public async Task Enter_OnTeammateHit_OpensTheCardAndChangesNoTab()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using TeamWebApplicationFactory own = new();
        await own.WriteDefinitionAsync("Nova", SearchBarTests.Nova, ct);
        await using SearchBarHarness h = new(own);
        Tab room = h.Tabs.Open("/rooms/ab12");
        h.Tabs.Keep(room.Id);
        IRenderedComponent<ContainerFragment> cut = h.Render();
        await SearchBarHarness.OpenAsync(cut);
        await SearchBarHarness.TypeAsync(cut, "teammate:nov");
        await h.FinishAsync([SearchFilter.Teammates], SearchBarTests.Only(SearchFilter.Teammates, SearchBarTests.Teammate("Nova")), ct);
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[role='option']")));
        await SearchBarHarness.PressAsync(cut, "ArrowDown");

        _ = SearchBarHarness.PressAsync(cut, "Enter");

        cut.WaitForState(() => cut.FindComponents<TeammateCard>().Count == 1);
        Assert.Equal("Nova", cut.FindComponent<TeammateCard>().Instance.Name);
        Tab only = Assert.Single(h.Tabs.Tabs);
        Assert.Equal("/rooms/ab12", only.Url);
    }

    /// <summary>Escape closes the dropdown.</summary>
    [Fact]
    public async Task Esc_ClosesTheDropdown()
    {
        await using SearchBarHarness h = new(factory);
        IRenderedComponent<ContainerFragment> cut = h.Render();
        await SearchBarHarness.OpenAsync(cut);
        Assert.Single(cut.FindAll(".search-dropdown"));

        await SearchBarHarness.PressAsync(cut, "Escape");

        Assert.Empty(cut.FindAll(".search-dropdown"));
        Assert.Equal("false", SearchBarHarness.Input(cut).GetAttribute("aria-expanded"));
    }

    /// <summary>An update that belongs to an older search is dropped, and the current search's update still draws.</summary>
    [Fact]
    public async Task StaleUpdate_FromAnOlderSearch_IsIgnored()
    {
        await using SearchBarHarness h = new(factory);
        IRenderedComponent<ContainerFragment> cut = h.Render();
        await SearchBarHarness.OpenAsync(cut);
        await SearchBarHarness.TypeAsync(cut, "team:abc");
        long older = h.Orchestrator.CurrentSearchId;
        await SearchBarHarness.TypeAsync(cut, "team:abcd");
        long current = h.Orchestrator.CurrentSearchId;

        h.Raise(new SearchGroupUpdate(older, SearchFilter.Teams, SearchScope.Active, new SearchGroupDone(new SearchResult([SearchBarTests.Team("stale")], false))));
        h.Raise(new SearchGroupUpdate(current, SearchFilter.Teams, SearchScope.Active, new SearchGroupDone(new SearchResult([SearchBarTests.Team("fresh")], false))));

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(["fresh"], SearchBarTests.Texts(cut.FindAll(".search-card-title")));
        });
    }

    /// <summary>Disposing the box removes its subscription to the orchestrator.</summary>
    [Fact]
    public async Task Dispose_Unsubscribes()
    {
        await using SearchBarHarness h = new(factory);
        _ = h.Render();
        int before = h.SubscriberCount();

        await h.Ctx.DisposeComponentsAsync();

        Assert.Equal(1, before);
        Assert.Equal(0, h.SubscriberCount());
    }

    /// <summary>Enter on a box whose last run found nothing runs the same text again, because the box cannot see an unchanged retype (D2).</summary>
    [Fact]
    public async Task Enter_WithNoCards_RunsTheSameTextAgain()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using SearchBarHarness h = new(factory);
        IRenderedComponent<ContainerFragment> cut = h.Render();
        await SearchBarHarness.OpenAsync(cut);
        await SearchBarHarness.TypeAsync(cut, "team:cor");
        await h.FinishAsync([SearchFilter.Teams], NoHits, ct);
        int before = h.Providers[SearchFilter.Teams].Calls.Count;

        await SearchBarHarness.PressAsync(cut, "Enter");

        await h.Providers[SearchFilter.Teams].WaitForCallsAsync(before + 1, ct);
        Assert.True(h.Providers[SearchFilter.Teams].Calls.Count > before);
    }

    /// <summary>Typing a text, changing it and typing the first text again runs the search each time.</summary>
    [Fact]
    public async Task RetypingEarlierText_RunsTheSearchAgain()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using SearchBarHarness h = new(factory);
        IRenderedComponent<ContainerFragment> cut = h.Render();
        await SearchBarHarness.OpenAsync(cut);
        await SearchBarHarness.TypeAsync(cut, "team:login");
        await SearchBarHarness.TypeAsync(cut, "team:logi");
        await SearchBarHarness.TypeAsync(cut, "team:login");

        IReadOnlyList<GatedCall> calls = await h.Providers[SearchFilter.Teams].WaitForCallsAsync(3, ct);

        Assert.Equal("login", calls[^1].Query.Text);
    }

    /// <summary>The dropdown is centred under the box: MudBlazor anchors it at the box's bottom centre and grows it from its own top centre.</summary>
    [Fact]
    public async Task Dropdown_IsCentredUnderTheBox()
    {
        await using SearchBarHarness h = new(factory);
        IRenderedComponent<ContainerFragment> cut = h.Render();

        await SearchBarHarness.OpenAsync(cut);

        IElement popover = cut.Find(".mud-popover-open");
        // contains-ok: MudBlazor writes the placement as class tokens on one attribute; the tokens are what's pinned.
        Assert.Contains("mud-popover-anchor-bottom-center", popover.ClassName, StringComparison.Ordinal);
        // contains-ok: MudBlazor writes the placement as class tokens on one attribute; the tokens are what's pinned.
        Assert.Contains("mud-popover-top-center", popover.ClassName, StringComparison.Ordinal);
    }

    /// <summary>Renders the box, searches the Teams filter for "cor" and answers with one Team named Core.</summary>
    /// <param name="h">The harness to render in.</param>
    /// <param name="ct">The test's cancellation token.</param>
    /// <returns>The rendered fragment, with the Core card drawn.</returns>
    private static async Task<IRenderedComponent<ContainerFragment>> RenderWithTeamHitAsync(SearchBarHarness h, CancellationToken ct)
    {
        IRenderedComponent<ContainerFragment> cut = h.Render();
        await SearchBarHarness.OpenAsync(cut);
        await SearchBarHarness.TypeAsync(cut, "team:cor");
        await h.FinishAsync([SearchFilter.Teams], SearchBarTests.Only(SearchFilter.Teams, SearchBarTests.Team("Core")), ct);
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[role='option']")));
        return cut;
    }
}

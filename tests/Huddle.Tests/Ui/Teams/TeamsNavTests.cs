using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using Agency.Huddle.App;
using Agency.Huddle.App.Components.Teams;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Teams;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>
/// Pins Spec §6.5's listing half: <c>TeamsNav</c> is one <c>MudNavMenu.teams-nav</c> holding a
/// "Teams" group; each Team is a <c>div.hover-reveal-row</c> whose link goes to the escaped
/// <c>/teams/{team}</c> route, with its Projects as <c>nav-project-link</c> rows, a "No members" hint
/// for a memberless Team, a "Team actions" menu (button or right-click) offering "New project", and a
/// trailing "New team" action. It follows <see cref="ITeamCatalog.Changed"/> and unsubscribes on
/// dispose.
/// </summary>
public sealed class TeamsNavTests
{
    /// <summary>The group is titled "Teams", and the Teams appear in the catalog's own order (not sorted).</summary>
    [Fact]
    public async Task Renders_GroupTitledTeams_WithTeamsInCatalogOrder()
    {
        FakeTeamCatalog catalog = new()
        {
            Teams = [Team("Sales & Ops"), Team("Business"), Team("Lab #2")],
        };

        await using MudBunitContext ctx = NewContext(catalog);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        Assert.Equal("Teams", cut.Find(".teams-nav .mud-nav-group .mud-nav-link-text").TextContent.Trim());
        string[] expected = ["Sales & Ops", "Business", "Lab #2"];
        string[] actual = [.. cut.FindAll(".hover-reveal-link a").Select(a => a.TextContent.Trim())];
        Assert.Equal(expected, actual);
    }

    /// <summary>Projects sit right after their own Team's row, and every segment of every href is escaped.</summary>
    [Fact]
    public async Task Project_Rows_AreNestedUnderTheirTeam()
    {
        FakeTeamCatalog catalog = new()
        {
            Teams =
            [
                Team("Business", projects: ["2025 Taxes Project", "Marketing"]),
                Team("Sales & Ops"),
                Team("Lab #2", projects: ["Item #1"]),
            ],
        };

        await using MudBunitContext ctx = NewContext(catalog);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        string?[] expectedHrefs =
        [
            "/teams/Business",
            "/teams/Business/projects/2025%20Taxes%20Project",
            "/teams/Business/projects/Marketing",
            "/teams/Sales%20%26%20Ops",
            "/teams/Lab%20%232",
            "/teams/Lab%20%232/projects/Item%20%231",
        ];
        string?[] actualHrefs = [.. cut.FindAll(".teams-nav a[href]").Select(a => a.GetAttribute("href"))];
        Assert.Equal(expectedHrefs, actualHrefs);

        string[] expectedProjects = ["2025 Taxes Project", "Marketing", "Item #1"];
        string[] actualProjects = [.. cut.FindAll(".nav-project-link a").Select(a => a.TextContent.Trim())];
        Assert.Equal(expectedProjects, actualProjects);
    }

    /// <summary>The Team row's link goes to the Team page, and its "Team actions" button opens a menu whose only item is "New project".</summary>
    [Fact]
    public async Task Team_Row_LinksToTheTeamPage_AndHasAMenuWithNewProject()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business")] };

        await using MudBunitContext ctx = NewContext(catalog);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        Assert.Equal("/teams/Business", cut.Find(".hover-reveal-link a").GetAttribute("href"));
        Assert.Empty(MenuItemTexts(cut));

        AngleSharp.Dom.IElement button = MenuButton(cut, "Business");
        Assert.Equal("Team actions", button.GetAttribute("aria-label"));
        await cut.InvokeAsync(() => button.ClickAsync());

        string[] expected = ["New project"];
        Assert.Equal(expected, MenuItemTexts(cut));
    }

    /// <summary>Right-clicking the Team row opens the same menu as its button does.</summary>
    [Fact]
    public async Task RightClickingATeamRow_OpensTheSameMenu()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business")] };

        await using MudBunitContext ctx = NewContext(catalog);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        await cut.InvokeAsync(() => RowFor(cut, "Business").ContextMenuAsync());

        string[] expected = ["New project"];
        Assert.Equal(expected, MenuItemTexts(cut));
    }

    /// <summary>A Team no Persona is labelled with shows the muted "No members" hint inside its own row.</summary>
    [Fact]
    public async Task TeamWithNoMembers_ShowsTheNoMembersHint()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business"), Team("Lab", hasMembers: false)] };

        await using MudBunitContext ctx = NewContext(catalog);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        Assert.Equal("No members", cut.Find(".teams-nav-nomembers").TextContent.Trim());
        Assert.Equal("No members", RowFor(cut, "Lab").QuerySelectorAll(".teams-nav-nomembers").Single().TextContent.Trim());
    }

    /// <summary>A Team with members shows no hint at all: with every Team staffed, the hint element is absent.</summary>
    [Fact]
    public async Task TeamWithMembers_DoesNotShowTheNoMembersHint()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business"), Team("Sales & Ops")] };

        await using MudBunitContext ctx = NewContext(catalog);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        Assert.Empty(cut.FindAll(".teams-nav-nomembers"));
        Assert.Empty(RowFor(cut, "Business").QuerySelectorAll(".teams-nav-nomembers"));
    }

    /// <summary>At <c>/teams/Business/files</c> the Business link carries the active class and the other Team's link does not.</summary>
    [Fact]
    public async Task CurrentRoute_HighlightsTheTeamLink()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business"), Team("Sales & Ops")] };

        await using MudBunitContext ctx = NewContext(catalog);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/teams/Business/files");
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        Assert.Contains("active", cut.Find("a[href='/teams/Business']").ClassList);
        Assert.DoesNotContain("active", cut.Find("a[href='/teams/Sales%20%26%20Ops']").ClassList);
    }

    /// <summary>The trailing "New team" action is present, styled as <c>nav-action-link</c>, after the Teams.</summary>
    [Fact]
    public async Task NewTeamAction_IsPresent()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business")] };

        await using MudBunitContext ctx = NewContext(catalog);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        Assert.Equal("New team", cut.Find(".nav-action-link").TextContent.Trim());
    }

    /// <summary>With no Teams at all the "New team" action is still offered and no Team row renders.</summary>
    [Fact]
    public async Task NewTeamAction_IsPresent_WhenThereAreNoTeams()
    {
        FakeTeamCatalog catalog = new();

        await using MudBunitContext ctx = NewContext(catalog);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        Assert.Equal("New team", cut.Find(".nav-action-link").TextContent.Trim());
        Assert.Empty(cut.FindAll("div.hover-reveal-row"));
    }

    /// <summary>A catalog change re-renders the list with the new Team, still in catalog order.</summary>
    [Fact]
    public async Task Changed_ReRenders_WithTheNewTeam()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business")] };

        await using MudBunitContext ctx = NewContext(catalog);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);
        string[] before = ["Business"];
        Assert.Equal(before, LinkTexts(cut));

        catalog.Teams = [Team("Business"), Team("Research")];
        catalog.Raise();

        string[] after = ["Business", "Research"];
        cut.WaitForAssertion(() => Assert.Equal(after, LinkTexts(cut)));
    }

    /// <summary>The component subscribes to the catalog on render and unsubscribes when its components are disposed.</summary>
    [Fact]
    public async Task Dispose_UnsubscribesFromTheCatalog()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business")] };

        await using MudBunitContext ctx = NewContext(catalog);
        _ = RenderNav(ctx);
        Assert.Equal(1, catalog.ChangedSubscriberCount);

        await ctx.DisposeComponentsAsync();

        Assert.Equal(0, catalog.ChangedSubscriberCount);
    }

    /// <summary>With nothing stored every Team is expanded: its Projects show, and its chevron says "Collapse Business" with <c>aria-expanded="true"</c>.</summary>
    [Fact]
    public async Task Default_AllTeamsExpanded_ShowingProjects()
    {
        FakeTeamCatalog catalog = new()
        {
            Teams = [Team("Business", projects: ["Marketing"]), Team("Sales & Ops", projects: ["Budget"])],
        };

        await using MudBunitContext ctx = NewContext(catalog);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(1, Reads(ctx));
            string[] projects = ["Marketing", "Budget"];
            Assert.Equal(projects, ProjectTexts(cut));
            Assert.Equal("true", Chevron(cut, "Business").GetAttribute("aria-expanded"));
            Assert.Equal("Collapse Business", Chevron(cut, "Business").GetAttribute("aria-label"));
            Assert.Equal("true", Chevron(cut, "Sales & Ops").GetAttribute("aria-expanded"));
            Assert.Equal("Collapse Sales & Ops", Chevron(cut, "Sales & Ops").GetAttribute("aria-label"));
        });
        Assert.Empty(Writes(ctx));
    }

    /// <summary>Clicking the chevron collapses the Team: label "Expand Business", <c>aria-expanded="false"</c>, its Projects gone, and <c>teamsNav:collapsed</c> stored as <c>["business"]</c>.</summary>
    [Fact]
    public async Task Chevron_Click_CollapsesAndHidesProjects()
    {
        FakeTeamCatalog catalog = new()
        {
            Teams = [Team("Business", projects: ["Marketing"]), Team("Sales & Ops", projects: ["Budget"])],
        };

        await using MudBunitContext ctx = NewContext(catalog);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);
        cut.WaitForAssertion(() => Assert.Equal(1, Reads(ctx)));

        await ClickChevronAsync(cut, "Business");

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("false", Chevron(cut, "Business").GetAttribute("aria-expanded"));
            Assert.Equal("Expand Business", Chevron(cut, "Business").GetAttribute("aria-label"));
            string[] projects = ["Budget"];
            Assert.Equal(projects, ProjectTexts(cut));
            Assert.Equal("true", Chevron(cut, "Sales & Ops").GetAttribute("aria-expanded"));
        });
        IReadOnlyList<object?> write = Assert.Single(Writes(ctx));
        Assert.Equal(CollapsedKey, write[0]);
        Assert.Equal(BusinessOnly, write[1]);
    }

    /// <summary>A second click expands the Team again, shows its Projects and stores the now-empty list <c>[]</c>.</summary>
    [Fact]
    public async Task Chevron_ClickedTwice_ExpandsAgainAndStoresAnEmptyList()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", projects: ["Marketing"])] };

        await using MudBunitContext ctx = NewContext(catalog);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);
        cut.WaitForAssertion(() => Assert.Equal(1, Reads(ctx)));

        await ClickChevronAsync(cut, "Business");
        cut.WaitForAssertion(() => Assert.Equal("false", Chevron(cut, "Business").GetAttribute("aria-expanded")));
        await ClickChevronAsync(cut, "Business");

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("true", Chevron(cut, "Business").GetAttribute("aria-expanded"));
            Assert.Equal("Collapse Business", Chevron(cut, "Business").GetAttribute("aria-label"));
            string[] projects = ["Marketing"];
            Assert.Equal(projects, ProjectTexts(cut));
        });
        IReadOnlyList<object?>[] writes = Writes(ctx);
        Assert.Equal(2, writes.Length);
        Assert.Equal(CollapsedKey, writes[1][0]);
        Assert.Equal("[]", writes[1][1]);
    }

    /// <summary>A Team stored as collapsed starts collapsed (the stored name is lower-case, the Team's own is not); an unstored Team stays expanded; nothing is written on load.</summary>
    [Fact]
    public async Task StoredCollapsed_OnLoad_StartsCollapsed()
    {
        FakeTeamCatalog catalog = new()
        {
            Teams = [Team("Business", projects: ["Marketing"]), Team("Sales & Ops", projects: ["Budget"])],
        };

        await using MudBunitContext ctx = NewContext(catalog, stored: BusinessOnly);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("false", Chevron(cut, "Business").GetAttribute("aria-expanded"));
            Assert.Equal("Expand Business", Chevron(cut, "Business").GetAttribute("aria-label"));
            string[] projects = ["Budget"];
            Assert.Equal(projects, ProjectTexts(cut));
            Assert.Equal("true", Chevron(cut, "Sales & Ops").GetAttribute("aria-expanded"));
        });
        Assert.Empty(Writes(ctx));
    }

    /// <summary>At a route inside a collapsed Team the Team is forced open, and nothing is written to storage.</summary>
    [Fact]
    public async Task CurrentRouteInsideACollapsedTeam_ForcesItOpen_WithoutChangingStorage()
    {
        FakeTeamCatalog catalog = new()
        {
            Teams = [Team("Business", projects: ["Marketing"]), Team("Sales & Ops", projects: ["Budget"])],
        };

        await using MudBunitContext ctx = NewContext(catalog, stored: """["business","sales & ops"]""");
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/teams/Business/projects/Marketing");
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("false", Chevron(cut, "Sales & Ops").GetAttribute("aria-expanded"));
            Assert.Equal("true", Chevron(cut, "Business").GetAttribute("aria-expanded"));
            Assert.Equal("Collapse Business", Chevron(cut, "Business").GetAttribute("aria-label"));
            string[] projects = ["Marketing"];
            Assert.Equal(projects, ProjectTexts(cut));
        });
        Assert.Empty(Writes(ctx));
    }

    /// <summary>The route's Team comes from the unescaped segment, compared ignoring case: <c>Sales%20%26%20Ops</c> and a lower-cased spelling both force "Sales &amp; Ops" open.</summary>
    [Theory]
    [InlineData("/teams/Sales%20%26%20Ops/files")]
    [InlineData("/teams/sales%20%26%20ops")]
    public async Task CurrentRoute_WithAnEscapedOrLowerCasedSegment_ForcesTheTeamOpen(string route)
    {
        ArgumentNullException.ThrowIfNull(route);
        FakeTeamCatalog catalog = new()
        {
            Teams = [Team("Business", projects: ["Marketing"]), Team("Sales & Ops", projects: ["Budget"])],
        };

        await using MudBunitContext ctx = NewContext(catalog, stored: """["sales & ops","business"]""");
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo(route);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("false", Chevron(cut, "Business").GetAttribute("aria-expanded"));
            Assert.Equal("true", Chevron(cut, "Sales & Ops").GetAttribute("aria-expanded"));
            string[] projects = ["Budget"];
            Assert.Equal(projects, ProjectTexts(cut));
        });
        Assert.Empty(Writes(ctx));
    }

    /// <summary>The nav sits in the layout and is not re-created on navigation, so it follows <c>LocationChanged</c>: navigating into a collapsed Team opens it, with no storage write.</summary>
    [Fact]
    public async Task NavigatingIntoACollapsedTeam_OpensItWithoutChangingStorage()
    {
        FakeTeamCatalog catalog = new()
        {
            Teams = [Team("Business", projects: ["Marketing"]), Team("Other", projects: ["Docs"])],
        };

        await using MudBunitContext ctx = NewContext(catalog, stored: BusinessOnly);
        NavigationManager nav = ctx.Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/teams/Other");
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);
        cut.WaitForAssertion(() =>
        {
            string[] before = ["Docs"];
            Assert.Equal(before, ProjectTexts(cut));
        });

        await cut.InvokeAsync(() => nav.NavigateTo("/teams/Business/projects/Marketing"));

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("true", Chevron(cut, "Business").GetAttribute("aria-expanded"));
            string[] after = ["Marketing", "Docs"];
            Assert.Equal(after, ProjectTexts(cut));
        });
        Assert.Empty(Writes(ctx));
    }

    /// <summary>A chevron click on the Team the route forced open collapses it for real: Projects hidden and storage written.</summary>
    [Fact]
    public async Task Chevron_OnTheRouteForcedOpenTeam_CollapsesItAndWritesStorage()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", projects: ["Marketing"])] };

        await using MudBunitContext ctx = NewContext(catalog, stored: BusinessOnly);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/teams/Business/projects/Marketing");
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(1, Reads(ctx));
            Assert.Equal("true", Chevron(cut, "Business").GetAttribute("aria-expanded"));
        });

        await ClickChevronAsync(cut, "Business");

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("false", Chevron(cut, "Business").GetAttribute("aria-expanded"));
            Assert.Equal("Expand Business", Chevron(cut, "Business").GetAttribute("aria-label"));
            Assert.Empty(ProjectTexts(cut));
        });
        IReadOnlyList<object?> write = Assert.Single(Writes(ctx));
        Assert.Equal(CollapsedKey, write[0]);
        Assert.Equal(BusinessOnly, write[1]);
    }

    /// <summary>The chevron's collapse of the route's own Team lasts only until the next navigation into that Team, which opens it again.</summary>
    [Fact]
    public async Task NavigatingIntoTheTeamAgain_ReopensATeamTheChevronCollapsed()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", projects: ["Marketing"])] };

        await using MudBunitContext ctx = NewContext(catalog);
        NavigationManager nav = ctx.Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/teams/Business/projects/Marketing");
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);
        cut.WaitForAssertion(() => Assert.Equal(1, Reads(ctx)));
        await ClickChevronAsync(cut, "Business");
        cut.WaitForAssertion(() => Assert.Equal("false", Chevron(cut, "Business").GetAttribute("aria-expanded")));

        await cut.InvokeAsync(() => nav.NavigateTo("/teams/Business/files"));

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("true", Chevron(cut, "Business").GetAttribute("aria-expanded"));
            string[] projects = ["Marketing"];
            Assert.Equal(projects, ProjectTexts(cut));
        });
    }

    /// <summary>Stored text that is not a JSON array of strings expands everything and never throws: a further chevron click stores just that Team, so nothing was half-loaded.</summary>
    [Theory]
    [InlineData("not json")]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("[1,2]")]
    [InlineData("[null]")]
    public async Task MalformedStorage_ExpandsEverything_WithoutThrowing(string stored)
    {
        ArgumentNullException.ThrowIfNull(stored);
        FakeTeamCatalog catalog = new()
        {
            Teams = [Team("Business", projects: ["Marketing"]), Team("Sales & Ops", projects: ["Budget"])],
        };

        await using MudBunitContext ctx = NewContext(catalog, stored: stored);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);
        cut.WaitForAssertion(() => Assert.Equal(1, Reads(ctx)));

        await ClickChevronAsync(cut, "Business");

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("false", Chevron(cut, "Business").GetAttribute("aria-expanded"));
            Assert.Equal("true", Chevron(cut, "Sales & Ops").GetAttribute("aria-expanded"));
            string[] projects = ["Budget"];
            Assert.Equal(projects, ProjectTexts(cut));
        });
        IReadOnlyList<object?> write = Assert.Single(Writes(ctx));
        Assert.Equal(BusinessOnly, write[1]);
    }

    /// <summary>A stored JSON <c>null</c> deserializes to no array at all; everything is expanded and a click still works.</summary>
    [Fact]
    public async Task StoredJsonNull_ExpandsEverything_WithoutThrowing()
    {
        FakeTeamCatalog catalog = new()
        {
            Teams = [Team("Business", projects: ["Marketing"]), Team("Sales & Ops", projects: ["Budget"])],
        };

        await using MudBunitContext ctx = NewContext(catalog, stored: "null");
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);
        cut.WaitForAssertion(() => Assert.Equal(1, Reads(ctx)));

        await ClickChevronAsync(cut, "Business");

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("false", Chevron(cut, "Business").GetAttribute("aria-expanded"));
            Assert.Equal("true", Chevron(cut, "Sales & Ops").GetAttribute("aria-expanded"));
        });
        IReadOnlyList<object?> write = Assert.Single(Writes(ctx));
        Assert.Equal(BusinessOnly, write[1]);
    }

    /// <summary>When the storage read throws (<c>JSException</c>), everything is expanded; when the write throws too, a click still collapses the Team in memory.</summary>
    [Fact]
    public async Task StorageThrows_ExpandsEverything()
    {
        FakeTeamCatalog catalog = new()
        {
            Teams = [Team("Business", projects: ["Marketing"]), Team("Sales & Ops", projects: ["Budget"])],
        };

        await using MudBunitContext ctx = NewContext(catalog, storageThrows: true);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(1, Reads(ctx));
            Assert.Equal("true", Chevron(cut, "Business").GetAttribute("aria-expanded"));
            Assert.Equal("true", Chevron(cut, "Sales & Ops").GetAttribute("aria-expanded"));
        });

        await ClickChevronAsync(cut, "Business");

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("false", Chevron(cut, "Business").GetAttribute("aria-expanded"));
            string[] projects = ["Budget"];
            Assert.Equal(projects, ProjectTexts(cut));
        });
    }

    /// <summary>A Team created after storage exists (its name is not in the stored list) starts expanded, and the collapsed one stays collapsed.</summary>
    [Fact]
    public async Task ATeamCreatedAfterStorageExists_StartsExpanded()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", projects: ["Marketing"])] };

        await using MudBunitContext ctx = NewContext(catalog, stored: BusinessOnly);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);
        cut.WaitForAssertion(() => Assert.Equal("false", Chevron(cut, "Business").GetAttribute("aria-expanded")));

        catalog.Teams = [Team("Business", projects: ["Marketing"]), Team("Research", projects: ["Notes"])];
        catalog.Raise();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("true", Chevron(cut, "Research").GetAttribute("aria-expanded"));
            Assert.Equal("false", Chevron(cut, "Business").GetAttribute("aria-expanded"));
            string[] projects = ["Notes"];
            Assert.Equal(projects, ProjectTexts(cut));
        });
        Assert.Empty(Writes(ctx));
    }

    /// <summary>"New team" opens the name dialog titled "New team", and it checks the name against the Teams the nav knows (a duplicate is refused inline).</summary>
    [Fact]
    public async Task NewTeam_OpensTheDialog_ThatKnowsTheExistingTeams()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business")] };
        await using MudBunitContext ctx = NewContext(catalog);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        OpenNewTeamDialog(cut);
        await TypeNameAsync(cut, "Business");

        Assert.Equal("New team", cut.Find(".mud-dialog-title").TextContent.Trim());
        Assert.Equal("Team name", cut.Find(".new-team-dialog-name-field label").TextContent.Trim());
        Assert.Equal("A Team named \"Business\" already exists.", cut.Find(".new-team-dialog-problem").TextContent.Trim());
    }

    /// <summary>OK on "Research" calls <c>EnsureTeam("Research")</c> once and, the catalog already listing it, navigates to <c>/teams/Research</c>.</summary>
    [Fact]
    public async Task NewTeam_Ok_CreatesTheTeam_AndNavigatesToIt()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business")] };
        FakeTeamFolders folders = new();
        await using MudBunitContext ctx = NewContext(catalog, folders: folders);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);
        NavigationManager nav = ctx.Services.GetRequiredService<NavigationManager>();

        OpenNewTeamDialog(cut);
        catalog.Teams = [Team("Business"), Team("Research")];
        await SubmitNameAsync(cut, "Research");

        cut.WaitForAssertion(() => Assert.Equal("teams/Research", nav.ToBaseRelativePath(nav.Uri)));
        string[] expected = ["EnsureTeam:Research"];
        Assert.Equal(expected, folders.Calls);
    }

    /// <summary>
    /// A Team the dialog just created is not in the catalog for about 500 ms (corrections-D6 #21): the nav
    /// makes the call, does not navigate yet (no "There is no Team named" flash), and navigates once the
    /// catalog raises <c>Changed</c> with the Team in it. The bounded timeout that navigates anyway after
    /// 2 s has no row here: it would need a real delay.
    /// </summary>
    [Fact]
    public async Task NewTeam_NotYetInTheCatalog_NavigatesOnlyAfterTheCatalogListsIt()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business")] };
        FakeTeamFolders folders = new();
        await using MudBunitContext ctx = NewContext(catalog, folders: folders);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);
        NavigationManager nav = ctx.Services.GetRequiredService<NavigationManager>();
        string before = nav.Uri;

        OpenNewTeamDialog(cut);
        await SubmitNameAsync(cut, "Research");
        string[] expectedCalls = ["EnsureTeam:Research"];
        cut.WaitForAssertion(() => Assert.Equal(expectedCalls, folders.Calls));
        await Flush(cut);
        Assert.Equal(before, nav.Uri);

        catalog.Teams = [Team("Business"), Team("Research")];
        catalog.Raise();

        cut.WaitForAssertion(() => Assert.Equal("teams/Research", nav.ToBaseRelativePath(nav.Uri)));
    }

    /// <summary>A refused <c>EnsureTeam</c> shows its error text in one error snackbar, closes nothing else and does not navigate.</summary>
    [Fact]
    public async Task NewTeam_Failure_ShowsAnErrorSnackbar_AndDoesNotNavigate()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business")] };
        FakeTeamFolders folders = new() { TeamResult = LibraryResult<LibraryPath>.Fail("Teams/Research is not writable.") };
        await using MudBunitContext ctx = NewContext(catalog, folders: folders);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);
        NavigationManager nav = ctx.Services.GetRequiredService<NavigationManager>();
        MudBlazor.ISnackbar snackbar = ctx.Services.GetRequiredService<MudBlazor.ISnackbar>();
        string before = nav.Uri;

        OpenNewTeamDialog(cut);
        await SubmitNameAsync(cut, "Research");

        cut.WaitForAssertion(() => Assert.Single(snackbar.ShownSnackbars));
        MudBlazor.Snackbar shown = Assert.Single(snackbar.ShownSnackbars);
        Assert.Equal("Teams/Research is not writable.", shown.Message);
        Assert.Equal(MudBlazor.Severity.Error, shown.Severity);
        await Flush(cut);
        Assert.Equal(before, nav.Uri);
    }

    /// <summary>Cancelling the dialog calls nothing, shows nothing and does not navigate.</summary>
    [Fact]
    public async Task NewTeam_Cancelled_CallsNothing_AndDoesNotNavigate()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business")] };
        FakeTeamFolders folders = new();
        await using MudBunitContext ctx = NewContext(catalog, folders: folders);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);
        NavigationManager nav = ctx.Services.GetRequiredService<NavigationManager>();
        MudBlazor.ISnackbar snackbar = ctx.Services.GetRequiredService<MudBlazor.ISnackbar>();
        string before = nav.Uri;
        OpenNewTeamDialog(cut);
        await TypeNameAsync(cut, "Research");

        await cut.InvokeAsync(() => cut.Find(".new-team-dialog-cancel-button").ClickAsync());
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-dialog-title")));
        await Flush(cut);

        Assert.Empty(folders.Calls);
        Assert.Empty(snackbar.ShownSnackbars);
        Assert.Equal(before, nav.Uri);
    }

    /// <summary>"New project" on Business opens the dialog titled "New project" for that Team: an existing Project is refused inline naming Business.</summary>
    [Fact]
    public async Task NewProject_OpensTheDialog_ForThatTeam()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", projects: ["Marketing"]), Team("Sales")] };
        await using MudBunitContext ctx = NewContext(catalog);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        await OpenNewProjectDialog(cut, "Business");
        await TypeNameAsync(cut, "marketing");

        Assert.Equal("New project", cut.Find(".mud-dialog-title").TextContent.Trim());
        Assert.Equal("Project name", cut.Find(".new-team-dialog-name-field label").TextContent.Trim());
        Assert.Equal("A Project named \"marketing\" already exists in Business.", cut.Find(".new-team-dialog-problem").TextContent.Trim());
    }

    /// <summary>OK on "Q4 Launch" calls <c>EnsureProjectIn("Business", "Q4 Launch")</c> once and navigates to the escaped Project route.</summary>
    [Fact]
    public async Task NewProject_Ok_CreatesTheProject_AndNavigatesToIt()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", projects: ["Marketing"])] };
        FakeTeamFolders folders = new();
        await using MudBunitContext ctx = NewContext(catalog, folders: folders);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);
        NavigationManager nav = ctx.Services.GetRequiredService<NavigationManager>();

        await OpenNewProjectDialog(cut, "Business");
        catalog.Teams = [Team("Business", projects: ["Marketing", "Q4 Launch"])];
        await SubmitNameAsync(cut, "Q4 Launch");

        cut.WaitForAssertion(() => Assert.Equal("teams/Business/projects/Q4%20Launch", nav.ToBaseRelativePath(nav.Uri)));
        string[] expected = ["EnsureProjectIn:Business/Q4 Launch"];
        Assert.Equal(expected, folders.Calls);
    }

    /// <summary>A Project not yet in the catalog is waited for like a Team (via <c>ProjectExists</c>): no navigation until <c>Changed</c> lists it. The 2 s timeout path has no row (it would need a real delay).</summary>
    [Fact]
    public async Task NewProject_NotYetInTheCatalog_NavigatesOnlyAfterTheCatalogListsIt()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", projects: ["Marketing"])] };
        FakeTeamFolders folders = new();
        await using MudBunitContext ctx = NewContext(catalog, folders: folders);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);
        NavigationManager nav = ctx.Services.GetRequiredService<NavigationManager>();
        string before = nav.Uri;

        await OpenNewProjectDialog(cut, "Business");
        await SubmitNameAsync(cut, "Q4 Launch");
        string[] expectedCalls = ["EnsureProjectIn:Business/Q4 Launch"];
        cut.WaitForAssertion(() => Assert.Equal(expectedCalls, folders.Calls));
        await Flush(cut);
        Assert.Equal(before, nav.Uri);

        catalog.Teams = [Team("Business", projects: ["Marketing", "Q4 Launch"])];
        catalog.Raise();

        cut.WaitForAssertion(() => Assert.Equal("teams/Business/projects/Q4%20Launch", nav.ToBaseRelativePath(nav.Uri)));
    }

    /// <summary>A refused <c>EnsureProjectIn</c> shows its error text in one error snackbar and does not navigate.</summary>
    [Fact]
    public async Task NewProject_Failure_ShowsAnErrorSnackbar_AndDoesNotNavigate()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business")] };
        FakeTeamFolders folders = new() { ProjectResult = LibraryResult<LibraryPath>.Fail("Teams/Business/Q4 Launch is not writable.") };
        await using MudBunitContext ctx = NewContext(catalog, folders: folders);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);
        NavigationManager nav = ctx.Services.GetRequiredService<NavigationManager>();
        MudBlazor.ISnackbar snackbar = ctx.Services.GetRequiredService<MudBlazor.ISnackbar>();
        string before = nav.Uri;

        await OpenNewProjectDialog(cut, "Business");
        await SubmitNameAsync(cut, "Q4 Launch");

        cut.WaitForAssertion(() => Assert.Single(snackbar.ShownSnackbars));
        MudBlazor.Snackbar shown = Assert.Single(snackbar.ShownSnackbars);
        Assert.Equal("Teams/Business/Q4 Launch is not writable.", shown.Message);
        Assert.Equal(MudBlazor.Severity.Error, shown.Severity);
        await Flush(cut);
        Assert.Equal(before, nav.Uri);
    }

    /// <summary>Cancelling the New project dialog calls nothing, shows nothing and does not navigate.</summary>
    [Fact]
    public async Task NewProject_Cancelled_CallsNothing_AndDoesNotNavigate()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business")] };
        FakeTeamFolders folders = new();
        await using MudBunitContext ctx = NewContext(catalog, folders: folders);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);
        NavigationManager nav = ctx.Services.GetRequiredService<NavigationManager>();
        MudBlazor.ISnackbar snackbar = ctx.Services.GetRequiredService<MudBlazor.ISnackbar>();
        string before = nav.Uri;
        await OpenNewProjectDialog(cut, "Business");
        await TypeNameAsync(cut, "Q4 Launch");

        await cut.InvokeAsync(() => cut.Find(".new-team-dialog-cancel-button").ClickAsync());
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-dialog-title")));
        await Flush(cut);

        Assert.Empty(folders.Calls);
        Assert.Empty(snackbar.ShownSnackbars);
        Assert.Equal(before, nav.Uri);
    }

    /// <summary>Spec §6.5 Gating: with Library and Tasks both off there is nowhere for a Project to live, so the Team menu does not offer "New project".</summary>
    [Fact]
    public async Task NewProject_Hidden_WhenLibraryAndTasksOff()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business")] };
        await using MudBunitContext ctx = NewContext(catalog, libraryEnabled: false, tasksEnabled: false);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        foreach (AngleSharp.Dom.IElement button in cut.FindAll(".hover-reveal-menu-button"))
        {
            await cut.InvokeAsync(() => button.ClickAsync());
        }

        Assert.Empty(MenuItemTexts(cut));
    }

    /// <summary>The twin: with either feature still on, the Team menu offers "New project".</summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task NewProject_Shown_WhenEitherLibraryOrTasksIsOn(bool libraryEnabled, bool tasksEnabled)
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business")] };
        await using MudBunitContext ctx = NewContext(catalog, libraryEnabled: libraryEnabled, tasksEnabled: tasksEnabled);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        await cut.InvokeAsync(() => MenuButton(cut, "Business").ClickAsync());

        string[] expected = ["New project"];
        Assert.Equal(expected, MenuItemTexts(cut));
    }

    /// <summary>Clicks the trailing "New team" action and waits for the dialog. The click's handler awaits the dialog, so it runs fire-and-forget.</summary>
    private static void OpenNewTeamDialog(IRenderedComponent<ContainerFragment> cut)
    {
        _ = cut.InvokeAsync(() => cut.Find(".nav-action-link .mud-nav-link").Click());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));
    }

    /// <summary>Opens <paramref name="team"/>'s "Team actions" menu, clicks "New project" and waits for the dialog.</summary>
    private static async Task OpenNewProjectDialog(IRenderedComponent<ContainerFragment> cut, string team)
    {
        await cut.InvokeAsync(() => MenuButton(cut, team).ClickAsync());
        _ = cut.InvokeAsync(() => cut.Find("div.mud-menu-item").Click());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));
    }

    /// <summary>Types <paramref name="name"/> into the open dialog's name field.</summary>
    private static Task TypeNameAsync(IRenderedComponent<ContainerFragment> cut, string name) =>
        cut.InvokeAsync(() => cut.Find(".new-team-dialog-name-field input").InputAsync(name));

    /// <summary>Types <paramref name="name"/> and clicks Create.</summary>
    private static async Task SubmitNameAsync(IRenderedComponent<ContainerFragment> cut, string name)
    {
        await TypeNameAsync(cut, name);
        await cut.InvokeAsync(() => cut.Find(".new-team-dialog-create-button").ClickAsync());
    }

    /// <summary>Runs an empty action on the renderer's synchronization context, so any handler continuation queued before it has finished before the caller asserts an absence.</summary>
    private static Task Flush(IRenderedComponent<ContainerFragment> cut) =>
        cut.InvokeAsync(static () => { });

    private const string CollapsedKey = "teamsNav:collapsed";

    private const string BusinessOnly = """["business"]""";

    /// <summary>
    /// A fresh MudBlazor-aware bUnit context holding everything <c>TeamsNav</c> injects: the catalog
    /// fake as <see cref="ITeamCatalog"/> and default <see cref="TeamOptions"/>, plus the
    /// <c>huddleStorage</c> JS calls: <c>get</c> answers <paramref name="stored"/> (or throws), and
    /// <c>set</c> is recorded (or throws when <paramref name="storageThrows"/>). Later tasks add their
    /// own services here so these rows stay green. The create actions' <see cref="ITeamFolders"/> is
    /// <paramref name="folders"/> (a fresh fake when omitted), and <paramref name="libraryEnabled"/> /
    /// <paramref name="tasksEnabled"/> set <c>Library.Enabled</c> / <c>Tasks.Enabled</c> (both on by default).
    /// </summary>
    private static MudBunitContext NewContext(
        FakeTeamCatalog catalog,
        string? stored = null,
        bool storageThrows = false,
        FakeTeamFolders? folders = null,
        bool libraryEnabled = true,
        bool tasksEnabled = true)
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton<ITeamCatalog>(catalog);
        ctx.Services.AddSingleton<ITeamFolders>(folders ?? new FakeTeamFolders());
        TeamOptions teamOptions = new();
        teamOptions.Library.Enabled = libraryEnabled;
        teamOptions.Tasks.Enabled = tasksEnabled;
        ctx.Services.AddSingleton(Options.Create(teamOptions));
        if (storageThrows)
        {
            ctx.JSInterop.Setup<string?>("huddleStorage.get", _ => true).SetException(new JSException("blocked"));
            ctx.JSInterop.SetupVoid("huddleStorage.set", _ => true).SetException(new JSException("blocked"));
        }
        else
        {
            ctx.JSInterop.Setup<string?>("huddleStorage.get", _ => true).SetResult(stored);
            ctx.JSInterop.SetupVoid("huddleStorage.set", _ => true).SetVoidResult();
        }

        return ctx;
    }

    /// <summary>How many times the nav has read <c>huddleStorage.get</c>.</summary>
    private static int Reads(MudBunitContext ctx) =>
        ctx.JSInterop.Invocations.Count(invocation => string.Equals(invocation.Identifier, "huddleStorage.get", StringComparison.Ordinal));

    /// <summary>The arguments of every <c>huddleStorage.set</c> call, in order.</summary>
    private static IReadOnlyList<object?>[] Writes(MudBunitContext ctx) =>
        [.. ctx.JSInterop.Invocations
            .Where(invocation => string.Equals(invocation.Identifier, "huddleStorage.set", StringComparison.Ordinal))
            .Select(invocation => invocation.Arguments)];

    /// <summary>The chevron button of <paramref name="team"/>'s row.</summary>
    private static AngleSharp.Dom.IElement Chevron(IRenderedComponent<ContainerFragment> cut, string team) =>
        RowFor(cut, team).QuerySelectorAll("button.teams-nav-chevron").Single();

    /// <summary>Clicks the chevron of <paramref name="team"/>'s row on the renderer's synchronization context.</summary>
    private static Task ClickChevronAsync(IRenderedComponent<ContainerFragment> cut, string team) =>
        cut.InvokeAsync(() => Chevron(cut, team).ClickAsync());

    /// <summary>The trimmed text of every Project link, in render order.</summary>
    private static string[] ProjectTexts(IRenderedComponent<ContainerFragment> cut) =>
        [.. cut.FindAll(".nav-project-link a").Select(a => a.TextContent.Trim())];

    private static IRenderedComponent<ContainerFragment> RenderNav(MudBunitContext ctx) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TeamsNav>(0);
            builder.CloseComponent();
        });

    /// <summary>A Team with the given Projects, and one member unless <paramref name="hasMembers"/> is <see langword="false"/>.</summary>
    private static TeamSummary Team(string name, IReadOnlyList<string>? projects = null, bool hasMembers = true)
    {
        IReadOnlyList<string> members = hasMembers ? ["Nova"] : [];
        return new TeamSummary(name, projects ?? [], members, true);
    }

    /// <summary>The row <c>div.hover-reveal-row</c> whose Team link text is <paramref name="team"/>.</summary>
    private static AngleSharp.Dom.IElement RowFor(IRenderedComponent<ContainerFragment> cut, string team) =>
        cut.FindAll("div.hover-reveal-row").Single(row => string.Equals(row.QuerySelector(".hover-reveal-link a")?.TextContent.Trim(), team, StringComparison.Ordinal));

    /// <summary>The "Team actions" button of <paramref name="team"/>'s row.</summary>
    private static AngleSharp.Dom.IElement MenuButton(IRenderedComponent<ContainerFragment> cut, string team) =>
        RowFor(cut, team).QuerySelectorAll(".hover-reveal-menu-button").Single();

    /// <summary>The trimmed text of every open <c>MudMenu</c> item (role <c>menuitem</c>, class <c>mud-menu-item</c>).</summary>
    private static string[] MenuItemTexts(IRenderedComponent<ContainerFragment> cut) =>
        [.. cut.FindAll("div.mud-menu-item").Select(item => item.TextContent.Trim())];

    /// <summary>The trimmed text of every Team link, in render order.</summary>
    private static string[] LinkTexts(IRenderedComponent<ContainerFragment> cut) =>
        [.. cut.FindAll(".hover-reveal-link a").Select(a => a.TextContent.Trim())];
}

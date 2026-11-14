using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using Agency.Huddle.App;
using Agency.Huddle.App.Components.Teams;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Agents;
using Agency.Huddle.App.Tabs;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Teams;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.Tasks;
using Agency.Huddle.App.Components.Shared;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>
/// Pins Spec §6.5's listing half: <c>TeamsNav</c> is one <c>MudNavMenu.teams-nav</c> whose root rows
/// are the Teams, under no "Teams" header; each Team is a <c>div.hover-reveal-row</c> whose link goes to
/// the escaped <c>/teams/{team}</c> route. An open Team is a scope list (the Team, then its Projects, each with a count of
/// open Tasks and one of them selected) followed by the sections of the selected scope: ROOMS (with a "+"), TASKS and
/// LIBRARY. A memberless Team shows a "No members" hint, the "Team actions" menu (button or right-click) offers
/// "New project", and "Build a team" trails the list. It follows <see cref="ITeamCatalog.Changed"/> and unsubscribes on dispose.
/// </summary>
public sealed class TeamsNavTests
{
    /// <summary>The Teams are the nav's root rows, in the catalog's own order (not sorted), with no "Teams" group header above them.</summary>
    [Fact]
    public async Task Renders_TeamsAsRootRows_InCatalogOrder()
    {
        FakeTeamCatalog catalog = new()
        {
            Teams = [Team("Sales & Ops"), Team("Business"), Team("Lab #2")],
        };

        await using MudBunitContext ctx = NewContext(catalog);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        Assert.Empty(cut.FindAll(".teams-nav .mud-nav-group"));
        string[] expected = ["Sales & Ops", "Business", "Lab #2"];
        string[] actual = [.. cut.FindAll(".teams-nav > div.hover-reveal-row .hover-reveal-link a .sidebar-row-label").Select(label => label.TextContent.Trim())];
        Assert.Equal(expected, actual);
    }

    /// <summary>Every open Team lists its scope rows (the Team, then its Projects) and its sections, right after its own row, and every segment of every href is escaped.</summary>
    [Fact]
    public async Task OpenTeams_ListTheirScopeRowsAndSections_AfterTheirOwnRow()
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

        await using MudBunitContext ctx = NewContext(catalog, stored: AllOpen);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        string?[] expectedHrefs =
        [
            "/teams/Business",
            "/teams/Business",
            "/teams/Business/projects/2025%20Taxes%20Project",
            "/teams/Business/projects/Marketing",
            "/teams/Business/tasks/all-tasks",
            "/teams/Business/tasks/my-tasks",
            "/teams/Business/files",
            "/teams/Sales%20%26%20Ops",
            "/teams/Sales%20%26%20Ops",
            "/teams/Sales%20%26%20Ops/tasks/all-tasks",
            "/teams/Sales%20%26%20Ops/tasks/my-tasks",
            "/teams/Sales%20%26%20Ops/files",
            "/teams/Lab%20%232",
            "/teams/Lab%20%232",
            "/teams/Lab%20%232/projects/Item%20%231",
            "/teams/Lab%20%232/tasks/all-tasks",
            "/teams/Lab%20%232/tasks/my-tasks",
            "/teams/Lab%20%232/files",
            "/teams/build",
        ];
        cut.WaitForAssertion(() =>
        {
            string?[] actualHrefs = [.. cut.FindAll(".teams-nav a[href]").Select(a => a.GetAttribute("href"))];
            Assert.Equal(expectedHrefs, actualHrefs);
        });

        string[] expectedProjects = ["2025 Taxes Project", "Marketing", "Item #1"];
        Assert.Equal(expectedProjects, ProjectTexts(cut));
    }

    /// <summary>The Team row's link goes to the Team page, and its "Team actions" button opens a menu whose items are "New room" and "New project".</summary>
    [Fact]
    public async Task Team_Row_LinksToTheTeamPage_AndHasAMenuWithNewProject()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business")] };

        await using MudBunitContext ctx = NewContext(catalog);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        Assert.Equal("/teams/Business", cut.Find(".hover-reveal-link a").GetAttribute("href"));
        Assert.Empty(MenuItemTexts(cut));

        AngleSharp.Dom.IElement button = MenuButton(cut, "Business");
        Assert.Equal("Actions for Business", button.GetAttribute("aria-label"));
        await cut.InvokeAsync(() => button.ClickAsync());

        string[] expected = ["New room", "New project…", "Rename team…", "Archive team…"];
        Assert.Equal(expected, MenuItemTexts(cut));
    }

    /// <summary>The Team's header card names the Team in its label and, under it, the Team type's display name and the member count.</summary>
    [Fact]
    public async Task TeamHeaderCard_WithAType_ShowsTheTypeAndMemberCount()
    {
        TeamSummary team = Team("Business") with { Members = ["A", "B", "C", "D", "E", "F", "G", "H"], TeamType = new VersionedRef("web", "1.2.0") };
        FakeTeamCatalog catalog = new() { Teams = [team] };

        await using MudBunitContext ctx = NewContext(catalog, stored: AllOpen);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        cut.WaitForAssertion(() =>
        {
            IElement card = cut.Find(".teams-nav-scope-row[data-scope='team']");
            Assert.Equal("Business", card.QuerySelector(".teams-nav-scope-label")?.TextContent.Trim());
            Assert.Equal("Web · 8 members", card.QuerySelector(".teams-nav-scope-subtitle")?.TextContent.Trim());
        });
    }

    /// <summary>A Team with no type shows just its member count under the name, singular for one member.</summary>
    [Fact]
    public async Task TeamHeaderCard_WithoutAType_ShowsOnlyTheMemberCount()
    {
        FakeTeamCatalog catalog = new()
        {
            Teams = [Team("Business"), Team("Lab #2", hasMembers: false) with { Members = ["A", "B"] }],
        };

        await using MudBunitContext ctx = NewContext(catalog, stored: AllOpen);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        cut.WaitForAssertion(() =>
        {
            string[] subtitles = [.. cut.FindAll(".teams-nav-scope-row[data-scope='team'] .teams-nav-scope-subtitle").Select(subtitle => subtitle.TextContent.Trim())];
            string[] expected = ["1 member", "2 members"];
            Assert.Equal(expected, subtitles);
        });
    }

    /// <summary>A Project row is the unchanged chip, name and count: it has no subtitle.</summary>
    [Fact]
    public async Task ProjectRows_HaveNoSubtitle()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", projects: ["Marketing"])] };

        await using MudBunitContext ctx = NewContext(catalog, stored: AllOpen);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        cut.WaitForAssertion(() =>
        {
            Assert.Single(cut.FindAll(".teams-nav-scope-row[data-scope='project']"));
            Assert.Empty(cut.FindAll(".teams-nav-scope-row[data-scope='project'] .teams-nav-scope-subtitle"));
            Assert.Single(cut.FindAll(".teams-nav-scope-subtitle"));
        });
    }

    /// <summary>Right-clicking the Team row opens the same menu as its button does.</summary>
    [Fact]
    public async Task RightClickingATeamRow_OpensTheSameMenu()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business")] };

        await using MudBunitContext ctx = NewContext(catalog);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        await cut.InvokeAsync(() => RowFor(cut, "Business").ContextMenuAsync());

        string[] expected = ["New room", "New project…", "Rename team…", "Archive team…"];
        Assert.Equal(expected, MenuItemTexts(cut));
    }

    /// <summary>
    /// Narrowed to one Team (its Activity bar shortcut), the nav lists only that Team's scope rows and sections, open without a
    /// click on any chevron: no Team rows of the all-Teams list, no other Team's links, and no "Build a team".
    /// </summary>
    [Fact]
    public async Task Team_Set_ListsOnlyThatTeamsScopesAndSections()
    {
        FakeTeamCatalog catalog = new()
        {
            Teams = [Team("Business", projects: ["Marketing"]), Team("Lab #2", projects: ["Item #1"])],
        };

        await using MudBunitContext ctx = NewContext(catalog);
        IRenderedComponent<ContainerFragment> cut = RenderShortcut(ctx, "lab #2");

        Assert.Empty(cut.FindAll("div.hover-reveal-row"));
        string?[] expectedHrefs =
        [
            "/teams/Lab%20%232",
            "/teams/Lab%20%232/projects/Item%20%231",
            "/teams/Lab%20%232/tasks/all-tasks",
            "/teams/Lab%20%232/tasks/my-tasks",
            "/teams/Lab%20%232/files",
        ];
        cut.WaitForAssertion(() =>
        {
            string?[] actualHrefs = [.. cut.FindAll(".teams-nav a[href]").Select(a => a.GetAttribute("href"))];
            Assert.Equal(expectedHrefs, actualHrefs);
        });
    }

    /// <summary>A Team with no Rooms, Projects or Task Views says so under its shortcut, below its header card, which is the one link to its Team page and keeps the switcher.</summary>
    [Fact]
    public async Task Team_SetAndEmpty_ShowsAnEmptyStateUnderTheHeaderCard()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business")] };

        await using MudBunitContext ctx = NewContext(catalog, tasksEnabled: false, libraryEnabled: false);
        IRenderedComponent<ContainerFragment> cut = RenderShortcut(ctx, "Business");

        cut.WaitForAssertion(() => Assert.Equal("Nothing in Business yet", cut.Find(".empty-state .empty-state-title").TextContent.Trim()));
        string? href = Assert.Single(cut.FindAll(".teams-nav a[href]")).GetAttribute("href");
        Assert.Equal("/teams/Business", href);
        IElement card = Assert.Single(cut.FindAll(".teams-nav-scope-row"));
        Assert.Equal("team", card.GetAttribute("data-scope"));
        Assert.NotNull(card.QuerySelector(".teams-nav-switcher-button"));
    }

    /// <summary>Under a Team shortcut with nothing waiting on the Human, the Rooms section lists its Rooms flat, with no Needs you or All folder and no "New room" row (the header's "+" opens it).</summary>
    [Fact]
    public async Task Team_SetAndNothingWaiting_ListsTheRoomsFlat_UnderTheRoomsHeader()
    {
        ITeamDirectory directory = ScriptedRoomsDirectory.Create(out ScriptedRoomsDirectory script);
        script.Rooms.Add(new Room("teamroom", "Business", DateTimeOffset.UnixEpoch) { Team = "Business", IsTeamRoom = true });
        script.Rooms.Add(new Room("chat", "Ada", DateTimeOffset.UnixEpoch.AddDays(1)) { Team = "Business" });
        FakeTeamCatalog catalog = new() { Teams = [Team("Business")] };

        await using MudBunitContext ctx = NewContext(catalog, tasksEnabled: false, directory: directory);
        IRenderedComponent<ContainerFragment> cut = RenderShortcut(ctx, "Business");

        cut.WaitForAssertion(() =>
        {
            string?[] rooms = ["/rooms/teamroom", "/rooms/chat"];
            Assert.Equal(rooms, SectionHrefs(cut, "rooms"));
            Assert.Empty(cut.FindAll(".teams-nav-subfolder"));
            Assert.Empty(cut.FindAll(".teams-nav-new-chat"));
            Assert.DoesNotContain("New room", cut.Find(".teams-nav").TextContent, StringComparison.Ordinal); // contains-ok: haystack is the rendered nav's text
        });
    }

    /// <summary>
    /// Every Room row takes its Members from the one batched read the list makes with its Rooms, never a query per row (the
    /// scripted directory refuses <c>GetRoomMembersAsync</c>): the 1:1 Room shows its Agent's avatar, the group Room does not.
    /// </summary>
    [Fact]
    public async Task Team_RoomRows_TakeMembersFromOneBatchedRead()
    {
        ITeamDirectory directory = ScriptedRoomsDirectory.Create(out ScriptedRoomsDirectory script);
        script.Rooms.Add(new Room("teamroom", "Business", DateTimeOffset.UnixEpoch) { Team = "Business", IsTeamRoom = true });
        script.Rooms.Add(new Room("chat", "Ada", DateTimeOffset.UnixEpoch.AddDays(1)) { Team = "Business" });
        User human = new("human", "You", UserKind.Human, null);
        User ada = new("ada-id", "Ada", UserKind.Agent, null);
        User ben = new("ben-id", "Ben", UserKind.Agent, null);
        script.Members["teamroom"] = [human, ada, ben];
        script.Members["chat"] = [human, ada];
        FakeTeamCatalog catalog = new() { Teams = [Team("Business")] };

        await using MudBunitContext ctx = NewContext(catalog, tasksEnabled: false, directory: directory);
        IRenderedComponent<ContainerFragment> cut = RenderShortcut(ctx, "Business");

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("a[href='/rooms/chat'] .room-row-avatar"));
            Assert.Empty(cut.FindAll("a[href='/rooms/teamroom'] .room-row-avatar"));
            Assert.Equal(script.RoomReads, script.MemberReads);
        });
    }

    /// <summary>
    /// Under a Team shortcut, a Room waiting on the Human splits the Rooms section into "Needs you" (that Room) above "All"
    /// (the rest), as the Chats view's All grouping does; once nothing waits the Rooms are flat again.
    /// </summary>
    [Fact]
    public async Task Team_SetAndARoomWaiting_SplitsTheRoomsIntoNeedsYouAndAll()
    {
        ITeamDirectory directory = ScriptedRoomsDirectory.Create(out ScriptedRoomsDirectory script);
        script.Rooms.Add(new Room("teamroom", "Business", DateTimeOffset.UnixEpoch) { Team = "Business", IsTeamRoom = true });
        script.Rooms.Add(new Room("chat", "Ada", DateTimeOffset.UnixEpoch.AddDays(1)) { Team = "Business" });
        FakeTeamCatalog catalog = new() { Teams = [Team("Business")] };

        await using MudBunitContext ctx = NewContext(catalog, tasksEnabled: false, directory: directory);
        IRenderedComponent<ContainerFragment> cut = RenderShortcut(ctx, "Business");
        Agency.Huddle.App.NeedsYou.NeedsYouTracker tracker = ctx.Services.GetRequiredService<Agency.Huddle.App.NeedsYou.NeedsYouTracker>();
        cut.WaitForAssertion(() => Assert.Equal(2, SectionHrefs(cut, "rooms").Length));

        await cut.InvokeAsync(() => tracker.Seed("chat", DateTimeOffset.UnixEpoch, "coo"));

        cut.WaitForAssertion(() =>
        {
            string[] subfolders = ["Needs you", "All"];
            string[] actual = [.. cut.FindAll(".teams-nav-subfolder > .mud-nav-link .mud-nav-link-text").Select(text => text.TextContent.Trim())];
            Assert.Equal(subfolders, actual);
            IElement needsYou = cut.Find(".teams-nav-subfolder.room-folder-needs-you");
            Assert.Equal("/rooms/chat", Assert.Single(needsYou.QuerySelectorAll("a[href]")).GetAttribute("href"));
            IElement all = cut.Find(".teams-nav-subfolder.teams-nav-all");
            Assert.Equal("/rooms/teamroom", Assert.Single(all.QuerySelectorAll("a[href]")).GetAttribute("href"));
        });

        await cut.InvokeAsync(() => tracker.Dismiss("chat"));

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".teams-nav-subfolder")));
    }

    /// <summary>The Rooms header's "+" under a Team shortcut opens the New chat panel for that Team (no Project) and a second click closes it.</summary>
    [Fact]
    public async Task Team_Set_RoomsPlus_OpensTheTeamsNewChatPanel()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business")] };

        await using MudBunitContext ctx = NewContext(catalog, tasksEnabled: false);
        IRenderedComponent<ContainerFragment> cut = RenderShortcut(ctx, "Business");
        Assert.Empty(cut.FindAll(".new-chat-panel"));

        await cut.InvokeAsync(() => cut.Find(".teams-nav-section-rooms button.teams-nav-section-add").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal("New room in Business", cut.Find(".new-chat-panel").GetAttribute("aria-label")));
        Assert.Equal("New room in Business", cut.Find(".new-chat-heading").TextContent.Trim());

        await cut.InvokeAsync(() => cut.Find(".teams-nav-section-rooms button.teams-nav-section-add").ClickAsync());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".new-chat-panel")));
    }

    /// <summary>Under a Team shortcut, the header card's Unfold More button unfolds every Team inside the same card (not a popover) in catalog order, the shown one marked in its own place so no Team moves with the selection, each drawn as the Team row is, and picking one folds it, raises <see cref="TeamsNav.TeamChanged"/> with it and opens its Team page.</summary>
    [Fact]
    public async Task Team_Set_Switcher_UnfoldsEveryTeamInCatalogOrder_AndRaisesThePickedOne()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Lab #2"), Team("Business", projects: ["Taxes"]), Team("Sales")] };
        List<string?> picked = [];

        await using MudBunitContext ctx = NewContext(catalog, tasksEnabled: false);
        IRenderedComponent<ContainerFragment> cut = RenderShortcut(ctx, "Business", picked.Add);

        IElement button = cut.Find(".teams-nav-scope-header button.teams-nav-switcher-button");
        Assert.Equal("Switch team", button.GetAttribute("aria-label"));
        Assert.Equal("false", button.GetAttribute("aria-expanded"));
        Assert.Empty(cut.FindAll(".teams-nav-switcher-list"));
        await cut.InvokeAsync(() => button.ClickAsync());

        cut.WaitForAssertion(() =>
        {
            IElement card = cut.Find(".teams-nav-team-card.teams-nav-team-card-open");
            IElement list = Assert.Single(card.Children, child => child.ClassList.Contains("teams-nav-switcher-list"));
            Assert.True(list.PreviousElementSibling?.ClassList.Contains("teams-nav-scope-header"));
            string[] expected = ["Lab #2", "Business", "Sales"];
            string[] names = [.. list.QuerySelectorAll(".teams-nav-switcher-item:not(.teams-nav-switcher-build) .teams-nav-scope-label").Select(label => label.TextContent.Trim())];
            Assert.Equal(expected, names);
            Assert.Equal("Build a team", list.Children[^1].TextContent.Trim());
            string[] subtitles = [.. list.QuerySelectorAll(".teams-nav-switcher-item .teams-nav-scope-subtitle").Select(subtitle => subtitle.TextContent.Trim())];
            string[] expectedSubtitles = ["1 member", "1 member", "1 member"];
            Assert.Equal(expectedSubtitles, subtitles);
            Assert.Equal(3, list.QuerySelectorAll(".teams-nav-switcher-item .mud-avatar.teams-nav-scope-avatar").Length);
            IElement current = Assert.Single(list.QuerySelectorAll(".teams-nav-switcher-item[aria-current='true']"));
            Assert.Equal("Business", current.GetAttribute("title"));
            Assert.Same(current, list.Children[1]);
            Assert.Contains("selected-shape", current.ClassList);
        });
        Assert.Equal("true", cut.Find("button.teams-nav-switcher-button").GetAttribute("aria-expanded"));

        IElement sales = cut.FindAll(".teams-nav-switcher-item").Single(item => item.GetAttribute("title") == "Sales");
        await cut.InvokeAsync(() => sales.ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal(["Sales"], picked));
        Assert.EndsWith("/teams/Sales", ctx.Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll(".teams-nav-switcher-list"));
        Assert.Empty(cut.FindAll(".teams-nav-team-card-open"));
    }

    /// <summary>The Activity bar's Teams view shows one Team as a shortcut does, the catalog's first while none was used, with its Team list unfolded from the start and still unfolded after a pick.</summary>
    [Fact]
    public async Task TeamsView_ShowsTheFirstTeam_WithTheListUnfolded_AndKeepsItUnfoldedAfterAPick()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", projects: ["Taxes"]), Team("Sales")] };
        List<string?> picked = [];

        await using MudBunitContext ctx = NewContext(catalog, tasksEnabled: false);
        IRenderedComponent<ContainerFragment> cut = RenderTeamsView(ctx, null, picked.Add);

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("Business", cut.Find(".teams-nav-scope-header .teams-nav-scope-label").TextContent.Trim());
            Assert.Equal("true", cut.Find("button.teams-nav-switcher-button").GetAttribute("aria-expanded"));
            Assert.Single(cut.FindAll(".teams-nav-switcher-item[title='Sales']"));
        });
        Assert.Empty(cut.FindAll(".teams-nav > div.hover-reveal-row"));

        await cut.InvokeAsync(() => cut.Find(".teams-nav-switcher-item[title='Sales']").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal(["Sales"], picked));
        Assert.Single(cut.FindAll(".teams-nav-team-card-open"));
    }

    /// <summary>The Teams view shows the last used Team while the catalog lists it, and falls back to the first when it is gone.</summary>
    [Fact]
    public async Task TeamsView_ShowsTheLastUsedTeam_OrTheFirstWhenItIsGone()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business"), Team("Sales")] };

        await using MudBunitContext salesCtx = NewContext(catalog, tasksEnabled: false);
        IRenderedComponent<ContainerFragment> sales = RenderTeamsView(salesCtx, "Sales");
        sales.WaitForAssertion(() => Assert.Equal("Sales", sales.Find(".teams-nav-scope-header .teams-nav-scope-label").TextContent.Trim()));

        await using MudBunitContext goneCtx = NewContext(catalog, tasksEnabled: false);
        IRenderedComponent<ContainerFragment> gone = RenderTeamsView(goneCtx, "Archived Lab");
        gone.WaitForAssertion(() => Assert.Equal("Business", gone.Find(".teams-nav-scope-header .teams-nav-scope-label").TextContent.Trim()));
    }

    /// <summary>"Build a team", the last row of the unfolded Team list, opens the Build a team page.</summary>
    [Fact]
    public async Task Switcher_BuildATeam_OpensTheBuildPage()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business")] };

        await using MudBunitContext ctx = NewContext(catalog, tasksEnabled: false);
        IRenderedComponent<ContainerFragment> cut = RenderTeamsView(ctx, null);

        IElement build = cut.Find(".teams-nav-switcher-list > .teams-nav-switcher-build");
        await cut.InvokeAsync(() => build.ClickAsync());

        Assert.EndsWith("/teams/build", ctx.Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);
    }

    /// <summary>The nav with no Team lists every Team already, so its header cards carry no switcher.</summary>
    [Fact]
    public async Task AllTeams_HeaderCard_HasNoSwitcher()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business")] };

        await using MudBunitContext ctx = NewContext(catalog, stored: AllOpen);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".teams-nav-scope-header")));
        Assert.Empty(cut.FindAll(".teams-nav-switcher-button"));
    }

    /// <summary>Renders the nav as the Activity bar's Teams view does: one Team, the last used or the first, with its Team list unfolded.</summary>
    /// <param name="ctx">The test context.</param>
    /// <param name="lastUsed">The last Team used, or <see langword="null"/> before any was.</param>
    /// <param name="onTeamChanged">Receives the Team picked in the switcher, when given.</param>
    private static IRenderedComponent<ContainerFragment> RenderTeamsView(MudBunitContext ctx, string? lastUsed, Action<string?>? onTeamChanged = null) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TeamsNav>(0);
            builder.AddComponentParameter(1, nameof(TeamsNav.IsTeamsView), true);
            builder.AddComponentParameter(2, nameof(TeamsNav.Team), lastUsed);
            if (onTeamChanged is not null)
            {
                builder.AddComponentParameter(3, nameof(TeamsNav.TeamChanged), EventCallback.Factory.Create(ctx, onTeamChanged));
            }

            builder.CloseComponent();
        });

    /// <summary>Renders the nav narrowed to <paramref name="team"/>, as its Activity bar shortcut does.</summary>
    /// <param name="ctx">The test context.</param>
    /// <param name="team">The Team the shortcut shows.</param>
    /// <param name="onTeamChanged">Receives the Team picked in the switcher, when given.</param>
    private static IRenderedComponent<ContainerFragment> RenderShortcut(MudBunitContext ctx, string team, Action<string?>? onTeamChanged = null) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TeamsNav>(0);
            builder.AddComponentParameter(1, nameof(TeamsNav.Team), team);
            if (onTeamChanged is not null)
            {
                builder.AddComponentParameter(2, nameof(TeamsNav.TeamChanged), EventCallback.Factory.Create(ctx, onTeamChanged));
            }

            builder.CloseComponent();
        });

    /// <summary>A Team no Persona is labelled with shows a trailing member count of 0 whose tooltip says "No members", inside its own row and never as row text.</summary>
    [Fact]
    public async Task TeamWithNoMembers_ShowsATrailingZeroWithTheNoMembersTooltip()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business"), Team("Lab", hasMembers: false)] };

        await using MudBunitContext ctx = NewContext(catalog);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        IElement meta = RowFor(cut, "Lab").QuerySelectorAll(".sidebar-row-meta").Single();
        Assert.Equal("0", meta.TextContent.Trim());
        Assert.Equal("No members", meta.GetAttribute("title"));
        Assert.DoesNotContain("No members", RowFor(cut, "Lab").QuerySelector(".hover-reveal-link")?.TextContent, StringComparison.Ordinal);
    }

    /// <summary>A Team with members shows its member count as the trailing meta, with a pluralised tooltip.</summary>
    [Fact]
    public async Task TeamWithMembers_ShowsTheMemberCount()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business"), Team("Sales & Ops")] };

        await using MudBunitContext ctx = NewContext(catalog);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        IElement meta = RowFor(cut, "Business").QuerySelectorAll(".sidebar-row-meta").Single();
        Assert.Equal("1", meta.TextContent.Trim());
        Assert.Equal("1 member", meta.GetAttribute("title"));
    }

    /// <summary>At <c>/teams/Business/files</c> the Business link carries the active class and the other Team's link does not.</summary>
    [Fact]
    public async Task CurrentRoute_HighlightsTheTeamLink()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business"), Team("Sales & Ops")] };

        await using MudBunitContext ctx = NewContext(catalog);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/teams/Business/files");
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        Assert.Contains("active", cut.Find(".hover-reveal-link a[href='/teams/Business']").ClassList);
        Assert.DoesNotContain("active", cut.Find(".hover-reveal-link a[href='/teams/Sales%20%26%20Ops']").ClassList);
    }

    /// <summary>The Teams group ends with a "Build a team" action that opens <c>/teams/build</c>.</summary>
    [Fact]
    public async Task BuildATeamAction_OpensTheBuildPage()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business")] };

        await using MudBunitContext ctx = NewContext(catalog);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        IReadOnlyList<IElement> links = cut.FindAll(".nav-action-link a");
        IElement link = links[^1];
        Assert.Equal("Build a team", link.TextContent.Trim());
        Assert.Equal("/teams/build", link.GetAttribute("href"));
    }

    /// <summary>With no Teams at all the "Build a team" action is still offered and no Team row renders.</summary>
    [Fact]
    public async Task BuildATeamAction_IsPresent_WhenThereAreNoTeams()
    {
        FakeTeamCatalog catalog = new();

        await using MudBunitContext ctx = NewContext(catalog);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        Assert.Equal("Build a team", cut.Find(".nav-action-link").TextContent.Trim());
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

        await ctx.DisposeComponentsOnDispatcherAsync();

        Assert.Equal(0, catalog.ChangedSubscriberCount);
    }

    /// <summary>With nothing stored every Team starts collapsed: no Project shows, and each chevron says "Expand Business" with <c>aria-expanded="false"</c>. Nothing is written on load.</summary>
    [Fact]
    public async Task Default_AllTeamsCollapsed_ShowingNoProjects()
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
            Assert.Empty(ProjectTexts(cut));
            Assert.Empty(cut.FindAll(".teams-nav-scope-row"));
            Assert.Equal("false", Chevron(cut, "Business").GetAttribute("aria-expanded"));
            Assert.Equal("Expand Business", Chevron(cut, "Business").GetAttribute("aria-label"));
            Assert.Equal("false", Chevron(cut, "Sales & Ops").GetAttribute("aria-expanded"));
            Assert.Equal("Expand Sales & Ops", Chevron(cut, "Sales & Ops").GetAttribute("aria-label"));
        });
        Assert.Empty(Writes(ctx));
    }

    /// <summary>Clicking the chevron opens the Team: label "Collapse Business", <c>aria-expanded="true"</c>, its Projects shown, and <c>teamsNav:expanded</c> stored as <c>["business"]</c>.</summary>
    [Fact]
    public async Task Chevron_Click_OpensAndShowsProjects()
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
            Assert.Equal("true", Chevron(cut, "Business").GetAttribute("aria-expanded"));
            Assert.Equal("Collapse Business", Chevron(cut, "Business").GetAttribute("aria-label"));
            string[] projects = ["Marketing"];
            Assert.Equal(projects, ProjectTexts(cut));
            Assert.Equal("false", Chevron(cut, "Sales & Ops").GetAttribute("aria-expanded"));
        });
        IReadOnlyList<object?> write = Assert.Single(Writes(ctx));
        Assert.Equal(ExpandedKey, write[0]);
        Assert.Equal(BusinessOnly, write[1]);
    }

    /// <summary>A second click collapses the Team again, hides its Projects and stores the now-empty list <c>[]</c>.</summary>
    [Fact]
    public async Task Chevron_ClickedTwice_CollapsesAgainAndStoresAnEmptyList()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", projects: ["Marketing"])] };

        await using MudBunitContext ctx = NewContext(catalog);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);
        cut.WaitForAssertion(() => Assert.Equal(1, Reads(ctx)));

        await ClickChevronAsync(cut, "Business");
        cut.WaitForAssertion(() => Assert.Equal("true", Chevron(cut, "Business").GetAttribute("aria-expanded")));
        await ClickChevronAsync(cut, "Business");

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("false", Chevron(cut, "Business").GetAttribute("aria-expanded"));
            Assert.Equal("Expand Business", Chevron(cut, "Business").GetAttribute("aria-label"));
            Assert.Empty(ProjectTexts(cut));
        });
        IReadOnlyList<object?>[] writes = Writes(ctx);
        Assert.Equal(2, writes.Length);
        Assert.Equal(ExpandedKey, writes[1][0]);
        Assert.Equal("[]", writes[1][1]);
    }

    /// <summary>A Team stored as opened starts open (the stored name is lower-case, the Team's own is not); an unstored Team stays collapsed; nothing is written on load.</summary>
    [Fact]
    public async Task StoredOpened_OnLoad_StartsOpen()
    {
        FakeTeamCatalog catalog = new()
        {
            Teams = [Team("Business", projects: ["Marketing"]), Team("Sales & Ops", projects: ["Budget"])],
        };

        await using MudBunitContext ctx = NewContext(catalog, stored: BusinessOnly);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("true", Chevron(cut, "Business").GetAttribute("aria-expanded"));
            Assert.Equal("Collapse Business", Chevron(cut, "Business").GetAttribute("aria-label"));
            string[] projects = ["Marketing"];
            Assert.Equal(projects, ProjectTexts(cut));
            Assert.Equal("false", Chevron(cut, "Sales & Ops").GetAttribute("aria-expanded"));
        });
        Assert.Empty(Writes(ctx));
    }

    /// <summary>At a route inside a collapsed Team the Team is forced open, the other stays collapsed, and nothing is written to storage.</summary>
    [Fact]
    public async Task CurrentRouteInsideACollapsedTeam_ForcesItOpen_WithoutChangingStorage()
    {
        FakeTeamCatalog catalog = new()
        {
            Teams = [Team("Business", projects: ["Marketing"]), Team("Sales & Ops", projects: ["Budget"])],
        };

        await using MudBunitContext ctx = NewContext(catalog, stored: "[]");
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

        await using MudBunitContext ctx = NewContext(catalog, stored: "[]");
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

        await using MudBunitContext ctx = NewContext(catalog, stored: "[]");
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
            Assert.Equal("false", Chevron(cut, "Other").GetAttribute("aria-expanded"));
            string[] after = ["Marketing"];
            Assert.Equal(after, ProjectTexts(cut));
        });
        Assert.Empty(Writes(ctx));
    }

    /// <summary>A chevron click on the Team the route forced open collapses it for real: Projects hidden and the (empty) opened list written.</summary>
    [Fact]
    public async Task Chevron_OnTheRouteForcedOpenTeam_CollapsesItAndWritesStorage()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", projects: ["Marketing"])] };

        await using MudBunitContext ctx = NewContext(catalog, stored: "[]");
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
        Assert.Equal(ExpandedKey, write[0]);
        Assert.Equal("[]", write[1]);
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

    /// <summary>Stored text that is not a JSON array of strings leaves everything collapsed and never throws: a further chevron click stores just that Team, so nothing was half-loaded.</summary>
    [Theory]
    [InlineData("not json")]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("[1,2]")]
    [InlineData("[null]")]
    public async Task MalformedStorage_CollapsesEverything_WithoutThrowing(string stored)
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
            Assert.Equal("true", Chevron(cut, "Business").GetAttribute("aria-expanded"));
            Assert.Equal("false", Chevron(cut, "Sales & Ops").GetAttribute("aria-expanded"));
            string[] projects = ["Marketing"];
            Assert.Equal(projects, ProjectTexts(cut));
        });
        IReadOnlyList<object?> write = Assert.Single(Writes(ctx));
        Assert.Equal(BusinessOnly, write[1]);
    }

    /// <summary>A stored JSON <c>null</c> deserializes to no array at all; everything is collapsed and a click still works.</summary>
    [Fact]
    public async Task StoredJsonNull_CollapsesEverything_WithoutThrowing()
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
            Assert.Equal("true", Chevron(cut, "Business").GetAttribute("aria-expanded"));
            Assert.Equal("false", Chevron(cut, "Sales & Ops").GetAttribute("aria-expanded"));
        });
        IReadOnlyList<object?> write = Assert.Single(Writes(ctx));
        Assert.Equal(BusinessOnly, write[1]);
    }

    /// <summary>When the storage read throws (<c>JSException</c>), everything is collapsed; when the write throws too, a click still opens the Team in memory.</summary>
    [Fact]
    public async Task StorageThrows_CollapsesEverything()
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
            Assert.Equal("false", Chevron(cut, "Business").GetAttribute("aria-expanded"));
            Assert.Equal("false", Chevron(cut, "Sales & Ops").GetAttribute("aria-expanded"));
        });

        await ClickChevronAsync(cut, "Business");

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("true", Chevron(cut, "Business").GetAttribute("aria-expanded"));
            string[] projects = ["Marketing"];
            Assert.Equal(projects, ProjectTexts(cut));
        });
    }

    /// <summary>A Team created after storage exists (its name is not in the stored list) starts collapsed, and the opened one stays open.</summary>
    [Fact]
    public async Task ATeamCreatedAfterStorageExists_StartsCollapsed()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", projects: ["Marketing"])] };

        await using MudBunitContext ctx = NewContext(catalog, stored: BusinessOnly);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);
        cut.WaitForAssertion(() => Assert.Equal("true", Chevron(cut, "Business").GetAttribute("aria-expanded")));

        catalog.Teams = [Team("Business", projects: ["Marketing"]), Team("Research", projects: ["Notes"])];
        catalog.Raise();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("false", Chevron(cut, "Research").GetAttribute("aria-expanded"));
            Assert.Equal("true", Chevron(cut, "Business").GetAttribute("aria-expanded"));
            string[] projects = ["Marketing"];
            Assert.Equal(projects, ProjectTexts(cut));
        });
        Assert.Empty(Writes(ctx));
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
        Assert.Equal("Project name", cut.Find(".mud-dialog label").TextContent.Trim());
        Assert.Equal("A Project named \"marketing\" already exists in Business.", cut.Find(".mud-dialog .mud-input-helper-text").TextContent.Trim());
    }

    /// <summary>The PROJECTS header's "+" (tooltip "Start a project") opens the same New project dialog for its Team, and OK creates the Project in that Team.</summary>
    [Fact]
    public async Task ProjectsHeaderPlus_OpensTheNewProjectDialog_AndCreatesTheProjectInThatTeam()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", projects: ["Marketing"])] };
        FakeTeamFolders folders = new();
        await using MudBunitContext ctx = NewContext(catalog, stored: BusinessOnly, folders: folders);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);
        // The "+" sits in the PROJECTS header, as the ROOMS and TASKS "+" sit in theirs, with a tooltip saying what it does.
        cut.WaitForAssertion(() => Assert.Equal("Start a project", Section(cut, "projects").QuerySelector(".teams-nav-section-header button.teams-nav-start-project")?.GetAttribute("aria-label")));
        Assert.Single(Section(cut, "projects").QuerySelectorAll(".teams-nav-section-header .mud-tooltip-root"));
        Assert.Contains("teams-nav-section-add", cut.Find("button.teams-nav-start-project").ClassList);

        _ = cut.InvokeAsync(() => { _ = cut.Find("button.teams-nav-start-project").ClickAsync(); });
        cut.WaitForAssertion(() => Assert.Equal("New project", cut.Find(".mud-dialog-title").TextContent.Trim()));
        catalog.Teams = [Team("Business", projects: ["Marketing", "Q4 Launch"])];
        await SubmitNameAsync(cut, "Q4 Launch");

        string[] expected = ["EnsureProjectIn:Business/Q4 Launch"];
        cut.WaitForAssertion(() => Assert.Equal(expected, folders.Calls));
    }

    /// <summary>
    /// "No project" narrows the sections to what belongs to no Project: the Rooms with no Project (or whose Project is gone),
    /// the <c>/no-project/tasks/{V}</c> Views and the <c>/no-project/files</c> Library; the Team row loses the Selected shape,
    /// and a second click returns to the Team scope.
    /// </summary>
    [Fact]
    public async Task NoProject_NarrowsTheSectionsToItemsWithNoProject_AndASecondClickReturnsToTheTeam()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", projects: ["Marketing"])] };
        await using MudBunitContext ctx = NewContext(catalog, stored: BusinessOnly, directory: ProjectRooms());
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);
        cut.WaitForAssertion(() => Assert.Equal(3, SectionHrefs(cut, "rooms").Length));

        await cut.InvokeAsync(() => cut.Find("button.teams-nav-no-project").ClickAsync());

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("true", cut.Find("button.teams-nav-no-project").GetAttribute("aria-pressed"));
            Assert.Empty(SelectedLabels(cut));
            string?[] rooms = ["/rooms/teamroom", "/rooms/orphan"];
            Assert.Equal(rooms, SectionHrefs(cut, "rooms"));
            string?[] views = ["/teams/Business/no-project/tasks/all-tasks", "/teams/Business/no-project/tasks/my-tasks"];
            Assert.Equal(views, SectionHrefs(cut, "tasks"));
            string?[] library = ["/teams/Business/no-project/files"];
            Assert.Equal(library, SectionHrefs(cut, "library"));
        });

        await cut.InvokeAsync(() => cut.Find("button.teams-nav-no-project").ClickAsync());

        string[] team = ["Business"];
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(team, SelectedLabels(cut));
            Assert.Equal(3, SectionHrefs(cut, "rooms").Length);
        });
    }

    /// <summary>A No project Task View's address selects No project, and opening a Room with no Project from there keeps it selected.</summary>
    [Fact]
    public async Task NoProjectAddress_SelectsNoProject_AndARoomWithNoProjectKeepsIt()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", projects: ["Marketing"])] };
        await using MudBunitContext ctx = NewContext(catalog, stored: BusinessOnly, directory: ProjectRooms());
        NavigationManager nav = ctx.Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/teams/Business/no-project/tasks/my-tasks");
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);
        cut.WaitForAssertion(() => Assert.Equal("true", cut.Find("button.teams-nav-no-project").GetAttribute("aria-pressed")));

        await cut.InvokeAsync(() => nav.NavigateTo("/rooms/teamroom"));
        await Flush(cut);

        Assert.Equal("true", cut.Find("button.teams-nav-no-project").GetAttribute("aria-pressed"));
        Assert.Empty(SelectedLabels(cut));
    }

    /// <summary>The Rooms "+" and the Tasks "+" each carry a tooltip saying what they do, and the Tasks "+" opens the New view page.</summary>
    [Fact]
    public async Task SectionPlusButtons_HaveTooltips_AndTheTasksPlusOpensNewView()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", projects: ["Marketing"])] };
        await using MudBunitContext ctx = NewContext(catalog, stored: BusinessOnly);
        NavigationManager nav = ctx.Services.GetRequiredService<NavigationManager>();
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("button.teams-nav-new-view")));

        Assert.Equal("New room in Business", cut.Find(".teams-nav-section-rooms button.teams-nav-section-add").GetAttribute("aria-label"));
        Assert.Equal("New task view", cut.Find("button.teams-nav-new-view").GetAttribute("aria-label"));
        Assert.Single(Section(cut, "rooms").QuerySelectorAll(".mud-tooltip-root"));
        Assert.Single(Section(cut, "tasks").QuerySelectorAll(".mud-tooltip-root"));

        await cut.InvokeAsync(() => cut.Find("button.teams-nav-new-view").ClickAsync());

        Assert.Equal("tasks/new", nav.ToBaseRelativePath(nav.Uri));
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

        await cut.InvokeAsync(() => cut.Find(".mud-dialog-actions button:first-child").ClickAsync());
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

        string[] expected = ["New room", "Rename team…", "Archive team…"];
        Assert.Equal(expected, MenuItemTexts(cut));
    }

    /// <summary>Spec §6.5: the Team menu is rendered whatever Library and Tasks say, so a Team with both off still offers New chat, Rename team… and Archive team… and nothing else.</summary>
    [Fact]
    public async Task NewChat_Shown_WhenLibraryAndTasksAreBothOff()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business")] };
        await using MudBunitContext ctx = NewContext(catalog, libraryEnabled: false, tasksEnabled: false);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        await cut.InvokeAsync(() => MenuButton(cut, "Business").ClickAsync());

        string[] expected = ["New room", "Rename team…", "Archive team…"];
        Assert.Equal(expected, MenuItemTexts(cut));
    }

    /// <summary>T1/T5 sidebar half: an open Team lists its scope rows, then (in the Team scope) its Team Room and other Project-less Rooms oldest first, its Tasks and its Files link; an archived scoped Room and a Project's Room are left out.</summary>
    [Fact]
    public async Task ExpandedTeam_ListsScopeRows_ThenTeamRoomFirst_ThenScopedRooms_ThenTasksAndFiles()
    {
        ITeamDirectory directory = ScriptedRoomsDirectory.Create(out ScriptedRoomsDirectory script);
        DateTimeOffset t0 = DateTimeOffset.UnixEpoch;
        script.Rooms.Add(new Room("newer", "Ada", t0.AddDays(2)) { Team = "Business" });
        script.Rooms.Add(new Room("older", "Nova, Bo", t0) { Team = "business" });
        script.Rooms.Add(new Room("teamroom", "Business", t0.AddDays(1)) { Team = "Business", IsTeamRoom = true });
        script.Rooms.Add(new Room("gone", "Archived one", t0.AddDays(3)) { Team = "Business", Archived = true });
        script.Rooms.Add(new Room("inproject", "In Marketing", t0.AddDays(4)) { Team = "Business", Project = "Marketing" });
        script.Rooms.Add(new Room("other", "Other team chat", t0) { Team = "Sales" });
        script.Rooms.Add(new Room("root", "Root chat", t0));
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", projects: ["Marketing"]), Team("Sales")] };

        await using MudBunitContext ctx = NewContext(catalog, stored: """["business","sales"]""", directory: directory);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        cut.WaitForAssertion(() =>
        {
            string?[] hrefs = [.. cut.FindAll(".teams-nav a[href]").Select(a => a.GetAttribute("href"))];
            string?[] expected =
            [
                "/teams/Business",
                "/teams/Business",
                "/teams/Business/projects/Marketing",
                "/rooms/teamroom",
                "/rooms/older",
                "/rooms/newer",
                "/rooms/inproject",
                "/teams/Business/tasks/all-tasks",
                "/teams/Business/tasks/my-tasks",
                "/teams/Business/files",
                "/teams/Sales",
                "/teams/Sales",
                "/rooms/other",
                "/teams/Sales/tasks/all-tasks",
                "/teams/Sales/tasks/my-tasks",
                "/teams/Sales/files",
                "/teams/build",
            ];
            Assert.Equal(expected, hrefs);
        });
    }

    /// <summary>The Project rows sit under a PROJECTS header, and the sections of the selected scope are headed ROOMS, TASKS and LIBRARY after it, in that order, each header above its own links.</summary>
    [Fact]
    public async Task ExpandedTeam_SectionHeaders_AppearInTheOrderProjectsRoomsTasksLibrary()
    {
        ITeamDirectory directory = ScriptedRoomsDirectory.Create(out ScriptedRoomsDirectory script);
        script.Rooms.Add(new Room("teamroom", "Business", DateTimeOffset.UnixEpoch) { Team = "Business", IsTeamRoom = true });
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", projects: ["Marketing"])] };

        await using MudBunitContext ctx = NewContext(catalog, stored: BusinessOnly, directory: directory);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        cut.WaitForAssertion(() =>
        {
            string[] titles = ["Projects", "Rooms", "Tasks", "Library"];
            Assert.Equal(titles, SectionTitles(cut));
            string[] kinds = [.. cut.FindAll(".teams-nav-section").Select(section => section.GetAttribute("data-section") ?? string.Empty)];
            string[] expectedKinds = ["projects", "rooms", "tasks", "library"];
            Assert.Equal(expectedKinds, kinds);
            string?[] rooms = ["/rooms/teamroom"];
            Assert.Equal(rooms, SectionHrefs(cut, "rooms"));
            string?[] views = ["/teams/Business/tasks/all-tasks", "/teams/Business/tasks/my-tasks"];
            Assert.Equal(views, SectionHrefs(cut, "tasks"));
            string?[] library = ["/teams/Business/files"];
            Assert.Equal(library, SectionHrefs(cut, "library"));
            Assert.Empty(cut.FindAll(".teams-nav-section .teams-nav-folder"));
            Assert.Equal("teams-nav-section teams-nav-section-rooms", Section(cut, "rooms").GetAttribute("class"));
            Assert.Equal("teams-nav-section teams-nav-section-tasks", Section(cut, "tasks").GetAttribute("class"));
            Assert.Equal("teams-nav-section teams-nav-section-library", Section(cut, "library").GetAttribute("class"));
            Assert.Single(cut.FindAll("div.teams-nav-scopes"));
        });
    }

    /// <summary>The Rooms header and its "+" are drawn for an open Team that has no Rooms at all, and the section holds no row.</summary>
    [Fact]
    public async Task ExpandedTeam_DrawsTheRoomsHeaderAndItsPlus_EvenWithNoRooms()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business")] };

        await using MudBunitContext ctx = NewContext(catalog, stored: BusinessOnly);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("Rooms", Section(cut, "rooms").QuerySelector(".teams-nav-section-title")?.TextContent.Trim());
            Assert.Equal("New room in Business", cut.Find(".teams-nav-section-rooms button.teams-nav-section-add").GetAttribute("aria-label"));
            Assert.Empty(SectionHrefs(cut, "rooms"));
        });
    }

    /// <summary>The Tasks section lists every View in the store's order, each linking to the escaped <c>/teams/{Team}/tasks/{ViewId}</c> route under its View's name.</summary>
    [Fact]
    public async Task TasksSection_ListsEveryView_LinkingToTheTeamScopedRoute()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Sales & Ops")] };
        await using MudBunitContext ctx = NewContext(catalog, stored: """["sales & ops"]""");
        ViewStore views = ctx.Services.GetRequiredService<ViewStore>();
        Assert.True(views.Save(new TaskView { Id = "board", Name = "Sprint board", Kind = ViewKind.List }).Saved);

        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        cut.WaitForAssertion(() =>
        {
            string?[] hrefs = ["/teams/Sales%20%26%20Ops/tasks/all-tasks", "/teams/Sales%20%26%20Ops/tasks/my-tasks", "/teams/Sales%20%26%20Ops/tasks/board"];
            Assert.Equal(hrefs, SectionHrefs(cut, "tasks"));
            string[] names = [.. views.Views.Select(static view => view.Name)];
            string[] labels = [.. Section(cut, "tasks").QuerySelectorAll(".nav-team-view-link .sidebar-row-label").Select(static label => label.TextContent.Trim())];
            Assert.Equal(names, labels);
        });
    }

    /// <summary>A View saved after the nav rendered appears in the Team's Tasks section: the nav follows <see cref="ViewStore.ViewsChanged"/>.</summary>
    [Fact]
    public async Task ViewsChanged_AddsTheNewViewToTheTasksSection()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business")] };
        await using MudBunitContext ctx = NewContext(catalog, stored: BusinessOnly);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);
        cut.WaitForAssertion(() => Assert.Equal(2, SectionHrefs(cut, "tasks").Length));

        Assert.True(ctx.Services.GetRequiredService<ViewStore>().Save(new TaskView { Id = "late", Name = "Late", Kind = ViewKind.List }).Saved);

        string?[] expected = ["/teams/Business/tasks/all-tasks", "/teams/Business/tasks/my-tasks", "/teams/Business/tasks/late"];
        cut.WaitForAssertion(() => Assert.Equal(expected, SectionHrefs(cut, "tasks")));
    }

    /// <summary>With Tasks turned off, an open Team draws no Tasks section and no count on its scope rows.</summary>
    [Fact]
    public async Task TasksSection_NotDrawn_WhenTasksAreOff()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", projects: ["Marketing"])] };
        await using MudBunitContext ctx = NewContext(catalog, stored: BusinessOnly, tasksEnabled: false);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        string?[] library = ["/teams/Business/files"];
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(library, SectionHrefs(cut, "library"));
            string[] titles = ["Projects", "Rooms", "Library"];
            Assert.Equal(titles, SectionTitles(cut));
            Assert.Empty(cut.FindAll(".teams-nav-scope-count"));
        });
    }

    /// <summary>Each list keeps its own 8-row limit: nine Projects fold behind a "Show all (9)" row while two Rooms show in full, and Show all lists every Project.</summary>
    [Fact]
    public async Task ProjectsOverTheLimit_FoldBehindTheirOwnShowAll()
    {
        ITeamDirectory directory = ScriptedRoomsDirectory.Create(out ScriptedRoomsDirectory script);
        script.Rooms.Add(new Room("teamroom", "Business", DateTimeOffset.UnixEpoch) { Team = "Business", IsTeamRoom = true });
        script.Rooms.Add(new Room("chat", "Ada", DateTimeOffset.UnixEpoch.AddDays(1)) { Team = "Business" });
        string[] projects = [.. Enumerable.Range(1, 9).Select(i => $"Project {i}")];
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", projects: projects)] };

        await using MudBunitContext ctx = NewContext(catalog, stored: BusinessOnly, directory: directory);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(2, SectionHrefs(cut, "rooms").Length);
            Assert.Equal(8, ProjectTexts(cut).Length);
        });
        Assert.Empty(Section(cut, "rooms").QuerySelectorAll(".sidebar-show-all"));
        IElement showAll = cut.FindAll(".sidebar-show-all .mud-nav-link").Single();
        Assert.Equal("Show all (9)", showAll.TextContent.Trim());

        await cut.InvokeAsync(() => showAll.ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal(9, ProjectTexts(cut).Length));
    }

    /// <summary>A selected Project past the 8-row limit stays in the folded list, so the row the sections belong to is never hidden.</summary>
    [Fact]
    public async Task SelectedProjectBeyondTheLimit_StaysVisible()
    {
        string[] projects = [.. Enumerable.Range(1, 9).Select(i => $"Project {i}")];
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", projects: projects)] };
        await using MudBunitContext ctx = NewContext(catalog, stored: BusinessOnly);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/teams/Business/projects/Project%209");

        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(9, ProjectTexts(cut).Length);
            string[] selected = ["Project 9"];
            Assert.Equal(selected, SelectedLabels(cut));
            Assert.Equal("Show all (9)", cut.FindAll(".sidebar-show-all .mud-nav-link").Single().TextContent.Trim());
        });
    }

    /// <summary>A collapsed Team shows none of its Rooms, only its row.</summary>
    [Fact]
    public async Task CollapsedTeam_HidesItsRooms()
    {
        ITeamDirectory directory = ScriptedRoomsDirectory.Create(out ScriptedRoomsDirectory script);
        script.Rooms.Add(new Room("teamroom", "Business", DateTimeOffset.UnixEpoch) { Team = "Business", IsTeamRoom = true });
        FakeTeamCatalog catalog = new() { Teams = [Team("Business")] };

        await using MudBunitContext ctx = NewContext(catalog, stored: "[]", directory: directory);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("a[href='/rooms/teamroom']")));
    }

    /// <summary>The nav follows <c>RoomEvents.RoomsChanged</c>: a Room created under the Team appears without a catalog change.</summary>
    [Fact]
    public async Task RoomsChanged_RefreshesTheTeamsRooms()
    {
        ITeamDirectory directory = ScriptedRoomsDirectory.Create(out ScriptedRoomsDirectory script);
        FakeTeamCatalog catalog = new() { Teams = [Team("Business")] };
        await using MudBunitContext ctx = NewContext(catalog, stored: BusinessOnly, directory: directory);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);
        Assert.Empty(cut.FindAll("a[href='/rooms/fresh']"));

        script.Rooms.Add(new Room("fresh", "Fresh chat", DateTimeOffset.UnixEpoch) { Team = "Business" });
        ctx.Services.GetRequiredService<RoomEvents>().PublishRoomsChanged();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("a[href='/rooms/fresh']")));
    }

    /// <summary>T5 entry: choosing New chat in the Team menu opens the panel headed "New room in Business".</summary>
    [Fact]
    public async Task NewChatMenuItem_OpensThePanelForThatTeam()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business")] };
        await using MudBunitContext ctx = NewContext(catalog);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        await cut.InvokeAsync(() => MenuButton(cut, "Business").ClickAsync());
        await cut.InvokeAsync(() => cut.FindAll("div.mud-menu-item").Single(item => string.Equals(item.TextContent.Trim(), "New room", StringComparison.Ordinal)).ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal("New room in Business", cut.Find(".new-chat-heading").TextContent.Trim()));
    }

    /// <summary>An archived Team Room is not listed, and is not listed as one of the other scoped Rooms either.</summary>
    [Fact]
    public async Task ArchivedTeamRoom_IsNotListed()
    {
        ITeamDirectory directory = ScriptedRoomsDirectory.Create(out ScriptedRoomsDirectory script);
        script.Rooms.Add(new Room("teamroom", "Business", DateTimeOffset.UnixEpoch) { Team = "Business", IsTeamRoom = true, Archived = true });
        script.Rooms.Add(new Room("chat", "Ada", DateTimeOffset.UnixEpoch.AddDays(1)) { Team = "Business" });
        FakeTeamCatalog catalog = new() { Teams = [Team("Business")] };

        await using MudBunitContext ctx = NewContext(catalog, stored: BusinessOnly, directory: directory);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("a[href^='/rooms/']")));
        Assert.Equal("/rooms/chat", cut.Find("a[href^='/rooms/']").GetAttribute("href"));
    }

    /// <summary>One read serves every Team (not two queries per Team), and a slower earlier load never overwrites a newer one that finished first.</summary>
    [Fact]
    public async Task OverlappingRefreshes_ASlowerEarlierLoad_DoesNotOverwriteANewerOne()
    {
        ITeamDirectory directory = ScriptedRoomsDirectory.Create(out ScriptedRoomsDirectory script);
        FakeTeamCatalog catalog = new() { Teams = [Team("Business"), Team("Sales")] };
        await using MudBunitContext ctx = NewContext(catalog, stored: BusinessOnly, directory: directory);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);
        cut.WaitForAssertion(() => Assert.Equal(1, script.RoomReads));
        RoomEvents events = ctx.Services.GetRequiredService<RoomEvents>();
        TaskCompletionSource slow = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource fast = new(TaskCreationOptions.RunContinuationsAsynchronously);
        script.Gates.Enqueue(slow);
        script.Gates.Enqueue(fast);

        script.Rooms.Add(new Room("first", "First chat", DateTimeOffset.UnixEpoch) { Team = "Business" });
        events.PublishRoomsChanged();
        cut.WaitForAssertion(() => Assert.Equal(2, script.RoomReads));
        script.Rooms.Add(new Room("second", "Second chat", DateTimeOffset.UnixEpoch.AddDays(1)) { Team = "Business" });
        events.PublishRoomsChanged();
        cut.WaitForAssertion(() => Assert.Equal(3, script.RoomReads));

        fast.SetResult();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("a[href='/rooms/second']")));
        slow.SetResult();
        await Flush(cut);
        await Flush(cut);

        string?[] hrefs = [.. cut.FindAll("a[href^='/rooms/']").Select(a => a.GetAttribute("href"))];
        string?[] expected = ["/rooms/first", "/rooms/second"];
        Assert.Equal(expected, hrefs);
    }

    /// <summary>The panel's Cancel button collapses the panel the Team menu opened.</summary>
    [Fact]
    public async Task NewChatPanel_Cancel_CollapsesThePanel()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business")] };
        await using MudBunitContext ctx = NewContext(catalog);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);
        await OpenNewChatAsync(cut, "Business");
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".new-chat-heading")));

        await cut.InvokeAsync(() => cut.Find("button.new-chat-cancel").ClickAsync());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".new-chat-heading")));
    }

    /// <summary>Choosing New chat in the menu of the Team whose panel is already open closes it again; choosing it for another Team moves the panel there.</summary>
    [Fact]
    public async Task NewChatMenuItem_TogglesThePanelForTheSameTeam()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business"), Team("Sales")] };
        await using MudBunitContext ctx = NewContext(catalog);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        await OpenNewChatAsync(cut, "Business");
        cut.WaitForAssertion(() => Assert.Equal("New room in Business", cut.Find(".new-chat-heading").TextContent.Trim()));

        await OpenNewChatAsync(cut, "Sales");
        cut.WaitForAssertion(() => Assert.Equal("New room in Sales", cut.Find(".new-chat-heading").TextContent.Trim()));

        await OpenNewChatAsync(cut, "Sales");
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".new-chat-heading")));
    }

    /// <summary>Opens <paramref name="team"/>'s menu and clicks New chat.</summary>
    private static async Task OpenNewChatAsync(IRenderedComponent<ContainerFragment> cut, string team)
    {
        await cut.InvokeAsync(() => MenuButton(cut, team).ClickAsync());
        await cut.InvokeAsync(() => cut.FindAll("div.mud-menu-item").Single(item => string.Equals(item.TextContent.Trim(), "New room", StringComparison.Ordinal)).ClickAsync());
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

        string[] expected = ["New room", "New project…", "Rename team…", "Archive team…"];
        Assert.Equal(expected, MenuItemTexts(cut));
    }

    /// <summary>Opens <paramref name="team"/>'s "Team actions" menu, clicks "New project" and waits for the dialog.</summary>
    private static async Task OpenNewProjectDialog(IRenderedComponent<ContainerFragment> cut, string team)
    {
        await cut.InvokeAsync(() => MenuButton(cut, team).ClickAsync());
        _ = cut.InvokeAsync(() => { _ = cut.FindAll("div.mud-menu-item").Single(item => string.Equals(item.TextContent.Trim(), "New project…", StringComparison.Ordinal)).ClickAsync(); });
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));
    }

    /// <summary>Types <paramref name="name"/> into the open dialog's name field.</summary>
    private static Task TypeNameAsync(IRenderedComponent<ContainerFragment> cut, string name) =>
        cut.InvokeAsync(() => cut.Find(".mud-dialog input").InputAsync(name));

    /// <summary>Types <paramref name="name"/> and clicks Create.</summary>
    private static async Task SubmitNameAsync(IRenderedComponent<ContainerFragment> cut, string name)
    {
        await TypeNameAsync(cut, name);
        await cut.InvokeAsync(() => cut.Find(".mud-dialog-actions button:last-child").ClickAsync());
    }

    /// <summary>Runs an empty action on the renderer's synchronization context, so any handler continuation queued before it has finished before the caller asserts an absence.</summary>
    private static Task Flush(IRenderedComponent<ContainerFragment> cut) =>
        cut.InvokeAsync(static () => { });

    private const string ExpandedKey = "teamsNav:expanded";

    private const string AllOpen = """["business","sales & ops","lab #2"]""";

    private const string BusinessOnly = """["business"]""";

    /// <summary>
    /// A fresh MudBlazor-aware bUnit context holding everything <c>TeamsNav</c> injects: the catalog
    /// fake as <see cref="ITeamCatalog"/> and default <see cref="TeamOptions"/>, plus the
    /// <c>storage.js</c> calls: <c>get</c> answers <paramref name="stored"/> (or throws), and
    /// <c>set</c> is recorded (or throws when <paramref name="storageThrows"/>). Later tasks add their
    /// own services here so these rows stay green. The create actions' <see cref="ITeamFolders"/> is
    /// <paramref name="folders"/> (a fresh fake when omitted), <paramref name="libraryEnabled"/> /
    /// <paramref name="tasksEnabled"/> set <c>Library.Enabled</c> / <c>Tasks.Enabled</c> (both on by default), and the
    /// Task store starts with <paramref name="tasks"/> (none when omitted).
    /// </summary>
    private static MudBunitContext NewContext(
        FakeTeamCatalog catalog,
        string? stored = null,
        bool storageThrows = false,
        FakeTeamFolders? folders = null,
        bool libraryEnabled = true,
        bool tasksEnabled = true,
        ITeamDirectory? directory = null,
        IReadOnlyList<TaskItem>? tasks = null)
    {
        MudBunitContext ctx = new();
        ITeamDirectory rooms = directory ?? ScriptedRoomsDirectory.Create(out _);
        ctx.Services.AddSingleton(rooms);
        ctx.Services.AddSingleton<TempDataDir>();
        ctx.Services.AddSingleton(new RoomEvents(NullLogger<RoomEvents>.Instance));
        // The tracker prunes against a directory of its own, so its reads on RoomsChanged never count as (or take the gate of) one of the nav's loads.
        ctx.Services.AddSingleton(sp => new Agency.Huddle.App.NeedsYou.NeedsYouTracker(
            sp.GetRequiredService<RoomEvents>(),
            new QuestionStore(sp.GetRequiredService<RoomEvents>()),
            new Agency.Huddle.App.Elicitation.ElicitationStore(sp.GetRequiredService<RoomEvents>()),
            ScriptedRoomsDirectory.Create(out _),
            NullLogger<Agency.Huddle.App.NeedsYou.NeedsYouTracker>.Instance));
        ctx.Services.AddSingleton<ChatService>(sp =>
        {
            TempDataDir dir = sp.GetRequiredService<TempDataDir>();
            RoomEvents events = sp.GetRequiredService<RoomEvents>();
            FileChatStore store = new(dir.Options(), NullLogger<FileChatStore>.Instance);
            return new ChatService(rooms, store, events, new FakeMentionAliasSource(), TestOptions.Create(), new ProposalStore(events), new QuestionStore(events), NullLogger<ChatService>.Instance) { TeamCatalog = catalog };
        });
        ctx.Services.AddSingleton<IAgentGateway>(new Ui.FakeAgentGateway());
        ctx.Services.AddSingleton(new PersonaHealth(TimeProvider.System, NullLogger<PersonaHealth>.Instance));
        ctx.Services.AddSingleton(sp => new AvatarStore(sp.GetRequiredService<TempDataDir>().Options(), NullLogger<AvatarStore>.Instance));
        ctx.Services.AddPersonaStore();
        ctx.Services.AddTaskStore(tasks);
        ctx.Services.AddTeamTypes();
        ctx.Services.AddSingleton<ITeamCatalog>(catalog);
        ctx.Services.AddSingleton<ITeamFolders>(folders ?? new FakeTeamFolders());
        ctx.Services.AddSingleton(sp => new Agency.Huddle.App.Tasks.Views.ViewStore(sp.GetRequiredService<TempDataDir>().Options(), NullLogger<Agency.Huddle.App.Tasks.Views.ViewStore>.Instance));
        TeamOptions teamOptions = new();
        teamOptions.Library.Enabled = libraryEnabled;
        teamOptions.Tasks.Enabled = tasksEnabled;
        ctx.Services.AddSingleton(Options.Create(teamOptions));
        ctx.Services.AddSingleton<Agency.Huddle.App.Tabs.TabService>();
        if (storageThrows)
        {
            ctx.Module(JsModule.Storage).Setup<string?>("get", _ => true).SetException(new JSException("blocked"));
            ctx.Module(JsModule.Storage).SetupVoid("set", _ => true).SetException(new JSException("blocked"));
        }
        else
        {
            ctx.Module(JsModule.Storage).Setup<string?>("get", _ => true).SetResult(stored);
            ctx.Module(JsModule.Storage).SetupVoid("set", _ => true).SetVoidResult();
        }

        return ctx;
    }

    /// <summary>How many times the nav has read <c>get</c> in <c>storage.js</c>.</summary>
    private static int Reads(MudBunitContext ctx) =>
        ctx.Module(JsModule.Storage).Invocations.Count(invocation => string.Equals(invocation.Identifier, "get", StringComparison.Ordinal));

    /// <summary>The arguments of every <c>set</c> call in <c>storage.js</c>, in order.</summary>
    private static IReadOnlyList<object?>[] Writes(MudBunitContext ctx) =>
        [.. ctx.Module(JsModule.Storage).Invocations
            .Where(invocation => string.Equals(invocation.Identifier, "set", StringComparison.Ordinal))
            .Select(invocation => invocation.Arguments)];

    /// <summary>The chevron button of <paramref name="team"/>'s row.</summary>
    private static AngleSharp.Dom.IElement Chevron(IRenderedComponent<ContainerFragment> cut, string team) =>
        RowFor(cut, team).QuerySelectorAll("button.teams-nav-chevron").Single();

    /// <summary>Clicks the chevron of <paramref name="team"/>'s row on the renderer's synchronization context.</summary>
    private static Task ClickChevronAsync(IRenderedComponent<ContainerFragment> cut, string team) =>
        cut.InvokeAsync(() => Chevron(cut, team).ClickAsync());

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

    /// <summary>The row <c>div.hover-reveal-row</c> whose Team link label (beside the Team's avatar) is <paramref name="team"/>.</summary>
    private static AngleSharp.Dom.IElement RowFor(IRenderedComponent<ContainerFragment> cut, string team) =>
        cut.FindAll("div.hover-reveal-row").Single(row => string.Equals(row.QuerySelector(".hover-reveal-link a .sidebar-row-label")?.TextContent.Trim(), team, StringComparison.Ordinal));

    /// <summary>The "Team actions" button of <paramref name="team"/>'s row.</summary>
    private static AngleSharp.Dom.IElement MenuButton(IRenderedComponent<ContainerFragment> cut, string team) =>
        RowFor(cut, team).QuerySelectorAll(".hover-reveal-menu-button").Single();

    /// <summary>The trimmed text of every open <c>MudMenu</c> item (role <c>menuitem</c>, class <c>mud-menu-item</c>).</summary>
    private static string[] MenuItemTexts(IRenderedComponent<ContainerFragment> cut) =>
        [.. cut.FindAll("div.mud-menu-item").Select(item => item.TextContent.Trim())];

    /// <summary>The trimmed name of every Project row of the scope lists, in render order.</summary>
    private static string[] ProjectTexts(IRenderedComponent<ContainerFragment> cut) =>
        [.. cut.FindAll(".teams-nav-scope-row[data-scope='project'] .teams-nav-scope-label").Select(label => label.TextContent.Trim())];

    /// <summary>The trimmed name of every scope row (the Team's, then its Projects'), in render order.</summary>
    private static string[] ScopeLabels(IRenderedComponent<ContainerFragment> cut) =>
        [.. cut.FindAll(".teams-nav-scope-row .teams-nav-scope-label").Select(label => label.TextContent.Trim())];

    /// <summary>The name of every scope row wearing the Selected shape.</summary>
    private static string[] SelectedLabels(IRenderedComponent<ContainerFragment> cut) =>
        [.. cut.FindAll(".teams-nav-scope-row.selected-shape .teams-nav-scope-label").Select(label => label.TextContent.Trim())];

    /// <summary>The scope row named <paramref name="label"/> (the Team's or a Project's).</summary>
    private static IElement ScopeRow(IRenderedComponent<ContainerFragment> cut, string label) =>
        cut.FindAll(".teams-nav-scope-row").Single(row => string.Equals(row.QuerySelector(".teams-nav-scope-label")?.TextContent.Trim(), label, StringComparison.Ordinal));

    /// <summary>The count text trailing the scope row named <paramref name="label"/>.</summary>
    private static string CountOf(IRenderedComponent<ContainerFragment> cut, string label) =>
        ScopeRow(cut, label).QuerySelector(".teams-nav-scope-count")?.TextContent.Trim() ?? string.Empty;

    /// <summary>The first section of kind <paramref name="kind"/> (<c>rooms</c>, <c>tasks</c> or <c>library</c>).</summary>
    private static IElement Section(IRenderedComponent<ContainerFragment> cut, string kind) =>
        cut.Find($".teams-nav-section[data-section='{kind}']");

    /// <summary>The href of every link inside the section of kind <paramref name="kind"/>, in render order.</summary>
    private static string?[] SectionHrefs(IRenderedComponent<ContainerFragment> cut, string kind) =>
        [.. Section(cut, kind).QuerySelectorAll("a[href]").Select(a => a.GetAttribute("href"))];

    /// <summary>The header label of every section, in render order.</summary>
    private static string[] SectionTitles(IRenderedComponent<ContainerFragment> cut) =>
        [.. cut.FindAll(".teams-nav-section-title").Select(title => title.TextContent.Trim())];

    /// <summary>The trimmed text of every Team link, in render order.</summary>
    private static string[] LinkTexts(IRenderedComponent<ContainerFragment> cut) =>
        [.. cut.FindAll(".hover-reveal-link a .sidebar-row-label").Select(label => label.TextContent.Trim())];

    /// <summary>A Room directory with one Team Room, one Room in the Project Marketing and one whose Project is gone, all of Business.</summary>
    private static ITeamDirectory ProjectRooms()
    {
        ITeamDirectory directory = ScriptedRoomsDirectory.Create(out ScriptedRoomsDirectory script);
        script.Rooms.Add(new Room("teamroom", "Business", DateTimeOffset.UnixEpoch) { Team = "Business", IsTeamRoom = true });
        script.Rooms.Add(new Room("inproject", "Ada", DateTimeOffset.UnixEpoch.AddDays(1)) { Team = "Business", Project = "marketing" });
        script.Rooms.Add(new Room("orphan", "Bo", DateTimeOffset.UnixEpoch.AddDays(2)) { Team = "Business", Project = "Vanished" });
        return directory;
    }

    /// <summary>A Task of Business (under <paramref name="project"/> when given) in <paramref name="status"/>, numbered <paramref name="number"/>.</summary>
    private static TaskItem BusinessTask(int number, TaskState status, string? project = null) =>
        TestTasks.Make(id: $"BIZ-{number:0000}", status: status, location: new TaskLocation("Business", project, false));

    /// <summary>
    /// The list starts with the Team's own row (its rounded-square avatar) and then one row per Project in catalog order, each with its
    /// sharp square avatar (ADR-0083) in place of the old Project icon: the stored label, else the initials of the name.
    /// </summary>
    [Fact]
    public async Task ScopeList_StartsWithTheTeamRow_ThenTheProjectsInOrder_WithTheProjectAvatar()
    {
        TeamSummary business = Team("Business", projects: ["Taxes", "Marketing"]) with
        {
            ProjectAvatars = new Dictionary<string, Avatar>(StringComparer.OrdinalIgnoreCase) { ["Taxes"] = new("🧾", null, null) },
        };
        FakeTeamCatalog catalog = new() { Teams = [business] };

        await using MudBunitContext ctx = NewContext(catalog, stored: BusinessOnly);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        cut.WaitForAssertion(() =>
        {
            string[] labels = ["Business", "Taxes", "Marketing"];
            Assert.Equal(labels, ScopeLabels(cut));
            string[] kinds = [.. cut.FindAll(".teams-nav-scope-row").Select(row => row.GetAttribute("data-scope") ?? string.Empty)];
            string[] expectedKinds = ["team", "project", "project"];
            Assert.Equal(expectedKinds, kinds);
            string?[] hrefs = [.. cut.FindAll(".teams-nav-scope-row a").Select(a => a.GetAttribute("href"))];
            string?[] expectedHrefs = ["/teams/Business", "/teams/Business/projects/Taxes", "/teams/Business/projects/Marketing"];
            Assert.Equal(expectedHrefs, hrefs);
            Assert.Single(ScopeRow(cut, "Business").QuerySelectorAll(".mud-avatar.avatar-square"));
            Assert.Empty(ScopeRow(cut, "Business").QuerySelectorAll(".mud-avatar.avatar-sharp"));
            IElement taxes = Assert.Single(ScopeRow(cut, "Taxes").QuerySelectorAll(".mud-avatar.avatar-square.avatar-sharp.mud-avatar-small"));
            Assert.Equal("🧾", taxes.TextContent.Trim());
            IElement marketing = Assert.Single(ScopeRow(cut, "Marketing").QuerySelectorAll(".mud-avatar.avatar-sharp"));
            Assert.Equal("MA", marketing.TextContent.Trim());
            Assert.Empty(cut.FindAll(".teams-nav-scope-row svg rect[stroke='currentColor']"));
        });
    }

    /// <summary>The Team row is selected by default (No project is not) and its sections list every Room of the Team, in a Project or not, the Team's Task Views and the Team's Files link; exactly one row wears the Selected shape.</summary>
    [Fact]
    public async Task TeamScope_IsSelectedByDefault_AndItsRoomsAreEveryRoomOfTheTeam()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", projects: ["Marketing"])] };

        await using MudBunitContext ctx = NewContext(catalog, stored: BusinessOnly, directory: ProjectRooms());
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        cut.WaitForAssertion(() =>
        {
            string[] selected = ["Business"];
            Assert.Equal(selected, SelectedLabels(cut));
            Assert.Equal("true", ScopeRow(cut, "Business").QuerySelector("a")?.GetAttribute("aria-current"));
            Assert.Null(ScopeRow(cut, "Marketing").QuerySelector("a")?.GetAttribute("aria-current"));
            Assert.Equal("false", cut.Find("button.teams-nav-no-project").GetAttribute("aria-pressed"));
            string?[] rooms = ["/rooms/teamroom", "/rooms/inproject", "/rooms/orphan"];
            Assert.Equal(rooms, SectionHrefs(cut, "rooms"));
            string?[] views = ["/teams/Business/tasks/all-tasks", "/teams/Business/tasks/my-tasks"];
            Assert.Equal(views, SectionHrefs(cut, "tasks"));
            string?[] library = ["/teams/Business/files"];
            Assert.Equal(library, SectionHrefs(cut, "library"));
        });
    }

    /// <summary>The scope rows are plain links wearing the Selected shape themselves: none carries NavLink's <c>active</c> class, so a selected row is never highlighted twice.</summary>
    [Fact]
    public async Task ScopeRows_NeverWearNavLinkActiveClass()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", projects: ["Marketing"])] };
        await using MudBunitContext ctx = NewContext(catalog, stored: BusinessOnly);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/teams/Business/projects/Marketing");

        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        cut.WaitForAssertion(() =>
        {
            string[] selected = ["Marketing"];
            Assert.Equal(selected, SelectedLabels(cut));
            Assert.Empty(cut.FindAll(".teams-nav-scope-row .active"));
            Assert.Empty(cut.FindAll(".teams-nav-scope-row .mud-nav-link"));
        });
    }

    /// <summary>Navigating to a Project's address selects its row, and the sections switch to its Rooms, its <c>/projects/{P}/tasks/{V}</c> Views and its <c>/projects/{P}/files</c> link.</summary>
    [Fact]
    public async Task NavigatingToAProject_SelectsIt_AndTheSectionsSwitchToIt()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", projects: ["Marketing", "Q4 Launch"])] };
        await using MudBunitContext ctx = NewContext(catalog, stored: BusinessOnly, directory: ProjectRooms());
        NavigationManager nav = ctx.Services.GetRequiredService<NavigationManager>();
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);
        cut.WaitForAssertion(() => Assert.Equal(3, SectionHrefs(cut, "rooms").Length));

        await cut.InvokeAsync(() => nav.NavigateTo("/teams/Business/projects/Marketing"));

        cut.WaitForAssertion(() =>
        {
            string[] selected = ["Marketing"];
            Assert.Equal(selected, SelectedLabels(cut));
            string?[] rooms = ["/rooms/inproject"];
            Assert.Equal(rooms, SectionHrefs(cut, "rooms"));
            string?[] views = ["/teams/Business/projects/Marketing/tasks/all-tasks", "/teams/Business/projects/Marketing/tasks/my-tasks"];
            Assert.Equal(views, SectionHrefs(cut, "tasks"));
            string?[] library = ["/teams/Business/projects/Marketing/files"];
            Assert.Equal(library, SectionHrefs(cut, "library"));
        });

        await cut.InvokeAsync(() => nav.NavigateTo("/teams/Business/projects/Q4%20Launch/files"));

        cut.WaitForAssertion(() =>
        {
            string[] selected = ["Q4 Launch"];
            Assert.Equal(selected, SelectedLabels(cut));
            Assert.Empty(SectionHrefs(cut, "rooms"));
            string?[] library = ["/teams/Business/projects/Q4%20Launch/files"];
            Assert.Equal(library, SectionHrefs(cut, "library"));
        });
    }

    /// <summary>Any address inside a Project selects it, whatever its spelling: its page, its Files or one of its Task Views, in any case.</summary>
    [Theory]
    [InlineData("/teams/Business/projects/Marketing")]
    [InlineData("/teams/Business/projects/marketing/files")]
    [InlineData("/teams/Business/projects/Marketing/tasks/my-tasks")]
    public async Task ProjectAddresses_SelectThatProject(string route)
    {
        ArgumentNullException.ThrowIfNull(route);
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", projects: ["Marketing", "Taxes"])] };
        await using MudBunitContext ctx = NewContext(catalog, stored: "[]");
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo(route);

        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        cut.WaitForAssertion(() =>
        {
            string[] selected = ["Marketing"];
            Assert.Equal(selected, SelectedLabels(cut));
        });
    }

    /// <summary>The Team page, its Task Views and its Files select the Team row again after a Project was selected.</summary>
    [Theory]
    [InlineData("/teams/Business")]
    [InlineData("/teams/Business/tasks/my-tasks")]
    [InlineData("/teams/Business/files")]
    public async Task TeamAddresses_SelectTheTeamRow(string route)
    {
        ArgumentNullException.ThrowIfNull(route);
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", projects: ["Marketing"])] };
        await using MudBunitContext ctx = NewContext(catalog, stored: BusinessOnly);
        NavigationManager nav = ctx.Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/teams/Business/projects/Marketing");
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);
        string[] project = ["Marketing"];
        cut.WaitForAssertion(() => Assert.Equal(project, SelectedLabels(cut)));

        await cut.InvokeAsync(() => nav.NavigateTo(route));

        string[] team = ["Business"];
        cut.WaitForAssertion(() => Assert.Equal(team, SelectedLabels(cut)));
    }

    /// <summary>A Room route selects the Room's Project when the Team lists it, and the Team row for a Room with no Project.</summary>
    [Fact]
    public async Task RoomRoute_SelectsTheRoomsProject_OrTheTeamRowWhenItHasNone()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", projects: ["Marketing"])] };
        await using MudBunitContext ctx = NewContext(catalog, stored: BusinessOnly, directory: ProjectRooms());
        NavigationManager nav = ctx.Services.GetRequiredService<NavigationManager>();
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);
        cut.WaitForAssertion(() => Assert.Equal(3, SectionHrefs(cut, "rooms").Length));

        await cut.InvokeAsync(() => nav.NavigateTo("/rooms/inproject"));

        cut.WaitForAssertion(() =>
        {
            string[] project = ["Marketing"];
            Assert.Equal(project, SelectedLabels(cut));
            string?[] rooms = ["/rooms/inproject"];
            Assert.Equal(rooms, SectionHrefs(cut, "rooms"));
        });

        await cut.InvokeAsync(() => nav.NavigateTo("/rooms/teamroom"));

        string[] team = ["Business"];
        cut.WaitForAssertion(() => Assert.Equal(team, SelectedLabels(cut)));
    }

    /// <summary>An address outside every scope (another page, or an unknown page of the Team) keeps the Team's last selection.</summary>
    [Theory]
    [InlineData("/agents")]
    [InlineData("/teams/Business/members")]
    public async Task UnrelatedAddress_KeepsThePreviousSelection(string route)
    {
        ArgumentNullException.ThrowIfNull(route);
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", projects: ["Marketing"])] };
        await using MudBunitContext ctx = NewContext(catalog, stored: BusinessOnly);
        NavigationManager nav = ctx.Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/teams/Business/projects/Marketing");
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);
        string[] project = ["Marketing"];
        cut.WaitForAssertion(() => Assert.Equal(project, SelectedLabels(cut)));

        await cut.InvokeAsync(() => nav.NavigateTo(route));
        await Flush(cut);

        Assert.Equal(project, SelectedLabels(cut));
    }

    /// <summary>A Room whose Project is gone from the catalog is listed in the Team scope, which stays selected on its address.</summary>
    [Fact]
    public async Task RoomWhoseProjectIsGone_ShowsInTheTeamScope()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", projects: ["Marketing"])] };
        await using MudBunitContext ctx = NewContext(catalog, stored: BusinessOnly, directory: ProjectRooms());
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/rooms/orphan");

        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        cut.WaitForAssertion(() =>
        {
            string[] team = ["Business"];
            Assert.Equal(team, SelectedLabels(cut));
            Assert.Contains("/rooms/orphan", SectionHrefs(cut, "rooms"));
        });
    }

    /// <summary>A Project address for a Project the catalog does not list (archived or gone) falls back to the Team row and the Team's sections.</summary>
    [Fact]
    public async Task ProjectGoneFromTheCatalog_FallsBackToTheTeamScope()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", projects: ["Marketing"])] };
        await using MudBunitContext ctx = NewContext(catalog, stored: BusinessOnly);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/teams/Business/projects/Vanished");

        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        cut.WaitForAssertion(() =>
        {
            string[] team = ["Business"];
            Assert.Equal(team, SelectedLabels(cut));
            string?[] library = ["/teams/Business/files"];
            Assert.Equal(library, SectionHrefs(cut, "library"));
        });
    }

    /// <summary>Each Team keeps its own selection for the circuit: selecting a Project of one Team does not move the other's.</summary>
    [Fact]
    public async Task Selection_IsKeptPerTeam()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", projects: ["Marketing"]), Team("Sales", projects: ["Budget"])] };
        await using MudBunitContext ctx = NewContext(catalog, stored: """["business","sales"]""");
        NavigationManager nav = ctx.Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/teams/Business/projects/Marketing");
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);
        string[] first = ["Marketing", "Sales"];
        cut.WaitForAssertion(() => Assert.Equal(first, SelectedLabels(cut)));

        await cut.InvokeAsync(() => nav.NavigateTo("/teams/Sales/projects/Budget"));

        string[] second = ["Marketing", "Budget"];
        cut.WaitForAssertion(() => Assert.Equal(second, SelectedLabels(cut)));
    }

    /// <summary>Clicking a Project row selects it at once and offers its page to the Tabs; the row links to that page.</summary>
    [Fact]
    public async Task ClickingAScopeRow_SelectsIt_AndOpensItsPageInATab()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", projects: ["Marketing"])] };
        await using MudBunitContext ctx = NewContext(catalog, stored: BusinessOnly);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);
        string[] team = ["Business"];
        cut.WaitForAssertion(() => Assert.Equal(team, SelectedLabels(cut)));
        IElement link = ScopeRow(cut, "Marketing").QuerySelectorAll("a").Single();
        Assert.Equal("/teams/Business/projects/Marketing", link.GetAttribute("href"));

        await cut.InvokeAsync(() => link.ClickAsync());

        string[] project = ["Marketing"];
        cut.WaitForAssertion(() => Assert.Equal(project, SelectedLabels(cut)));
        Tab tab = Assert.Single(ctx.Services.GetRequiredService<TabService>().Tabs);
        Assert.Equal("/teams/Business/projects/Marketing", tab.Url);
        Assert.Equal("Marketing", tab.Title);
    }

    /// <summary>Each Project row counts its open Tasks (not Done, Cancelled, Duplicate or Rejected, as the Tasks page counts them), a Project with none shows an em dash, and the tooltip says what the number is; the Team's header card shows no count.</summary>
    [Fact]
    public async Task Counts_ShowTheOpenTasksOfEachScope_AndADashForZero()
    {
        TaskItem[] tasks =
        [
            BusinessTask(1, TaskState.ToDo),
            BusinessTask(2, TaskState.InProgress),
            BusinessTask(3, TaskState.Done),
            BusinessTask(4, TaskState.ToDo, "Marketing"),
            BusinessTask(5, TaskState.Cancelled, "Marketing"),
            TestTasks.Make(id: "SAL-0001", status: TaskState.ToDo, location: new TaskLocation("Sales", null, false)),
        ];
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", projects: ["Marketing", "Taxes"]), Team("Sales")] };

        await using MudBunitContext ctx = NewContext(catalog, stored: AllOpen, tasks: tasks);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(ScopeRow(cut, "Business").QuerySelectorAll(".teams-nav-scope-count"));
            Assert.Equal("1", CountOf(cut, "Marketing"));
            Assert.Equal("—", CountOf(cut, "Taxes"));
            Assert.Equal("1 open task", ScopeRow(cut, "Marketing").QuerySelector(".teams-nav-scope-count")?.GetAttribute("title"));
            Assert.Equal("No open tasks", ScopeRow(cut, "Taxes").QuerySelector(".teams-nav-scope-count")?.GetAttribute("title"));
        });
    }

    /// <summary>A Task created after the nav rendered changes its scope's count: the nav follows <see cref="TaskEvents"/> as the Tasks page does.</summary>
    [Fact]
    public async Task Counts_FollowTaskEvents()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", projects: ["Marketing"])] };
        await using MudBunitContext ctx = NewContext(catalog, stored: BusinessOnly, tasks: [BusinessTask(1, TaskState.ToDo, "Marketing")]);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);
        cut.WaitForAssertion(() =>
        {
            Assert.Equal("1", CountOf(cut, "Marketing"));
        });

        TaskItem added = BusinessTask(2, TaskState.ToDo, "Marketing");
        TaskStore store = ctx.Services.GetRequiredService<TaskStore>();
        Assert.NotNull(store.Create(added, TaskFileFormat.Compose(added)));
        ctx.Services.GetRequiredService<TaskEvents>().RaiseTasksReloaded();

        cut.WaitForAssertion(() => Assert.Equal("2", CountOf(cut, "Marketing")));
    }

    /// <summary>The "+" in the Rooms header opens the New chat panel for the Team scope: the Team and no Project.</summary>
    [Fact]
    public async Task RoomsPlus_InTheTeamScope_OpensNewChatForTheTeam()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", projects: ["Marketing"])] };
        await using MudBunitContext ctx = NewContext(catalog, stored: BusinessOnly);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);
        cut.WaitForAssertion(() => Assert.Equal("New room in Business", cut.Find(".teams-nav-section-rooms button.teams-nav-section-add").GetAttribute("aria-label")));

        await cut.InvokeAsync(() => cut.Find(".teams-nav-section-rooms button.teams-nav-section-add").ClickAsync());

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("New room in Business", cut.Find(".new-chat-heading").TextContent.Trim());
            Assert.Single(cut.FindAll(".new-chat-panel"));
            Assert.Single(Section(cut, "rooms").QuerySelectorAll(".new-chat-panel"));
        });
    }

    /// <summary>With a Project selected, the "+" opens the New chat panel for the Team and that Project, under the Rooms header; the Project's scope names the button.</summary>
    [Fact]
    public async Task RoomsPlus_InAProject_OpensNewChatForTheTeamAndProject()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", projects: ["Marketing"])] };
        await using MudBunitContext ctx = NewContext(catalog, stored: BusinessOnly);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/teams/Business/projects/Marketing");
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);
        cut.WaitForAssertion(() => Assert.Equal("New room in Marketing", cut.Find(".teams-nav-section-rooms button.teams-nav-section-add").GetAttribute("aria-label")));

        await cut.InvokeAsync(() => cut.Find(".teams-nav-section-rooms button.teams-nav-section-add").ClickAsync());

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("New room in Business / Marketing", cut.Find(".new-chat-heading").TextContent.Trim());
            Assert.Single(Section(cut, "rooms").QuerySelectorAll(".new-chat-panel"));
        });
    }

    /// <summary>Moving to another scope closes a New chat panel left open for the old one, so the panel never names a scope that is no longer selected.</summary>
    [Fact]
    public async Task NewChatPanel_ClosesWhenAnotherScopeIsSelected()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", projects: ["Marketing"])] };
        await using MudBunitContext ctx = NewContext(catalog, stored: BusinessOnly);
        NavigationManager nav = ctx.Services.GetRequiredService<NavigationManager>();
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".teams-nav-section-rooms button.teams-nav-section-add")));
        await cut.InvokeAsync(() => cut.Find(".teams-nav-section-rooms button.teams-nav-section-add").ClickAsync());
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".new-chat-panel")));

        await cut.InvokeAsync(() => nav.NavigateTo("/teams/Business/projects/Marketing"));

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".new-chat-panel")));
    }

    /// <summary>The Library section is one Files link to the Files tab of the scope, and is not drawn at all (header included) while the Library is off.</summary>
    [Fact]
    public async Task LibrarySection_LinksToTheFilesTabOfTheScope_AndIsNotDrawnWhenTheLibraryIsOff()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Sales & Ops", projects: ["Q4 Launch"])] };
        await using (MudBunitContext on = NewContext(catalog, stored: """["sales & ops"]"""))
        {
            on.Services.GetRequiredService<NavigationManager>().NavigateTo("/teams/Sales%20%26%20Ops/projects/Q4%20Launch");
            IRenderedComponent<ContainerFragment> cut = RenderNav(on);

            cut.WaitForAssertion(() =>
            {
                string?[] expected = ["/teams/Sales%20%26%20Ops/projects/Q4%20Launch/files"];
                Assert.Equal(expected, SectionHrefs(cut, "library"));
                Assert.Equal("Files", Section(cut, "library").QuerySelector(".teams-nav-library-link .sidebar-row-label")?.TextContent.Trim());
            });
        }

        await using MudBunitContext off = NewContext(catalog, stored: """["sales & ops"]""", libraryEnabled: false);
        IRenderedComponent<ContainerFragment> offCut = RenderNav(off);

        offCut.WaitForAssertion(() =>
        {
            Assert.NotEmpty(offCut.FindAll(".teams-nav-section"));
            Assert.Empty(offCut.FindAll(".teams-nav-section-library"));
            Assert.DoesNotContain("Library", SectionTitles(offCut));
        });
    }

    /// <summary>Each Team row leads with the Team's avatar as a rounded square, before the label: its own label when it has one, else the monogram.</summary>
    [Fact]
    public async Task TeamRow_LeadsWithTheTeamsRoundedSquareAvatar()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business") with { Avatar = new Avatar("BZ", null, null) }, Team("Research")] };
        await using MudBunitContext ctx = NewContext(catalog);

        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        string[] avatars = [.. cut.FindAll(".hover-reveal-link a .mud-avatar.avatar-square").Select(avatar => avatar.TextContent.Trim())];
        Assert.Equal(["BZ", "RE"], avatars);
        Assert.Equal(["Business", "Research"], LinkTexts(cut));
    }
}

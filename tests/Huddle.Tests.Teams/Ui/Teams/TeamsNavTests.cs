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
using Agency.Huddle.App.Tasks.Views;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Teams;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>
/// Pins Spec §6.5's listing half: <c>TeamsNav</c> is one <c>MudNavMenu.teams-nav</c> whose root rows
/// are the Teams, under no "Teams" header; each Team is a <c>div.hover-reveal-row</c> whose link goes to
/// the escaped <c>/teams/{team}</c> route, and an open Team holds a Rooms folder and a Library folder
/// (its Projects as <c>nav-project-link</c> rows). A memberless Team shows a "No members" hint, the
/// "Team actions" menu (button or right-click) offers "New project", and "Build a team" trails the list.
/// It follows <see cref="ITeamCatalog.Changed"/> and unsubscribes on dispose.
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

        await using MudBunitContext ctx = NewContext(catalog, stored: AllOpen);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        string?[] expectedHrefs =
        [
            "/teams/Business",
            "/teams/Business/projects/2025%20Taxes%20Project",
            "/teams/Business/projects/Marketing",
            "/teams/Business/tasks/all-tasks",
            "/teams/Business/tasks/my-tasks",
            "/teams/Sales%20%26%20Ops",
            "/teams/Sales%20%26%20Ops/tasks/all-tasks",
            "/teams/Sales%20%26%20Ops/tasks/my-tasks",
            "/teams/Lab%20%232",
            "/teams/Lab%20%232/projects/Item%20%231",
            "/teams/Lab%20%232/tasks/all-tasks",
            "/teams/Lab%20%232/tasks/my-tasks",
            "/teams/build",
        ];
        cut.WaitForAssertion(() =>
        {
            string?[] actualHrefs = [.. cut.FindAll(".teams-nav a[href]").Select(a => a.GetAttribute("href"))];
            Assert.Equal(expectedHrefs, actualHrefs);
        });

        string[] expectedProjects = ["2025 Taxes Project", "Marketing", "Item #1"];
        string[] actualProjects = [.. cut.FindAll(".nav-project-link a").Select(a => a.TextContent.Trim())];
        Assert.Equal(expectedProjects, actualProjects);
    }

    /// <summary>The Team row's link goes to the Team page, and its "Team actions" button opens a menu whose items are "New chat" and "New project".</summary>
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

        string[] expected = ["New chat", "New project…"];
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

        string[] expected = ["New chat", "New project…"];
        Assert.Equal(expected, MenuItemTexts(cut));
    }

    /// <summary>
    /// Narrowed to one Team (its Activity bar shortcut), the nav lists only that Team's folders, open without a click on
    /// any chevron: no Team rows, no other Team's links, and no "Build a team".
    /// </summary>
    [Fact]
    public async Task Team_Set_ListsOnlyThatTeamsFolders()
    {
        FakeTeamCatalog catalog = new()
        {
            Teams = [Team("Business", projects: ["Marketing"]), Team("Lab #2", projects: ["Item #1"])],
        };

        await using MudBunitContext ctx = NewContext(catalog);
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TeamsNav>(0);
            builder.AddComponentParameter(1, nameof(TeamsNav.Team), "lab #2");
            builder.CloseComponent();
        });

        Assert.Empty(cut.FindAll("div.hover-reveal-row"));
        string?[] expectedHrefs =
        [
            "/teams/Lab%20%232/projects/Item%20%231",
            "/teams/Lab%20%232/tasks/all-tasks",
            "/teams/Lab%20%232/tasks/my-tasks",
        ];
        cut.WaitForAssertion(() =>
        {
            string?[] actualHrefs = [.. cut.FindAll(".teams-nav a[href]").Select(a => a.GetAttribute("href"))];
            Assert.Equal(expectedHrefs, actualHrefs);
        });
    }

    /// <summary>A Team with no Rooms, Projects or Task Views says so under its shortcut and links to its Team page, rather than showing nothing.</summary>
    [Fact]
    public async Task Team_SetAndEmpty_ShowsAnEmptyStateAndTheTeamPageLink()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business")] };

        await using MudBunitContext ctx = NewContext(catalog, tasksEnabled: false);
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TeamsNav>(0);
            builder.AddComponentParameter(1, nameof(TeamsNav.Team), "Business");
            builder.CloseComponent();
        });

        cut.WaitForAssertion(() => Assert.Equal("Nothing in Business yet", cut.Find(".empty-state .empty-state-title").TextContent.Trim()));
        string? href = Assert.Single(cut.FindAll(".teams-nav a[href]")).GetAttribute("href");
        Assert.Equal("/teams/Business", href);
    }

    /// <summary>Under a Team shortcut with nothing waiting on the Human, the Rooms folder lists its Rooms flat, with no Needs you or All folder, and ends with "New chat".</summary>
    [Fact]
    public async Task Team_SetAndNothingWaiting_ListsTheRoomsFlat_ThenNewChat()
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
            Assert.Equal(rooms, FolderHrefs(cut, "Rooms"));
            Assert.Empty(cut.FindAll(".teams-nav-subfolder"));
            Assert.Equal("New chat", Folder(cut, "Rooms").QuerySelectorAll(".mud-collapse-container .mud-nav-link-text")[^1].TextContent.Trim());
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
    /// Under a Team shortcut, a Room waiting on the Human splits the Rooms folder into "Needs you" (that Room) above "All"
    /// (the rest, then "New chat"), as the Chats view's All grouping does; once nothing waits the Rooms are flat again.
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
        cut.WaitForAssertion(() => Assert.Equal(2, FolderHrefs(cut, "Rooms").Length));

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
            Assert.Equal("New chat", all.QuerySelectorAll(".mud-nav-link-text")[^1].TextContent.Trim());
        });

        await cut.InvokeAsync(() => tracker.Dismiss("chat"));

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".teams-nav-subfolder")));
    }

    /// <summary>The "New chat" row under a Team shortcut opens the New chat panel for that Team.</summary>
    [Fact]
    public async Task Team_Set_NewChatRow_OpensTheTeamsNewChatPanel()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business")] };

        await using MudBunitContext ctx = NewContext(catalog, tasksEnabled: false);
        IRenderedComponent<ContainerFragment> cut = RenderShortcut(ctx, "Business");
        Assert.Empty(cut.FindAll(".new-chat-panel"));

        await cut.InvokeAsync(() => cut.Find(".teams-nav-new-chat .mud-nav-link").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal("New chat in Business", cut.Find(".new-chat-panel").GetAttribute("aria-label")));
    }

    /// <summary>Renders the nav narrowed to <paramref name="team"/>, as its Activity bar shortcut does.</summary>
    private static IRenderedComponent<ContainerFragment> RenderShortcut(MudBunitContext ctx, string team) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TeamsNav>(0);
            builder.AddComponentParameter(1, nameof(TeamsNav.Team), team);
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

        Assert.Contains("active", cut.Find("a[href='/teams/Business']").ClassList);
        Assert.DoesNotContain("active", cut.Find("a[href='/teams/Sales%20%26%20Ops']").ClassList);
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

        string[] expected = ["New chat"];
        Assert.Equal(expected, MenuItemTexts(cut));
    }

    /// <summary>Spec §6.5: the Team menu is rendered whatever Library and Tasks say, so a Team with both off still offers New chat and nothing else.</summary>
    [Fact]
    public async Task NewChat_Shown_WhenLibraryAndTasksAreBothOff()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business")] };
        await using MudBunitContext ctx = NewContext(catalog, libraryEnabled: false, tasksEnabled: false);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        await cut.InvokeAsync(() => MenuButton(cut, "Business").ClickAsync());

        string[] expected = ["New chat"];
        Assert.Equal(expected, MenuItemTexts(cut));
    }

    /// <summary>T1/T5 sidebar half: an expanded Team lists its Team Room first, then its other scoped Rooms oldest first (the Rooms folder), then its Projects (the Library folder); an archived scoped Room is left out.</summary>
    [Fact]
    public async Task ExpandedTeam_ListsTeamRoomFirst_ThenScopedRooms_ThenProjects()
    {
        ITeamDirectory directory = ScriptedRoomsDirectory.Create(out ScriptedRoomsDirectory script);
        DateTimeOffset t0 = DateTimeOffset.UnixEpoch;
        script.Rooms.Add(new Room("newer", "Ada", t0.AddDays(2)) { Team = "Business" });
        script.Rooms.Add(new Room("older", "Nova, Bo", t0) { Team = "business" });
        script.Rooms.Add(new Room("teamroom", "Business", t0.AddDays(1)) { Team = "Business", IsTeamRoom = true });
        script.Rooms.Add(new Room("gone", "Archived one", t0.AddDays(3)) { Team = "Business", Archived = true });
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
                "/rooms/teamroom",
                "/rooms/older",
                "/rooms/newer",
                "/teams/Business/projects/Marketing",
                "/teams/Business/tasks/all-tasks",
                "/teams/Business/tasks/my-tasks",
                "/teams/Sales",
                "/rooms/other",
                "/teams/Sales/tasks/all-tasks",
                "/teams/Sales/tasks/my-tasks",
                "/teams/build",
            ];
            Assert.Equal(expected, hrefs);
        });
    }

    /// <summary>An open Team holds a Rooms folder with its Rooms, a Library folder with its Projects, then a Tasks folder with the Views, all open; nothing else sits in any.</summary>
    [Fact]
    public async Task ExpandedTeam_GroupsItsRoomsProjectsAndViews_IntoRoomsLibraryThenTasksFolders()
    {
        ITeamDirectory directory = ScriptedRoomsDirectory.Create(out ScriptedRoomsDirectory script);
        script.Rooms.Add(new Room("teamroom", "Business", DateTimeOffset.UnixEpoch) { Team = "Business", IsTeamRoom = true });
        script.Rooms.Add(new Room("chat", "Ada", DateTimeOffset.UnixEpoch.AddDays(1)) { Team = "Business" });
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", projects: ["Marketing", "Taxes"])] };

        await using MudBunitContext ctx = NewContext(catalog, stored: BusinessOnly, directory: directory);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        cut.WaitForAssertion(() =>
        {
            string[] folders = ["Rooms", "Library", "Tasks"];
            Assert.Equal(folders, FolderTitles(cut));
            string?[] rooms = ["/rooms/teamroom", "/rooms/chat"];
            Assert.Equal(rooms, FolderHrefs(cut, "Rooms"));
            string?[] projects = ["/teams/Business/projects/Marketing", "/teams/Business/projects/Taxes"];
            Assert.Equal(projects, FolderHrefs(cut, "Library"));
            string?[] views = ["/teams/Business/tasks/all-tasks", "/teams/Business/tasks/my-tasks"];
            Assert.Equal(views, FolderHrefs(cut, "Tasks"));
            Assert.Equal("true", FolderHeader(cut, "Rooms").GetAttribute("aria-expanded"));
            Assert.Equal("true", FolderHeader(cut, "Library").GetAttribute("aria-expanded"));
            Assert.Equal("true", FolderHeader(cut, "Tasks").GetAttribute("aria-expanded"));
        });
    }

    /// <summary>An empty folder is not drawn: a Team with Rooms and no Projects has only Rooms (and Tasks), one with Projects and no Rooms only Library (and Tasks).</summary>
    [Fact]
    public async Task ExpandedTeam_DrawsNoFolderForWhatItDoesNotHave()
    {
        ITeamDirectory directory = ScriptedRoomsDirectory.Create(out ScriptedRoomsDirectory script);
        script.Rooms.Add(new Room("teamroom", "Business", DateTimeOffset.UnixEpoch) { Team = "Business", IsTeamRoom = true });
        FakeTeamCatalog catalog = new() { Teams = [Team("Business"), Team("Sales", projects: ["Budget"])] };

        await using MudBunitContext ctx = NewContext(catalog, stored: """["business","sales"]""", directory: directory);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        cut.WaitForAssertion(() =>
        {
            string[] folders = ["Rooms", "Tasks", "Library", "Tasks"];
            Assert.Equal(folders, FolderTitles(cut));
            string?[] rooms = ["/rooms/teamroom"];
            Assert.Equal(rooms, FolderHrefs(cut, "Rooms"));
            string?[] projects = ["/teams/Sales/projects/Budget"];
            Assert.Equal(projects, FolderHrefs(cut, "Library"));
        });
    }

    /// <summary>Clicking a folder's header closes that folder only, and writes nothing to storage (folders are not remembered).</summary>
    [Fact]
    public async Task FolderHeader_Click_ClosesThatFolderOnly()
    {
        ITeamDirectory directory = ScriptedRoomsDirectory.Create(out ScriptedRoomsDirectory script);
        script.Rooms.Add(new Room("teamroom", "Business", DateTimeOffset.UnixEpoch) { Team = "Business", IsTeamRoom = true });
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", projects: ["Marketing"])] };

        await using MudBunitContext ctx = NewContext(catalog, stored: BusinessOnly, directory: directory);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);
        cut.WaitForAssertion(() => Assert.Equal(3, FolderTitles(cut).Length));

        await cut.InvokeAsync(() => FolderHeader(cut, "Library").ClickAsync());

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("false", FolderHeader(cut, "Library").GetAttribute("aria-expanded"));
            Assert.Equal("true", FolderHeader(cut, "Rooms").GetAttribute("aria-expanded"));
            Assert.Equal("true", FolderHeader(cut, "Tasks").GetAttribute("aria-expanded"));
        });
        Assert.Empty(Writes(ctx));
    }

    /// <summary>Navigating to a Project reopens its Team's Library folder the Human closed, so the page they are on is never folded away.</summary>
    [Fact]
    public async Task NavigatingToAProject_ReopensAClosedLibraryFolder()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", projects: ["Marketing"])] };
        await using MudBunitContext ctx = NewContext(catalog, stored: BusinessOnly);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);
        cut.WaitForAssertion(() => Assert.Equal("true", FolderHeader(cut, "Library").GetAttribute("aria-expanded")));
        await cut.InvokeAsync(() => FolderHeader(cut, "Library").ClickAsync());
        cut.WaitForAssertion(() => Assert.Equal("false", FolderHeader(cut, "Library").GetAttribute("aria-expanded")));

        NavigationManager nav = ctx.Services.GetRequiredService<NavigationManager>();
        await cut.InvokeAsync(() => nav.NavigateTo("/teams/Business/projects/Marketing"));

        cut.WaitForAssertion(() => Assert.Equal("true", FolderHeader(cut, "Library").GetAttribute("aria-expanded")));
    }

    /// <summary>The Tasks folder lists every View in the store's order, each linking to the escaped <c>/teams/{Team}/tasks/{ViewId}</c> route under its View's name.</summary>
    [Fact]
    public async Task TasksFolder_ListsEveryView_LinkingToTheTeamScopedRoute()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Sales & Ops")] };
        await using MudBunitContext ctx = NewContext(catalog, stored: """["sales & ops"]""");
        ViewStore views = ctx.Services.GetRequiredService<ViewStore>();
        Assert.True(views.Save(new TaskView { Id = "board", Name = "Sprint board", Kind = ViewKind.List }).Saved);

        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        cut.WaitForAssertion(() =>
        {
            string?[] hrefs = ["/teams/Sales%20%26%20Ops/tasks/all-tasks", "/teams/Sales%20%26%20Ops/tasks/my-tasks", "/teams/Sales%20%26%20Ops/tasks/board"];
            Assert.Equal(hrefs, FolderHrefs(cut, "Tasks"));
            string[] names = [.. views.Views.Select(static view => view.Name)];
            string[] labels = [.. Folder(cut, "Tasks").QuerySelectorAll(".nav-team-view-link .sidebar-row-label").Select(static label => label.TextContent.Trim())];
            Assert.Equal(names, labels);
        });
    }

    /// <summary>A View saved after the nav rendered appears in the Team's Tasks folder: the nav follows <see cref="ViewStore.ViewsChanged"/>.</summary>
    [Fact]
    public async Task ViewsChanged_AddsTheNewViewToTheTasksFolder()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business")] };
        await using MudBunitContext ctx = NewContext(catalog, stored: BusinessOnly);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);
        cut.WaitForAssertion(() => Assert.Equal(2, FolderHrefs(cut, "Tasks").Length));

        Assert.True(ctx.Services.GetRequiredService<ViewStore>().Save(new TaskView { Id = "late", Name = "Late", Kind = ViewKind.List }).Saved);

        string?[] expected = ["/teams/Business/tasks/all-tasks", "/teams/Business/tasks/my-tasks", "/teams/Business/tasks/late"];
        cut.WaitForAssertion(() => Assert.Equal(expected, FolderHrefs(cut, "Tasks")));
    }

    /// <summary>With Tasks turned off, an open Team draws no Tasks folder.</summary>
    [Fact]
    public async Task TasksFolder_NotDrawn_WhenTasksAreOff()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business", projects: ["Marketing"])] };
        await using MudBunitContext ctx = NewContext(catalog, stored: BusinessOnly, tasksEnabled: false);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        string[] expected = ["Library"];
        cut.WaitForAssertion(() => Assert.Equal(expected, FolderTitles(cut)));
    }

    /// <summary>Navigating to a Team-scoped View reopens that Team's Tasks folder the Human closed, so the page they are on is never folded away.</summary>
    [Fact]
    public async Task NavigatingToATeamView_ReopensAClosedTasksFolder()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business")] };
        await using MudBunitContext ctx = NewContext(catalog, stored: BusinessOnly);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);
        cut.WaitForAssertion(() => Assert.Equal("true", FolderHeader(cut, "Tasks").GetAttribute("aria-expanded")));
        await cut.InvokeAsync(() => FolderHeader(cut, "Tasks").ClickAsync());
        cut.WaitForAssertion(() => Assert.Equal("false", FolderHeader(cut, "Tasks").GetAttribute("aria-expanded")));

        NavigationManager nav = ctx.Services.GetRequiredService<NavigationManager>();
        await cut.InvokeAsync(() => nav.NavigateTo("/teams/Business/tasks/my-tasks"));

        cut.WaitForAssertion(() => Assert.Equal("true", FolderHeader(cut, "Tasks").GetAttribute("aria-expanded")));
    }

    /// <summary>Each folder keeps its own 8-row limit: nine Projects fold behind the Library's "Show all (9)" while two Rooms show in full, and Show all lists every Project.</summary>
    [Fact]
    public async Task LibraryOverTheLimit_FoldsBehindItsOwnShowAll()
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
            Assert.Equal(2, FolderHrefs(cut, "Rooms").Length);
            Assert.Equal(8, ProjectTexts(cut).Length);
        });
        Assert.Empty(Folder(cut, "Rooms").QuerySelectorAll(".sidebar-show-all"));
        IElement showAll = Folder(cut, "Library").QuerySelectorAll(".sidebar-show-all .mud-nav-link").Single();
        Assert.Equal("Show all (9)", showAll.TextContent.Trim());

        await cut.InvokeAsync(() => showAll.ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal(9, ProjectTexts(cut).Length));
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

    /// <summary>T5 entry: choosing New chat in the Team menu opens the panel headed "New chat in Business".</summary>
    [Fact]
    public async Task NewChatMenuItem_OpensThePanelForThatTeam()
    {
        FakeTeamCatalog catalog = new() { Teams = [Team("Business")] };
        await using MudBunitContext ctx = NewContext(catalog);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        await cut.InvokeAsync(() => MenuButton(cut, "Business").ClickAsync());
        await cut.InvokeAsync(() => cut.FindAll("div.mud-menu-item").Single(item => string.Equals(item.TextContent.Trim(), "New chat", StringComparison.Ordinal)).ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal("New chat in Business", cut.Find(".new-chat-heading").TextContent.Trim()));
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
        cut.WaitForAssertion(() => Assert.Equal("New chat in Business", cut.Find(".new-chat-heading").TextContent.Trim()));

        await OpenNewChatAsync(cut, "Sales");
        cut.WaitForAssertion(() => Assert.Equal("New chat in Sales", cut.Find(".new-chat-heading").TextContent.Trim()));

        await OpenNewChatAsync(cut, "Sales");
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".new-chat-heading")));
    }

    /// <summary>Opens <paramref name="team"/>'s menu and clicks New chat.</summary>
    private static async Task OpenNewChatAsync(IRenderedComponent<ContainerFragment> cut, string team)
    {
        await cut.InvokeAsync(() => MenuButton(cut, team).ClickAsync());
        await cut.InvokeAsync(() => cut.FindAll("div.mud-menu-item").Single(item => string.Equals(item.TextContent.Trim(), "New chat", StringComparison.Ordinal)).ClickAsync());
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

        string[] expected = ["New chat", "New project…"];
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
        bool tasksEnabled = true,
        ITeamDirectory? directory = null)
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
        ctx.Services.AddSingleton<ITeamCatalog>(catalog);
        ctx.Services.AddSingleton<ITeamFolders>(folders ?? new FakeTeamFolders());        ctx.Services.AddSingleton(sp => new Agency.Huddle.App.Tasks.Views.ViewStore(sp.GetRequiredService<TempDataDir>().Options(), NullLogger<Agency.Huddle.App.Tasks.Views.ViewStore>.Instance));
        TeamOptions teamOptions = new();
        teamOptions.Library.Enabled = libraryEnabled;
        teamOptions.Tasks.Enabled = tasksEnabled;
        ctx.Services.AddSingleton(Options.Create(teamOptions));
        ctx.Services.AddSingleton<Agency.Huddle.App.Tabs.TabService>();
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

    /// <summary>The row <c>div.hover-reveal-row</c> whose Team link label (beside the Team's avatar) is <paramref name="team"/>.</summary>
    private static AngleSharp.Dom.IElement RowFor(IRenderedComponent<ContainerFragment> cut, string team) =>
        cut.FindAll("div.hover-reveal-row").Single(row => string.Equals(row.QuerySelector(".hover-reveal-link a .sidebar-row-label")?.TextContent.Trim(), team, StringComparison.Ordinal));

    /// <summary>The "Team actions" button of <paramref name="team"/>'s row.</summary>
    private static AngleSharp.Dom.IElement MenuButton(IRenderedComponent<ContainerFragment> cut, string team) =>
        RowFor(cut, team).QuerySelectorAll(".hover-reveal-menu-button").Single();

    /// <summary>The trimmed text of every open <c>MudMenu</c> item (role <c>menuitem</c>, class <c>mud-menu-item</c>).</summary>
    private static string[] MenuItemTexts(IRenderedComponent<ContainerFragment> cut) =>
        [.. cut.FindAll("div.mud-menu-item").Select(item => item.TextContent.Trim())];

    /// <summary>The titles of every Rooms or Library folder, in render order.</summary>
    private static string[] FolderTitles(IRenderedComponent<ContainerFragment> cut) =>
        [.. cut.FindAll(".teams-nav-folder > .mud-nav-link .mud-nav-link-text").Select(text => text.TextContent.Trim())];

    /// <summary>The one folder titled <paramref name="title"/>.</summary>
    private static IElement Folder(IRenderedComponent<ContainerFragment> cut, string title) =>
        cut.FindAll(".teams-nav-folder").Single(folder => string.Equals(folder.QuerySelector(":scope > .mud-nav-link .mud-nav-link-text")?.TextContent.Trim(), title, StringComparison.Ordinal));

    /// <summary>The header of the one folder titled <paramref name="title"/>, which opens and closes it.</summary>
    private static IElement FolderHeader(IRenderedComponent<ContainerFragment> cut, string title) =>
        Folder(cut, title).QuerySelectorAll(":scope > .mud-nav-link").Single();

    /// <summary>The href of every link inside the folder titled <paramref name="title"/>, in render order.</summary>
    private static string?[] FolderHrefs(IRenderedComponent<ContainerFragment> cut, string title) =>
        [.. Folder(cut, title).QuerySelectorAll("a[href]").Select(a => a.GetAttribute("href"))];

    /// <summary>The trimmed text of every Team link, in render order.</summary>
    private static string[] LinkTexts(IRenderedComponent<ContainerFragment> cut) =>
        [.. cut.FindAll(".hover-reveal-link a .sidebar-row-label").Select(label => label.TextContent.Trim())];

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

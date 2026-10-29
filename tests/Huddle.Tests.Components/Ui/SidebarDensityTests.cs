using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Components.Teams;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Agents;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks.Views;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Teams;
using Agency.Huddle.Tests.Ui.Teams;
using Agency.Huddle.Tests.Css;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.NeedsYou;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// The sidebar's density rules (DS-7): a row is one line that truncates with an ellipsis, meta trails the
/// label instead of wrapping the row, only the group holding the current page opens by default, a list
/// longer than eight rows folds behind "Show all (N)", and the first section is the defined term "Rooms".
/// </summary>
public sealed class SidebarDensityTests
{
    /// <summary>The row limit the folds are written against.</summary>
    private const int Limit = 8;

    /// <summary>A Room's row label is a one-line span carrying the full name as its tooltip.</summary>
    [Fact]
    public async Task RoomRow_Label_IsAOneLineSpanWithTheFullNameAsTooltip()
    {
        await using MudBunitContext ctx = SidebarDensityTests.NewContext(out ScriptedRoomsDirectory script, new FakeTeamCatalog());
        script.Rooms.Add(new Room("r1", "A very long room name that would otherwise wrap the sidebar row onto two lines", DateTimeOffset.UnixEpoch));

        IRenderedComponent<ContainerFragment> cut = SidebarDensityTests.Render<RoomList>(ctx);

        IElement label = cut.WaitForElement(".hover-reveal-link .sidebar-row-label");
        Assert.Equal("A very long room name that would otherwise wrap the sidebar row onto two lines", label.GetAttribute("title"));
        Assert.Equal(label.GetAttribute("title"), label.TextContent.Trim());
    }

    /// <summary>The label truncates with an ellipsis on one line: the nav text container is no-wrap, clipped and ellipsised.</summary>
    [Fact]
    public void AppCss_SidebarLabels_AreNoWrapAndEllipsised()
    {
        string css = CssText.AppCss(CssNewlines.AsIs);
        Match match = Regex.Match(css, @"(?m)^\.mud-nav-group \.mud-collapse-container \.mud-nav-link \.mud-nav-link-text\s*\{([^}]*)\}");

        Assert.True(match.Success, "app.css has no truncation rule for the nav link text.");
        string rule = match.Groups[1].Value;
        Assert.Contains("white-space: nowrap", rule, StringComparison.Ordinal); // contains-ok: stylesheet source text, not rendered markup
        Assert.Contains("text-overflow: ellipsis", rule, StringComparison.Ordinal); // contains-ok: stylesheet source text, not rendered markup
        Assert.Contains("overflow: hidden", rule, StringComparison.Ordinal); // contains-ok: stylesheet source text, not rendered markup
        Assert.Contains("min-width: 0", rule, StringComparison.Ordinal); // contains-ok: stylesheet source text, not rendered markup
    }

    /// <summary>A Team's meta (its member count) is a trailing sibling of the link, never inside it, so it cannot wrap the row.</summary>
    [Fact]
    public async Task TeamRow_Meta_TrailsTheLinkAndIsNotRowText()
    {
        FakeTeamCatalog catalog = new() { Teams = [SidebarDensityTests.Team("Lab", members: 0), SidebarDensityTests.Team("Business", members: 3)] };
        await using MudBunitContext ctx = SidebarDensityTests.NewContext(out _, catalog);

        IRenderedComponent<ContainerFragment> cut = SidebarDensityTests.Render<TeamsNav>(ctx);

        IElement lab = cut.FindAll("div.hover-reveal-row").Single(row => row.QuerySelector(".hover-reveal-link")?.TextContent.Trim() == "Lab");
        Assert.Equal("0", lab.QuerySelector(".sidebar-row-meta")?.TextContent.Trim());
        Assert.Equal("No members", lab.QuerySelector(".sidebar-row-meta")?.GetAttribute("title"));
        Assert.Null(lab.QuerySelector(".hover-reveal-link .sidebar-row-meta"));
        IElement business = cut.FindAll("div.hover-reveal-row").Single(row => row.QuerySelector(".hover-reveal-link")?.TextContent.Trim() == "Business");
        Assert.Equal("3", business.QuerySelector(".sidebar-row-meta")?.TextContent.Trim());
        Assert.Equal("3 members", business.QuerySelector(".sidebar-row-meta")?.GetAttribute("title"));
    }

    /// <summary>The Chats view draws no "Rooms" section header: its first folder is "All".</summary>
    [Fact]
    public async Task FirstFolder_IsTitledAll_NotRooms()
    {
        await using MudBunitContext ctx = SidebarDensityTests.NewContext(out _, new FakeTeamCatalog());

        IRenderedComponent<ContainerFragment> cut = SidebarDensityTests.Render<RoomList>(ctx);

        Assert.Equal("All", cut.Find(".mud-nav-group .mud-nav-link-text").TextContent.Trim());
    }

    /// <summary>The Chats view's only group is the Rooms group, so it starts open (audit B3a), whatever the current page.</summary>
    [Fact]
    public async Task RoomsGroup_IsOpenByDefault()
    {
        await using MudBunitContext ctx = SidebarDensityTests.NewContext(out ScriptedRoomsDirectory script, new FakeTeamCatalog());
        script.Rooms.Add(new Room("r1", "First", DateTimeOffset.UnixEpoch));

        IRenderedComponent<ContainerFragment> cut = SidebarDensityTests.Render<RoomList>(ctx);

        cut.WaitForAssertion(() => Assert.Equal("true", SidebarDensityTests.GroupHeader(cut).GetAttribute("aria-expanded")));
    }

    /// <summary>When the current page is one of the Rooms, the Rooms group is the one that is open.</summary>
    [Fact]
    public async Task RoomsGroup_IsOpen_WhenARoomIsTheCurrentPage()
    {
        await using MudBunitContext ctx = SidebarDensityTests.NewContext(out ScriptedRoomsDirectory script, new FakeTeamCatalog());
        script.Rooms.Add(new Room("r1", "First", DateTimeOffset.UnixEpoch));
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/rooms/r1");

        IRenderedComponent<ContainerFragment> cut = SidebarDensityTests.Render<RoomList>(ctx);

        cut.WaitForAssertion(() => Assert.Equal("true", SidebarDensityTests.GroupHeader(cut).GetAttribute("aria-expanded")));
    }

    /// <summary>Navigating into a Room leaves the group open, with no click.</summary>
    [Fact]
    public async Task RoomsGroup_StaysOpen_WhenNavigationLandsOnARoom()
    {
        await using MudBunitContext ctx = SidebarDensityTests.NewContext(out ScriptedRoomsDirectory script, new FakeTeamCatalog());
        script.Rooms.Add(new Room("r1", "First", DateTimeOffset.UnixEpoch));
        IRenderedComponent<ContainerFragment> cut = SidebarDensityTests.Render<RoomList>(ctx);
        cut.WaitForAssertion(() => Assert.Equal("true", SidebarDensityTests.GroupHeader(cut).GetAttribute("aria-expanded")));

        await cut.InvokeAsync(() => ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/rooms/r1"));

        cut.WaitForAssertion(() => Assert.Equal("true", SidebarDensityTests.GroupHeader(cut).GetAttribute("aria-expanded")));
    }

    /// <summary>Eight Rooms are all shown and there is no "Show all" row.</summary>
    [Fact]
    public async Task Rooms_AtTheLimit_ShowEveryRow_AndNoShowAll()
    {
        await using MudBunitContext ctx = SidebarDensityTests.NewContext(out ScriptedRoomsDirectory script, new FakeTeamCatalog());
        SidebarDensityTests.AddRooms(script, Limit);

        IRenderedComponent<ContainerFragment> cut = SidebarDensityTests.Render<RoomList>(ctx);

        cut.WaitForAssertion(() => Assert.Equal(Limit, cut.FindAll("a[href^='/rooms/']").Count));
        Assert.Empty(cut.FindAll(".sidebar-show-all"));
    }

    /// <summary>The All list never folds: more than eight Rooms are all listed and there is no "Show all" row.</summary>
    [Fact]
    public async Task Rooms_OverTheLimit_AreAllListed_WithNoShowAll()
    {
        await using MudBunitContext ctx = SidebarDensityTests.NewContext(out ScriptedRoomsDirectory script, new FakeTeamCatalog());
        SidebarDensityTests.AddRooms(script, 11);
        IRenderedComponent<ContainerFragment> cut = SidebarDensityTests.Render<RoomList>(ctx);

        cut.WaitForAssertion(() => Assert.Equal(11, cut.FindAll("a[href^='/rooms/']").Count));
        Assert.Empty(cut.FindAll(".sidebar-show-all"));
    }

    /// <summary>The current Room is listed once even when it would have been beyond the old eight-row limit.</summary>
    [Fact]
    public async Task Rooms_ActiveRoomBeyondTheOldLimit_IsListedOnce()
    {
        await using MudBunitContext ctx = SidebarDensityTests.NewContext(out ScriptedRoomsDirectory script, new FakeTeamCatalog());
        SidebarDensityTests.AddRooms(script, 11);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/rooms/room-10");

        IRenderedComponent<ContainerFragment> cut = SidebarDensityTests.Render<RoomList>(ctx);

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("a[href='/rooms/room-10']")));
        Assert.Equal(11, cut.FindAll("a[href^='/rooms/']").Count);
    }

    /// <summary>The Teams view's only group is the Teams group, so it starts open (audit B3a), away from the Teams and on a Team's page alike.</summary>
    [Fact]
    public async Task TeamsGroup_IsOpenByDefault_AndOnATeamPage()
    {
        FakeTeamCatalog catalog = new() { Teams = [SidebarDensityTests.Team("Business", members: 1)] };
        await using MudBunitContext closed = SidebarDensityTests.NewContext(out _, catalog);
        IRenderedComponent<ContainerFragment> closedCut = SidebarDensityTests.Render<TeamsNav>(closed);
        closedCut.WaitForAssertion(() => Assert.Equal("true", SidebarDensityTests.GroupHeader(closedCut).GetAttribute("aria-expanded")));

        await using MudBunitContext open = SidebarDensityTests.NewContext(out _, catalog);
        open.Services.GetRequiredService<NavigationManager>().NavigateTo("/teams/Business");
        IRenderedComponent<ContainerFragment> openCut = SidebarDensityTests.Render<TeamsNav>(open);

        openCut.WaitForAssertion(() => Assert.Equal("true", SidebarDensityTests.GroupHeader(openCut).GetAttribute("aria-expanded")));
    }

    /// <summary>More than eight Teams show eight and "Show all (N)", which expands to every Team.</summary>
    [Fact]
    public async Task Teams_OverTheLimit_FoldBehindShowAllN()
    {
        FakeTeamCatalog catalog = new() { Teams = [.. Enumerable.Range(1, 10).Select(i => SidebarDensityTests.Team($"Team {i}", members: 1))] };
        await using MudBunitContext ctx = SidebarDensityTests.NewContext(out _, catalog);
        IRenderedComponent<ContainerFragment> cut = SidebarDensityTests.Render<TeamsNav>(ctx);

        cut.WaitForAssertion(() => Assert.Equal(Limit, cut.FindAll("div.hover-reveal-row").Count));
        Assert.Equal("Show all (10)", cut.Find(".sidebar-show-all").TextContent.Trim());

        await cut.InvokeAsync(() => cut.Find(".sidebar-show-all .mud-nav-link").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal(10, cut.FindAll("div.hover-reveal-row").Count));
    }

    /// <summary>The Tasks group starts closed away from Tasks and is open on a View's page.</summary>
    [Fact]
    public async Task TasksGroup_IsClosedByDefault_AndOpenOnAViewPage()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        await using MudBunitContext closed = SidebarDensityTests.NewTasksContext(harness);
        IRenderedComponent<ContainerFragment> closedCut = SidebarDensityTests.Render<TaskViewNav>(closed);
        Assert.Equal("false", SidebarDensityTests.GroupHeader(closedCut).GetAttribute("aria-expanded"));

        await using MudBunitContext open = SidebarDensityTests.NewTasksContext(harness);
        open.Services.GetRequiredService<NavigationManager>().NavigateTo("/tasks/my-tasks");
        IRenderedComponent<ContainerFragment> openCut = SidebarDensityTests.Render<TaskViewNav>(open);

        Assert.Equal("true", SidebarDensityTests.GroupHeader(openCut).GetAttribute("aria-expanded"));
    }

    /// <summary>More than eight Views show eight and "Show all (N)", which expands to every View.</summary>
    [Fact]
    public async Task Tasks_OverTheLimit_FoldBehindShowAllN()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        for (int i = 1; i <= 8; i++)
        {
            _ = harness.Views.Save(new TaskView { Id = $"custom{i}", Name = $"View {i}", Kind = ViewKind.List });
        }

        await using MudBunitContext ctx = SidebarDensityTests.NewTasksContext(harness);
        IRenderedComponent<ContainerFragment> cut = SidebarDensityTests.Render<TaskViewNav>(ctx);

        Assert.Equal(Limit, cut.FindAll("div.hover-reveal-row").Count);
        Assert.Equal("Show all (10)", cut.Find(".sidebar-show-all").TextContent.Trim());

        await cut.InvokeAsync(() => cut.Find(".sidebar-show-all .mud-nav-link").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal(10, cut.FindAll("div.hover-reveal-row").Count));
    }

    /// <summary>A View's row label is a one-line span with the full name as its tooltip.</summary>
    [Fact]
    public async Task TaskViewRow_Label_IsAOneLineSpanWithTheFullNameAsTooltip()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        await using MudBunitContext ctx = SidebarDensityTests.NewTasksContext(harness);

        IRenderedComponent<ContainerFragment> cut = SidebarDensityTests.Render<TaskViewNav>(ctx);

        IElement label = cut.FindAll(".hover-reveal-link .sidebar-row-label")[0];
        Assert.Equal(label.TextContent.Trim(), label.GetAttribute("title"));
    }

    /// <summary>The group header of the first <c>MudNavGroup</c> in the rendered tree.</summary>
    /// <param name="cut">The rendered component.</param>
    private static IElement GroupHeader(IRenderedComponent<ContainerFragment> cut) =>
        cut.Find(".mud-nav-group > .mud-nav-link");

    /// <summary>Adds <paramref name="count"/> root Rooms named <c>room-1</c>, <c>room-2</c>, ... in creation order.</summary>
    /// <param name="script">The scripted directory to add them to.</param>
    /// <param name="count">How many Rooms to add.</param>
    private static void AddRooms(ScriptedRoomsDirectory script, int count)
    {
        for (int i = 1; i <= count; i++)
        {
            script.Rooms.Add(new Room($"room-{i}", $"Room {i}", DateTimeOffset.UnixEpoch.AddMinutes(i)));
        }
    }

    /// <summary>A Team with <paramref name="members"/> members and no Projects.</summary>
    /// <param name="name">The Team's name.</param>
    /// <param name="members">How many members it has.</param>
    private static TeamSummary Team(string name, int members) =>
        new(name, [], [.. Enumerable.Range(1, members).Select(i => $"Member {i}")], true);

    /// <summary>Renders <typeparamref name="TComponent"/> with its menu popovers.</summary>
    /// <typeparam name="TComponent">The sidebar component to render.</typeparam>
    /// <param name="ctx">The context to render in.</param>
    private static IRenderedComponent<ContainerFragment> Render<TComponent>(MudBunitContext ctx)
        where TComponent : IComponent =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TComponent>(0);
            builder.CloseComponent();
        });

    /// <summary>A context holding what the Rooms and Teams navs inject, over a scripted Room directory and the given Team catalog.</summary>
    /// <param name="script">The handle for scripting the Rooms the directory answers with.</param>
    /// <param name="catalog">The Team catalog.</param>
    private static MudBunitContext NewContext(out ScriptedRoomsDirectory script, FakeTeamCatalog catalog)
    {
        ITeamDirectory rooms = ScriptedRoomsDirectory.Create(out script);
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton(rooms);
        ctx.Services.AddSingleton<TempDataDir>();
        ctx.Services.AddSingleton(new RoomEvents(NullLogger<RoomEvents>.Instance));
        ctx.Services.AddNeedsYouTracker();
        ctx.Services.AddSingleton<ChatService>(sp =>
        {
            TempDataDir dir = sp.GetRequiredService<TempDataDir>();
            RoomEvents events = sp.GetRequiredService<RoomEvents>();
            FileChatStore store = new(dir.Options(), NullLogger<FileChatStore>.Instance);
            return new ChatService(rooms, store, events, new FakeMentionAliasSource(), TestOptions.Create(), new ProposalStore(events), new QuestionStore(events), NullLogger<ChatService>.Instance) { TeamCatalog = catalog };
        });
        ctx.Services.AddSingleton<IAgentGateway>(new FakeAgentGateway());
        ctx.Services.AddSingleton(new PersonaHealth(TimeProvider.System, NullLogger<PersonaHealth>.Instance));
        ctx.Services.AddSingleton(sp => new AvatarStore(sp.GetRequiredService<TempDataDir>().Options(), NullLogger<AvatarStore>.Instance));
        ctx.Services.AddSingleton<ITeamCatalog>(catalog);
        ctx.Services.AddSingleton<ITeamFolders>(new FakeTeamFolders());
        ctx.Services.AddSingleton(TestOptions.Create());
        ctx.Services.AddSingleton<Agency.Huddle.App.Tabs.TabService>();
        ctx.JSInterop.Setup<string?>("huddleStorage.get", _ => true).SetResult(null);
        ctx.JSInterop.SetupVoid("huddleStorage.set", _ => true).SetVoidResult();
        return ctx;
    }

    /// <summary>A context for the Tasks nav over the task harness.</summary>
    /// <param name="harness">The harness holding the Views.</param>
    private static MudBunitContext NewTasksContext(TaskToolHarness harness)
    {
        MudBunitContext ctx = new();
        harness.AddTo(ctx.Services);
        ctx.Services.AddSingleton<Agency.Huddle.App.Tabs.TabService>();
        return ctx;
    }
}

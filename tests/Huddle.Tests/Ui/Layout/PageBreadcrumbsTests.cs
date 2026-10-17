using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Components.Layout;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Tabs;

namespace Agency.Huddle.Tests.Ui.Layout;

/// <summary>Pins the breadcrumb trail every Tab page has: the <see cref="TopBar"/> draws <see cref="PageBreadcrumbs"/> for the focused pane, and no pane draws its own.</summary>
public sealed partial class PageBreadcrumbsTests
{
    private static MudBunitContext NewContext()
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton<TabService>();
        ctx.Services.AddSingleton(StubPages.Routes());
        return ctx;
    }

    private static string Words(string text) => Whitespace().Replace(text, " ").Trim();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();

    /// <summary>The trail is in the Top Bar, above the whole window, and the pane's body starts with the page itself.</summary>
    [Fact]
    public async Task TabPage_HasItsTrailInTheTopBar_NotAboveThePage()
    {
        await using MudBunitContext ctx = NewContext();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/rooms/ab12");

        IRenderedComponent<TabHost> host = ctx.Render<TabHost>();
        IRenderedComponent<TopBar> bar = ctx.Render<TopBar>();

        Assert.Equal("stub-page stub-room", host.Find(".tab-body").Children[0].ClassName);
        Assert.Empty(host.FindAll(".page-breadcrumbs"));
        Assert.Equal("Rooms", Words(bar.Find(".top-bar-start .page-breadcrumbs").TextContent));
    }

    /// <summary>The ancestor is a link and the last crumb, the page itself, is plain text marked as the current page.</summary>
    [Fact]
    public async Task Trail_LinksTheAncestorAndLeavesTheLastCrumbPlain()
    {
        await using MudBunitContext ctx = NewContext();
        TabService tabs = ctx.Services.GetRequiredService<TabService>();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/tasks/my-tasks");
        _ = ctx.Render<TabHost>();
        tabs.SetTitle("tasks:my-tasks", "My Tasks");
        IRenderedComponent<TopBar> bar = ctx.Render<TopBar>();

        IElement link = Assert.Single(bar.FindAll(".page-breadcrumbs a[href]"));
        Assert.Equal("/tasks", link.GetAttribute("href"));
        Assert.Equal("Tasks", link.TextContent.Trim());
        IElement current = bar.Find(".page-breadcrumbs [aria-current='page']");
        Assert.Equal("My Tasks", current.TextContent.Trim());
    }

    /// <summary>Retitling the Tab, as a Task View rename does, changes the last crumb.</summary>
    [Fact]
    public async Task Trail_FollowsTheTabTitle()
    {
        await using MudBunitContext ctx = NewContext();
        TabService tabs = ctx.Services.GetRequiredService<TabService>();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/tasks/my-tasks");
        _ = ctx.Render<TabHost>();
        IRenderedComponent<TopBar> bar = ctx.Render<TopBar>();

        await bar.InvokeAsync(() => tabs.SetTitle("tasks:my-tasks", "Planning"));

        bar.WaitForAssertion(() => Assert.Equal("Tasks › Planning", Words(bar.Find(".page-breadcrumbs").TextContent)));
    }

    /// <summary>Following an ancestor crumb navigates the Tab in place, so one Tab remains and it now shows the ancestor's page.</summary>
    [Fact]
    public async Task ClickingAnAncestor_NavigatesTheTabInPlace()
    {
        await using MudBunitContext ctx = NewContext();
        TabService tabs = ctx.Services.GetRequiredService<TabService>();
        NavigationManager nav = ctx.Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/rooms/ab12");
        _ = ctx.Render<TabHost>();
        IRenderedComponent<TopBar> bar = ctx.Render<TopBar>();

        bar.Find(".page-breadcrumbs a[href]").Click();

        bar.WaitForAssertion(() =>
        {
            Tab only = Assert.Single(tabs.Tabs);
            Assert.Equal("chat", only.Key);
            Assert.Equal("/", only.Url);
        });
        Assert.Equal(nav.BaseUri, nav.Uri);
    }

    /// <summary>With two panes there is still one Top Bar: it shows the focused pane's trail, follows focus, and a crumb acts on that pane's Tab.</summary>
    [Fact]
    public async Task SplitPanes_HaveOneTopBar_ThatFollowsFocus_AndACrumbActsOnTheFocusedPane()
    {
        await using MudBunitContext ctx = NewContext();
        TabService tabs = ctx.Services.GetRequiredService<TabService>();
        Tab room = tabs.Open("/rooms/ab12");
        tabs.Keep(room.Id);
        Tab settings = tabs.Open("/settings");
        tabs.Keep(settings.Id);
        Assert.True(tabs.MoveRight(settings.Id));
        string left = tabs.Groups[0].Id;
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/settings");
        IRenderedComponent<TabHost> host = ctx.Render<TabHost>();
        IRenderedComponent<TopBar> bar = ctx.Render<TopBar>();

        Assert.Equal(2, host.FindAll("section.tab-group").Count);
        Assert.Empty(host.FindAll(".page-breadcrumbs"));
        Assert.Single(bar.FindAll(".page-breadcrumbs"));
        Assert.DoesNotContain("Rooms",Words(bar.Find(".page-breadcrumbs").TextContent), StringComparison.Ordinal);

        await bar.InvokeAsync(() => tabs.FocusGroup(left));

        bar.WaitForAssertion(() =>
        {
            Assert.Equal("Rooms", Words(bar.Find(".page-breadcrumbs").TextContent));
            Assert.Equal(left, tabs.FocusedGroupId);
        });
        bar.Find("a.page-crumb-link").Click();

        bar.WaitForAssertion(() =>
        {
            Assert.Equal("chat", tabs.Groups[0].Tabs[0].Key);
            Assert.Equal("settings", tabs.Groups[1].Tabs[0].Key);
        });
    }

    /// <summary>The Top Bar keeps its three cells, so the later search box and toolbar have a place beside the trail.</summary>
    [Fact]
    public async Task TopBar_HasCellsForTheTrailASearchBoxAndAToolbar()
    {
        await using MudBunitContext ctx = NewContext();
        _ = ctx.Services.GetRequiredService<TabService>().Open("/rooms/ab12");
        IRenderedComponent<TopBar> bar = ctx.Render<TopBar>(parameters => parameters
            .Add(p => p.Search, "find")
            .Add(p => p.Actions, "tools"));

        Assert.Equal("find", bar.Find(".top-bar-center").TextContent);
        Assert.Equal("tools", bar.Find(".top-bar-end").TextContent);
        Assert.NotNull(bar.Find(".top-bar-start .page-breadcrumbs"));
    }

    /// <summary>The control draws every crumb of a trail.</summary>
    [Fact]
    public async Task Control_ShowsEveryCrumb()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<PageBreadcrumbs> cut = ctx.Render<PageBreadcrumbs>(parameters => parameters
            .Add(p => p.Crumbs, ThreeCrumbs()));

        Assert.Equal(["Teams", "Core", "Alpha"], CrumbTexts(cut));
    }

    private static List<string> CrumbTexts(IRenderedComponent<PageBreadcrumbs> cut) =>
        [.. cut.FindAll(".page-crumb").Select(static crumb => Words(crumb.TextContent))];

    private static List<TrailCrumb> ThreeCrumbs() =>
    [
        new("Teams", null, "team"),
        new("Core", "/teams/Core", "team"),
        new("Alpha", null, "project"),
    ];
}

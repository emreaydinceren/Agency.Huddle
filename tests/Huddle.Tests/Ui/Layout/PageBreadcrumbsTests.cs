using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Components.Layout;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Tabs;

namespace Agency.Huddle.Tests.Ui.Layout;

/// <summary>Pins the breadcrumb bar every Tab page wears: <see cref="TabPageHost"/> mounts <see cref="PageBreadcrumbs"/> above the page, one per pane.</summary>
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

    /// <summary>The bar is the first thing in the pane's body, above the page it belongs to, and reads the area then the Tab's title.</summary>
    [Fact]
    public async Task TabPage_HasABreadcrumbBarAboveThePage()
    {
        await using MudBunitContext ctx = NewContext();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/rooms/ab12");

        IRenderedComponent<TabHost> cut = ctx.Render<TabHost>();

        IElement body = cut.Find(".tab-body");
        Assert.Equal("page-breadcrumbs", body.Children[0].ClassName);
        Assert.Equal("stub-page stub-room", body.Children[1].ClassName);
        Assert.Equal("Chats › Chat", Words(cut.Find(".page-breadcrumbs").TextContent));
    }

    /// <summary>The ancestor is a link and the last crumb, the page itself, is plain text marked as the current page.</summary>
    [Fact]
    public async Task Trail_LinksTheAncestorAndLeavesTheLastCrumbPlain()
    {
        await using MudBunitContext ctx = NewContext();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/rooms/ab12");

        IRenderedComponent<TabHost> cut = ctx.Render<TabHost>();

        IElement link = Assert.Single(cut.FindAll(".page-breadcrumbs a[href]"));
        Assert.Equal("/", link.GetAttribute("href"));
        Assert.Equal("Chats", link.TextContent.Trim());
        IElement current = cut.Find(".page-breadcrumbs [aria-current='page']");
        Assert.Equal("Chat", current.TextContent.Trim());
    }

    /// <summary>Retitling the Tab, as a Room rename does, changes the last crumb.</summary>
    [Fact]
    public async Task Trail_FollowsTheTabTitle()
    {
        await using MudBunitContext ctx = NewContext();
        TabService tabs = ctx.Services.GetRequiredService<TabService>();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/rooms/ab12");
        IRenderedComponent<TabHost> cut = ctx.Render<TabHost>();

        await cut.InvokeAsync(() => tabs.SetTitle("room:ab12", "Planning"));

        cut.WaitForAssertion(() => Assert.Equal("Chats › Planning", Words(cut.Find(".page-breadcrumbs").TextContent)));
    }

    /// <summary>Following an ancestor crumb navigates the Tab in place, so one Tab remains and it now shows the ancestor's page.</summary>
    [Fact]
    public async Task ClickingAnAncestor_NavigatesTheTabInPlace()
    {
        await using MudBunitContext ctx = NewContext();
        TabService tabs = ctx.Services.GetRequiredService<TabService>();
        NavigationManager nav = ctx.Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/rooms/ab12");
        IRenderedComponent<TabHost> cut = ctx.Render<TabHost>();

        cut.Find(".page-breadcrumbs a[href]").Click();

        cut.WaitForAssertion(() =>
        {
            Tab only = Assert.Single(tabs.Tabs);
            Assert.Equal("chat", only.Key);
            Assert.Equal("/", only.Url);
        });
        Assert.Equal(nav.BaseUri, nav.Uri);
    }

    /// <summary>With two panes each draws its own bar, and following a crumb in the unfocused pane changes only that pane's Tab and focuses it.</summary>
    [Fact]
    public async Task SplitPanes_EachHaveABar_AndACrumbActsOnItsOwnPane()
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
        IRenderedComponent<TabHost> cut = ctx.Render<TabHost>();

        IReadOnlyList<IElement> panes = cut.FindAll("section.tab-group");
        Assert.Equal(2, panes.Count);
        Assert.All(panes, static pane => Assert.NotNull(pane.QuerySelector(".page-breadcrumbs")));
        panes[0].QuerySelector("a.page-crumb-link")?.Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(left, tabs.FocusedGroupId);
            Assert.Equal("chat", tabs.Groups[0].Tabs[0].Key);
            Assert.Equal("settings", tabs.Groups[1].Tabs[0].Key);
        });
    }

    /// <summary>The control draws every crumb of a trail when it is not collapsed.</summary>
    [Fact]
    public async Task Control_NotCollapsed_ShowsEveryCrumb()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<PageBreadcrumbs> cut = ctx.Render<PageBreadcrumbs>(parameters => parameters
            .Add(p => p.Crumbs, ThreeCrumbs())
            .Add(p => p.Collapse, false));

        Assert.Equal(["Teams", "Core", "Alpha"], CrumbTexts(cut));
    }

    /// <summary>A collapsed trail of three keeps the first and last crumb and hides the middle one behind an ellipsis.</summary>
    [Fact]
    public async Task Control_Collapsed_HidesTheMiddleCrumb()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<PageBreadcrumbs> cut = ctx.Render<PageBreadcrumbs>(parameters => parameters
            .Add(p => p.Crumbs, ThreeCrumbs())
            .Add(p => p.Collapse, true));

        Assert.Equal(["Teams", "Alpha"], CrumbTexts(cut));
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
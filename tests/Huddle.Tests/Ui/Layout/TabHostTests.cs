using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Components.Layout;
using Agency.Huddle.App.Tabs;

namespace Agency.Huddle.Tests.Ui.Layout;

/// <summary>Pins the tab strip as the Human meets it: <see cref="TabHost"/>, <see cref="TabStrip"/> and <see cref="NavTabLink"/> over a real <see cref="TabService"/>.</summary>
public sealed class TabHostTests
{
    private static MudBunitContext NewContext()
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton<TabService>();
        return ctx;
    }

    private static IRenderedComponent<TabHost> RenderHost(MudBunitContext ctx) =>
        ctx.Render<TabHost>(parameters => parameters.Add(
            host => host.ChildContent,
            (RenderFragment)(builder => builder.AddMarkupContent(0, "<p id=\"page\">page</p>"))));

    /// <summary>The location the circuit starts on opens as an active, italic preview Tab.</summary>
    [Fact]
    public async Task StartingLocation_OpensAnActivePreviewTab()
    {
        await using MudBunitContext ctx = NewContext();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/teammates");

        var cut = RenderHost(ctx);

        var tab = Assert.Single(cut.FindAll(".tab-item"));
        Assert.Contains("active", tab.ClassList);
        Assert.Contains("preview", tab.ClassList);
        Assert.Equal("Teammates", tab.QuerySelector(".tab-title")?.TextContent);
    }

    /// <summary>Navigating to another page replaces the preview Tab instead of adding one.</summary>
    [Fact]
    public async Task Navigating_ReplacesThePreviewTab()
    {
        await using MudBunitContext ctx = NewContext();
        var nav = ctx.Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/teammates");
        var cut = RenderHost(ctx);

        nav.NavigateTo("/settings");

        cut.WaitForAssertion(() => Assert.Equal("Settings", Assert.Single(cut.FindAll(".tab-item")).QuerySelector(".tab-title")?.TextContent));
    }

    /// <summary>A double-click on a Tab makes it permanent, so the next page opens beside it.</summary>
    [Fact]
    public async Task DoubleClickingATab_KeepsIt()
    {
        await using MudBunitContext ctx = NewContext();
        var nav = ctx.Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/teammates");
        var cut = RenderHost(ctx);

        cut.Find(".tab-item").DoubleClick();
        nav.NavigateTo("/settings");

        cut.WaitForAssertion(() =>
        {
            var tabs = cut.FindAll(".tab-item");
            Assert.Equal(2, tabs.Count);
            Assert.DoesNotContain("preview", tabs[0].ClassList);
            Assert.Contains("preview", tabs[1].ClassList);
        });
    }

    /// <summary>Closing the only Tab shows the empty state and hides the page.</summary>
    [Fact]
    public async Task ClosingTheLastTab_ShowsTheEmptyState()
    {
        await using MudBunitContext ctx = NewContext();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/teammates");
        var cut = RenderHost(ctx);

        cut.Find(".tab-close").Click();

        Assert.Empty(cut.FindAll(".tab-item"));
        Assert.NotNull(cut.Find(".tab-empty"));
        Assert.Empty(cut.FindAll("#page"));
    }

    /// <summary>Closing the active Tab navigates the router to the Tab that becomes active.</summary>
    [Fact]
    public async Task ClosingTheActiveTab_NavigatesToTheNeighbour()
    {
        await using MudBunitContext ctx = NewContext();
        var nav = ctx.Services.GetRequiredService<NavigationManager>();
        var tabs = ctx.Services.GetRequiredService<TabService>();
        nav.NavigateTo("/teammates");
        var cut = RenderHost(ctx);
        cut.Find(".tab-item").DoubleClick();
        nav.NavigateTo("/settings");
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".tab-item").Count));

        cut.FindAll(".tab-close")[1].Click();

        Assert.EndsWith("/teammates", nav.Uri, StringComparison.Ordinal);
        Assert.Equal("teammates", tabs.Active?.Key);
    }

    /// <summary>A menu link opens its page with the title the menu shows, not the address's fallback.</summary>
    [Fact]
    public async Task NavTabLink_Click_OpensTheTabWithTheMenusTitle()
    {
        await using MudBunitContext ctx = NewContext();
        var tabs = ctx.Services.GetRequiredService<TabService>();
        var cut = ctx.Render<NavTabLink>(parameters => parameters
            .Add(link => link.Href, "/rooms/ab12")
            .Add(link => link.TabTitle, "Design")
            .Add(link => link.ChildContent, (RenderFragment)(builder => builder.AddContent(0, "Design"))));

        cut.Find(".nav-tab-link").Click();

        Tab tab = Assert.Single(tabs.Tabs);
        Assert.Equal("Design", tab.Title);
        Assert.True(tab.IsPreview);
    }

    /// <summary>A double-click on a menu link makes its Tab permanent.</summary>
    [Fact]
    public async Task NavTabLink_DoubleClick_KeepsTheTab()
    {
        await using MudBunitContext ctx = NewContext();
        var tabs = ctx.Services.GetRequiredService<TabService>();
        var cut = ctx.Render<NavTabLink>(parameters => parameters
            .Add(link => link.Href, "/settings")
            .Add(link => link.ChildContent, (RenderFragment)(builder => builder.AddContent(0, "Settings"))));

        cut.Find(".nav-tab-link").Click();
        cut.Find(".nav-tab-link").DoubleClick();

        Assert.False(Assert.Single(tabs.Tabs).IsPreview);
    }
}

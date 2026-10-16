using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Components.Layout;
using Agency.Huddle.App.Tabs;

namespace Agency.Huddle.Tests.Ui.Layout;

/// <summary>
/// Pins the Right sidebar (<c>S4</c>): the Top bar's toggle opens and closes it, and it shows the blocks
/// the focused pane's active page put in it with <c>RightSidebarContent</c>, and nothing from any other Tab.
/// </summary>
public sealed class RightSidebarTests
{
    private static MudBunitContext NewContext(string url)
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton<TabService>();
        ctx.Services.AddSingleton<RightSidebarService>();
        ctx.Services.AddSingleton(StubPages.Routes());
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo(url);
        return ctx;
    }

    private static IElement ToggleButton(IRenderedComponent<RightSidebarToggle> toggle) => toggle.Find("button.right-sidebar-toggle");

    /// <summary>The sidebar starts closed; the toggle opens it and says so, then closes it again.</summary>
    [Fact]
    public async Task Toggle_OpensAndClosesTheSidebar()
    {
        await using MudBunitContext ctx = NewContext("/sidebar");
        _ = ctx.Render<TabHost>();
        IRenderedComponent<RightSidebar> sidebar = ctx.Render<RightSidebar>();
        IRenderedComponent<RightSidebarToggle> toggle = ctx.Render<RightSidebarToggle>();

        Assert.Equal("false", ToggleButton(toggle).GetAttribute("aria-pressed"));
        Assert.Equal("Show right sidebar", ToggleButton(toggle).GetAttribute("aria-label"));
        Assert.Empty(sidebar.FindAll(".right-sidebar-section"));

        await ToggleButton(toggle).ClickAsync(new());

        Assert.Equal("true", ToggleButton(toggle).GetAttribute("aria-pressed"));
        Assert.Equal("Hide right sidebar", ToggleButton(toggle).GetAttribute("aria-label"));
        Assert.Equal(RightSidebar.ElementId, ToggleButton(toggle).GetAttribute("aria-controls"));
        sidebar.WaitForAssertion(() => Assert.Contains("mud-drawer--open", sidebar.Find("#right-sidebar").ClassList));

        await ToggleButton(toggle).ClickAsync(new());

        Assert.Equal("false", ToggleButton(toggle).GetAttribute("aria-pressed"));
        sidebar.WaitForAssertion(() => Assert.Empty(sidebar.FindAll(".right-sidebar-section")));
    }

    /// <summary>An open sidebar draws the page's blocks sorted by Order, each under its title.</summary>
    [Fact]
    public async Task OpenSidebar_DrawsThePagesBlocksInOrder()
    {
        await using MudBunitContext ctx = NewContext("/sidebar");
        _ = ctx.Render<TabHost>();
        IRenderedComponent<RightSidebar> sidebar = ctx.Render<RightSidebar>();

        await sidebar.InvokeAsync(ctx.Services.GetRequiredService<RightSidebarService>().Toggle);

        sidebar.WaitForAssertion(() =>
        {
            IReadOnlyList<string> titles = [.. sidebar.FindAll(".right-sidebar-title").Select(static title => title.TextContent.Trim())];
            Assert.Equal(["First", "Second"], titles);
        });
        Assert.Single(sidebar.FindAll(".block-second"));
    }

    /// <summary>A page that puts nothing in the sidebar gets the empty state, not the previous page's blocks.</summary>
    [Fact]
    public async Task OpenSidebar_OnAPageWithNoBlocks_ShowsTheEmptyState()
    {
        await using MudBunitContext ctx = NewContext("/sidebar");
        TabService tabs = ctx.Services.GetRequiredService<TabService>();
        _ = ctx.Render<TabHost>();
        IRenderedComponent<RightSidebar> sidebar = ctx.Render<RightSidebar>();
        await sidebar.InvokeAsync(ctx.Services.GetRequiredService<RightSidebarService>().Toggle);
        sidebar.WaitForAssertion(() => Assert.Equal(2, sidebar.FindAll(".right-sidebar-section").Count));

        await sidebar.InvokeAsync(() => ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/settings"));

        sidebar.WaitForAssertion(() =>
        {
            Assert.Empty(sidebar.FindAll(".right-sidebar-section"));
            Assert.Equal("This page has nothing to show here.", sidebar.Find("[role='status']").TextContent.Trim());
        });
        Assert.Equal("/settings", tabs.Active?.Url);
    }

    /// <summary>Going back to the page's Tab brings its blocks back, because they follow the active Tab.</summary>
    [Fact]
    public async Task ReturningToTheTab_BringsItsBlocksBack()
    {
        await using MudBunitContext ctx = NewContext("/sidebar");
        TabService tabs = ctx.Services.GetRequiredService<TabService>();
        NavigationManager navigation = ctx.Services.GetRequiredService<NavigationManager>();
        _ = ctx.Render<TabHost>();
        IRenderedComponent<RightSidebar> sidebar = ctx.Render<RightSidebar>();
        await sidebar.InvokeAsync(ctx.Services.GetRequiredService<RightSidebarService>().Toggle);
        await sidebar.InvokeAsync(() => tabs.KeepByKey(tabs.Active?.Key ?? string.Empty));
        await sidebar.InvokeAsync(() => navigation.NavigateTo("/settings"));
        sidebar.WaitForAssertion(() => Assert.Empty(sidebar.FindAll(".right-sidebar-section")));

        await sidebar.InvokeAsync(() => navigation.NavigateTo("/sidebar"));

        sidebar.WaitForAssertion(() => Assert.Equal(2, sidebar.FindAll(".right-sidebar-section").Count));
    }

    /// <summary>A button in a block runs the page's own handler, and the block redraws with the page.</summary>
    [Fact]
    public async Task BlockButton_RunsThePagesHandler_AndTheBlockRedraws()
    {
        await using MudBunitContext ctx = NewContext("/sidebar");
        _ = ctx.Render<TabHost>();
        IRenderedComponent<RightSidebar> sidebar = ctx.Render<RightSidebar>();
        await sidebar.InvokeAsync(ctx.Services.GetRequiredService<RightSidebarService>().Toggle);
        sidebar.WaitForAssertion(() => Assert.Equal("Clicked 0", sidebar.Find(".block-first").TextContent));

        await sidebar.Find(".block-first").ClickAsync(new());

        sidebar.WaitForAssertion(() => Assert.Equal("Clicked 1", sidebar.Find(".block-first").TextContent));
    }

    /// <summary>Sections are kept per Tab: another Tab's are not returned, and equal Orders keep the order they were added in.</summary>
    [Fact]
    public void SectionsFor_FiltersByTab_AndKeepsAddOrderOnTies()
    {
        RightSidebarService service = new();
        RenderFragment empty = static _ => { };
        long first = service.NewId();
        long other = service.NewId();
        long second = service.NewId();

        service.Set(first, new RightSidebarSection("t1", "A", 0, empty));
        service.Set(other, new RightSidebarSection("t2", "Other", 0, empty));
        service.Set(second, new RightSidebarSection("t1", "B", 0, empty));
        service.Set(first, new RightSidebarSection("t1", "A again", 0, empty));

        Assert.Equal(["A again", "B"], service.SectionsFor("t1").Select(static section => section.Title));
        Assert.Empty(service.SectionsFor(null));
    }

    /// <summary>Removing a section that is not there raises no change, so an unmounting page cannot cause a redraw loop.</summary>
    [Fact]
    public void Remove_OfAMissingSection_RaisesNoChange()
    {
        RightSidebarService service = new();
        int changes = 0;
        service.Changed += () => changes++;
        long id = service.NewId();
        service.Set(id, new RightSidebarSection("t1", null, 0, static (RenderTreeBuilder _) => { }));

        service.Remove(id);
        service.Remove(id);

        Assert.Equal(2, changes);
    }
}

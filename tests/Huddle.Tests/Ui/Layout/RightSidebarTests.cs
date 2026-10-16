using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Components.Layout;
using Agency.Huddle.App.Sidebar;
using Agency.Huddle.App.Tabs;

namespace Agency.Huddle.Tests.Ui.Layout;

/// <summary>
/// Pins the Right sidebar (<c>S4</c>): the Top bar's toggle opens and closes it and, until the panel host lands, shows only its empty state.
/// </summary>
public sealed class RightSidebarTests
{
    private static MudBunitContext NewContext(string url)
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton<TabService>();
        ctx.Services.AddSingleton<RightSidebarService>();
        ctx.Services.AddSingleton(StubPages.Routes());
        ctx.Services.AddSingleton(new SidebarPanelCatalog([], Options.Create(new TeamOptions())));
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

    /// <summary>A page that puts nothing in the sidebar gets the empty state, not the previous page's blocks.</summary>
    [Fact]
    public async Task OpenSidebar_OnAPageWithNoBlocks_ShowsTheEmptyState()
    {
        await using MudBunitContext ctx = NewContext("/sidebar");
        TabService tabs = ctx.Services.GetRequiredService<TabService>();
        _ = ctx.Render<TabHost>();
        IRenderedComponent<RightSidebar> sidebar = ctx.Render<RightSidebar>();
        await sidebar.InvokeAsync(ctx.Services.GetRequiredService<RightSidebarService>().Toggle);
        await sidebar.InvokeAsync(() => ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/settings"));

        sidebar.WaitForAssertion(() =>
        {
            Assert.Empty(sidebar.FindAll(".right-sidebar-section"));
            Assert.Equal("This page has nothing to show here.", sidebar.Find("[role='status']").TextContent.Trim());
        });
        Assert.Equal("/settings", tabs.Active?.Url);
    }
}

using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Components.Layout;
using Agency.Huddle.App.Sidebar;
using Agency.Huddle.App.Tabs;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.App.Components.Shared;

namespace Agency.Huddle.Tests.Ui.Layout;

/// <summary>
/// Pins the Right sidebar (<c>S4</c>): the Top bar's toggle opens and closes it and, until the panel host lands, shows only its empty state.
/// </summary>
public sealed class RightSidebarTests
{
    private static IElement ToggleButton(IRenderedComponent<RightSidebarToggle> toggle) => toggle.Find("button.right-sidebar-toggle");

    /// <summary>With nothing remembered and no panel for the page, the sidebar stays closed rather than sitting open on its empty state; the viewer's own open is remembered (#702).</summary>
    [Fact]
    public async Task NothingRemembered_NoPanels_StaysClosed_AndRemembersTheViewersOpen()
    {
        await using MudBunitContext ctx = UiTestHost.Create(services => services.AddSingleton(new SidebarPanelCatalog([], TestOptions.Create()))).WithTabs().WithRoutes("/sidebar");
        ctx.RememberSidebarOpen(null);
        _ = ctx.Render<TabHost>();
        IRenderedComponent<RightSidebar> sidebar = ctx.Render<RightSidebar>();
        RightSidebarService service = ctx.Services.GetRequiredService<RightSidebarService>();

        Assert.False(service.IsOpen);
        await sidebar.InvokeAsync(service.Toggle);

        Assert.True(service.IsOpen);
        sidebar.WaitForAssertion(() => Assert.Contains(ctx.Module(JsModule.Storage).Invocations["set"], static call => call.Arguments is ["shell:rightSidebarOpen", "true"]));
        Assert.Equal("Open a Room or a Task to see its panels.", sidebar.Find(".empty-state-text").TextContent.Trim());
    }

    /// <summary>A sidebar the viewer left closed stays closed.</summary>
    [Fact]
    public async Task RememberedClosed_StaysClosed()
    {
        await using MudBunitContext ctx = UiTestHost.Create(services => services.AddSingleton(new SidebarPanelCatalog([], TestOptions.Create()))).WithTabs().WithRoutes("/sidebar");
        ctx.RememberSidebarOpen(false);
        _ = ctx.Render<TabHost>();
        _ = ctx.Render<RightSidebar>();

        Assert.False(ctx.Services.GetRequiredService<RightSidebarService>().IsOpen);
    }

    /// <summary>A sidebar the viewer left open opens again.</summary>
    [Fact]
    public async Task RememberedOpen_OpensAgain()
    {
        await using MudBunitContext ctx = UiTestHost.Create(services => services.AddSingleton(new SidebarPanelCatalog([], TestOptions.Create()))).WithTabs().WithRoutes("/sidebar");
        ctx.RememberSidebarOpen(true);
        _ = ctx.Render<TabHost>();
        IRenderedComponent<RightSidebar> sidebar = ctx.Render<RightSidebar>();

        sidebar.WaitForAssertion(() => Assert.True(ctx.Services.GetRequiredService<RightSidebarService>().IsOpen));
    }

    /// <summary>The sidebar starts closed; the toggle opens it and says so, then closes it again.</summary>
    [Fact]
    public async Task Toggle_OpensAndClosesTheSidebar()
    {
        await using MudBunitContext ctx = UiTestHost.Create(services => services.AddSingleton(new SidebarPanelCatalog([], TestOptions.Create()))).WithTabs().WithRoutes("/sidebar");
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
        await using MudBunitContext ctx = UiTestHost.Create(services => services.AddSingleton(new SidebarPanelCatalog([], TestOptions.Create()))).WithTabs().WithRoutes("/sidebar");
        TabService tabs = ctx.Services.GetRequiredService<TabService>();
        _ = ctx.Render<TabHost>();
        IRenderedComponent<RightSidebar> sidebar = ctx.Render<RightSidebar>();
        await sidebar.InvokeAsync(ctx.Services.GetRequiredService<RightSidebarService>().Toggle);
        await sidebar.InvokeAsync(() => ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/settings"));

        sidebar.WaitForAssertion(() =>
        {
            Assert.Empty(sidebar.FindAll(".right-sidebar-section"));
            Assert.Equal("Nothing to show here", sidebar.Find("[role='status']").TextContent.Trim());
        });
        Assert.Equal("/settings", tabs.Active?.Url);
    }
}

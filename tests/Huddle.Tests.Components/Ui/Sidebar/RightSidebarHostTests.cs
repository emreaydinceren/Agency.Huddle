using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MudBlazor;
using Agency.Huddle.App;
using Agency.Huddle.App.Components.Layout;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Sidebar;
using Agency.Huddle.App.Tabs;

namespace Agency.Huddle.Tests.Ui.Sidebar;

/// <summary>Pins the Right sidebar host (Spec D-5, D-7, D-8): it draws the catalog's panels, keys them by Subject, and mounts them only while seen.</summary>
public sealed class RightSidebarHostTests
{
    private const string EmptyText = "Nothing to show for this page.";

    private static readonly LibraryPath NotePath = new(
        new LibraryRoot("teams", "Teams", "C:\\teams", LibraryRootKind.Teams),
        "note.md",
        "C:\\teams\\note.md",
        LibraryNodeRole.File);

    private static MudBunitContext NewContext(string url, params SidebarPanel[] panels)
    {
        MudBunitContext ctx = UiTestHost.Create(services => services.AddSingleton(new SidebarPanelCatalog(panels, Options.Create(new TeamOptions()))))
            .WithTabs()
            .WithRoutes(url);
        return ctx;
    }
    private static SidebarPanel RoomPanel(string id, string title, int order) =>
        new(id, title, Icons.Material.Filled.Toc, order, static subject => subject is RoomSubject, typeof(ProbePanel));

    private static SidebarPanel DocumentPanel(string id, string title, int order) =>
        new(id, title, Icons.Material.Filled.Toc, order, static subject => subject is DocumentSubject, typeof(ProbePanel));

    private static async Task<IRenderedComponent<RightSidebar>> OpenAsync(MudBunitContext ctx)
    {
        _ = ctx.Render<TabHost>();
        IRenderedComponent<RightSidebar> sidebar = ctx.Render<RightSidebar>();
        await sidebar.InvokeAsync(ctx.Services.GetRequiredService<RightSidebarService>().Toggle);
        return sidebar;
    }

    private static Task PublishAsync(MudBunitContext ctx, IRenderedComponent<RightSidebar> sidebar, long handle, PageSubject subject)
    {
        string tabId = ctx.Services.GetRequiredService<TabService>().Active?.Id ?? string.Empty;
        return sidebar.InvokeAsync(() => ctx.Services.GetRequiredService<RightSidebarService>().Set(handle, tabId, subject));
    }

    /// <summary>Each panel that applies is drawn under its own title, in Order; one that does not apply is not drawn.</summary>
    [Fact]
    public async Task Host_DrawsEachApplyingPanelUnderItsTitle()
    {
        await using MudBunitContext ctx = NewContext(
            "/sidebar",
            RoomPanel("alpha", "Alpha", 20),
            RoomPanel("beta", "Beta", 10),
            DocumentPanel("gamma", "Gamma", 5));

        IRenderedComponent<RightSidebar> sidebar = await OpenAsync(ctx);

        sidebar.WaitForAssertion(() =>
        {
            IReadOnlyList<string> titles = [.. sidebar.FindAll(".sidebar-panel-title").Select(static title => title.TextContent.Trim())];
            Assert.Equal(["Beta", "Alpha"], titles);
        });
        Assert.Equal(2, sidebar.FindAll(".probe").Count);
    }

    /// <summary>A new snapshot with the same Key updates the mounted panel in place: no remount (Spec D-5).</summary>
    [Fact]
    public async Task Host_SameKeyNewText_UpdatesWithoutRemount()
    {
        await using MudBunitContext ctx = NewContext("/settings", DocumentPanel("doc", "Doc", 10));
        IRenderedComponent<RightSidebar> sidebar = await OpenAsync(ctx);
        long handle = ctx.Services.GetRequiredService<RightSidebarService>().NewId();
        await PublishAsync(ctx, sidebar, handle, new DocumentSubject(NotePath, LibraryFileKind.Markdown, "first"));
        sidebar.WaitForAssertion(() => Assert.Equal("first", sidebar.Find(".probe").GetAttribute("data-text")));
        string? instance = sidebar.Find(".probe").GetAttribute("data-instance");

        await PublishAsync(ctx, sidebar, handle, new DocumentSubject(NotePath, LibraryFileKind.Markdown, "second"));

        sidebar.WaitForAssertion(() => Assert.Equal("second", sidebar.Find(".probe").GetAttribute("data-text")));
        Assert.Equal("1", sidebar.Find(".probe").GetAttribute("data-inits"));
        Assert.Equal(instance, sidebar.Find(".probe").GetAttribute("data-instance"));
    }

    /// <summary>A Subject with a new Key remounts the panel, so the last Room's state never shows for a frame (Spec D-5).</summary>
    [Fact]
    public async Task Host_NewKey_Remounts()
    {
        await using MudBunitContext ctx = NewContext("/settings", RoomPanel("room", "Room", 10));
        IRenderedComponent<RightSidebar> sidebar = await OpenAsync(ctx);
        long handle = ctx.Services.GetRequiredService<RightSidebarService>().NewId();
        await PublishAsync(ctx, sidebar, handle, new RoomSubject("r1"));
        sidebar.WaitForAssertion(() => Assert.Equal("r1", sidebar.Find(".probe").GetAttribute("data-text")));
        string? instance = sidebar.Find(".probe").GetAttribute("data-instance");

        await PublishAsync(ctx, sidebar, handle, new RoomSubject("r2"));

        sidebar.WaitForAssertion(() => Assert.Equal("r2", sidebar.Find(".probe").GetAttribute("data-text")));
        Assert.Equal("1", sidebar.Find(".probe").GetAttribute("data-inits"));
        Assert.NotEqual(instance, sidebar.Find(".probe").GetAttribute("data-instance"));
    }

    /// <summary>Closing the sidebar disposes its panels: nothing stays mounted behind a closed drawer (Spec D-7).</summary>
    [Fact]
    public async Task Host_Closed_DisposesPanels()
    {
        await using MudBunitContext ctx = NewContext("/sidebar", RoomPanel("room", "Room", 10));
        IRenderedComponent<RightSidebar> sidebar = await OpenAsync(ctx);
        sidebar.WaitForAssertion(() => Assert.Single(sidebar.FindAll(".probe")));

        await sidebar.InvokeAsync(ctx.Services.GetRequiredService<RightSidebarService>().Toggle);

        sidebar.WaitForAssertion(() => Assert.Empty(sidebar.FindAll(".probe")));
    }

    /// <summary>A page that publishes no Subject gets the empty state.</summary>
    [Fact]
    public async Task Host_NoSubject_ShowsEmptyState()
    {
        await using MudBunitContext ctx = NewContext("/settings", RoomPanel("room", "Room", 10));

        IRenderedComponent<RightSidebar> sidebar = await OpenAsync(ctx);

        sidebar.WaitForAssertion(() => Assert.Equal(EmptyText, sidebar.Find("[role='status']").TextContent.Trim()));
        Assert.Empty(sidebar.FindAll(".probe"));
    }

    /// <summary>A Subject that no registered panel applies to gets the same empty state.</summary>
    [Fact]
    public async Task Host_NoApplyingPanel_ShowsEmptyState()
    {
        await using MudBunitContext ctx = NewContext("/sidebar", DocumentPanel("doc", "Doc", 10));

        IRenderedComponent<RightSidebar> sidebar = await OpenAsync(ctx);

        sidebar.WaitForAssertion(() => Assert.Equal(EmptyText, sidebar.Find("[role='status']").TextContent.Trim()));
        Assert.Empty(sidebar.FindAll(".probe"));
    }

    /// <summary>The sidebar follows the focused pane's Tab: leaving the Tab drops its panels, returning brings them back (Spec D-7).</summary>
    [Fact]
    public async Task Host_FollowsTheFocusedPane()
    {
        await using MudBunitContext ctx = NewContext("/sidebar", RoomPanel("room", "Room", 10));
        TabService tabs = ctx.Services.GetRequiredService<TabService>();
        NavigationManager navigation = ctx.Services.GetRequiredService<NavigationManager>();
        IRenderedComponent<RightSidebar> sidebar = await OpenAsync(ctx);
        await sidebar.InvokeAsync(() => tabs.KeepByKey(tabs.Active?.Key ?? string.Empty));
        await sidebar.InvokeAsync(() => navigation.NavigateTo("/settings"));
        sidebar.WaitForAssertion(() => Assert.Empty(sidebar.FindAll(".probe")));

        await sidebar.InvokeAsync(() => navigation.NavigateTo("/sidebar"));

        sidebar.WaitForAssertion(() => Assert.Single(sidebar.FindAll(".probe")));
    }
}

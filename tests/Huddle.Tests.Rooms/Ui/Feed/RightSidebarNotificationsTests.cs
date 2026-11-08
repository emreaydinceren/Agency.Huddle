using Agency.Huddle.App.Components.Layout;
using Agency.Huddle.App.Sidebar;
using Agency.Huddle.App.Tabs;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.Library;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace Agency.Huddle.Tests.Ui;

/// <summary>Pins how <see cref="RightSidebar"/> hosts the Notifications panel in its Notifications mode (Feed spec §6.8).</summary>
public sealed class RightSidebarNotificationsTests
{
    private static MudBunitContext NewContext(FeedPanelHost host, LibraryFileServiceFixture library)
    {
        MudBunitContext ctx = FeedTestData.NewContext(library);
        host.AddTo(ctx.Services);
        ctx.Services.AddSingleton(new SidebarPanelCatalog([], TestOptions.Create()));
        return ctx;
    }

    /// <summary>In Notifications mode the sidebar draws one Notifications frame, with the unseen count in its header, and no Subject panels.</summary>
    [Fact]
    public async Task NotificationsMode_DrawsTheNotificationsFrame()
    {
        using FeedPanelHost host = new();
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();
        _ = host.PostNeedsYou();
        _ = host.PostNeedsYou(1);
        await using MudBunitContext ctx = NewContext(host, library);
        IRenderedComponent<RightSidebar> sidebar = ctx.Render<RightSidebar>();

        await sidebar.InvokeAsync(ctx.Services.GetRequiredService<RightSidebarService>().ToggleNotifications);

        sidebar.WaitForAssertion(() =>
        {
            Assert.Single(sidebar.FindAll("section.sidebar-panel"));
            Assert.Equal("Notifications", sidebar.Find("section.sidebar-panel").GetAttribute("aria-label"));
            Assert.Equal("2", sidebar.Find(".sidebar-panel-count").TextContent.Trim());
            Assert.Equal(2, sidebar.FindAll(".feed-row").Count);
            Assert.DoesNotContain("Nothing to show here", sidebar.Markup, StringComparison.Ordinal);
        });
    }

    /// <summary>The Subject toggle opens the sidebar in Subject mode, which draws no Notifications frame.</summary>
    [Fact]
    public async Task SubjectMode_DrawsNoNotificationsFrame()
    {
        using FeedPanelHost host = new();
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();
        await using MudBunitContext ctx = NewContext(host, library);
        IRenderedComponent<RightSidebar> sidebar = ctx.Render<RightSidebar>();

        await sidebar.InvokeAsync(ctx.Services.GetRequiredService<RightSidebarService>().Toggle);

        sidebar.WaitForAssertion(() =>
        {
            Assert.Empty(sidebar.FindAll("section.sidebar-panel"));
            Assert.Equal("Nothing to show here", sidebar.Find("[role='status']").TextContent.Trim());
        });
    }

    /// <summary>Switching from Notifications to the Subject toggle removes the Notifications frame.</summary>
    [Fact]
    public async Task LeavingNotificationsMode_RemovesTheFrame()
    {
        using FeedPanelHost host = new();
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();
        await using MudBunitContext ctx = NewContext(host, library);
        RightSidebarService service = ctx.Services.GetRequiredService<RightSidebarService>();
        IRenderedComponent<RightSidebar> sidebar = ctx.Render<RightSidebar>();
        await sidebar.InvokeAsync(service.ToggleNotifications);
        sidebar.WaitForAssertion(() => Assert.Single(sidebar.FindAll("section.sidebar-panel")));

        await sidebar.InvokeAsync(service.Toggle);

        sidebar.WaitForAssertion(() => Assert.Empty(sidebar.FindAll("section.sidebar-panel")));
    }
}

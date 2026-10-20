using Agency.Huddle.App.Components.Feed;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Tabs;
using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace Agency.Huddle.Tests.Ui;

/// <summary>Renders <see cref="NotificationsButton"/>: the Top bar bell with its unseen count (Feed spec §6.8).</summary>
public sealed class NotificationsButtonTests
{
    private static MudBunitContext NewContext(FeedPanelHost host, bool enabled = true)
    {
        MudBunitContext ctx = new();
        host.Options.Value.Feed.Enabled = enabled;
        host.AddTo(ctx.Services);
        return ctx;
    }

    private static IElement Button(IRenderedComponent<NotificationsButton> cut) => cut.Find("button.notifications-button");

    /// <summary>The badge shows the unseen count and the label says how many are new.</summary>
    [Fact]
    public async Task Count_ShowsTheUnseenNotifications()
    {
        using FeedPanelHost host = new();
        _ = host.PostNeedsYou();
        _ = host.PostNeedsYou(1);
        _ = host.PostTaskCreated(2);
        await using MudBunitContext ctx = NewContext(host);

        IRenderedComponent<NotificationsButton> cut = ctx.Render<NotificationsButton>();

        Assert.Equal("Notifications, 2 new", Button(cut).GetAttribute("aria-label"));
        Assert.Equal("2", cut.Find(".mud-badge").TextContent.Trim());
    }

    /// <summary>With nothing unseen the label is plain "Notifications" and no badge is drawn.</summary>
    [Fact]
    public async Task Count_AtZero_HidesTheBadge()
    {
        using FeedPanelHost host = new();
        await using MudBunitContext ctx = NewContext(host);

        IRenderedComponent<NotificationsButton> cut = ctx.Render<NotificationsButton>();

        Assert.Equal("Notifications", Button(cut).GetAttribute("aria-label"));
        Assert.Empty(cut.FindAll(".mud-badge.mud-badge-top"));
    }

    /// <summary>Counts above 99 read as 99+.</summary>
    [Fact]
    public async Task Count_AboveNinetyNine_ReadsNinetyNinePlus()
    {
        using FeedPanelHost host = new();
        for (int i = 0; i < 100; i++)
        {
            _ = host.PostNeedsYou();
        }

        await using MudBunitContext ctx = NewContext(host);

        IRenderedComponent<NotificationsButton> cut = ctx.Render<NotificationsButton>();

        Assert.Equal("99+", cut.Find(".mud-badge").TextContent.Trim());
    }

    /// <summary>The button draws nothing while the Feed is disabled.</summary>
    [Fact]
    public async Task Disabled_DrawsNothing()
    {
        using FeedPanelHost host = new();
        _ = host.PostNeedsYou();
        await using MudBunitContext ctx = NewContext(host, enabled: false);

        IRenderedComponent<NotificationsButton> cut = ctx.Render<NotificationsButton>();

        Assert.Empty(cut.FindAll("button"));
    }

    /// <summary>Pressing the bell opens the sidebar in Notifications mode and says it is pressed; pressing again closes it.</summary>
    [Fact]
    public async Task Click_TogglesTheNotificationsMode()
    {
        using FeedPanelHost host = new();
        await using MudBunitContext ctx = NewContext(host);
        RightSidebarService sidebar = ctx.Services.GetRequiredService<RightSidebarService>();
        IRenderedComponent<NotificationsButton> cut = ctx.Render<NotificationsButton>();
        Assert.Equal("false", Button(cut).GetAttribute("aria-pressed"));

        await Button(cut).ClickAsync(new());

        Assert.True(sidebar.IsOpen);
        Assert.Equal(RightSidebarMode.Notifications, sidebar.Mode);
        cut.WaitForAssertion(() => Assert.Equal("true", Button(cut).GetAttribute("aria-pressed")));

        await Button(cut).ClickAsync(new());

        Assert.False(sidebar.IsOpen);
        cut.WaitForAssertion(() => Assert.Equal("false", Button(cut).GetAttribute("aria-pressed")));
    }

    /// <summary>The Subject toggle's open sidebar is not the Notifications panel, so the bell is not pressed.</summary>
    [Fact]
    public async Task SubjectMode_IsNotPressed()
    {
        using FeedPanelHost host = new();
        await using MudBunitContext ctx = NewContext(host);
        RightSidebarService sidebar = ctx.Services.GetRequiredService<RightSidebarService>();
        IRenderedComponent<NotificationsButton> cut = ctx.Render<NotificationsButton>();

        await cut.InvokeAsync(sidebar.Toggle);

        Assert.Equal("false", Button(cut).GetAttribute("aria-pressed"));
    }

    /// <summary>A Notification that arrives, and a batch that marks them seen, change the count on their own.</summary>
    [Fact]
    public async Task Events_RereadTheCount()
    {
        using FeedPanelHost host = new();
        await using MudBunitContext ctx = NewContext(host);
        IRenderedComponent<NotificationsButton> cut = ctx.Render<NotificationsButton>();
        Assert.Equal("Notifications", Button(cut).GetAttribute("aria-label"));

        FeedItem item = host.PostNeedsYou();
        await cut.InvokeAsync(() => host.Events.RaiseItemsAdded([item]));

        cut.WaitForAssertion(() => Assert.Equal("Notifications, 1 new", Button(cut).GetAttribute("aria-label")));

        host.Queue.MarkSeen(item.Id);
        _ = host.ApplyQueued();
        await cut.InvokeAsync(host.Events.RaiseItemsChanged);

        cut.WaitForAssertion(() => Assert.Equal("Notifications", Button(cut).GetAttribute("aria-label")));
    }
}

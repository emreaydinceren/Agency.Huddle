using System.Text.RegularExpressions;
using Agency.Huddle.App.Components.Feed;
using Agency.Huddle.App.Feed;
using AngleSharp.Dom;
using Bunit;
using Agency.Huddle.Tests.Library;

namespace Agency.Huddle.Tests.Ui;

/// <summary>Renders <see cref="NotificationsPanel"/>: the Category control, the rows, marking seen, dismissing and the empty states (Feed spec §6.8, D-21).</summary>
public sealed partial class NotificationsPanelTests
{
    private static MudBunitContext NewContext(FeedPanelHost host, LibraryFileServiceFixture library)
    {
        MudBunitContext ctx = FeedTestData.NewContext(library);
        host.AddTo(ctx.Services);
        return ctx;
    }

    private static IRenderedComponent<NotificationsPanel> Render(MudBunitContext ctx) => ctx.Render<NotificationsPanel>();

    private static string[] ChipLabels(IRenderedComponent<NotificationsPanel> cut) =>
        [.. cut.FindAll(".mud-chip").Select(static chip => Whitespace().Replace(chip.TextContent, " ").Trim())];

    private static async Task Choose(IRenderedComponent<NotificationsPanel> cut, string label)
    {
        IElement chip = cut.FindAll(".mud-chip").Single(chip => Whitespace().Replace(chip.TextContent, " ").Trim().StartsWith(label, StringComparison.Ordinal));
        await chip.ClickAsync(new());
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    /// <summary>The panel offers All and Needs you, only Needs you carries its count, and it opens on Needs you.</summary>
    [Fact]
    public async Task Categories_ShowTheirCounts_AndOpenOnNeedsYou()
    {
        using FeedPanelHost host = new();
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();
        _ = host.PostNeedsYou();
        _ = host.PostNeedsYou(1);
        _ = host.PostTaskCreated(2);
        await using MudBunitContext ctx = NewContext(host, library);

        IRenderedComponent<NotificationsPanel> cut = Render(ctx);

        Assert.Equal(["All", "Needs you 2"], ChipLabels(cut));
        IElement selected = Assert.Single(cut.FindAll(".mud-chip[aria-checked='true']"));
        Assert.StartsWith("Needs you", Whitespace().Replace(selected.TextContent, " ").Trim(), StringComparison.Ordinal);
        Assert.Equal(2, cut.FindAll(".feed-row").Count);
    }

    /// <summary>Choosing All loads every item, and choosing Needs you again narrows back to the waiting ones.</summary>
    [Fact]
    public async Task SwitchingCategory_LoadsThatCategory()
    {
        using FeedPanelHost host = new();
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();
        _ = host.PostNeedsYou();
        _ = host.PostTaskCreated(1);
        await using MudBunitContext ctx = NewContext(host, library);
        IRenderedComponent<NotificationsPanel> cut = Render(ctx);
        Assert.Single(cut.FindAll(".feed-row"));

        await Choose(cut, "All");

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".feed-row").Count));

        await Choose(cut, "Needs you");

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".feed-row")));
    }

    /// <summary>Opening the panel queues a MarkSeen for the highest id shown, and once applied the unseen count is zero.</summary>
    [Fact]
    public async Task Opening_MarksTheShownNotificationsSeen()
    {
        using FeedPanelHost host = new();
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();
        _ = host.PostNeedsYou(2);
        FeedItem newest = host.PostNeedsYou();
        Assert.Equal(2, host.Store.UnseenCount());
        await using MudBunitContext ctx = NewContext(host, library);

        _ = Render(ctx);

        IReadOnlyList<FeedCommand> commands = host.ApplyQueued();
        MarkSeenCommand seen = Assert.IsType<MarkSeenCommand>(Assert.Single(commands));
        Assert.Equal(newest.Id, seen.UpToId);
        Assert.Equal(0, host.Store.UnseenCount());
    }

    /// <summary>A panel with nothing to show marks nothing seen.</summary>
    [Fact]
    public async Task Opening_WithNothing_MarksNothing()
    {
        using FeedPanelHost host = new();
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();
        await using MudBunitContext ctx = NewContext(host, library);

        _ = Render(ctx);

        Assert.Empty(host.ApplyQueued());
    }

    /// <summary>The x on a Needs you row queues Dismiss(id) and removes the row at once.</summary>
    [Fact]
    public async Task Dismiss_InNeedsYou_RemovesTheRow()
    {
        using FeedPanelHost host = new();
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();
        FeedItem item = host.PostNeedsYou();
        await using MudBunitContext ctx = NewContext(host, library);
        IRenderedComponent<NotificationsPanel> cut = Render(ctx);
        _ = host.ApplyQueued();

        await cut.Find("button[aria-label='Dismiss']").ClickAsync(new());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".feed-row")));
        DismissCommand dismissed = Assert.IsType<DismissCommand>(Assert.Single(host.ApplyQueued()));
        Assert.Equal(item.Id, dismissed.Id);
    }

    /// <summary>In All a dismissed Notification stays, because dismissing hides it from the bell and not from the Feed.</summary>
    [Fact]
    public async Task Dismiss_InAll_KeepsTheRow()
    {
        using FeedPanelHost host = new();
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();
        _ = host.PostNeedsYou();
        await using MudBunitContext ctx = NewContext(host, library);
        IRenderedComponent<NotificationsPanel> cut = Render(ctx);
        await Choose(cut, "All");
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".feed-row")));
        _ = host.ApplyQueued();

        await cut.Find("button[aria-label='Dismiss']").ClickAsync(new());
        IReadOnlyList<FeedCommand> commands = host.ApplyQueued();
        await cut.InvokeAsync(host.Events.RaiseItemsChanged);

        Assert.IsType<DismissCommand>(Assert.Single(commands));
        Assert.Single(cut.FindAll(".feed-row"));
    }

    /// <summary>A Notification dismissed earlier is not in Needs you when the panel opens.</summary>
    [Fact]
    public async Task NeedsYou_HidesDismissedNotifications()
    {
        using FeedPanelHost host = new();
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();
        FeedItem gone = host.PostNeedsYou(1);
        _ = host.PostNeedsYou();
        host.Dismiss(gone);
        await using MudBunitContext ctx = NewContext(host, library);

        IRenderedComponent<NotificationsPanel> cut = Render(ctx);

        Assert.Single(cut.FindAll(".feed-row"));
    }

    /// <summary>A resolved Notification is dimmed in All and absent from Needs you.</summary>
    [Fact]
    public async Task ResolvedNotification_IsDimmedInAll_AndLeavesNeedsYou()
    {
        using FeedPanelHost host = new();
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();
        FeedItem done = host.PostNeedsYou(1);
        _ = host.PostNeedsYou();
        host.Resolve(done);
        await using MudBunitContext ctx = NewContext(host, library);
        IRenderedComponent<NotificationsPanel> cut = Render(ctx);
        Assert.Single(cut.FindAll(".feed-row"));
        Assert.Empty(cut.FindAll(".feed-row-resolved"));

        await Choose(cut, "All");

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(2, cut.FindAll(".feed-row").Count);
            Assert.Single(cut.FindAll(".feed-row-resolved"));
        });
    }

    /// <summary>An empty Needs you and an empty All each have their own sentence.</summary>
    [Fact]
    public async Task EmptyStates_AreWorded()
    {
        using FeedPanelHost host = new();
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();
        await using MudBunitContext ctx = NewContext(host, library);
        IRenderedComponent<NotificationsPanel> cut = Render(ctx);

        Assert.Equal("Nothing needs you", cut.Find("[role='status']").TextContent.Trim());
        Assert.Contains("Asks from your Agents appear here.", cut.Find(".empty-state").TextContent, StringComparison.Ordinal);

        await Choose(cut, "All");

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("Nothing yet", cut.Find("[role='status']").TextContent.Trim());
            Assert.Contains("What your Agents do appears here.", cut.Find(".empty-state").TextContent, StringComparison.Ordinal);
        });
    }

    /// <summary>The last row opens the Feed Tab on the panel's Category.</summary>
    [Fact]
    public async Task SeeAllActivity_NamesThePanelsCategory()
    {
        using FeedPanelHost host = new();
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();
        await using MudBunitContext ctx = NewContext(host, library);
        IRenderedComponent<NotificationsPanel> cut = Render(ctx);

        IElement link = cut.Find(".feed-see-all a");
        Assert.Equal("See all activity", link.TextContent.Trim());
        Assert.Equal("/feed?category=needs-you", link.GetAttribute("href"));

        await Choose(cut, "All");

        cut.WaitForAssertion(() => Assert.Equal("/feed?category=all", cut.Find(".feed-see-all a").GetAttribute("href")));
    }

    /// <summary>"Show more" appears when a page was full and loads the next page by BeforeId.</summary>
    [Fact]
    public async Task ShowMore_LoadsTheNextPage()
    {
        using FeedPanelHost host = new(feed => feed.PanelLimit = 2);
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();
        _ = host.PostNeedsYou(3);
        _ = host.PostNeedsYou(2);
        _ = host.PostNeedsYou(1);
        await using MudBunitContext ctx = NewContext(host, library);
        IRenderedComponent<NotificationsPanel> cut = Render(ctx);
        Assert.Equal(2, cut.FindAll(".feed-row").Count);

        await cut.Find("button.feed-show-more").ClickAsync(new());

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".feed-row").Count));
        Assert.Empty(cut.FindAll("button.feed-show-more"));
    }

    /// <summary>A new Notification appears in Needs you; an Activity item does not, though it appears in All.</summary>
    [Fact]
    public async Task NewArrivals_OnlyAppearInTheirCategory()
    {
        using FeedPanelHost host = new();
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();
        await using MudBunitContext ctx = NewContext(host, library);
        IRenderedComponent<NotificationsPanel> cut = Render(ctx);
        FeedItem activity = host.PostTaskCreated();
        FeedItem ask = host.PostNeedsYou();

        await cut.InvokeAsync(() => host.Events.RaiseItemsAdded([ask, activity]));

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".feed-row")));
        Assert.Equal(ask.Id, Assert.IsType<MarkSeenCommand>(Assert.Single(host.ApplyQueued())).UpToId);
    }

    /// <summary>When a batch resolves a Notification, the panel reloads and the row leaves Needs you.</summary>
    [Fact]
    public async Task ItemsChanged_ReloadsTheList()
    {
        using FeedPanelHost host = new();
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();
        FeedItem item = host.PostNeedsYou();
        await using MudBunitContext ctx = NewContext(host, library);
        IRenderedComponent<NotificationsPanel> cut = Render(ctx);
        Assert.Single(cut.FindAll(".feed-row"));

        host.Resolve(item);
        await cut.InvokeAsync(host.Events.RaiseItemsChanged);

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".feed-row")));
        Assert.Equal("Needs you 0", ChipLabels(cut)[1]);
    }
}

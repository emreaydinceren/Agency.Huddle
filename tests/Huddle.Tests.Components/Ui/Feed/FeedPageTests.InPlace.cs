using Agency.Huddle.App.Feed;
using Bunit;
using Bunit.Rendering;

namespace Agency.Huddle.Tests.Ui;

/// <summary>The Feed Tab refreshes the rows it shows in place when one is resolved, instead of mounting a new list.</summary>
public sealed partial class FeedPageTests
{
    /// <summary>A resolve of a shown row keeps the expanded card expanded, and the row is dimmed where it stands.</summary>
    [Fact]
    public async Task Resolve_KeepsTheExpandedCard_AndDimsTheRowInPlace()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        FeedItem ask = host.Feed.PostNeedsYou(1);
        _ = host.Feed.PostNeedsYou(0);
        await using MudBunitContext ctx = NewContext(host);
        IRenderedComponent<ContainerFragment> cut = Render(ctx);
        long[] before = CardIds(cut);
        cut.FindAll("button[aria-label='Show details']")[1].Click();
        Assert.Single(cut.FindAll(".feed-card-details"));

        host.Feed.Resolve(ask);
        await cut.InvokeAsync(() => host.Feed.Events.RaiseItemsChanged());

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".feed-card-resolved")));
        Assert.Single(cut.FindAll(".feed-card-details"));
        Assert.Single(cut.FindAll(".feed-card-expanded.feed-card-resolved"));
        Assert.Equal(before, CardIds(cut));
    }

    /// <summary>A "new items" pill that is waiting survives a resolve of a shown row.</summary>
    [Fact]
    public async Task Resolve_KeepsTheWaitingNewItemsPill()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        FeedItem ask = host.Feed.PostNeedsYou(5);
        await using MudBunitContext ctx = NewContext(host);
        ctx.JSInterop.Setup<bool>("huddleFeed.isScrolled", _ => true).SetResult(true);
        IRenderedComponent<ContainerFragment> cut = Render(ctx);
        FeedItem fresh = host.Feed.PostTaskCreated();
        await cut.InvokeAsync(() => host.Feed.Events.RaiseItemsAdded([fresh]));
        cut.WaitForAssertion(() => Assert.Equal("↑ 1 new item", Text(cut.Find(".feed-new-pill"))));

        host.Feed.Resolve(ask);
        await cut.InvokeAsync(() => host.Feed.Events.RaiseItemsChanged());

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".feed-card-resolved")));
        Assert.Equal("↑ 1 new item", Text(cut.Find(".feed-new-pill")));
        Assert.DoesNotContain(fresh.Id, CardIds(cut));
    }

    /// <summary>In Needs you a resolved row leaves the list.</summary>
    [Fact]
    public async Task Resolve_InNeedsYou_RemovesTheRow()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        FeedItem ask = host.Feed.PostNeedsYou(1);
        FeedItem other = host.Feed.PostNeedsYou(0);
        await using MudBunitContext ctx = NewContext(host);
        IRenderedComponent<ContainerFragment> cut = Render(ctx, category: "needs-you");
        Assert.Equal(2, CardIds(cut).Length);

        host.Feed.Resolve(ask);
        await cut.InvokeAsync(() => host.Feed.Events.RaiseItemsChanged());

        cut.WaitForAssertion(() => Assert.Equal([other.Id], CardIds(cut)));
    }
}

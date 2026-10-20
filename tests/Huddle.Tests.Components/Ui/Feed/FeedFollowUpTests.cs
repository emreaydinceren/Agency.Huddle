using Agency.Huddle.App.Components.Feed;
using Agency.Huddle.App.Feed;
using Agency.Huddle.Tests.Library;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Agency.Huddle.Tests.Ui;

/// <summary>The Feed follow-ups: the batch dismiss of a group, keyboard access to cards and rows, and the list's one minute ticker.</summary>
public sealed class FeedFollowUpTests
{
    private static readonly DateTimeOffset Now = FeedTestData.Now;

    private static string Where(NavigationManager navigation) => navigation.ToBaseRelativePath(navigation.Uri);

    private static FeedItem NeedsYou(string id, long itemId, int minutesAgo = 20) =>
        FeedTestData.Item(
            FeedSourceType.TaskNeedsYou,
            new TaskNeedsYouPayload(id, "Draft the pricing page", "Growth", 1, 2),
            notify: true,
            at: Now.AddMinutes(-minutesAgo),
            actor: new FeedActor(FeedActorKind.Agent, "Agent" + itemId),
            id: itemId);

    private static string Words(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>A group of three Notifications calls the batch callback once with all three, and the single callback not at all.</summary>
    [Fact]
    public async Task Row_GroupDismiss_CallsTheBatchCallbackOnce()
    {
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();
        await using MudBunitContext ctx = FeedTestData.NewContext(library);
        List<IReadOnlyList<FeedItem>> batches = [];
        int singles = 0;
        FeedItem[] items = [NeedsYou("GRO-3", 3, 1), NeedsYou("GRO-2", 2, 2), NeedsYou("GRO-1", 1, 3)];

        IRenderedComponent<FeedItemRow> row = ctx.Render<FeedItemRow>(parameters =>
        {
            parameters.Add(r => r.Group, new FeedGroup(items));
            parameters.Add(r => r.OnDismiss, EventCallback.Factory.Create<FeedItem>(ctx, _ => singles++));
            parameters.Add(r => r.OnDismissMany, EventCallback.Factory.Create<IReadOnlyList<FeedItem>>(ctx, batches.Add));
        });
        await row.Find("button[aria-label='Dismiss']").ClickAsync(new());

        IReadOnlyList<FeedItem> batch = Assert.Single(batches);
        Assert.Equal(3, batch.Count);
        Assert.Equal(0, singles);
    }

    /// <summary>Enter on a card body opens its target like a click; the body is a link and a tab stop only when it has a target.</summary>
    [Fact]
    public async Task Card_Enter_OpensTheTarget_AndAnItemWithoutOneIsNoTabStop()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        await using MudBunitContext ctx = host.NewContext();
        NavigationManager navigation = ctx.Services.GetRequiredService<NavigationManager>();
        IRenderedComponent<FeedCard> card = ctx.Render<FeedCard>(parameters => parameters.Add(c => c.Group, new FeedGroup([NeedsYou("GRO-1", 1)])));
        IRenderedComponent<FeedCard> bare = ctx.Render<FeedCard>(parameters => parameters.Add(c => c.Group, new FeedGroup([FeedTestData.Item(null, null)])));
        string before = navigation.Uri;

        card.Find(".feed-card-main").KeyDown(new KeyboardEventArgs { Key = "a" });
        Assert.Equal(before, navigation.Uri);
        card.Find(".feed-card-main").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal("tasks/item/GRO-1", Where(navigation));
        Assert.Equal("link", card.Find(".feed-card-main").GetAttribute("role"));
        Assert.Equal("0", card.Find(".feed-card-main").GetAttribute("tabindex"));
        Assert.Null(bare.Find(".feed-card-main").GetAttribute("tabindex"));
        Assert.Null(bare.Find(".feed-card-main").GetAttribute("role"));
    }

    /// <summary>Enter on a row opens its first reference like a click; a row with no reference is no tab stop.</summary>
    [Fact]
    public async Task Row_Enter_OpensTheTarget_AndARowWithoutOneIsNoTabStop()
    {
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();
        await using MudBunitContext ctx = FeedTestData.NewContext(library);
        NavigationManager navigation = ctx.Services.GetRequiredService<NavigationManager>();
        IRenderedComponent<FeedItemRow> row = ctx.Render<FeedItemRow>(parameters => parameters.Add(r => r.Group, new FeedGroup([NeedsYou("GRO-1", 1)])));
        IRenderedComponent<FeedItemRow> bare = ctx.Render<FeedItemRow>(parameters => parameters.Add(r => r.Group, new FeedGroup([FeedTestData.Item(null, null)])));

        row.Find(".feed-row").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal("tasks/item/GRO-1", Where(navigation));
        Assert.Equal("link", row.Find(".feed-row").GetAttribute("role"));
        Assert.Equal("0", row.Find(".feed-row").GetAttribute("tabindex"));
        Assert.Null(bare.Find(".feed-row").GetAttribute("tabindex"));
    }

    /// <summary>A list of cards starts one minute timer, not one per card, and stops it when it is disposed.</summary>
    [Fact]
    public async Task List_Cards_RunOneMinuteTimer_AndDisposeIt()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        CountingClock clock = new();
        MudBunitContext ctx = host.NewContext();
        ctx.Services.AddSingleton<TimeProvider>(clock);

        IRenderedComponent<FeedList> list = ctx.Render<FeedList>(parameters =>
        {
            parameters.Add(l => l.Items, [NeedsYou("GRO-1", 3), NeedsYou("GRO-2", 2), NeedsYou("GRO-3", 1)]);
            parameters.Add(l => l.Mode, FeedListMode.Cards);
        });

        Assert.Equal(3, list.FindAll(".feed-card").Count);
        Assert.Single(clock.MinuteTimers);
        await ctx.DisposeAsync();
        Assert.True(clock.MinuteTimers[0].Disposed);
    }

    /// <summary>One tick redraws every card's waiting minutes, and the expanded card's related list is not read again.</summary>
    [Fact]
    public async Task Tick_RedrawsEveryCard_WithoutReadingRelatedAgain()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        CountingClock clock = new();
        await using MudBunitContext ctx = host.NewContext();
        ctx.Services.AddSingleton<TimeProvider>(clock);
        IRenderedComponent<FeedList> list = ctx.Render<FeedList>(parameters =>
        {
            parameters.Add(l => l.Items, [NeedsYou("GRO-2", 2, 2), NeedsYou("GRO-1", 1, 1)]);
            parameters.Add(l => l.Mode, FeedListMode.Cards);
        });
        list.FindAll("button[aria-label='Show details']")[0].Click();
        FeedRelated related = list.FindComponent<FeedRelated>().Instance;
        Assert.Equal(1, related.LoadCount);

        clock.Advance(TimeSpan.FromMinutes(3));
        clock.MinuteTimers[0].Fire();

        list.WaitForAssertion(() => Assert.Equal(["Waiting for you · 5 min", "Waiting for you · 4 min"], [.. list.FindAll(".feed-pill-waiting").Select(pill => Words(pill.TextContent))]));
        Assert.Equal(1, related.LoadCount);
    }

    private sealed class CountingClock : TimeProvider
    {
        private DateTimeOffset utcNow = Now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public List<CountingTimer> MinuteTimers { get; } = [];

        public override DateTimeOffset GetUtcNow() => this.utcNow;

        public void Advance(TimeSpan delta) => this.utcNow += delta;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            CountingTimer timer = new(callback, state);
            if (period == TimeSpan.FromMinutes(1))
            {
                this.MinuteTimers.Add(timer);
            }

            return timer;
        }
    }

    private sealed class CountingTimer(TimerCallback callback, object? state) : ITimer
    {
        public bool Disposed { get; private set; }

        public void Fire() => callback(state);

        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose() => this.Disposed = true;

        public ValueTask DisposeAsync()
        {
            this.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}

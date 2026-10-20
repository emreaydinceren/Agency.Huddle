using Agency.Huddle.App.Components.Feed;
using Agency.Huddle.App.Feed;
using Agency.Huddle.Tests.Library;
using Bunit;

namespace Agency.Huddle.Tests.Ui;

/// <summary>Renders <see cref="FeedList"/>: day headings, per-day grouping and the New items pill (Feed spec §6.12, D-22, D-23).</summary>
public sealed class FeedListTests
{
    private static readonly DateTimeOffset Now = FeedTestData.Now;

    private static FeedItem Archived(long id, string actor, DateTimeOffset at) =>
        FeedTestData.Item(FeedSourceType.RoomArchived, new RoomArchivedPayload("r" + id, "Room" + id, null), at: at, actor: new FeedActor(FeedActorKind.Agent, actor), id: id);

    private static IRenderedComponent<FeedList> Render(MudBunitContext ctx, IReadOnlyList<FeedItem> items) =>
        ctx.Render<FeedList>(parameters => parameters.Add(list => list.Items, items));

    /// <summary>Each local day gets a heading: the word in 600, then the date; older days read as the date alone.</summary>
    [Fact]
    public async Task DayHeadings_NameEachLocalDay()
    {
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();
        await using MudBunitContext ctx = FeedTestData.NewContext(library);
        FeedItem[] items = [Archived(3, "Nova", Now), Archived(2, "Kai", Now.AddDays(-1)), Archived(1, "Vera", Now.AddDays(-10))];

        IRenderedComponent<FeedList> cut = Render(ctx, items);

        string[] words = [.. cut.FindAll(".feed-day-word").Select(static word => word.TextContent.Trim())];
        string[] dates = [.. cut.FindAll(".feed-day-heading").Select(static heading => heading.QuerySelector(".feed-day-date")?.TextContent.Trim() ?? string.Empty)];
        Assert.Equal(["Today", "Yesterday", "Oct 10"], words);
        Assert.Equal(["Tue, Oct 20", "Mon, Oct 19", string.Empty], dates);
    }

    /// <summary>Items are grouped inside a day and never across a day heading.</summary>
    [Fact]
    public async Task Grouping_RunsPerDay()
    {
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();
        await using MudBunitContext ctx = FeedTestData.NewContext(library);
        DateTimeOffset justAfterMidnight = new(2026, 10, 20, 0, 3, 0, TimeSpan.Zero);
        FeedItem[] items =
        [
            Archived(3, "Nova", justAfterMidnight.AddMinutes(1)),
            Archived(2, "Nova", justAfterMidnight),
            Archived(1, "Nova", justAfterMidnight.AddMinutes(-5)),
        ];

        IRenderedComponent<FeedList> cut = Render(ctx, items);

        Assert.Equal(2, cut.FindAll(".feed-row").Count);
        Assert.Equal("and 1 more Rooms", cut.Find("button.feed-row-more").TextContent.Trim());
        Assert.Equal(2, cut.FindAll(".feed-day-heading").Count);
    }

    /// <summary>When the list is at its top, newly arrived items are inserted at once and no pill is drawn.</summary>
    [Fact]
    public async Task NewItems_AtTop_AreInsertedAtOnce()
    {
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();
        await using MudBunitContext ctx = FeedTestData.NewContext(library);
        IRenderedComponent<FeedList> cut = Render(ctx, [Archived(1, "Vera", Now.AddHours(-1))]);

        await cut.InvokeAsync(() => cut.Instance.AddNewItemsAsync([Archived(2, "Nova", Now)], Xunit.TestContext.Current.CancellationToken));

        Assert.Equal(2, cut.FindAll(".feed-row").Count);
        Assert.Empty(cut.FindAll(".feed-new-pill"));
    }

    /// <summary>When the list is scrolled down, new items wait behind the pill, and clicking it inserts them and scrolls to the top.</summary>
    [Fact]
    public async Task NewItems_WhenScrolled_WaitBehindThePill_UntilClicked()
    {
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();
        await using MudBunitContext ctx = FeedTestData.NewContext(library);
        ctx.JSInterop.Setup<bool>("huddleFeed.isScrolled", _ => true).SetResult(true);
        ctx.JSInterop.SetupVoid("huddleFeed.scrollToTop", _ => true).SetVoidResult();
        IRenderedComponent<FeedList> cut = Render(ctx, [Archived(1, "Vera", Now.AddHours(-1))]);

        await cut.InvokeAsync(() => cut.Instance.AddNewItemsAsync([Archived(3, "Kai", Now), Archived(2, "Nova", Now.AddMinutes(-30))], Xunit.TestContext.Current.CancellationToken));

        Assert.Single(cut.FindAll(".feed-row"));
        Assert.Equal("↑ 2 new items", cut.Find(".feed-new-pill").TextContent.Trim());

        await cut.InvokeAsync(() => cut.Find(".feed-new-pill").ClickAsync());

        Assert.Equal(3, cut.FindAll(".feed-row").Count);
        Assert.Empty(cut.FindAll(".feed-new-pill"));
        ctx.JSInterop.VerifyInvoke("huddleFeed.scrollToTop");
    }

    /// <summary>One waiting item reads "↑ 1 new item", and the pill counts items, not groups.</summary>
    [Fact]
    public async Task Pill_CountsItems_AndSpellsTheSingular()
    {
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();
        await using MudBunitContext ctx = FeedTestData.NewContext(library);
        ctx.JSInterop.Setup<bool>("huddleFeed.isScrolled", _ => true).SetResult(true);
        IRenderedComponent<FeedList> cut = Render(ctx, [Archived(1, "Vera", Now.AddHours(-1))]);

        await cut.InvokeAsync(() => cut.Instance.AddNewItemsAsync([Archived(2, "Nova", Now)], Xunit.TestContext.Current.CancellationToken));
        Assert.Equal("↑ 1 new item", cut.Find(".feed-new-pill").TextContent.Trim());

        await cut.InvokeAsync(() => cut.Instance.AddNewItemsAsync([Archived(4, "Nova", Now), Archived(3, "Nova", Now)], Xunit.TestContext.Current.CancellationToken));
        Assert.Equal("↑ 3 new items", cut.Find(".feed-new-pill").TextContent.Trim());
    }
}

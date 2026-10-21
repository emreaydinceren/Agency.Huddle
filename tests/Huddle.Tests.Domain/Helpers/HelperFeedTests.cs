using Agency.Huddle.App;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Helpers;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Feed;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Helpers;

/// <summary>
/// <see cref="HelperFeed"/> tells the Feed when a Helper pauses or keeps failing, once per pause and once per streak, and
/// resolves each item when the condition ends (Helpers in the Feed spec §5.3, plan HF-5 and HF-7).
/// </summary>
public sealed class HelperFeedTests : IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Noon.AddMinutes(5);

    private readonly TempDataDir dir = new();
    private readonly RecordingFeedPublisher feed = new();
    private readonly ScriptedHelper helper = new("NeedsHuman");
    private readonly ManualTimeProvider clock = new(Noon, TimeZoneInfo.Utc);
    private readonly HelperStatusStore status;
    private readonly HelperBudget budget;
    private readonly HelperSettingsStore settingsStore;
    private readonly IOptions<TeamOptions> options;

    /// <summary>Opens the stores over the temp directory and gives the Helper a cap of 2 calls a day.</summary>
    public HelperFeedTests()
    {
        this.options = this.dir.Options();
        this.status = new HelperStatusStore(this.options);
        this.budget = new HelperBudget(new HelperUsageStore(this.options), this.clock);
        this.settingsStore = new HelperSettingsStore(this.options);
        this.settingsStore.Save(this.helper.Id, new HelperSettingsRow(true, null, null, null, 2));
    }

    /// <summary>Removes the temp data directory.</summary>
    public void Dispose() => this.dir.Dispose();

    private HelperSettings Settings => new HelperSettingsResolver(this.settingsStore, this.options, NullLogger<HelperSettingsResolver>.Instance).For(this.helper);

    private HelperFeed Build(IFeedPublisher? publisher = null) =>
        new(
            publisher ?? this.feed,
            this.status,
            this.budget,
            new HelperRegistry([this.helper]),
            new HelperSettingsResolver(this.settingsStore, this.options, NullLogger<HelperSettingsResolver>.Instance),
            NullLogger<HelperFeed>.Instance);

    private HelperStatus Fail(string error)
    {
        this.clock.Advance(TimeSpan.FromMinutes(1));
        return this.status.RecordFailure(this.helper.Id, this.clock.GetUtcNow(), error);
    }

    /// <summary>A run that ends over budget posts one Notification naming the cap, by Huddle, about the Helper.</summary>
    [Fact]
    public void Paused_PostsOneNotificationForTheHelper()
    {
        HelperFeed helperFeed = this.Build();

        helperFeed.Paused(this.helper, this.Settings);

        FeedMessage post = Assert.Single(this.feed.Posts);
        Assert.Equal(FeedSourceType.HelperPaused, post.SourceType);
        Assert.True(post.Notify);
        Assert.Equal("helper:NeedsHuman", post.TargetKey);
        Assert.Equal(FeedActor.Huddle, post.Actor);
        HelperPausedPayload payload = Assert.IsType<HelperPausedPayload>(post.Payload);
        Assert.Equal(("NeedsHuman", "Scripted", 2, 0), (payload.HelperId, payload.Name, payload.DailyCap, payload.Granted));
    }

    /// <summary>Every skipped save while paused is one item, not one per skip.</summary>
    [Fact]
    public void Paused_CalledAgainWhileOpen_PostsNothingMore()
    {
        HelperFeed helperFeed = this.Build();

        helperFeed.Paused(this.helper, this.Settings);
        helperFeed.Paused(this.helper, this.Settings);
        helperFeed.Paused(this.helper, this.Settings);

        _ = Assert.Single(this.feed.Posts);
    }

    /// <summary>Continue resolves the pause item, and a later pause posts a new one.</summary>
    [Fact]
    public void Continued_ResolvesThePauseAndAllowsANewOne()
    {
        HelperFeed helperFeed = this.Build();
        helperFeed.Paused(this.helper, this.Settings);

        helperFeed.Continued(this.helper.Id);
        helperFeed.Paused(this.helper, this.Settings);

        Assert.Equal(new FeedResolution("helper:NeedsHuman", FeedSourceType.HelperPaused), Assert.Single(this.feed.Resolutions));
        Assert.Equal(2, this.feed.Posts.Count);
    }

    /// <summary>The first run that goes ahead after a pause means the cap reset at midnight, so the pause resolves; a run with no pause open resolves nothing.</summary>
    [Fact]
    public void Ran_AfterAPause_ResolvesIt()
    {
        HelperFeed helperFeed = this.Build();
        helperFeed.Ran(this.helper, HelperRunKind.Applied, HelperStatus.None);
        Assert.Empty(this.feed.Resolutions);
        helperFeed.Paused(this.helper, this.Settings);

        helperFeed.Ran(this.helper, HelperRunKind.Applied, HelperStatus.None);

        Assert.Equal(new FeedResolution("helper:NeedsHuman", FeedSourceType.HelperPaused), Assert.Single(this.feed.Resolutions));
    }

    /// <summary>Two failures post nothing; the third posts one item with the count and the last error, and a fourth and fifth post nothing more.</summary>
    [Fact]
    public void Ran_FailuresInARow_PostOnceAtTheThird()
    {
        HelperFeed helperFeed = this.Build();

        helperFeed.Ran(this.helper, HelperRunKind.Failed, this.Fail("one"));
        helperFeed.Ran(this.helper, HelperRunKind.Failed, this.Fail("two"));
        Assert.Empty(this.feed.Posts);
        helperFeed.Ran(this.helper, HelperRunKind.Failed, this.Fail("The adapter isn't signed in"));
        helperFeed.Ran(this.helper, HelperRunKind.Failed, this.Fail("four"));
        helperFeed.Ran(this.helper, HelperRunKind.Failed, this.Fail("five"));

        FeedMessage post = Assert.Single(this.feed.Posts);
        Assert.Equal(FeedSourceType.HelperFailing, post.SourceType);
        Assert.True(post.Notify);
        Assert.Equal("helper:NeedsHuman", post.TargetKey);
        HelperFailingPayload payload = Assert.IsType<HelperFailingPayload>(post.Payload);
        Assert.Equal((3, "The adapter isn't signed in"), (payload.Failures, payload.LastError));
    }

    /// <summary>A long error is cut to 120 characters in the item.</summary>
    [Fact]
    public void Ran_LongError_IsCutTo120Characters()
    {
        HelperFeed helperFeed = this.Build();
        _ = this.Fail("a");
        _ = this.Fail("b");

        helperFeed.Ran(this.helper, HelperRunKind.Failed, this.Fail(new string('x', 500)));

        HelperFailingPayload payload = Assert.IsType<HelperFailingPayload>(Assert.Single(this.feed.Posts).Payload);
        Assert.Equal(120, payload.LastError.Length);
    }

    /// <summary>A good run resolves the failing item, and a new streak of three posts again.</summary>
    [Fact]
    public void Ran_GoodRunAfterFailures_ResolvesThenANewStreakPostsAgain()
    {
        HelperFeed helperFeed = this.Build();
        _ = this.Fail("a");
        _ = this.Fail("b");
        helperFeed.Ran(this.helper, HelperRunKind.Failed, this.Fail("c"));

        this.status.RecordSuccess(this.helper.Id, Later, "Done");
        helperFeed.Ran(this.helper, HelperRunKind.Applied, this.status.Get(this.helper.Id));
        _ = this.Fail("d");
        _ = this.Fail("e");
        helperFeed.Ran(this.helper, HelperRunKind.Failed, this.Fail("f"));

        Assert.Contains(new FeedResolution("helper:NeedsHuman", FeedSourceType.HelperFailing), this.feed.Resolutions);
        Assert.Equal(2, this.feed.Posts.Count(static post => post.SourceType == FeedSourceType.HelperFailing));
    }

    /// <summary>Turning the Helper off resolves both items.</summary>
    [Fact]
    public void TurnedOff_ResolvesBothItems()
    {
        HelperFeed helperFeed = this.Build();
        helperFeed.Paused(this.helper, this.Settings);
        _ = this.Fail("a");
        _ = this.Fail("b");
        helperFeed.Ran(this.helper, HelperRunKind.Failed, this.Fail("c"));

        helperFeed.TurnedOff(this.helper.Id);

        Assert.Contains(new FeedResolution("helper:NeedsHuman", FeedSourceType.HelperPaused), this.feed.Resolutions);
        Assert.Contains(new FeedResolution("helper:NeedsHuman", FeedSourceType.HelperFailing), this.feed.Resolutions);
    }

    /// <summary>A Feed that throws never escapes into the dispatcher or the card.</summary>
    [Fact]
    public void EveryMember_WhenTheFeedThrows_DoesNotThrow()
    {
        RecordingFeedPublisher broken = new() { Throws = true };
        HelperFeed helperFeed = this.Build(broken);
        _ = this.Fail("a");
        _ = this.Fail("b");
        HelperStatus third = this.Fail("c");

        helperFeed.Paused(this.helper, this.Settings);
        helperFeed.Continued(this.helper.Id);
        helperFeed.Ran(this.helper, HelperRunKind.Failed, third);
        helperFeed.Ran(this.helper, HelperRunKind.Applied, HelperStatus.None);
        helperFeed.TurnedOff(this.helper.Id);

        Assert.Empty(broken.Posts);
    }

    /// <summary>After a restart an unresolved pause that still holds is remembered, so a skipped save posts no second item.</summary>
    [Fact]
    public void Reconcile_PauseThatStillHolds_IsSeededSoNothingIsPostedTwice()
    {
        FeedStore store = new(this.options);
        _ = FeedTestSupport.Post(store, new FeedMessage(FeedSourceType.HelperPaused, "helper:NeedsHuman", FeedActor.Huddle, true, new HelperPausedPayload("NeedsHuman", "Scripted", 2, 0)));
        _ = this.budget.TryReserve(this.helper.Id, 2);
        _ = this.budget.TryReserve(this.helper.Id, 2);
        HelperFeed helperFeed = this.Build();

        helperFeed.Reconcile(store);
        helperFeed.Paused(this.helper, this.Settings);

        Assert.Empty(this.feed.Posts);
        Assert.Empty(this.feed.Resolutions);
    }

    /// <summary>After a restart a pause whose cap reset, a failing streak that ended, or a Helper that was turned off while stopped is resolved.</summary>
    [Fact]
    public void Reconcile_ConditionEndedWhileStopped_Resolves()
    {
        FeedStore store = new(this.options);
        _ = FeedTestSupport.Post(store, new FeedMessage(FeedSourceType.HelperPaused, "helper:NeedsHuman", FeedActor.Huddle, true, new HelperPausedPayload("NeedsHuman", "Scripted", 2, 0)));
        _ = FeedTestSupport.Post(store, new FeedMessage(FeedSourceType.HelperFailing, "helper:NeedsHuman", FeedActor.Huddle, true, new HelperFailingPayload("NeedsHuman", "Scripted", 3, "x")));
        _ = FeedTestSupport.Post(store, new FeedMessage(FeedSourceType.HelperPaused, "helper:Gone", FeedActor.Huddle, true, new HelperPausedPayload("Gone", "Gone", 2, 0)));
        HelperFeed helperFeed = this.Build();

        helperFeed.Reconcile(store);

        Assert.Contains(new FeedResolution("helper:NeedsHuman", FeedSourceType.HelperPaused), this.feed.Resolutions);
        Assert.Contains(new FeedResolution("helper:NeedsHuman", FeedSourceType.HelperFailing), this.feed.Resolutions);
        Assert.Contains(new FeedResolution("helper:Gone", FeedSourceType.HelperPaused), this.feed.Resolutions);
    }

    /// <summary>After a restart a failing streak that still holds is remembered, so the next failure posts no second item.</summary>
    [Fact]
    public void Reconcile_FailingThatStillHolds_IsSeeded()
    {
        FeedStore store = new(this.options);
        _ = FeedTestSupport.Post(store, new FeedMessage(FeedSourceType.HelperFailing, "helper:NeedsHuman", FeedActor.Huddle, true, new HelperFailingPayload("NeedsHuman", "Scripted", 3, "x")));
        _ = this.Fail("a");
        _ = this.Fail("b");
        _ = this.Fail("c");
        HelperFeed helperFeed = this.Build();

        helperFeed.Reconcile(store);
        helperFeed.Ran(this.helper, HelperRunKind.Failed, this.Fail("d"));

        Assert.Empty(this.feed.Posts);
        Assert.Empty(this.feed.Resolutions);
    }
}

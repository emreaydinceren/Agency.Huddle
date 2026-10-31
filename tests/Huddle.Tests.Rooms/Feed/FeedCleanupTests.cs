using Agency.Huddle.App.Feed;

namespace Agency.Huddle.Tests.Feed;

/// <summary>Pins the writer's clean-up and its retention boundaries (spec 6.6, 6.11).</summary>
public sealed class FeedCleanupTests
{
    private static readonly DateTimeOffset T0 = FeedTestSupport.T0;

    private static FeedQuery Everything => new(null, 1000, null, null);

    /// <summary>A dismissed Notification is kept one second before its retention ends and deleted one second after.</summary>
    [Fact]
    public void RunCleanup_DismissedBoundary_OneSecondEachSide()
    {
        using FeedHarness harness = new();
        FeedItem item = FeedTestSupport.Post(harness.Store, FeedTestSupport.NeedsYou("GRO-1"), T0.AddDays(-100));
        _ = harness.Store.Apply([new DismissCommand(item.Id, T0)]);

        harness.Clock.UtcNow = T0.AddDays(30).AddSeconds(-1);
        harness.Writer.RunCleanup();
        int before = harness.Store.Page(Everything).Count;
        harness.Clock.UtcNow = T0.AddDays(30).AddSeconds(1);
        harness.Writer.RunCleanup();

        Assert.Equal(1, before);
        Assert.Empty(harness.Store.Page(Everything));
    }

    /// <summary>An activity item is kept one second before its retention ends and deleted one second after.</summary>
    [Fact]
    public void RunCleanup_ActivityBoundary_OneSecondEachSide()
    {
        using FeedHarness harness = new();
        _ = FeedTestSupport.Post(harness.Store, FeedTestSupport.TaskCreated("GRO-1"), T0);

        harness.Clock.UtcNow = T0.AddDays(30).AddSeconds(-1);
        harness.Writer.RunCleanup();
        int before = harness.Store.Page(Everything).Count;
        harness.Clock.UtcNow = T0.AddDays(30).AddSeconds(1);
        harness.Writer.RunCleanup();

        Assert.Equal(1, before);
        Assert.Empty(harness.Store.Page(Everything));
    }

    /// <summary>Undismissed Notifications, resolved or not, are kept however old, and dismissed ones are judged by the dismissal time, not the creation time.</summary>
    [Fact]
    public void RunCleanup_UndismissedNotifications_AreKept()
    {
        using FeedHarness harness = new();
        _ = FeedTestSupport.Post(harness.Store, FeedTestSupport.NeedsYou("OPEN"), T0.AddDays(-400));
        _ = FeedTestSupport.Post(harness.Store, FeedTestSupport.NeedsYou("RESOLVED"), T0.AddDays(-400));
        FeedItem recentlyDismissed = FeedTestSupport.Post(harness.Store, FeedTestSupport.NeedsYou("DISMISSED"), T0.AddDays(-400));
        _ = harness.Store.Apply([
            new ResolveCommand("RESOLVED", FeedSourceType.TaskNeedsYou, T0.AddDays(-399)),
            new DismissCommand(recentlyDismissed.Id, T0.AddDays(-1)),
        ]);

        harness.Writer.RunCleanup();

        Assert.Equal(["DISMISSED", "RESOLVED", "OPEN"], harness.Store.Page(Everything).Select(row => row.TargetKey));
    }

    /// <summary>A retention of zero keeps those rows forever.</summary>
    [Fact]
    public void RunCleanup_ZeroRetention_KeepsEverything()
    {
        using FeedHarness harness = new(options =>
        {
            options.DismissedRetentionDays = 0;
            options.ActivityRetentionDays = 0;
        });
        FeedItem dismissed = FeedTestSupport.Post(harness.Store, FeedTestSupport.NeedsYou("GRO-1"), T0);
        _ = FeedTestSupport.Post(harness.Store, FeedTestSupport.TaskCreated("GRO-2"), T0);
        _ = harness.Store.Apply([new DismissCommand(dismissed.Id, T0)]);

        harness.Clock.UtcNow = T0.AddDays(5000);
        harness.Writer.RunCleanup();

        Assert.Equal(2, harness.Store.Page(Everything).Count);
    }

    /// <summary>Starting the writer runs one clean-up straight away.</summary>
    [Fact]
    public async Task StartAsync_RunsCleanupOnStart()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using FeedHarness harness = new();
        _ = FeedTestSupport.Post(harness.Store, FeedTestSupport.TaskCreated("OLD"), T0.AddDays(-60));
        _ = FeedTestSupport.Post(harness.Store, FeedTestSupport.TaskCreated("NEW"), T0.AddDays(-1));

        await harness.Writer.StartAsync(ct);
        await FeedHarness.WaitUntilAsync(() => harness.Store.Page(Everything).Count == 1, ct);
        await harness.Writer.StopAsync(ct);

        Assert.Equal("NEW", harness.Store.Page(Everything).Single().TargetKey);
    }
}

using Microsoft.Extensions.Logging;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Feed;

namespace Agency.Huddle.Tests.Feed;

/// <summary>Pins <see cref="FeedWriter"/> against a real store and a real Team directory (spec 6.6, 8).</summary>
public sealed class FeedWriterTests
{
    private static FeedQuery Everything => new(null, 1000, null, null);

    /// <summary>A Post and a Resolve queued together apply in order in one batch, so the row ends resolved.</summary>
    [Fact]
    public async Task Batch_PostThenResolve_LeavesTheRowResolved()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using FeedHarness harness = new();
        harness.Queue.Post(FeedTestSupport.NeedsYou("GRO-1"));
        harness.Queue.Resolve("GRO-1", FeedSourceType.TaskNeedsYou);

        await harness.Writer.StartAsync(ct);
        await harness.DrainAsync(ct);
        await harness.Writer.StopAsync(ct);

        FeedItem item = harness.Store.Page(Everything).Single(row => row.TargetKey == "GRO-1");
        Assert.Equal(FeedTestSupport.T0, item.ResolvedAt);
        Assert.Equal(0, harness.Store.UnseenCount());
    }

    /// <summary>Resolve, MarkSeen and Dismiss change only Notification rows; an activity row is untouched by all three.</summary>
    [Fact]
    public async Task Commands_OnActivityRow_TouchNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using FeedHarness harness = new();
        FeedItem activity = FeedTestSupport.Post(harness.Store, FeedTestSupport.TaskCreated("GRO-1"));
        FeedItem notification = FeedTestSupport.Post(harness.Store, FeedTestSupport.NeedsYou("GRO-2"));
        harness.Queue.Resolve("GRO-1", FeedSourceType.TaskCreated);
        harness.Queue.MarkSeen(notification.Id + 10);
        harness.Queue.Dismiss(activity.Id);

        await harness.Writer.StartAsync(ct);
        await harness.DrainAsync(ct);
        await harness.Writer.StopAsync(ct);

        FeedItem activityAfter = harness.Store.Page(Everything).Single(row => row.Id == activity.Id);
        FeedItem notificationAfter = harness.Store.Page(Everything).Single(row => row.Id == notification.Id);
        Assert.Null(activityAfter.SeenAt);
        Assert.Null(activityAfter.DismissedAt);
        Assert.Null(activityAfter.ResolvedAt);
        Assert.NotNull(notificationAfter.SeenAt);
    }

    /// <summary>MarkSeen marks every unseen Notification up to the id once, and Dismiss dismisses one.</summary>
    [Fact]
    public async Task MarkSeenAndDismiss_UpdateNotifications()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using FeedHarness harness = new();
        FeedItem a = FeedTestSupport.Post(harness.Store, FeedTestSupport.NeedsYou("GRO-1"));
        FeedItem b = FeedTestSupport.Post(harness.Store, FeedTestSupport.NeedsYou("GRO-2"));
        FeedItem c = FeedTestSupport.Post(harness.Store, FeedTestSupport.NeedsYou("GRO-3"));
        harness.Queue.MarkSeen(b.Id);
        harness.Queue.Dismiss(a.Id);

        await harness.Writer.StartAsync(ct);
        await harness.DrainAsync(ct);
        await harness.Writer.StopAsync(ct);

        IReadOnlyList<FeedItem> rows = harness.Store.Page(Everything);
        Assert.NotNull(rows.Single(row => row.Id == a.Id).DismissedAt);
        Assert.NotNull(rows.Single(row => row.Id == b.Id).SeenAt);
        Assert.Null(rows.Single(row => row.Id == c.Id).SeenAt);
    }

    /// <summary>The Tab's seen mark is written through the writer, and a lower id never moves it back.</summary>
    [Fact]
    public async Task MarkTabSeen_WritesTheMarkMonotonically()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using FeedHarness harness = new();
        harness.Queue.MarkTabSeen(12);
        harness.Queue.MarkTabSeen(5);

        await harness.Writer.StartAsync(ct);
        await harness.DrainAsync(ct);
        await harness.Writer.StopAsync(ct);

        Assert.Equal(12, harness.Store.TabSeenUpTo());
    }

    /// <summary>ItemsAdded is raised only after the commit, so a subscriber reading the store already sees the rows; ItemsChanged follows a touching update.</summary>
    [Fact]
    public async Task Events_AreRaisedAfterCommit()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using FeedHarness harness = new();
        int raises = 0;
        int notVisible = 0;
        harness.Events.ItemsAdded += items =>
        {
            _ = Interlocked.Increment(ref raises);
            HashSet<long> stored = [.. harness.Store.Page(Everything).Select(row => row.Id)];
            _ = Interlocked.Add(ref notVisible, items.Count(added => !stored.Contains(added.Id)));
        };
        harness.Queue.Post(FeedTestSupport.NeedsYou("GRO-1"));
        harness.Queue.Post(FeedTestSupport.NeedsYou("GRO-2"));
        harness.Queue.Resolve("GRO-1", FeedSourceType.TaskNeedsYou);

        await harness.Writer.StartAsync(ct);
        await harness.DrainAsync(ct);
        await harness.Writer.StopAsync(ct);

        Assert.True(Volatile.Read(ref raises) >= 1);
        Assert.Equal(0, Volatile.Read(ref notVisible));
        Assert.Equal(["GRO-1", "GRO-2"], harness.Added.Select(item => item.TargetKey).Where(key => key.StartsWith("GRO-", StringComparison.Ordinal)));
        Assert.Equal(1, harness.ChangedCount);
    }

    /// <summary>A batch whose updates touch no row raises no ItemsChanged.</summary>
    [Fact]
    public async Task Events_UpdateTouchingNothing_RaisesNoItemsChanged()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using FeedHarness harness = new();
        harness.Queue.Resolve("nothing-here", FeedSourceType.TaskNeedsYou);
        harness.Queue.Dismiss(999);

        await harness.Writer.StartAsync(ct);
        await harness.DrainAsync(ct);
        await harness.Writer.StopAsync(ct);

        Assert.Equal(0, harness.ChangedCount);
    }

    /// <summary>More commands than BatchSize are all applied, in order, across several batches.</summary>
    [Fact]
    public async Task Batch_MoreThanBatchSize_AllApplyInOrder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using FeedHarness harness = new(options => options.BatchSize = 3);
        for (int i = 1; i <= 8; i++)
        {
            harness.Queue.Post(FeedTestSupport.TaskCreated("GRO-" + i));
        }

        await harness.Writer.StartAsync(ct);
        await harness.DrainAsync(ct);
        await harness.Writer.StopAsync(ct);

        List<string> targets = [.. harness.Store.Page(Everything).Select(row => row.TargetKey).Where(key => key.StartsWith("GRO-", StringComparison.Ordinal)).Reverse()];
        Assert.Equal(Enumerable.Range(1, 8).Select(i => "GRO-" + i), targets);
    }

    /// <summary>A batch the database refuses is dropped with a warning, the writer lives, and the next batch succeeds.</summary>
    [Fact]
    public async Task Failure_DropsTheBatchAndTheNextBatchSucceeds()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using FeedHarness harness = new();
        await harness.Writer.StartAsync(ct);
        FeedTestSupport.Exec(harness.DataDir, "ALTER TABLE feed_events RENAME TO feed_events_away;");

        harness.Queue.Post(FeedTestSupport.TaskCreated("LOST-1"));
        await FeedHarness.WaitUntilAsync(() => harness.Logger.CountAtLeast(LogLevel.Warning) >= 1, ct);
        FeedTestSupport.Exec(harness.DataDir, "ALTER TABLE feed_events_away RENAME TO feed_events;");
        await harness.DrainAsync(ct);
        await harness.Writer.StopAsync(ct);

        IReadOnlyList<string> targets = [.. harness.Store.Page(Everything).Select(row => row.TargetKey)];
        Assert.DoesNotContain("LOST-1", targets);
        Assert.Single(targets);
    }

    /// <summary>A Room wait posted without a Room name or Team gets both from the directory before it is stored.</summary>
    [Fact]
    public async Task RoomWait_WithoutNameAndTeam_IsFilledInFromTheDirectory()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using FeedHarness harness = new();
        Room room = await harness.CreateRoomAsync("Pricing", "Growth", ct);
        harness.Queue.Post(FeedTestSupport.RoomMention(room.Id));

        await harness.Writer.StartAsync(ct);
        await harness.DrainAsync(ct);
        await harness.Writer.StopAsync(ct);

        FeedItem item = harness.Store.Page(Everything).Single(row => row.TargetKey == room.Id);
        RoomWaitPayload payload = Assert.IsType<RoomWaitPayload>(item.Payload);
        Assert.Equal("Pricing", payload.RoomName);
        Assert.Equal("Growth", payload.Team);
        Assert.Equal("Pricing", Assert.IsType<RoomWaitPayload>(harness.Added.Single(added => added.TargetKey == room.Id).Payload).RoomName);
    }

    /// <summary>Values the sender already supplied are kept, and a Room that is gone keeps its nulls.</summary>
    [Fact]
    public async Task RoomWait_SenderValuesKeptAndMissingRoomKeepsNulls()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using FeedHarness harness = new();
        Room room = await harness.CreateRoomAsync("Pricing", "Growth", ct);
        harness.Queue.Post(FeedTestSupport.RoomMention(room.Id, "Sender name", "Sender team"));
        harness.Queue.Post(FeedTestSupport.RoomMention("gone-room"));

        await harness.Writer.StartAsync(ct);
        await harness.DrainAsync(ct);
        await harness.Writer.StopAsync(ct);

        RoomWaitPayload kept = Assert.IsType<RoomWaitPayload>(harness.Store.Page(Everything).Single(row => row.TargetKey == room.Id).Payload);
        RoomWaitPayload gone = Assert.IsType<RoomWaitPayload>(harness.Store.Page(Everything).Single(row => row.TargetKey == "gone-room").Payload);
        Assert.Equal("Sender name", kept.RoomName);
        Assert.Equal("Sender team", kept.Team);
        Assert.Null(gone.RoomName);
        Assert.Null(gone.Team);
    }
}

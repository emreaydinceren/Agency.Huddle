using Agency.Huddle.App.Feed;
using Microsoft.Extensions.Logging;

namespace Agency.Huddle.Tests.Feed;

/// <summary>Pins <see cref="FeedQueue"/>, <see cref="NullFeedPublisher"/> and <see cref="FeedEvents"/> (spec 6.2, 8).</summary>
public sealed class FeedQueueTests
{
    /// <summary>A Post is queued as a PostCommand stamped with the clock's time at the call, not at the read.</summary>
    [Fact]
    public void Post_StampsTheTimeFromTheInjectedClockAtTheCall()
    {
        using FeedHarness harness = new();
        FeedMessage message = FeedTestSupport.NeedsYou();

        harness.Queue.Post(message);
        harness.Clock.Advance(TimeSpan.FromHours(3));

        Assert.True(harness.Queue.Reader.TryRead(out FeedCommand? command));
        PostCommand post = Assert.IsType<PostCommand>(command);
        Assert.Equal(message, post.Message);
        Assert.Equal(FeedTestSupport.T0, post.At);
    }

    /// <summary>A Resolve is queued in order after the Post it follows, with its own time.</summary>
    [Fact]
    public void Resolve_IsQueuedAfterThePostInFifoOrder()
    {
        using FeedHarness harness = new();

        harness.Queue.Post(FeedTestSupport.NeedsYou("GRO-1"));
        harness.Clock.Advance(TimeSpan.FromMinutes(1));
        harness.Queue.Resolve("GRO-1", FeedSourceType.TaskNeedsYou);

        Assert.True(harness.Queue.Reader.TryRead(out FeedCommand? first));
        Assert.True(harness.Queue.Reader.TryRead(out FeedCommand? second));
        Assert.IsType<PostCommand>(first);
        ResolveCommand resolve = Assert.IsType<ResolveCommand>(second);
        Assert.Equal("GRO-1", resolve.TargetKey);
        Assert.Equal(FeedSourceType.TaskNeedsYou, resolve.Type);
        Assert.Equal(FeedTestSupport.T0.AddMinutes(1), resolve.At);
    }

    /// <summary>A payload of the wrong type for its source type is dropped with a warning and never throws.</summary>
    [Fact]
    public void Post_MismatchedPayload_IsDroppedAndLogged()
    {
        using FeedHarness harness = new();
        FeedMessage bad = new(FeedSourceType.RoomArchived, "GRO-1", FeedTestSupport.Agent(), false, FeedTestSupport.SamplePayload(FeedSourceType.TaskCreated));

        Exception? thrown = Record.Exception(() => harness.Queue.Post(bad));

        Assert.Null(thrown);
        Assert.False(harness.Queue.Reader.TryRead(out _));
        Assert.True(harness.Logger.CountAtLeast(LogLevel.Warning) >= 1);
    }

    /// <summary>A full queue drops the newest command with a warning, keeps the older ones, and never throws.</summary>
    [Fact]
    public void Post_FullQueue_DropsAndLogsWithoutThrowing()
    {
        using FeedHarness harness = new(options => options.QueueCapacity = 2);

        Exception? thrown = Record.Exception(() =>
        {
            harness.Queue.Post(FeedTestSupport.TaskCreated("GRO-1"));
            harness.Queue.Post(FeedTestSupport.TaskCreated("GRO-2"));
            harness.Queue.Post(FeedTestSupport.TaskCreated("GRO-3"));
            harness.Queue.Resolve("GRO-1", FeedSourceType.TaskNeedsYou);
        });

        Assert.Null(thrown);
        List<string> targets = [];
        while (harness.Queue.Reader.TryRead(out FeedCommand? command))
        {
            targets.Add(((PostCommand)command).Message.TargetKey);
        }

        Assert.Equal(["GRO-1", "GRO-2"], targets);
        Assert.True(harness.Logger.CountAtLeast(LogLevel.Warning) >= 1);
    }

    /// <summary>The UI methods queue the three UI commands with the clock's time.</summary>
    [Fact]
    public void UiMethods_QueueMarkSeenDismissAndMarkTabSeen()
    {
        using FeedHarness harness = new();

        harness.Queue.MarkSeen(7);
        harness.Queue.Dismiss(3);
        harness.Queue.MarkTabSeen(9);

        Assert.True(harness.Queue.Reader.TryRead(out FeedCommand? a));
        Assert.True(harness.Queue.Reader.TryRead(out FeedCommand? b));
        Assert.True(harness.Queue.Reader.TryRead(out FeedCommand? c));
        Assert.Equal(new MarkSeenCommand(7, FeedTestSupport.T0), a);
        Assert.Equal(new DismissCommand(3, FeedTestSupport.T0), b);
        Assert.Equal(new MarkTabSeenCommand(9, FeedTestSupport.T0), c);
    }

    /// <summary>The disabled publisher accepts every call and does nothing.</summary>
    [Fact]
    public void NullFeedPublisher_DoesNothingAndNeverThrows()
    {
        NullFeedPublisher publisher = new();

        Exception? thrown = Record.Exception(() =>
        {
            publisher.Post(FeedTestSupport.TaskCreated());
            publisher.Resolve("GRO-1", FeedSourceType.TaskNeedsYou);
        });

        Assert.Null(thrown);
    }

    /// <summary>The events hub delivers to subscribers and tolerates having none.</summary>
    [Fact]
    public void FeedEvents_RaiseReachesSubscribersAndToleratesNone()
    {
        FeedEvents events = new();
        events.RaiseItemsAdded([]);
        events.RaiseItemsChanged();
        int added = 0;
        int changed = 0;
        events.ItemsAdded += items => added += items.Count;
        events.ItemsChanged += () => changed++;

        events.RaiseItemsAdded([new FeedItem(1, FeedTestSupport.T0, FeedSourceType.TaskCreated, "GRO-1", FeedActor.Huddle, false, null, null, null, null)]);
        events.RaiseItemsChanged();

        Assert.Equal(1, added);
        Assert.Equal(1, changed);
    }
}

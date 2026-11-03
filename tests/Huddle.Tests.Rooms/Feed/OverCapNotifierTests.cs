using Agency.Huddle.App.Feed;
using Agency.Huddle.Tests.Feed;
using Microsoft.Extensions.Time.Testing;
using OverCapNotifier = Agency.Huddle.App.SecondBrain.OverCapNotifier;

namespace Agency.Huddle.Tests.SecondBrain;

/// <summary>
/// Pins SB-R13 (Spec §6.8, Settled text T14): an index builder that reports overflow causes one Feed Notification per folder per UTC day,
/// in category Second Brain, held in memory under the key (folder, UTC date).
/// </summary>
public sealed class OverCapNotifierTests
{
    private const string Folder = "Teams/Home/memory";

    // T14 with Folder "Teams/Home/memory", Count 50 and Cap 50.
    private const string ExpectedText = "Teams/Home/memory has more Memory than Agents are shown (50 of 50 listed). Ask the Team's manager to tidy it.";

    private static readonly DateTimeOffset Noon = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    /// <summary>An over-cap report publishes one Notification with the T14 text, in category Second Brain, about the folder.</summary>
    [Fact]
    public void Report_OverCapFolder_PostsOneNotificationWithT14()
    {
        RecordingFeedPublisher feed = new();
        OverCapNotifier notifier = new(feed, new FakeTimeProvider(Noon));

        notifier.Report(Folder, 50, 50);

        FeedMessage message = Assert.Single(feed.Posts);
        Assert.True(message.Notify);
        Assert.Equal(Folder, message.TargetKey);
        Assert.Equal(FeedCategory.SecondBrain, FeedCategories.Of(message.SourceType));
        Assert.Equal("Second Brain", FeedCategories.Label(FeedCategory.SecondBrain));
        SecondBrainOverCapPayload payload = Assert.IsType<SecondBrainOverCapPayload>(message.Payload);
        Assert.Equal(ExpectedText, payload.Message);
        Assert.Equal(Folder, payload.Folder);
    }

    /// <summary>The text counts what is listed against the cap, not the overflow: 3 of 5 renders as "3 of 5 listed".</summary>
    [Fact]
    public void Report_ListedBelowCap_RendersListedAndCap()
    {
        RecordingFeedPublisher feed = new();
        OverCapNotifier notifier = new(feed, new FakeTimeProvider(Noon));

        notifier.Report("Teams/Home/Repairs/memory", 3, 5);

        SecondBrainOverCapPayload payload = Assert.IsType<SecondBrainOverCapPayload>(Assert.Single(feed.Posts).Payload);
        Assert.Equal("Teams/Home/Repairs/memory has more Memory than Agents are shown (3 of 5 listed). Ask the Team's manager to tidy it.", payload.Message);
    }

    /// <summary>A second report for the same folder on the same UTC day publishes nothing, even at the last second of the day.</summary>
    [Fact]
    public void Report_SameFolderSameUtcDay_PostsNothingMore()
    {
        RecordingFeedPublisher feed = new();
        FakeTimeProvider clock = new(Noon);
        OverCapNotifier notifier = new(feed, clock);

        notifier.Report(Folder, 50, 50);
        clock.SetUtcNow(new DateTimeOffset(2026, 11, 3, 23, 59, 59, TimeSpan.Zero));
        notifier.Report(Folder, 50, 50);

        Assert.Single(feed.Posts);
    }

    /// <summary>The next UTC day publishes the folder again.</summary>
    [Fact]
    public void Report_NextUtcDay_PostsAgain()
    {
        RecordingFeedPublisher feed = new();
        FakeTimeProvider clock = new(Noon);
        OverCapNotifier notifier = new(feed, clock);

        notifier.Report(Folder, 50, 50);
        clock.SetUtcNow(new DateTimeOffset(2026, 11, 4, 0, 0, 1, TimeSpan.Zero)); // One second past UTC midnight.
        notifier.Report(Folder, 50, 50);

        Assert.Equal(2, feed.Posts.Count);
    }

    /// <summary>Two folders over the cap on one day publish one Notification each, and neither suppresses the other.</summary>
    [Fact]
    public void Report_TwoFolders_PostOneEach()
    {
        RecordingFeedPublisher feed = new();
        OverCapNotifier notifier = new(feed, new FakeTimeProvider(Noon));

        notifier.Report(Folder, 50, 50);
        notifier.Report("Teams/memory", 30, 30);
        notifier.Report(Folder, 50, 50);
        notifier.Report("Teams/memory", 30, 30);

        Assert.Equal([Folder, "Teams/memory"], feed.Posts.Select(post => post.TargetKey).ToArray());
    }

    /// <summary>A Feed that throws never reaches the index builder: the report is swallowed.</summary>
    [Fact]
    public void Report_FeedThrows_DoesNotThrow()
    {
        RecordingFeedPublisher feed = new() { Throws = true };
        OverCapNotifier notifier = new(feed, new FakeTimeProvider(Noon));

        Exception? thrown = Record.Exception(() =>
        {
            notifier.Report(Folder, 50, 50);
        });

        Assert.Null(thrown);
    }
}

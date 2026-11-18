using Agency.Huddle.App.Components.Feed;
using Agency.Huddle.App.Feed;

namespace Agency.Huddle.Tests.Feed;

/// <summary>A goldfish's Feed items (actor "Name · goldfish") show under the Agent's own filter, in the store and for live arrivals.</summary>
public sealed class GoldfishFeedFilterTests
{
    private const string Stamped = "Nova · goldfish";

    /// <summary>The store's Agent filter returns the Agent's own rows and its goldfish rows, and still not another Agent's.</summary>
    [Fact]
    public void Page_AgentFilter_IncludesTheAgentsGoldfishRows()
    {
        using TempDataDir dir = new();
        FeedStore store = new(dir.Options());
        _ = FeedTestSupport.Post(store, FeedTestSupport.TaskCreated("GRO-1", "Growth", "Nova"));
        _ = FeedTestSupport.Post(store, FeedTestSupport.TaskCreated("GRO-2", "Growth", Stamped));
        _ = FeedTestSupport.Post(store, FeedTestSupport.TaskCreated("GRO-3", "Growth", "Sage"));

        IReadOnlyList<FeedItem> nova = store.Page(new FeedQuery(null, 10, "nova", null));

        Assert.Equal(["GRO-2", "GRO-1"], nova.Select(item => item.TargetKey));
    }

    /// <summary>A live item by the goldfish matches the pager's Agent filter for the Agent, and one by another Agent does not.</summary>
    [Fact]
    public void Matches_AgentFilter_AcceptsAGoldfishItem()
    {
        using TempDataDir dir = new();
        FeedStore store = new(dir.Options());
        FeedItem goldfish = FeedTestSupport.Post(store, FeedTestSupport.TaskCreated("GRO-1", "Growth", Stamped));
        FeedItem other = FeedTestSupport.Post(store, FeedTestSupport.TaskCreated("GRO-2", "Growth", "Sage"));
        FeedPager pager = new(store, 10) { Agent = "Nova" };

        Assert.True(pager.Matches(goldfish));
        Assert.False(pager.Matches(other));
    }
}

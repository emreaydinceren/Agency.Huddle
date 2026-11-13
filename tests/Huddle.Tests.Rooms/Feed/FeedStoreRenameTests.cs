using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Feed;

/// <summary>Pins R11 for the Feed filter: a Team filter matches rows recorded under today's name and under any earlier name of that Team, ignoring case.</summary>
public sealed class FeedStoreRenameTests
{
    /// <summary>Filtering on Growth returns rows recorded as Marketing and as Growth, not one recorded as Research.</summary>
    [Fact]
    public void Page_TeamFilter_MatchesEarlierNames()
    {
        using FeedHarness harness = new();
        TeamRenameStore journal = new(harness.Options);
        journal.Complete(journal.Begin(TeamRenameKind.Team, "Marketing", "Marketing", "Growth"));
        FeedStore store = new(harness.Options, journal);
        _ = FeedTestSupport.Post(store, FeedTestSupport.TaskCreated("MKT-1", team: "Marketing"));
        _ = FeedTestSupport.Post(store, FeedTestSupport.TaskCreated("GRO-1", team: "Growth"));
        _ = FeedTestSupport.Post(store, FeedTestSupport.TaskCreated("RES-1", team: "Research"));

        IReadOnlyList<FeedItem> page = store.Page(new FeedQuery(null, 50, null, "growth"));

        Assert.Equal(["GRO-1", "MKT-1"], page.Select(static item => item.TargetKey).Order(StringComparer.Ordinal));
    }

    /// <summary>Without a journal row the filter keeps matching only the name given.</summary>
    [Fact]
    public void Page_TeamFilter_NoJournal_MatchesOnlyThatName()
    {
        using FeedHarness harness = new();
        FeedStore store = new(harness.Options, new TeamRenameStore(harness.Options));
        _ = FeedTestSupport.Post(store, FeedTestSupport.TaskCreated("MKT-1", team: "Marketing"));
        _ = FeedTestSupport.Post(store, FeedTestSupport.TaskCreated("GRO-1", team: "Growth"));

        IReadOnlyList<FeedItem> page = store.Page(new FeedQuery(null, 50, null, "Growth"));

        Assert.Equal("GRO-1", Assert.Single(page).TargetKey);
    }
}

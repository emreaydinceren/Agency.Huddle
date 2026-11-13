using Agency.Huddle.App.Components.Feed;
using Agency.Huddle.App.Feed;

namespace Agency.Huddle.Tests.Feed;

/// <summary>Pins the Feed source of a stuck Team rename (corrections-batch3 tr10): its wire name, payload, Category and summary line.</summary>
public sealed class TeamRenameStuckFeedTests
{
    private const string Text = "Renaming Marketing to Growth did not finish: a file is locked. Huddle will try again at the next start.";

    /// <summary>The source has its own wire name, which parses back.</summary>
    [Fact]
    public void WireName_TeamRenameStuck_IsItsOwn()
    {
        Assert.Equal("team-rename-stuck", FeedSourceTypes.ToWire(FeedSourceType.TeamRenameStuck));
        Assert.True(FeedSourceTypes.TryParse("team-rename-stuck", out FeedSourceType parsed));
        Assert.Equal(FeedSourceType.TeamRenameStuck, parsed);
    }

    /// <summary>The payload pairs with the source and survives the envelope.</summary>
    [Fact]
    public void Envelope_TeamRenameStuck_RoundTrips()
    {
        TeamRenameStuckPayload original = new("Growth", Text);

        string json = FeedPayloads.Serialize(FeedActorKind.Huddle, original);
        FeedEnvelope read = FeedPayloads.Read(json, FeedSourceType.TeamRenameStuck);

        Assert.Equal(typeof(TeamRenameStuckPayload), FeedPayloads.TypeOf(FeedSourceType.TeamRenameStuck));
        Assert.Equal(original, Assert.IsType<TeamRenameStuckPayload>(read.Payload));
    }

    /// <summary>The notice sits in the same Category as the archive notice.</summary>
    [Fact]
    public void Category_TeamRenameStuck_IsSecondBrain()
    {
        Assert.Equal(FeedCategory.SecondBrain, FeedCategories.Of(FeedSourceType.TeamRenameStuck));
    }

    /// <summary>The first line is the message text alone, with no actor name.</summary>
    [Fact]
    public void Of_TeamRenameStuck_ReadsAsTheMessageText()
    {
        FeedItem item = new(1, FeedTestSupport.T0, FeedSourceType.TeamRenameStuck, "team-rename:Growth", FeedTestSupport.Agent("Nova"), true, null, null, null, new TeamRenameStuckPayload("Growth", Text));

        FeedSummaryLine line = FeedSummary.Of(item);

        Assert.Equal(Text, string.Concat(line.Segments.Select(segment => segment.Text)));
        Assert.True(line.OmitActor);
    }
}

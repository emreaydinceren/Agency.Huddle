using Agency.Huddle.App.Data;
using Agency.Huddle.App.Feed;
using Agency.Huddle.Tests.Feed;

namespace Agency.Huddle.Tests.NeedsYou;

/// <summary>What <see cref="Agency.Huddle.App.NeedsYou.NeedsYouTracker"/> tells the Feed when a reason begins and ends (Feed spec §6.5.2).</summary>
public sealed class NeedsYouTrackerFeedTests
{
    /// <summary>A Mention posts one room-mention Notification by the Agent, once, with the Room's name and no Message text.</summary>
    [Fact]
    public async Task Mention_PostsOnceWithTheRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RecordingFeedPublisher feed = new();
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct, feed);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync("Pricing", [nova], ct);

        _ = await fixture.Service.PostAsync(room.Id, nova.Id, "which database, @You? SENTINEL-TEXT", ct: ct);
        _ = await fixture.Service.PostAsync(room.Id, nova.Id, "@You still there?", ct: ct);

        FeedMessage post = Assert.Single(feed.Posts);
        Assert.Equal(FeedSourceType.RoomMention, post.SourceType);
        Assert.True(post.Notify);
        Assert.Equal(room.Id, post.TargetKey);
        Assert.Equal(new FeedActor(FeedActorKind.Agent, "Nova"), post.Actor);
        RoomWaitPayload payload = Assert.IsType<RoomWaitPayload>(post.Payload);
        Assert.Equal("Pricing", payload.RoomName);
        Assert.DoesNotContain("SENTINEL", System.Text.Json.JsonSerializer.Serialize(post.Payload, post.Payload.GetType()), StringComparison.Ordinal);
    }

    /// <summary>The Human posting resolves the Mention.</summary>
    [Fact]
    public async Task HumanPosts_ResolvesMention()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RecordingFeedPublisher feed = new();
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct, feed);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync("Pricing", [nova], ct);
        _ = await fixture.Service.PostAsync(room.Id, nova.Id, "@You?", ct: ct);

        _ = await fixture.Service.PostAsync(room.Id, KnownIds.Human, "postgres", ct: ct);

        Assert.Contains(new FeedResolution(room.Id, FeedSourceType.RoomMention), feed.Resolutions);
    }

    /// <summary>The Human's dismiss resolves the Mention reason only; the Question stays unresolved.</summary>
    [Fact]
    public async Task Dismiss_ResolvesMentionOnly()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RecordingFeedPublisher feed = new();
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct, feed);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync("Pricing", [nova], ct);
        _ = await fixture.Service.PostAsync(room.Id, nova.Id, "@You?", ct: ct);
        _ = fixture.Questions.TryPut(NeedsYouFixture.Card(room.Id, nova));

        fixture.Tracker.Dismiss(room.Id);

        FeedResolution resolution = Assert.Single(feed.Resolutions);
        Assert.Equal(new FeedResolution(room.Id, FeedSourceType.RoomMention), resolution);
        Assert.Contains(feed.Posts, post => post.SourceType == FeedSourceType.RoomQuestion);
    }

    /// <summary>A Question card posts room-question and the Human's answer resolves it.</summary>
    [Fact]
    public async Task Question_PostsAndResolves()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RecordingFeedPublisher feed = new();
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct, feed);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync("Fitness", [coach], ct);

        _ = fixture.Questions.TryPut(NeedsYouFixture.Card(room.Id, coach));
        _ = await fixture.Service.PostAsync(room.Id, KnownIds.Human, "Strength", ct: ct);

        Assert.Equal(FeedSourceType.RoomQuestion, Assert.Single(feed.Posts).SourceType);
        Assert.Equal(new FeedResolution(room.Id, FeedSourceType.RoomQuestion), Assert.Single(feed.Resolutions));
    }

    /// <summary>Forms post when the first begins and resolve only when the last is taken.</summary>
    [Fact]
    public async Task Forms_PostOnceAndResolveWhenTheLastIsTaken()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RecordingFeedPublisher feed = new();
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct, feed);
        User claude = await fixture.AddAgentAsync("Claude", ct);
        Room room = await fixture.CreateRoomAsync("Docs", [claude], ct);
        App.Elicitation.PendingElicitation first = fixture.AddForm(room.Id, claude);
        App.Elicitation.PendingElicitation second = fixture.AddForm(room.Id, claude);

        _ = fixture.Forms.TryTake(room.Id, first.Id);
        Assert.Empty(feed.Resolutions);
        _ = fixture.Forms.TryTake(room.Id, second.Id);

        Assert.Equal(FeedSourceType.RoomForm, Assert.Single(feed.Posts).SourceType);
        Assert.Equal(new FeedResolution(room.Id, FeedSourceType.RoomForm), Assert.Single(feed.Resolutions));
    }

    /// <summary>A spent Budget posts room-budget as Huddle, and the Human's post resolves it.</summary>
    [Fact]
    public async Task Budget_PostsAsHuddleAndResolves()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RecordingFeedPublisher feed = new();
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct, feed);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync("Research", [nova], ct);

        for (int i = 0; i < 3; i++)
        {
            _ = await fixture.Service.PostAsync(room.Id, nova.Id, "working " + i, ct: ct);
        }

        FeedMessage post = Assert.Single(feed.Posts);
        Assert.Equal(FeedSourceType.RoomBudget, post.SourceType);
        Assert.Equal(FeedActor.Huddle, post.Actor);

        _ = await fixture.Service.PostAsync(room.Id, KnownIds.Human, "carry on", ct: ct);

        Assert.Contains(new FeedResolution(room.Id, FeedSourceType.RoomBudget), feed.Resolutions);
    }

    /// <summary>Deleting a Room drops its tracker entry, which resolves all four Room reasons for it.</summary>
    [Fact]
    public async Task DeletedRoom_ResolvesAllFourReasons()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RecordingFeedPublisher feed = new();
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct, feed);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync("Pricing", [nova], ct);
        _ = await fixture.Service.PostAsync(room.Id, nova.Id, "@You?", ct: ct);

        await fixture.Service.DeleteRoomAsync(room.Id, ct);
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (feed.Resolutions.Count(resolution => resolution.TargetKey == room.Id && resolution.Type != FeedSourceType.RoomMention) < 3 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10, ct);
        }

        Assert.Equal(
            [FeedSourceType.RoomMention, FeedSourceType.RoomQuestion, FeedSourceType.RoomForm, FeedSourceType.RoomBudget],
            feed.Resolutions.Where(resolution => resolution.TargetKey == room.Id).Select(resolution => resolution.Type).Distinct());
    }

    /// <summary>The startup seed of an old Mention posts nothing: it was posted when it happened.</summary>
    [Fact]
    public async Task Seed_PostsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RecordingFeedPublisher feed = new();
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct, feed);

        fixture.Tracker.Seed("room-1", DateTimeOffset.UnixEpoch, "Nova");

        Assert.Empty(feed.Posts);
        Assert.Empty(feed.Resolutions);
    }
}

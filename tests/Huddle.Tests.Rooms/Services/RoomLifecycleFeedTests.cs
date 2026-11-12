using Agency.Huddle.App.Data;
using Agency.Huddle.App.Feed;
using Agency.Huddle.Tests.Feed;
using Agency.Huddle.Tests.NeedsYou;

namespace Agency.Huddle.Tests.Services;

/// <summary>What <see cref="Agency.Huddle.App.Services.ChatService"/> tells the Feed about a Room's life (Feed spec §6.5.3).</summary>
public sealed class RoomLifecycleFeedTests
{
    /// <summary>Creating the Human's direct Room posts room-created once; asking for it again finds it and posts nothing.</summary>
    [Fact]
    public async Task EnsureRoomFor_PostsOnlyWhenItCreatesTheRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RecordingFeedPublisher feed = new();
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        fixture.Service.FeedPublisher = feed;
        User nova = await fixture.AddAgentAsync("Nova", ct);

        Room first = await fixture.Service.EnsureRoomForAsync(nova, ct);
        _ = await fixture.Service.EnsureRoomForAsync(nova, ct);

        FeedMessage post = Assert.Single(feed.Posts);
        Assert.Equal(FeedSourceType.RoomCreated, post.SourceType);
        Assert.False(post.Notify);
        Assert.Equal(first.Id, post.TargetKey);
        Assert.Equal(FeedActorKind.Human, post.Actor.Kind);
        RoomCreatedPayload payload = Assert.IsType<RoomCreatedPayload>(post.Payload);
        Assert.Equal(["You", "Nova"], payload.Members);
    }

    /// <summary>
    /// A one-to-one Huddle creates on its own (an Agent connecting for the first time, onboarding) posts nothing: it is housekeeping,
    /// and a fresh install would otherwise open with one item per Agent. The same Room created by the Human is still posted.
    /// </summary>
    [Fact]
    public async Task EnsureRoomFor_ByHuddle_PostsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RecordingFeedPublisher feed = new();
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        fixture.Service.FeedPublisher = feed;
        User nova = await fixture.AddAgentAsync("Nova", ct);
        User kai = await fixture.AddAgentAsync("Kai", ct);

        _ = await fixture.Service.EnsureRoomForAsync(nova, FeedActor.Huddle, ct);
        _ = await fixture.Service.EnsureRoomForAsync(kai, ct);

        FeedMessage post = Assert.Single(feed.Posts);
        Assert.Equal(["You", "Kai"], Assert.IsType<RoomCreatedPayload>(post.Payload).Members);
    }

    /// <summary>A Room an Agent opens is attributed to that Agent.</summary>
    [Fact]
    public async Task CreateRoomFor_ByAgent_NamesTheAgentAsActor()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RecordingFeedPublisher feed = new();
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        fixture.Service.FeedPublisher = feed;
        User nova = await fixture.AddAgentAsync("Nova", ct);
        User kai = await fixture.AddAgentAsync("Kai", ct);

        Room room = await fixture.Service.CreateRoomForAsync([nova.Id, kai.Id], null, null, new FeedActor(FeedActorKind.Agent, "Nova"), ct);

        FeedMessage post = Assert.Single(feed.Posts);
        Assert.Equal(room.Id, post.TargetKey);
        Assert.Equal(new FeedActor(FeedActorKind.Agent, "Nova"), post.Actor);
        Assert.Equal(["You", "Nova", "Kai"], Assert.IsType<RoomCreatedPayload>(post.Payload).Members);
    }

    /// <summary>Adding an Agent to a Room posts room-joined with the new Member's Name and the given actor.</summary>
    [Fact]
    public async Task Invite_PostsRoomJoinedWithTheNewMember()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RecordingFeedPublisher feed = new();
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        User kai = await fixture.AddAgentAsync("Kai", ct);
        Room room = await fixture.CreateRoomAsync("Plan", [nova], ct);
        fixture.Service.FeedPublisher = feed;

        _ = await fixture.Service.InviteAsync(room.Id, "Kai", new FeedActor(FeedActorKind.Agent, "Nova"), ct);

        FeedMessage post = Assert.Single(feed.Posts);
        Assert.Equal(FeedSourceType.RoomJoined, post.SourceType);
        Assert.Equal(room.Id, post.TargetKey);
        Assert.Equal("Nova", post.Actor.Name);
        Assert.Equal(kai.Name, Assert.IsType<RoomJoinedPayload>(post.Payload).Member);
    }

    /// <summary>Archiving posts room-archived; unarchiving posts nothing.</summary>
    [Fact]
    public async Task Archive_PostsOnArchiveButNotOnUnarchive()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RecordingFeedPublisher feed = new();
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync("Plan", [nova], ct);
        fixture.Service.FeedPublisher = feed;

        _ = await fixture.Service.SetRoomArchivedAsync(room.Id, true, ct);
        _ = await fixture.Service.SetRoomArchivedAsync(room.Id, false, ct);

        FeedMessage post = Assert.Single(feed.Posts);
        Assert.Equal(FeedSourceType.RoomArchived, post.SourceType);
        Assert.Equal("Plan", Assert.IsType<RoomArchivedPayload>(post.Payload).RoomName);
        Assert.Equal(FeedActorKind.Human, post.Actor.Kind);
    }
}

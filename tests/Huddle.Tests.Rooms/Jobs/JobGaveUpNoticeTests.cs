using Agency.Huddle.App.Data;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Jobs;
using Agency.Huddle.App.Services;
using Agency.Huddle.Tests.Feed;
using Agency.Huddle.Tests.NeedsYou;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Jobs;

/// <summary>Pins the trace <see cref="JobGaveUpNotice"/> leaves when a one-off is removed without posting.</summary>
public sealed class JobGaveUpNoticeTests
{
    /// <summary>An Agent's one-off leaves an Activity item and one Huddle line, with no Mention, in its Job Room.</summary>
    [Fact]
    public async Task AgentJob_WritesActivityAndALineInItsJobRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RecordingFeedPublisher feed = new();
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct, feed);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        JobRoomStore rooms = new(fixture.Directory.Options());
        Room room = await fixture.Team.CreateRoomAsync("⏰ Digest", [KnownIds.Human, nova.Id], ct);
        rooms.Set("job-1", room.Id);
        JobGaveUpNotice notice = new(feed, rooms, fixture.Team, fixture.Service, NullLogger<JobGaveUpNotice>.Instance);

        await notice.NoteAsync(Draft(JobOwner.Agent("Nova")), "its Room is gone", ct);

        FeedMessage item = Assert.Single(feed.Posts);
        Assert.Equal((FeedSourceType.JobGaveUp, false, FeedActorKind.Agent), (item.SourceType, item.Notify, item.Actor.Kind));
        Assert.Equal(new JobGaveUpPayload("job-1", "Digest", "its Room is gone", room.Id), item.Payload);
        Assert.Equal("⏰ Nova's Job \"Digest\" did not run: its Room is gone.", Assert.Single(await fixture.Transcript.ReadAllAsync(room.Id, ct)).Text);
    }

    /// <summary>The Huddle line Mentions nobody, so it wakes no Agent and spends no Budget.</summary>
    [Fact]
    public async Task AgentJob_Line_MentionsNobodyAndSpendsNoBudget()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RecordingFeedPublisher feed = new();
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct, feed);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        JobRoomStore rooms = new(fixture.Directory.Options());
        Room room = await fixture.Team.CreateRoomAsync("⏰ Digest", [KnownIds.Human, nova.Id], ct);
        rooms.Set("job-1", room.Id);
        JobGaveUpNotice notice = new(feed, rooms, fixture.Team, fixture.Service, NullLogger<JobGaveUpNotice>.Instance);
        List<MessagePostedEvent> published = [];
        fixture.Events.MessagePosted += published.Add;

        await notice.NoteAsync(Draft(JobOwner.Agent("Nova")), "its Room is gone", ct);

        MessagePostedEvent only = Assert.Single(published);
        Assert.Empty(only.Mentions);
        Assert.Equal(0, only.Budget.Used);
    }

    /// <summary>The Human's one-off leaves a Notification and no Huddle line.</summary>
    [Fact]
    public async Task HumanJob_WritesANotificationAndNoLine()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RecordingFeedPublisher feed = new();
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct, feed);
        JobRoomStore rooms = new(fixture.Directory.Options());
        JobGaveUpNotice notice = new(feed, rooms, fixture.Team, fixture.Service, NullLogger<JobGaveUpNotice>.Instance);

        await notice.NoteAsync(Draft(JobOwner.Human), "it failed three times", ct);

        FeedMessage item = Assert.Single(feed.Posts);
        Assert.Equal((FeedSourceType.JobGaveUp, true, FeedActorKind.Huddle), (item.SourceType, item.Notify, item.Actor.Kind));
        Assert.Empty(await fixture.Team.GetRoomsAsync(ct));
    }

    /// <summary>With the Job Room gone, an Agent's line goes into its one-to-one with the Human.</summary>
    [Fact]
    public async Task AgentJob_RoomGone_LineGoesToTheOneToOne()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RecordingFeedPublisher feed = new();
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct, feed);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        JobRoomStore rooms = new(fixture.Directory.Options());
        Room oneToOne = await fixture.Team.CreateRoomAsync("Nova", [KnownIds.Human, nova.Id], ct);
        JobGaveUpNotice notice = new(feed, rooms, fixture.Team, fixture.Service, NullLogger<JobGaveUpNotice>.Instance);

        await notice.NoteAsync(Draft(JobOwner.Agent("Nova")), "its Room is gone", ct);

        Assert.Equal("⏰ Nova's Job \"Digest\" did not run: its Room is gone.", Assert.Single(await fixture.Transcript.ReadAllAsync(oneToOne.Id, ct)).Text);
    }

    /// <summary>A Job Room that still exists but no longer has the Agent as a Member gets no line: it goes to the Agent's one-to-one (spec 7.5).</summary>
    [Fact]
    public async Task AgentJob_AgentLeftTheJobRoom_LineGoesToTheOneToOne()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RecordingFeedPublisher feed = new();
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct, feed);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        JobRoomStore rooms = new(fixture.Directory.Options());
        Room jobRoom = await fixture.Team.CreateRoomAsync("⏰ Digest", [KnownIds.Human], ct);
        rooms.Set("job-1", jobRoom.Id);
        Room oneToOne = await fixture.Team.CreateRoomAsync("Nova", [KnownIds.Human, nova.Id], ct);
        JobGaveUpNotice notice = new(feed, rooms, fixture.Team, fixture.Service, NullLogger<JobGaveUpNotice>.Instance);

        await notice.NoteAsync(Draft(JobOwner.Agent("Nova")), "it failed three times", ct);

        Assert.Empty(await fixture.Transcript.ReadAllAsync(jobRoom.Id, ct));
        Assert.Equal("⏰ Nova's Job \"Digest\" did not run: it failed three times.", Assert.Single(await fixture.Transcript.ReadAllAsync(oneToOne.Id, ct)).Text);
    }

    private static Job Draft(JobOwner owner) =>
        new("job-1", owner, "Digest", "Summarise yesterday.", JobRecurrence.Daily(new TimeOnly(9, 0)), "UTC", "You", false);
}

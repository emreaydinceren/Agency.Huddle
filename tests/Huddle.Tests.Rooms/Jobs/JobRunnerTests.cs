using Agency.Huddle.App.Data;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Jobs;
using Agency.Huddle.App.NeedsYou;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Feed;
using Agency.Huddle.Tests.NeedsYou;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Jobs;

/// <summary>Pins where <see cref="JobRunner"/> posts a Job and what it tells the Human.</summary>
public sealed class JobRunnerTests
{
    private static readonly DateTimeOffset RanAt = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    /// <summary>An Agent's Job gets its own Room with the Human and the Agent, posted into as Huddle with a Mention; a second run reuses it.</summary>
    [Fact]
    public async Task AgentJob_PostsIntoItsOwnRoomAndReusesIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        (JobRunner runner, JobRoomStore rooms) = Build(fixture);
        Job job = Draft(JobOwner.Agent("Nova"));

        Assert.Equal(JobOutcome.Delivered, await runner.RunAsync(job, RanAt, ct));
        Assert.Equal(JobOutcome.Delivered, await runner.RunAsync(job, RanAt, ct));

        string roomId = rooms.RoomOf(job.Id) ?? throw new InvalidOperationException("No Job Room.");
        Room room = Assert.Single(await fixture.Team.GetRoomsAsync(ct));
        Assert.Equal((roomId, "⏰ Digest"), (room.Id, room.Name));
        Assert.Equal(
            new[] { KnownIds.Human, nova.Id }.Order(StringComparer.Ordinal),
            (await fixture.Team.GetRoomMembersAsync(roomId, ct)).Select(user => user.Id).Order(StringComparer.Ordinal));
        IReadOnlyList<ChatMessage> messages = await fixture.Transcript.ReadAllAsync(roomId, ct);
        Assert.Equal(2, messages.Count);
        Assert.Equal((KnownIds.Huddle, "@Nova ⏰ Digest\n\nSummarise yesterday."), (messages[0].SenderId, messages[0].Text));
        Assert.Null(await fixture.Team.FindRoomWithExactMembersAsync(KnownIds.Human, nova.Id, ct));
    }

    /// <summary>The Human's Job gets a Room of their own, goes on Needs You and raises one Notification from Huddle.</summary>
    [Fact]
    public async Task HumanJob_GoesOnNeedsYouAndNotifies()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RecordingFeedPublisher feed = new();
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct, feed);
        (JobRunner runner, JobRoomStore rooms) = Build(fixture);
        Job job = Draft(JobOwner.Human);

        Assert.Equal(JobOutcome.Delivered, await runner.RunAsync(job, RanAt, ct));

        string roomId = rooms.RoomOf(job.Id) ?? throw new InvalidOperationException("No Job Room.");
        Assert.Equal([KnownIds.Human], (await fixture.Team.GetRoomMembersAsync(roomId, ct)).Select(user => user.Id));
        Assert.Equal("⏰ Digest\n\nSummarise yesterday.", Assert.Single(await fixture.Transcript.ReadAllAsync(roomId, ct)).Text);
        Assert.Equal(NeedsYouReason.Job, fixture.Tracker.ReasonsHeld(roomId));
        FeedMessage notification = Assert.Single(feed.Posts);
        Assert.Equal((FeedSourceType.RoomJob, roomId, FeedActor.Huddle, true), (notification.SourceType, notification.TargetKey, notification.Actor, notification.Notify));
    }

    /// <summary>The Human posting in the Job Room answers it: the Room leaves Needs You.</summary>
    [Fact]
    public async Task HumanJob_HumanReply_ClearsNeedsYou()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        (JobRunner runner, JobRoomStore rooms) = Build(fixture);
        Job job = Draft(JobOwner.Human);
        _ = await runner.RunAsync(job, RanAt, ct);
        string roomId = rooms.RoomOf(job.Id) ?? throw new InvalidOperationException("No Job Room.");

        _ = await fixture.Service.PostAsync(roomId, KnownIds.Human, "Done.", ct: ct);

        Assert.Equal(NeedsYouReason.None, fixture.Tracker.ReasonsHeld(roomId));
    }

    /// <summary>A Job for an Agent that has never come online does nothing and says so.</summary>
    [Fact]
    public async Task UnknownAgent_RunsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        (JobRunner runner, _) = Build(fixture);

        Assert.Equal(JobOutcome.NoAgent, await runner.RunAsync(Draft(JobOwner.Agent("Ghost")), RanAt, ct));

        Assert.Empty(await fixture.Team.GetRoomsAsync(ct));
    }

    /// <summary>Into a Job Room whose Budget is spent nothing is posted, because the Agent could not reply.</summary>
    [Fact]
    public async Task BudgetSpent_PostsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        (JobRunner runner, JobRoomStore rooms) = Build(fixture);
        Job job = Draft(JobOwner.Agent("Nova"));
        _ = await runner.RunAsync(job, RanAt, ct);
        string roomId = rooms.RoomOf(job.Id) ?? throw new InvalidOperationException("No Job Room.");
        for (int i = 0; i < 3; i++)
        {
            _ = await fixture.Service.PostAsync(roomId, nova.Id, "reply", ct: ct);
        }

        Assert.Equal(JobOutcome.BudgetSpent, await runner.RunAsync(job, RanAt, ct));

        Assert.Equal(4, (await fixture.Transcript.ReadAllAsync(roomId, ct)).Count);
    }

    /// <summary>An archived Job Room is brought back; a deleted one is replaced by a new Room.</summary>
    [Fact]
    public async Task ArchivedRoomComesBack_DeletedRoomIsReplaced()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        _ = await fixture.AddAgentAsync("Nova", ct);
        (JobRunner runner, JobRoomStore rooms) = Build(fixture);
        Job job = Draft(JobOwner.Agent("Nova"));
        _ = await runner.RunAsync(job, RanAt, ct);
        string first = rooms.RoomOf(job.Id) ?? throw new InvalidOperationException("No Job Room.");

        await fixture.Team.SetRoomArchivedAsync(first, true, ct);
        _ = await runner.RunAsync(job, RanAt, ct);
        Assert.False((await fixture.Team.GetRoomAsync(first, ct))?.Archived);

        await fixture.Team.DeleteRoomAsync(first, ct);
        _ = await runner.RunAsync(job, RanAt, ct);
        Assert.NotEqual(first, rooms.RoomOf(job.Id));
    }

    private static (JobRunner Runner, JobRoomStore Rooms) Build(NeedsYouFixture fixture)
    {
        JobRoomStore rooms = new(fixture.Directory.Options());
        JobRunner runner = new(fixture.Team, rooms, fixture.Service, fixture.Events, fixture.Tracker, NullLogger<JobRunner>.Instance);
        return (runner, rooms);
    }

    private static Job Draft(JobOwner owner) =>
        new("job-1", owner, "Digest", "Summarise yesterday.", JobRecurrence.Daily(new TimeOnly(9, 0)), "UTC", "You", false);
}

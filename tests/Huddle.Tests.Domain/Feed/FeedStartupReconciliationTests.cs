using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.NeedsYou;
using Agency.Huddle.App.Profile;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.NeedsYou;

namespace Agency.Huddle.Tests.Feed;

/// <summary>Each producer resolves, at startup, the Notifications whose condition ended while Huddle was stopped (Feed spec §6.10).</summary>
public sealed class FeedStartupReconciliationTests
{
    /// <summary>A Room Notification stays unresolved while the tracker holds that reason, and is resolved when it does not.</summary>
    [Fact]
    public async Task Rooms_ResolvesOnlyReasonsTheTrackerNoLongerHolds()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using FeedHarness harness = new();
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        await harness.Directory.InitializeAsync("You", ct);
        await harness.Writer.StartAsync(ct);
        harness.Queue.Post(FeedTestSupport.RoomMention("room-held"));
        harness.Queue.Post(FeedTestSupport.RoomMention("room-gone"));
        harness.Queue.Post(new FeedMessage(FeedSourceType.RoomQuestion, "room-held", FeedTestSupport.Agent(), true, new RoomWaitPayload("room-held", null, null)));
        await harness.DrainAsync(ct);
        await harness.Writer.StopAsync(ct);
        fixture.Tracker.Seed("room-held", DateTimeOffset.UnixEpoch, "Nova");
        RecordingFeedPublisher recorder = new();
        NeedsYouStartupScan scan = new(fixture.Tracker, fixture.Team, fixture.Transcript, new FakeMentionAliasSource(), NullLogger<NeedsYouStartupScan>.Instance, harness.Store, recorder);

        scan.ReconcileFeed();

        Assert.Contains(new FeedResolution("room-gone", FeedSourceType.RoomMention), recorder.Resolutions);
        Assert.Contains(new FeedResolution("room-held", FeedSourceType.RoomQuestion), recorder.Resolutions);
        Assert.DoesNotContain(new FeedResolution("room-held", FeedSourceType.RoomMention), recorder.Resolutions);
        Assert.Empty(recorder.Posts);
    }

    /// <summary>A Task Notification is resolved when its Task is gone or no longer meets its condition, and kept when it still does.</summary>
    [Fact]
    public async Task Tasks_ResolvesGoneAndChangedTasksOnly()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using FeedHarness harness = new();
        using TaskToolHarness tasks = new();
        string human = tasks.Options.Value.HumanName;
        TaskActor actor = TaskActors.Human(tasks.Options.Value);
        TaskItem held = Assert.IsType<TaskResult.Saved>(tasks.Service.Create(new TaskDraft("Held", "Platform", null, Assignee: human), actor)).Task;
        TaskItem moved = Assert.IsType<TaskResult.Saved>(tasks.Service.Create(new TaskDraft("Moved", "Platform", null), actor)).Task;
        await harness.Writer.StartAsync(ct);
        harness.Queue.Post(Assigned(held.Id.ToString()));
        harness.Queue.Post(Assigned(moved.Id.ToString()));
        harness.Queue.Post(Assigned("PLAT-9999"));
        await harness.DrainAsync(ct);
        await harness.Writer.StopAsync(ct);
        RecordingFeedPublisher recorder = new();
        TaskFeedStartup startup = new(harness.Store, tasks.Store, recorder, new NoConfirmers(), new ConfiguredHumanIdentity(human), NullLogger<TaskFeedStartup>.Instance);

        startup.Reconcile();

        Assert.Contains(new FeedResolution(moved.Id.ToString(), FeedSourceType.TaskAssignedToYou), recorder.Resolutions);
        Assert.Contains(new FeedResolution("PLAT-9999", FeedSourceType.TaskAssignedToYou), recorder.Resolutions);
        Assert.DoesNotContain(new FeedResolution(held.Id.ToString(), FeedSourceType.TaskAssignedToYou), recorder.Resolutions);
        Assert.Empty(recorder.Posts);
    }

    private static FeedMessage Assigned(string id) =>
        new(FeedSourceType.TaskAssignedToYou, id, FeedTestSupport.Agent(), true, new TaskAssignedPayload(id, "T", "Platform", null));

    /// <summary>A directory in which no Team has a member who can confirm a Request.</summary>
    private sealed class NoConfirmers : IRequestConfirmers
    {
        /// <inheritdoc />
        public bool IsVerifierOf(string team, string member) => false;

        /// <inheritdoc />
        public bool TeamHasConfirmer(string team) => false;
    }
}

using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Jobs;
using Agency.Huddle.App.Services;
using Agency.Huddle.Tests.Acp;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.Feed;
using Agency.Huddle.Tests.NeedsYou;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Jobs;

/// <summary>Pins how <see cref="JobScheduler"/> runs, holds and removes a Follow-up through the real runner and trace (Turn Queue spec §7.4, §7.5).</summary>
public sealed class FollowUpSchedulerTests : IDisposable
{
    private static readonly DateTimeOffset Monday0800 = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

    private readonly NeedsYouFixture fixture;
    private readonly RecordingFeedPublisher feed = new();
    private readonly PersonaStore personas;
    private readonly JobStore store;
    private readonly ManualTimeProvider clock = new(Monday0800, TimeZoneInfo.Utc);
    private readonly HashSet<string> livePersonas = new(StringComparer.Ordinal) { "Nova" };
    private readonly JobScheduler scheduler;
    private readonly User nova;
    private readonly Room room;

    /// <summary>Opens the stores, an Agent Nova with a Persona, and a Room she is in.</summary>
    public FollowUpSchedulerTests()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.fixture = NeedsYouFixture.CreateAsync(ct, this.feed).GetAwaiter().GetResult();
        IOptions<TeamOptions> options = this.fixture.Directory.Options();
        TestPersonaFiles.Write(new AgentPaths(options), "Nova", PersonaSupervisorTestSupport.PersonaText("Nova", "You are Nova."));
        this.personas = options.CreatePersonaStore();
        this.store = new JobStore(this.personas, options, NullLogger<JobStore>.Instance);
        PersonaExists exists = name => this.livePersonas.Contains(name);
        JobRoomStore rooms = new(options);
        FollowUpDestination followUp = new(this.fixture.Team, this.fixture.Service, exists);
        JobRoomDestination jobRoom = new(this.fixture.Team, rooms, this.fixture.Service, this.fixture.Events);
        JobRunner runner = new(this.fixture.Team, rooms, this.fixture.Service, this.fixture.Events, this.fixture.Tracker, NullLogger<JobRunner>.Instance, [followUp, jobRoom]);
        JobGaveUpNotice notice = new(this.feed, rooms, this.fixture.Team, this.fixture.Service, NullLogger<JobGaveUpNotice>.Instance, exists);
        this.scheduler = new JobScheduler(this.store, new JobStateStore(options), runner, options, NullLogger<JobScheduler>.Instance, notice, this.clock);
        this.nova = this.fixture.AddAgentAsync("Nova", ct).GetAwaiter().GetResult();
        this.room = this.fixture.CreateRoomAsync("Talk", [this.nova], ct).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.scheduler.Dispose();
        this.store.Dispose();
        this.personas.Dispose();
        this.fixture.Dispose();
    }

    /// <summary>A Follow-up due in a Room that is not paused posts and is removed, with no trace.</summary>
    [Fact]
    public async Task Due_RoomOpen_PostsAndIsRemoved()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Job job = this.AddFollowUp();

        this.clock.UtcNow = Monday0800.AddHours(1).AddSeconds(30);
        await this.scheduler.TickAsync(ct);

        Assert.Equal("@Nova ⏰ Follow-up: Check build\nLook at it.", Assert.Single(await this.fixture.Transcript.ReadAllAsync(this.room.Id, ct)).Text);
        Assert.Null(this.store.Find(job.Id));
        Assert.Empty(this.feed.Posts);
    }

    /// <summary>In a paused Room it is held with its due time and posts on the first tick after the Human speaks.</summary>
    [Fact]
    public async Task Due_RoomPaused_IsHeldThenPostsOnTheTickAfterTheHumanSpeaks()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await this.PauseAsync(ct);
        Job job = this.AddFollowUp();

        this.clock.UtcNow = Monday0800.AddHours(1).AddSeconds(30);
        await this.scheduler.TickAsync(ct);

        Assert.Equal(3, (await this.fixture.Transcript.ReadAllAsync(this.room.Id, ct)).Count);
        Assert.NotNull(this.store.Find(job.Id));
        Assert.Equal(JobOutcome.Held, this.scheduler.StateOf(job)?.LastOutcome);

        _ = await this.fixture.Service.PostAsync(this.room.Id, KnownIds.Human, "Go on.", ct: ct);
        this.clock.UtcNow = Monday0800.AddHours(1).AddSeconds(90);
        await this.scheduler.TickAsync(ct);

        Assert.Equal("@Nova ⏰ Follow-up: Check build\nLook at it.", (await this.fixture.Transcript.ReadAllAsync(this.room.Id, ct))[^1].Text);
        Assert.Null(this.store.Find(job.Id));
    }

    /// <summary>Held for 25 hours it gives up: removed, an Activity item, and the Huddle line in its Room.</summary>
    [Fact]
    public async Task Held25Hours_GivesUpWithAnActivityItemAndALineInItsRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await this.PauseAsync(ct);
        Job job = this.AddFollowUp();
        this.clock.UtcNow = Monday0800.AddHours(1).AddSeconds(30);
        await this.scheduler.TickAsync(ct);

        this.clock.UtcNow = Monday0800.AddHours(26);
        await this.scheduler.TickAsync(ct);

        Assert.Null(this.store.Find(job.Id));
        FeedMessage item = Assert.Single(this.feed.Posts, post => post.SourceType == FeedSourceType.JobGaveUp);
        Assert.False(item.Notify);
        Assert.Equal(
            "⏰ Nova's Follow-up \"Check build\" did not run: it was more than 24 hours late.",
            (await this.fixture.Transcript.ReadAllAsync(this.room.Id, ct))[^1].Text);
    }

    /// <summary>A Room that was deleted removes the Follow-up, with the line in the Agent's one-to-one.</summary>
    [Fact]
    public async Task RoomDeleted_RemovesItWithALineInTheOneToOne()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Room direct = await this.fixture.CreateRoomAsync("Nova", [this.nova], ct);
        Job job = this.AddFollowUp();
        await this.fixture.Team.DeleteRoomAsync(this.room.Id, ct);

        this.clock.UtcNow = Monday0800.AddHours(1).AddSeconds(30);
        await this.scheduler.TickAsync(ct);

        Assert.Null(this.store.Find(job.Id));
        Assert.Single(this.feed.Posts);
        Assert.Equal(
            "⏰ Nova's Follow-up \"Check build\" did not run: its Room is gone.",
            Assert.Single(await this.fixture.Transcript.ReadAllAsync(direct.Id, ct)).Text);
    }

    /// <summary>A removed Persona removes the Follow-up with a Feed item and no line anywhere.</summary>
    [Fact]
    public async Task PersonaRemoved_RemovesItWithNoLine()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Room direct = await this.fixture.CreateRoomAsync("Nova", [this.nova], ct);
        Job job = this.AddFollowUp();
        _ = this.livePersonas.Remove("Nova");

        this.clock.UtcNow = Monday0800.AddHours(1).AddSeconds(30);
        await this.scheduler.TickAsync(ct);

        Assert.Null(this.store.Find(job.Id));
        Assert.Single(this.feed.Posts);
        Assert.Empty(await this.fixture.Transcript.ReadAllAsync(this.room.Id, ct));
        Assert.Empty(await this.fixture.Transcript.ReadAllAsync(direct.Id, ct));
    }

    /// <summary>A Follow-up whose Room has Nova but is not her one-to-one gives up with the line in that Room, not in the one-to-one.</summary>
    [Fact]
    public async Task GivesUp_InAGroupRoom_PostsTheLineInTheGroupRoomNotTheOneToOne()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        User rex = await this.fixture.AddAgentAsync("Rex", ct);
        Room group = await this.fixture.CreateRoomAsync("Group", [this.nova, rex], ct);
        await this.PauseAsync(ct, group);
        Job job = this.AddFollowUp(group);
        this.clock.UtcNow = Monday0800.AddHours(1).AddSeconds(30);
        await this.scheduler.TickAsync(ct);

        this.clock.UtcNow = Monday0800.AddHours(26);
        await this.scheduler.TickAsync(ct);

        Assert.Null(this.store.Find(job.Id));
        Assert.Equal(
            "⏰ Nova's Follow-up \"Check build\" did not run: it was more than 24 hours late.",
            (await this.fixture.Transcript.ReadAllAsync(group.Id, ct))[^1].Text);
        Assert.Empty(await this.fixture.Transcript.ReadAllAsync(this.room.Id, ct));
    }

    /// <summary>When Nova is no longer a Member of the Follow-up's Room it is removed and the line goes to her one-to-one.</summary>
    [Fact]
    public async Task AgentNotInTheRoom_RemovesItWithTheLineInTheOneToOne()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        User rex = await this.fixture.AddAgentAsync("Rex", ct);
        Room others = await this.fixture.CreateRoomAsync("Others", [rex], ct);
        Job job = this.AddFollowUp(others);

        this.clock.UtcNow = Monday0800.AddHours(1).AddSeconds(30);
        await this.scheduler.TickAsync(ct);

        Assert.Null(this.store.Find(job.Id));
        Assert.Empty(await this.fixture.Transcript.ReadAllAsync(others.Id, ct));
        Assert.Equal(
            "⏰ Nova's Follow-up \"Check build\" did not run: its Agent is no longer available.",
            Assert.Single(await this.fixture.Transcript.ReadAllAsync(this.room.Id, ct)).Text);
    }

    private Job AddFollowUp(Room? target = null)
    {
        Job job = this.store.Add(Job.FollowUp(string.Empty, "Nova", (target ?? this.room).Id, "Check build", "Look at it.", new DateOnly(2026, 11, 2), new TimeOnly(9, 0), "UTC"));
        this.scheduler.Seed(job);
        return job;
    }

    private async Task PauseAsync(CancellationToken ct, Room? target = null)
    {
        for (int i = 0; i < 3; i++)
        {
            _ = await this.fixture.Service.PostAsync((target ?? this.room).Id,this.nova.Id, "reply", ct: ct);
        }
    }
}

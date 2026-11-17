using Agency.Huddle.App.Data;
using Agency.Huddle.App.Jobs;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.NeedsYou;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Jobs;

/// <summary>Pins the Follow-up: a one-off Job with a Room, its file form, its rule and where its run posts (Turn Queue spec §7.1, §7.2, §7.4).</summary>
public sealed class FollowUpTests
{
    private static readonly DateOnly Day = new(2026, 11, 2);

    private static readonly TimeOnly Nine = new(9, 0);

    /// <summary>A Follow-up written to schedule.md and read back is the same Job, and the file holds its room line.</summary>
    [Fact]
    public void Document_RoundTripsTheRoom()
    {
        Job job = Job.FollowUp("job-1", "Nova", "r1", "Check build", "Look at it.", Day, Nine, "UTC");

        string text = JobDocument.Format(job);
        JobSection section = Assert.Single(JobDocument.Parse(text, JobOwner.Agent("Nova")));

        Assert.Equal(job, section.Job);
        Assert.Equal(
            "## Check build\n- id: job-1\n- when: once on 2026-11-02 at 09:00\n- timezone: UTC\n- by: Nova\n- room: r1\n- paused: no\n\nLook at it.",
            text);
    }

    /// <summary>An ordinary Job's section has no room line.</summary>
    [Fact]
    public void Document_OrdinaryJob_WritesNoRoomLine()
    {
        Job job = new("job-1", JobOwner.Agent("Nova"), "Digest", "Do it.", JobRecurrence.Daily(Nine), "UTC", null, false);

        Assert.DoesNotContain("room", JobDocument.Format(job), StringComparison.Ordinal);
    }

    /// <summary>A room line with a recurring time is a reported problem, not a crash, and says what to do.</summary>
    [Fact]
    public void Document_RoomWithRecurringWhen_IsAProblem()
    {
        string text = "## Check\n- id: job-1\n- when: daily at 09:00\n- room: r1\n\nDo it.";

        JobSection section = Assert.Single(JobDocument.Parse(text, JobOwner.Agent("Nova")));

        Assert.Null(section.Job);
        Assert.Equal("A Follow-up runs once; delete it and create a Job instead", section.Problem);
    }

    /// <summary>A room id that is not an id is a reported problem.</summary>
    [Fact]
    public void Document_InvalidRoomId_IsAProblem()
    {
        string text = "## Check\n- id: job-1\n- when: once on 2026-11-02 at 09:00\n- room: ../x\n\nDo it.";

        JobSection section = Assert.Single(JobDocument.Parse(text, JobOwner.Agent("Nova")));

        Assert.Null(section.Job);
        Assert.Equal("'../x' is not a Room id.", section.Problem);
    }

    /// <summary>The rule refuses a Room with a recurring time and allows a Follow-up and an ordinary Job.</summary>
    [Fact]
    public void Rules_RefuseOnlyARoomWithARecurringWhen()
    {
        Job followUp = Job.FollowUp("job-1", "Nova", "r1", "Check", "Do it.", Day, Nine);
        Job ordinary = new("job-2", JobOwner.Agent("Nova"), "Digest", "Do it.", JobRecurrence.Daily(Nine), null, null, false);

        Assert.Null(JobRules.Check(followUp));
        Assert.Null(JobRules.Check(ordinary));
        Assert.Equal(JobRules.FollowUpRunsOnce, JobRules.Check(followUp with { When = JobRecurrence.Daily(Nine) }));
        Assert.Equal(JobRules.FollowUpRunsOnce, JobRules.Check(followUp with { When = JobRecurrence.EveryHours(2) }));
    }

    /// <summary>The factory builds a one-off for the Agent, set by the Agent, in the Room.</summary>
    [Fact]
    public void FollowUp_Factory_BuildsAOneOffForTheAgent()
    {
        Job job = Job.FollowUp("job-1", "Nova", "r1", "Check", "Do it.", Day, Nine, "UTC");

        Assert.Equal((JobCadence.Once, "Nova", "Nova", "r1", false), (job.When.Cadence, job.Owner.AgentName, job.CreatedBy, job.RoomId, job.Paused));
    }

    /// <summary>The Follow-up serves only a Job with a Room, and the Job Room destination only one without.</summary>
    [Fact]
    public async Task Serves_SplitsOnTheRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        Job followUp = Job.FollowUp("job-1", "Nova", "r1", "Check", "Do it.", Day, Nine);
        Job ordinary = followUp with { RoomId = null };

        Assert.Equal((true, false), (Follow(fixture, _ => true).Serves(followUp), Follow(fixture, _ => true).Serves(ordinary)));
        Assert.Equal((false, true), (Rooms(fixture).Serves(followUp), Rooms(fixture).Serves(ordinary)));
    }

    /// <summary>A run posts into the set Room as Huddle, leaves an archived Room archived, writes no room_jobs row and reads exactly.</summary>
    [Fact]
    public async Task Run_PostsIntoTheSetRoom_LeavesItArchived_WritesNoRoomJobsRow()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync("Talk", [nova], ct);
        await fixture.Team.SetRoomArchivedAsync(room.Id, true, ct);
        JobRoomStore rooms = new(fixture.Directory.Options());
        JobRunner runner = Runner(fixture, rooms, _ => true);
        Job job = Job.FollowUp("job-1", "Nova", room.Id, "Check build", "Look at it.", Day, Nine);

        JobOutcome outcome = await runner.RunAsync(job, new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero), ct);

        Assert.Equal(JobOutcome.Delivered, outcome);
        ChatMessage posted = Assert.Single(await fixture.Transcript.ReadAllAsync(room.Id, ct));
        Assert.Equal((KnownIds.Huddle, "@Nova ⏰ Follow-up: Check build\nLook at it."), (posted.SenderId, posted.Text));
        Assert.True((await fixture.Team.GetRoomAsync(room.Id, ct))?.Archived);
        Assert.Null(rooms.RoomOf(job.Id));
        Assert.Single(await fixture.Team.GetRoomsAsync(ct));
    }

    /// <summary>The text on time and late reads exactly, and an Agent that was renamed is Mentioned by the Name it has now.</summary>
    [Fact]
    public async Task TextOf_OnTimeLateAndRenamedAgent_ReadsExactly()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        User renamed = await fixture.AddAgentAsync("Nia", ct);
        Room room = await fixture.CreateRoomAsync("Talk", [renamed], ct);
        FollowUpDestination destination = Follow(fixture, _ => true);
        Job job = Job.FollowUp("job-1", "Nia", room.Id, "Check build", "Look at it.", Day, Nine);

        Assert.Equal("@Nia ⏰ Follow-up: Check build\nLook at it.", destination.TextOf(job, renamed, null));
        Assert.Equal(
            "@Nia ⏰ Follow-up: Check build (ran late; due Mon 09:00)\nLook at it.",
            destination.TextOf(job, renamed, new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero)));
        Assert.Equal(JobOutcome.Delivered, await Runner(fixture, new JobRoomStore(fixture.Directory.Options()), _ => true).RunAsync(job, Day.ToDateTime(Nine), ct));
        Assert.Equal("@Nia ⏰ Follow-up: Check build\nLook at it.", Assert.Single(await fixture.Transcript.ReadAllAsync(room.Id, ct)).Text);
    }

    /// <summary>A Room that is gone is NoRoom: nothing is posted and no Room is made.</summary>
    [Fact]
    public async Task Run_RoomGone_IsNoRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync("Talk", [nova], ct);
        await fixture.Team.DeleteRoomAsync(room.Id, ct);
        Job job = Job.FollowUp("job-1", "Nova", room.Id, "Check", "Do it.", Day, Nine);

        JobTarget target = await Follow(fixture, _ => true).ResolveAsync(job, nova, ct);

        Assert.Equal((null, JobOutcome.NoRoom), (target.Room, target.Outcome));
        Assert.Empty(await fixture.Team.GetRoomsAsync(ct));
    }

    /// <summary>An Agent that never came online is NoAgent.</summary>
    [Fact]
    public async Task Run_AgentNeverOnline_IsNoAgent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        Room room = await fixture.CreateRoomAsync("Talk", [], ct);
        Job job = Job.FollowUp("job-1", "Ghost", room.Id, "Check", "Do it.", Day, Nine);

        JobOutcome outcome = await Runner(fixture, new JobRoomStore(fixture.Directory.Options()), _ => true).RunAsync(job, Day.ToDateTime(Nine), ct);

        Assert.Equal(JobOutcome.NoAgent, outcome);
        Assert.Empty(await fixture.Transcript.ReadAllAsync(room.Id, ct));
    }

    /// <summary>An Agent whose Persona was removed is NoAgent, though its Agent row and Membership remain.</summary>
    [Fact]
    public async Task Run_PersonaRemoved_IsNoAgent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync("Talk", [nova], ct);
        Job job = Job.FollowUp("job-1", "Nova", room.Id, "Check", "Do it.", Day, Nine);

        JobTarget target = await Follow(fixture, _ => false).ResolveAsync(job, nova, ct);

        Assert.Equal(JobOutcome.NoAgent, target.Outcome);
    }

    /// <summary>An Agent that left the Room is NoAgent.</summary>
    [Fact]
    public async Task Run_AgentNoLongerAMember_IsNoAgent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync("Talk", [], ct);
        Job job = Job.FollowUp("job-1", "Nova", room.Id, "Check", "Do it.", Day, Nine);

        JobTarget target = await Follow(fixture, _ => true).ResolveAsync(job, nova, ct);

        Assert.Equal(JobOutcome.NoAgent, target.Outcome);
    }

    /// <summary>A paused Room (Budget spent) is Held: nothing is posted.</summary>
    [Fact]
    public async Task Run_RoomPaused_IsHeldAndPostsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync("Talk", [nova], ct);
        for (int i = 0; i < 3; i++)
        {
            _ = await fixture.Service.PostAsync(room.Id, nova.Id, "reply", ct: ct);
        }

        Job job = Job.FollowUp("job-1", "Nova", room.Id, "Check", "Do it.", Day, Nine);
        JobOutcome outcome = await Runner(fixture, new JobRoomStore(fixture.Directory.Options()), _ => true).RunAsync(job, Day.ToDateTime(Nine), ct);

        Assert.Equal(JobOutcome.Held, outcome);
        Assert.Equal(3, (await fixture.Transcript.ReadAllAsync(room.Id, ct)).Count);
    }

    private static FollowUpDestination Follow(NeedsYouFixture fixture, Func<string, bool> persona) =>
        new(fixture.Team, fixture.Service, new PersonaExists(persona));

    private static JobRoomDestination Rooms(NeedsYouFixture fixture) =>
        new(fixture.Team, new JobRoomStore(fixture.Directory.Options()), fixture.Service, fixture.Events);

    private static JobRunner Runner(NeedsYouFixture fixture, JobRoomStore rooms, Func<string, bool> persona) =>
        new(fixture.Team, rooms, fixture.Service, fixture.Events, fixture.Tracker, NullLogger<JobRunner>.Instance, [Follow(fixture, persona), new JobRoomDestination(fixture.Team, rooms, fixture.Service, fixture.Events)]);
}

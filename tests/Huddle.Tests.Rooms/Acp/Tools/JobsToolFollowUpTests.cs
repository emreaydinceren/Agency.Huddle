using System.Text.Json.Nodes;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Jobs;
using Agency.Huddle.App.Profile;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.Feed;
using Agency.Huddle.Tests.NeedsYou;
using Agency.Huddle.Tests.Teams;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Acp.Tools;

/// <summary>Pins the <c>jobs</c> tool's <c>follow_up</c> action: a Room-bound one-off, set for the caller only, with its limits, cap and Feed item.</summary>
public sealed class JobsToolFollowUpTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

    /// <summary><c>in 30 minutes</c> creates a one-off bound to the Room, due at the absolute time, and the answer names the id, the time and the Room.</summary>
    [Fact]
    public async Task FollowUp_InThirtyMinutes_CreatesARoomBoundOneOff()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(Now, TimeZoneInfo.Utc, ct);

        string answer = await rig.Tool("Nova").InvokeAsync(Rig.Args(rig.Room.Id, "in 30 minutes"), ct);

        Job job = Assert.Single(rig.Store.Jobs);
        Assert.Equal((rig.Room.Id, JobOwner.Agent("Nova"), JobCadence.Once, "Nova"), (job.RoomId, job.Owner, job.When.Cadence, job.CreatedBy));
        Assert.Equal($"Set {job.Id} \"Check the build\": you will come back to Ops on Mon 2 Nov 2026 at 08:30.", answer);
    }

    /// <summary>A relative time is rounded up to the next whole minute: 10:00:20 plus 30 minutes is 10:31.</summary>
    [Fact]
    public async Task FollowUp_RoundsUpToTheNextWholeMinute()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(new DateTimeOffset(2026, 11, 2, 10, 0, 20, TimeSpan.Zero), TimeZoneInfo.Utc, ct);

        _ = await rig.Tool("Nova").InvokeAsync(Rig.Args(rig.Room.Id, "in 30 minutes"), ct);

        Assert.Equal(new DateTimeOffset(2026, 11, 2, 10, 31, 0, TimeSpan.Zero), rig.NextRunOfOnlyJob());
    }

    /// <summary>Across a spring-forward change <c>in 2 hours</c> is two real hours later, written as the wall time in the zone the Job uses.</summary>
    [Fact]
    public async Task FollowUp_InTwoHours_AcrossADaylightSavingChange_IsTheRightInstant()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TimeZoneInfo berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        using Rig rig = await Rig.CreateAsync(new DateTimeOffset(2026, 3, 29, 0, 30, 0, TimeSpan.Zero), berlin, ct);

        string answer = await rig.Tool("Nova").InvokeAsync(Rig.Args(rig.Room.Id, "in 2 hours"), ct);

        Assert.Equal(new DateTimeOffset(2026, 3, 29, 2, 30, 0, TimeSpan.Zero), rig.NextRunOfOnlyJob());
        Assert.Contains("Sun 29 Mar 2026 at 04:30.", answer, StringComparison.Ordinal); // contains-ok: the id in the sentence is random.
    }

    /// <summary>Jobs' own <c>once on …</c> grammar is accepted.</summary>
    [Fact]
    public async Task FollowUp_OnceOn_IsAccepted()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(Now, TimeZoneInfo.Utc, ct);

        _ = await rig.Tool("Nova").InvokeAsync(Rig.Args(rig.Room.Id, "once on 2026-11-03 at 09:00"), ct);

        Assert.Equal(new DateTimeOffset(2026, 11, 3, 9, 0, 0, TimeSpan.Zero), rig.NextRunOfOnlyJob());
    }

    /// <summary>The Follow-up's state row is written at once, so a one-off due inside the minute is not lost.</summary>
    [Fact]
    public async Task FollowUp_SeedsItsStateAtOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(Now, TimeZoneInfo.Utc, ct);

        _ = await rig.Tool("Nova").InvokeAsync(Rig.Args(rig.Room.Id, "in 5 minutes"), ct);

        Job job = Assert.Single(rig.Store.Jobs);
        Assert.Equal(new DateTimeOffset(2026, 11, 2, 8, 5, 0, TimeSpan.Zero), rig.Scheduler.StateOf(job)?.NextRunUtc);
    }

    /// <summary>A Follow-up leaves a <c>job-follow-up-set</c> Activity item naming the Agent, the Room and the time.</summary>
    [Fact]
    public async Task FollowUp_WritesAnActivityFeedItem()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(Now, TimeZoneInfo.Utc, ct);

        _ = await rig.Tool("Nova").InvokeAsync(Rig.Args(rig.Room.Id, "in 30 minutes"), ct);

        Job job = Assert.Single(rig.Store.Jobs);
        FeedMessage item = Assert.Single(rig.Feed.Posts);
        Assert.Equal((FeedSourceType.JobFollowUpSet, false, FeedActorKind.Agent), (item.SourceType, item.Notify, item.Actor.Kind));
        Assert.Equal(new JobFollowUpSetPayload(job.Id, "Nova", "Check the build", rig.Room.Id, "Ops", "Mon 2 Nov 2026 at 08:30"), item.Payload);
    }

    /// <summary>A <c>target</c> is refused: a Follow-up is for the caller only.</summary>
    [Fact]
    public async Task FollowUp_WithATarget_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(Now, TimeZoneInfo.Utc, ct);
        JsonObject arguments = Rig.Args(rig.Room.Id, "in 30 minutes");
        arguments["target"] = "Orion";

        string answer = await rig.Tool("Nova").InvokeAsync(arguments, ct);

        Assert.Equal("A Follow-up is always for you; 'target' is not accepted.", answer);
        Assert.Empty(rig.Store.Jobs);
    }

    /// <summary>A Room the caller is not a Member of is refused.</summary>
    [Fact]
    public async Task FollowUp_InARoomTheCallerIsNotIn_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(Now, TimeZoneInfo.Utc, ct);

        string answer = await rig.Tool("Cleo").InvokeAsync(Rig.Args(rig.Room.Id, "in 30 minutes"), ct);

        Assert.Equal($"You are not a Member of Room '{rig.Room.Id}'. A Follow-up can only come back to a Room you are in.", answer);
        Assert.Empty(rig.Store.Jobs);
    }

    /// <summary>Less than a minute ahead is refused, naming the limit.</summary>
    [Fact]
    public async Task FollowUp_TooSoon_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(Now, TimeZoneInfo.Utc, ct);

        string relative = await rig.Tool("Nova").InvokeAsync(Rig.Args(rig.Room.Id, "in 0 minutes"), ct);
        string absolute = await rig.Tool("Nova").InvokeAsync(Rig.Args(rig.Room.Id, "once on 2026-11-02 at 08:00"), ct);

        Assert.Equal(("A Follow-up must be at least 1 minute ahead.", "A Follow-up must be at least 1 minute ahead."), (relative, absolute));
        Assert.Empty(rig.Store.Jobs);
    }

    /// <summary>More than 31 days ahead is refused, naming the limit; exactly 31 days is allowed.</summary>
    [Fact]
    public async Task FollowUp_TooFar_IsRefused_AtThirtyOneDaysIsAllowed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(Now, TimeZoneInfo.Utc, ct);

        string relative = await rig.Tool("Nova").InvokeAsync(Rig.Args(rig.Room.Id, "in 32 days"), ct);
        string absolute = await rig.Tool("Nova").InvokeAsync(Rig.Args(rig.Room.Id, "once on 2026-12-31 at 09:00"), ct);
        string allowed = await rig.Tool("Nova").InvokeAsync(Rig.Args(rig.Room.Id, "in 31 days"), ct);

        Assert.Equal(("A Follow-up may be at most 31 days ahead.", "A Follow-up may be at most 31 days ahead."), (relative, absolute));
        Job only = Assert.Single(rig.Store.Jobs);
        Assert.Equal($"Set {only.Id} \"Check the build\": you will come back to Ops on Thu 3 Dec 2026 at 08:00.", allowed);
    }

    /// <summary>At the cap the refusal is the exact terminal text, a paused Follow-up counts, and the cap follows the setting.</summary>
    [Fact]
    public async Task FollowUp_AtTheCap_IsRefusedWithTheExactText_PausedOnesCount()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(Now, TimeZoneInfo.Utc, ct);
        rig.Options.Value.Jobs.MaxFollowUpsPerAgent = 2;
        _ = await rig.Tool("Nova").InvokeAsync(Rig.Args(rig.Room.Id, "in 1 hour"), ct);
        _ = rig.Store.Update(Assert.Single(rig.Store.Jobs) with { Paused = true });
        _ = await rig.Tool("Nova").InvokeAsync(Rig.Args(rig.Room.Id, "in 2 hours"), ct);

        string answer = await rig.Tool("Nova").InvokeAsync(Rig.Args(rig.Room.Id, "in 3 hours"), ct);

        Assert.Equal("You already have 2 Follow-ups waiting, the most one Agent may have. Delete one with jobs delete, or say in the Room what you will check and let the Human remind you.", answer);
        Assert.Equal(2, rig.Store.Jobs.Count);
    }

    /// <summary>Another Agent's Follow-ups, and ordinary Jobs, do not count against the caller's cap.</summary>
    [Fact]
    public async Task FollowUp_CapCountsOnlyTheCallersFollowUps()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(Now, TimeZoneInfo.Utc, ct);
        rig.Options.Value.Jobs.MaxFollowUpsPerAgent = 1;
        _ = await rig.Tool("Orion").InvokeAsync(Rig.Args(rig.Room.Id, "in 1 hour"), ct);
        _ = await rig.Tool("Nova").InvokeAsync(new JsonObject { ["action"] = "create", ["title"] = "Digest", ["instruction"] = "Go.", ["when"] = "daily at 09:00" }, ct);

        string answer = await rig.Tool("Nova").InvokeAsync(Rig.Args(rig.Room.Id, "in 2 hours"), ct);

        Job mine = Assert.Single(rig.Store.Jobs, job => job.RoomId is not null && job.Owner.IsAgent("Nova"));
        Assert.Equal($"Set {mine.Id} \"Check the build\": you will come back to Ops on Mon 2 Nov 2026 at 10:00.", answer);
    }

    /// <summary>With the cap at zero no Agent may set a Follow-up.</summary>
    [Fact]
    public async Task FollowUp_CapZero_RefusesEvenTheFirst()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(Now, TimeZoneInfo.Utc, ct);
        rig.Options.Value.Jobs.MaxFollowUpsPerAgent = 0;

        string answer = await rig.Tool("Nova").InvokeAsync(Rig.Args(rig.Room.Id, "in 1 hour"), ct);

        Assert.Equal("Follow-ups are switched off here. Say in the Room what you will check and let the Human remind you.", answer);
        Assert.Empty(rig.Store.Jobs);
    }

    /// <summary>With the cap at zero the switched-off text comes before any other check: a call missing its Room, title and time still gets it.</summary>
    [Fact]
    public async Task FollowUp_CapZero_AnswersBeforeAnyOtherValidation()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(Now, TimeZoneInfo.Utc, ct);
        rig.Options.Value.Jobs.MaxFollowUpsPerAgent = 0;

        string empty = await rig.Tool("Nova").InvokeAsync(new JsonObject { ["action"] = "follow_up" }, ct);
        string target = await rig.Tool("Nova").InvokeAsync(new JsonObject { ["action"] = "follow_up", ["target"] = "Orion" }, ct);
        string stranger = await rig.Tool("Cleo").InvokeAsync(Rig.Args(rig.Room.Id, "in 1 hour"), ct);

        const string Off = "Follow-ups are switched off here. Say in the Room what you will check and let the Human remind you.";
        Assert.Equal([Off, Off, Off], [empty, target, stranger]);
    }

    /// <summary><c>update</c> applies the same at-least-1-minute and at-most-31-days bounds <c>follow_up</c> does, and accepts a time inside them.</summary>
    [Fact]
    public async Task Update_AFollowUp_WhenOutsideTheBounds_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(Now, TimeZoneInfo.Utc, ct);
        _ = await rig.Tool("Nova").InvokeAsync(Rig.Args(rig.Room.Id, "in 1 hour"), ct);
        Job job = Assert.Single(rig.Store.Jobs);

        string past = await rig.Tool("Nova").InvokeAsync(new JsonObject { ["action"] = "update", ["id"] = job.Id, ["when"] = "once on 2026-11-02 at 08:00" }, ct);
        string far = await rig.Tool("Nova").InvokeAsync(new JsonObject { ["action"] = "update", ["id"] = job.Id, ["when"] = "once on 2026-12-31 at 09:00" }, ct);
        Job unchanged = Assert.Single(rig.Store.Jobs);
        string fine = await rig.Tool("Nova").InvokeAsync(new JsonObject { ["action"] = "update", ["id"] = job.Id, ["when"] = "once on 2026-11-03 at 09:00" }, ct);

        Assert.Equal(("A Follow-up must be at least 1 minute ahead.", "A Follow-up may be at most 31 days ahead."), (past, far));
        Assert.Equal(job, unchanged);
        Assert.Equal(JobRecurrence.Once(new DateOnly(2026, 11, 3), new TimeOnly(9, 0)), rig.Store.Find(job.Id)?.When);
        Assert.NotEmpty(fine);
    }

    /// <summary><c>update</c> cannot give a Follow-up a recurring timetable:<see cref="JobRules"/>' text is the answer.</summary>
    [Fact]
    public async Task Update_AFollowUp_ToARecurringWhen_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(Now, TimeZoneInfo.Utc, ct);
        _ = await rig.Tool("Nova").InvokeAsync(Rig.Args(rig.Room.Id, "in 1 hour"), ct);
        Job job = Assert.Single(rig.Store.Jobs);
        JsonObject update = new() { ["action"] = "update", ["id"] = job.Id, ["when"] = "daily at 09:00" };

        string answer = await rig.Tool("Nova").InvokeAsync(update, ct);

        Assert.Equal("A Follow-up runs once; delete it and create a Job instead", answer);
        Assert.Equal(job, Assert.Single(rig.Store.Jobs));
    }

    /// <summary><c>update</c> cannot move a Follow-up to another owner.</summary>
    [Fact]
    public async Task Update_AFollowUp_ToAnotherOwner_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(Now, TimeZoneInfo.Utc, ct);
        _ = await rig.Tool("Nova").InvokeAsync(Rig.Args(rig.Room.Id, "in 1 hour"), ct);
        Job job = Assert.Single(rig.Store.Jobs);
        JsonObject update = new() { ["action"] = "update", ["id"] = job.Id, ["target"] = "Orion" };

        string answer = await rig.Tool("Cleo").InvokeAsync(update, ct);

        Assert.Equal("A Follow-up cannot move to another owner.", answer);
        Assert.Equal(JobOwner.Agent("Nova"), rig.Store.Find(job.Id)?.Owner);
    }

    /// <summary>A missing Room id, title, instruction or time is asked for.</summary>
    [Fact]
    public async Task FollowUp_WithoutARoomId_AsksForIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(Now, TimeZoneInfo.Utc, ct);

        string answer = await rig.Tool("Nova").InvokeAsync(new JsonObject { ["action"] = "follow_up", ["title"] = "T", ["instruction"] = "I", ["when"] = "in 1 hour" }, ct);

        Assert.Equal("A Follow-up needs a 'roomId', a 'title', an 'instruction' and a 'when'.", answer);
    }

    /// <summary>Everything a test of the tool needs: the stores, a real Room Nova is in, and a clock and zone it chooses.</summary>
    private sealed class Rig : IDisposable
    {
        private readonly NeedsYouFixture fixture;
        private readonly PersonaStore personas;
        private readonly FakeTeamCatalog catalog = new();

        private Rig(NeedsYouFixture fixture, Room room, DateTimeOffset now, TimeZoneInfo zone)
        {
            this.fixture = fixture;
            this.Room = room;
            this.Options = fixture.Directory.Options();
            AgentPaths paths = new(this.Options);
            foreach (string name in new[] { "Nova", "Orion" })
            {
                TestPersonaFiles.Write(paths, name, PersonaSupervisorTestSupport.PersonaText(name, $"You are {name}."));
            }

            TestPersonaFiles.Write(paths, "Cleo", "---\nName: Cleo\nTitle: Chief of Staff\nAlias: cos\n_builtin: chief-of-staff\n---\nYou are Cleo.");
            this.personas = this.Options.CreatePersonaStore();
            this.Store = new JobStore(this.personas, this.Options, NullLogger<JobStore>.Instance);
            this.Clock = new ManualTimeProvider(now, zone);
            this.Scheduler = new JobScheduler(this.Store, new JobStateStore(this.Options), new NullRunner(), this.Options, NullLogger<JobScheduler>.Instance, new Agency.Huddle.Tests.Jobs.NoJobNotice(), this.Clock);
        }

        public Room Room { get; }

        public IOptions<TeamOptions> Options { get; }

        public JobStore Store { get; }

        public JobScheduler Scheduler { get; }

        public RecordingFeedPublisher Feed { get; } = new();

        private ManualTimeProvider Clock { get; }

        public static async Task<Rig> CreateAsync(DateTimeOffset now, TimeZoneInfo zone, CancellationToken ct)
        {
            NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
            User nova = await fixture.AddAgentAsync("Nova", ct);
            User orion = await fixture.AddAgentAsync("Orion", ct);
            Room room = await fixture.Team.CreateRoomAsync("Ops", [KnownIds.Human, nova.Id, orion.Id], ct);
            return new Rig(fixture, room, now, zone);
        }

        public static JsonObject Args(string roomId, string when) => new()
        {
            ["action"] = "follow_up",
            ["roomId"] = roomId,
            ["title"] = "Check the build",
            ["instruction"] = "Look at the build and report.",
            ["when"] = when,
        };

        public DateTimeOffset? NextRunOfOnlyJob() => this.Scheduler.StateOf(Assert.Single(this.Store.Jobs))?.NextRunUtc;

        public JobsTool Tool(string persona)
        {
            ServiceCollection services = new();
            _ = services.AddSingleton(this.Store).AddSingleton(this.Scheduler).AddSingleton(this.personas).AddSingleton(this.Options);
            _ = services.AddSingleton<IHumanIdentity>(new ConfiguredHumanIdentity("You")).AddSingleton<IPromptSource>(new FakePromptSource());
            _ = services.AddSingleton<ITeamCatalog>(this.catalog).AddSingleton<ITeamDirectory>(this.fixture.Team);
            _ = services.AddSingleton<IFeedPublisher>(this.Feed).AddSingleton<TimeProvider>(this.Clock);
            using ServiceProvider provider = services.BuildServiceProvider();
            return ActivatorUtilities.CreateInstance<JobsTool>(provider, persona);
        }

        public void Dispose()
        {
            this.Scheduler.Dispose();
            this.Store.Dispose();
            this.personas.Dispose();
            this.fixture.Dispose();
        }
    }

    /// <summary>A runner that posts nothing.</summary>
    private sealed class NullRunner : IJobRunner
    {
        public Task<JobOutcome> RunAsync(Job job, DateTimeOffset ranAt, DateTimeOffset? lateDue, CancellationToken ct) => Task.FromResult(JobOutcome.Delivered);
    }
}

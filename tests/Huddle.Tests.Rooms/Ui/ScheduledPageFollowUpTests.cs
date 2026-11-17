using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components.Jobs;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Jobs;
using Agency.Huddle.App.Profile;
using Agency.Huddle.Tests.Acp;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.NeedsYou;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Ui;

/// <summary>Pins the Scheduled page's words for a Follow-up: the <c>J2</c> row and the <c>J4</c> Room link.</summary>
public sealed class ScheduledPageFollowUpTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

    /// <summary>The list row of a Follow-up reads "For Nova · Follow-up in Ops · Next: ...".</summary>
    [Fact]
    public async Task List_FollowUpRow_NamesTheOwnerTheRoomAndTheNextRun()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using MudBunitContext ctx = new();
        using Rig rig = await Rig.CreateAsync(ctx, ct);

        IRenderedComponent<ScheduledPage> cut = ctx.Render<ScheduledPage>();

        cut.WaitForAssertion(() => Assert.Equal("For Nova · Follow-up in Ops · Next: Mon 2 Nov, 09:30", cut.Find(".scheduled-row .mud-text-secondary").TextContent.Trim()));
    }

    /// <summary>The detail view shows the Room as a link to its page.</summary>
    [Fact]
    public async Task Detail_FollowUp_ShowsTheRoomAsALink()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using MudBunitContext ctx = new();
        using Rig rig = await Rig.CreateAsync(ctx, ct);

        IRenderedComponent<ScheduledPage> cut = ctx.Render<ScheduledPage>(parameters => parameters.Add(p => p.JobId, rig.Job.Id));

        cut.WaitForAssertion(() =>
        {
            AngleSharp.Dom.IElement line = cut.Find(".scheduled-follow-up-room");
            Assert.Equal("Follow-up in Ops", line.TextContent.Trim());
            Assert.Equal($"/rooms/{rig.RoomId}", line.QuerySelector("a")?.GetAttribute("href"));
        });
    }

    private sealed class Rig : IDisposable
    {
        private readonly NeedsYouFixture fixture;
        private readonly PersonaStore personas;

        private Rig(NeedsYouFixture fixture, PersonaStore personas, JobStore store, JobScheduler scheduler, Job job, string roomId)
        {
            this.fixture = fixture;
            this.personas = personas;
            this.Store = store;
            this.Scheduler = scheduler;
            this.Job = job;
            this.RoomId = roomId;
        }

        public Job Job { get; }

        public string RoomId { get; }

        private JobStore Store { get; }

        private JobScheduler Scheduler { get; }

        public static async Task<Rig> CreateAsync(MudBunitContext ctx, CancellationToken ct)
        {
            NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
            User nova = await fixture.AddAgentAsync("Nova", ct);
            Room room = await fixture.Team.CreateRoomAsync("Ops", [KnownIds.Human, nova.Id], ct);
            IOptions<TeamOptions> options = fixture.Directory.Options();
            TestPersonaFiles.Write(new AgentPaths(options), "Nova", PersonaSupervisorTestSupport.PersonaText("Nova", "You are Nova."));
            PersonaStore personas = options.CreatePersonaStore();
            JobStore store = new(personas, options, NullLogger<JobStore>.Instance);
            ManualTimeProvider clock = new(Now, TimeZoneInfo.Utc);
            JobScheduler scheduler = new(store, new JobStateStore(options), new NullRunner(), options, NullLogger<JobScheduler>.Instance, new Agency.Huddle.Tests.Jobs.NoJobNotice(), clock);
            Job job = store.Add(Job.FollowUp(string.Empty, "Nova", room.Id, "Check the build", "Look.", new DateOnly(2026, 11, 2), new TimeOnly(9, 30)));
            scheduler.Seed(job);
            ctx.Services.AddSingleton(store);
            ctx.Services.AddSingleton(scheduler);
            ctx.Services.AddSingleton<TimeProvider>(clock);
            ctx.Services.AddSingleton(new JobRoomStore(options));
            ctx.Services.AddSingleton<ITeamDirectory>(fixture.Team);
            ctx.Services.AddSingleton<IHumanIdentity>(new ConfiguredHumanIdentity("You"));
            return new Rig(fixture, personas, store, scheduler, job, room.Id);
        }

        public void Dispose()
        {
            this.Scheduler.Dispose();
            this.Store.Dispose();
            this.personas.Dispose();
            this.fixture.Dispose();
        }
    }

    private sealed class NullRunner : IJobRunner
    {
        public Task<JobOutcome> RunAsync(Job job, DateTimeOffset ranAt, DateTimeOffset? lateDue, CancellationToken ct) => Task.FromResult(JobOutcome.Delivered);
    }
}

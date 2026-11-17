using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Jobs;
using Agency.Huddle.Tests.Acp;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Factories;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Pins <c>C13</c> (Turn Queue spec §7.5): <see cref="FollowUpsNotice"/> shows one status row per Follow-up waiting in the Room with its exact text,
/// words the paused Room, cancels without asking, shows nothing when none waits and follows the Job store.
/// </summary>
public sealed class FollowUpsNoticeTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

    /// <summary>Each Follow-up of this Room, and only this Room, is one status row with the exact text.</summary>
    [Fact]
    public async Task Renders_OneStatusRowPerFollowUpOfThisRoom()
    {
        await using MudBunitContext ctx = new();
        using Rig rig = Rig.Register(ctx);
        rig.Add("r1", "Check the build", new TimeOnly(9, 30));
        rig.Add("r2", "Elsewhere", new TimeOnly(10, 0));

        IRenderedComponent<FollowUpsNotice> cut = Rig.Render(ctx, paused: false);

        IReadOnlyList<AngleSharp.Dom.IElement> rows = cut.FindAll(".follow-ups-notice");
        Assert.Equal("⏰ Nova will follow up Mon 2 Nov, 09:30: Check the build", Assert.Single(rows).TextContent.Replace("Cancel", string.Empty, StringComparison.Ordinal).Trim());
        Assert.Equal(("status", true), (rows[0].GetAttribute("role"), rows[0].ClassList.Contains("room-notice-column")));
    }

    /// <summary>In a paused Room the row says it is waiting and shows no time.</summary>
    [Fact]
    public async Task PausedRoom_UsesTheWaitingWording()
    {
        await using MudBunitContext ctx = new();
        using Rig rig = Rig.Register(ctx);
        rig.Add("r1", "Check the build", new TimeOnly(9, 30));

        IRenderedComponent<FollowUpsNotice> cut = Rig.Render(ctx, paused: true);

        Assert.Equal("⏰ Nova will follow up: Check the build (waiting: this Room is paused)", cut.Find(".follow-ups-notice-text").TextContent.Trim());
    }

    /// <summary>A paused Follow-up reads "(paused)" instead of a time.</summary>
    [Fact]
    public async Task PausedFollowUp_SaysPausedInsteadOfATime()
    {
        await using MudBunitContext ctx = new();
        using Rig rig = Rig.Register(ctx);
        Job job = rig.Add("r1", "Check the build", new TimeOnly(9, 30));
        _ = rig.Store.Update(job with { Paused = true });

        IRenderedComponent<FollowUpsNotice> cut = Rig.Render(ctx, paused: false);

        Assert.Equal("⏰ Nova will follow up: Check the build (paused)", cut.Find(".follow-ups-notice-text").TextContent.Trim());
    }

    /// <summary>Disposing the notice lets go of the store's change event: a later change renders nothing and throws nothing.</summary>
    [Fact]
    public async Task Dispose_UnsubscribesFromTheJobStore()
    {
        await using MudBunitContext ctx = new();
        using Rig rig = Rig.Register(ctx);
        IRenderedComponent<FollowUpsNotice> cut = Rig.Render(ctx, paused: false);
        int before = cut.RenderCount;
        int handlersBefore = HandlerCount(rig.Store);

        await ctx.DisposeComponentsOnDispatcherAsync();
        int handlersAfter = HandlerCount(rig.Store);
        _ = rig.Add("r1", "Later", new TimeOnly(9, 30));

        Assert.Equal((1, 0, before), (handlersBefore, handlersAfter, cut.RenderCount));
    }

    private static int HandlerCount(JobStore store) =>
        ((Delegate?)typeof(JobStore).GetField(nameof(JobStore.Changed), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(store))?.GetInvocationList().Length ?? 0;

    /// <summary>Cancel deletes the Job at once, without a dialog, and the row goes.</summary>
    [Fact]
    public async Task Cancel_DeletesTheJob_AndTheRowGoes()
    {
        await using MudBunitContext ctx = new();
        using Rig rig = Rig.Register(ctx);
        Job job = rig.Add("r1", "Check the build", new TimeOnly(9, 30));
        IRenderedComponent<FollowUpsNotice> cut = Rig.Render(ctx, paused: false);

        cut.Find(".follow-ups-notice button").Click();

        Assert.Null(rig.Store.Find(job.Id));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".follow-ups-notice")));
    }

    /// <summary>With no Follow-up in the Room nothing is drawn.</summary>
    [Fact]
    public async Task NoFollowUps_RendersNothing()
    {
        await using MudBunitContext ctx = new();
        using Rig rig = Rig.Register(ctx);
        rig.Add("r2", "Elsewhere", new TimeOnly(10, 0));

        IRenderedComponent<FollowUpsNotice> cut = Rig.Render(ctx, paused: false);

        Assert.Empty(cut.FindAll(".follow-ups-notice"));
    }

    /// <summary>A Follow-up added after the first render appears, and one that has run is gone.</summary>
    [Fact]
    public async Task FollowsTheJobStore()
    {
        await using MudBunitContext ctx = new();
        using Rig rig = Rig.Register(ctx);
        IRenderedComponent<FollowUpsNotice> cut = Rig.Render(ctx, paused: false);

        Job job = rig.Add("r1", "Check the build", new TimeOnly(9, 30));

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".follow-ups-notice")));
        _ = rig.Store.Remove(job.Id);
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".follow-ups-notice")));
    }

    private sealed class Rig : IDisposable
    {
        private readonly TempDataDir dir = new();
        private readonly PersonaStore personas;

        private Rig()
        {
            IOptions<TeamOptions> options = this.dir.Options();
            TestPersonaFiles.Write(new AgentPaths(options), "Nova", PersonaSupervisorTestSupport.PersonaText("Nova", "You are Nova."));
            this.personas = options.CreatePersonaStore();
            this.Store = new JobStore(this.personas, options, NullLogger<JobStore>.Instance);
            this.Clock = new ManualTimeProvider(Now, TimeZoneInfo.Utc);
            this.Scheduler = new JobScheduler(this.Store, new JobStateStore(options), new NullRunner(), options, NullLogger<JobScheduler>.Instance, new Agency.Huddle.Tests.Jobs.NoJobNotice(), this.Clock);
        }

        public JobStore Store { get; }

        private JobScheduler Scheduler { get; }

        private ManualTimeProvider Clock { get; }

        public static Rig Register(MudBunitContext ctx)
        {
            Rig rig = new();
            ctx.Services.AddSingleton(rig.Store);
            ctx.Services.AddSingleton(rig.Scheduler);
            ctx.Services.AddSingleton<TimeProvider>(rig.Clock);
            return rig;
        }

        public Job Add(string roomId, string title, TimeOnly at) =>
            this.Store.Add(Job.FollowUp(string.Empty, "Nova", roomId, title, "Look.", new DateOnly(2026, 11, 2), at));

        public static IRenderedComponent<FollowUpsNotice> Render(MudBunitContext ctx, bool paused) =>
            ctx.Render<FollowUpsNotice>(parameters => parameters.Add(p => p.RoomId, "r1").Add(p => p.RoomPaused, paused));

        public void Dispose()
        {
            this.Scheduler.Dispose();
            this.Store.Dispose();
            this.personas.Dispose();
            this.dir.Dispose();
        }
    }

    private sealed class NullRunner : IJobRunner
    {
        public Task<JobOutcome> RunAsync(Job job, DateTimeOffset ranAt, DateTimeOffset? lateDue, CancellationToken ct) => Task.FromResult(JobOutcome.Delivered);
    }
}

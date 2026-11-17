using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components.Jobs;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Jobs;
using Agency.Huddle.Tests.Acp;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Factories;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Jobs;

/// <summary>Pins how <see cref="JobScheduler"/> treats a run that is overdue, failing, held or paused (Turn Queue spec §8).</summary>
public sealed class JobSchedulerOverdueTests : IDisposable
{
    private static readonly DateTimeOffset Monday0800 = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

    private readonly TempDataDir dir = new();
    private readonly IOptions<TeamOptions> options;
    private readonly PersonaStore personas;
    private readonly JobStore store;
    private readonly JobStateStore states;
    private readonly ScriptedRunner runner = new();
    private readonly RecordingNotice notice = new();
    private readonly ManualTimeProvider clock = new(Monday0800, TimeZoneInfo.Utc);
    private readonly JobScheduler scheduler;

    /// <summary>Seeds the Agent Nova and opens the stores over it.</summary>
    public JobSchedulerOverdueTests()
    {
        this.options = this.dir.Options();
        TestPersonaFiles.Write(new AgentPaths(this.options), "Nova", PersonaSupervisorTestSupport.PersonaText("Nova", "You are Nova."));
        this.personas = this.options.CreatePersonaStore();
        this.store = new JobStore(this.personas, this.options, NullLogger<JobStore>.Instance);
        this.states = new JobStateStore(this.options);
        this.scheduler = new JobScheduler(this.store, this.states, this.runner, this.options, NullLogger<JobScheduler>.Instance, this.notice, this.clock);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.scheduler.Dispose();
        this.store.Dispose();
        this.personas.Dispose();
        this.dir.Dispose();
    }

    /// <summary>A daily Job three days late runs once, says it ran late and due when, and its next run is after now; the same minute again runs nothing.</summary>
    [Fact]
    public async Task Daily_ThreeDaysLate_RunsOnceMarkedLate()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Job job = this.Add(JobRecurrence.Daily(new TimeOnly(9, 0)));
        await this.scheduler.TickAsync(ct);

        this.clock.UtcNow = Monday0800.AddDays(3).AddHours(2);
        await this.scheduler.TickAsync(ct);
        await this.scheduler.TickAsync(ct);

        Assert.Equal([Monday0800.AddHours(1)], this.runner.LateDues);
        Assert.Equal((JobOutcome.DeliveredLate, Monday0800.AddDays(4).AddHours(1)), (this.states.Get(job.Id)?.LastOutcome, this.states.Get(job.Id)?.NextRunUtc));
    }

    /// <summary>A run within the two-minute grace is on time: no late wording is passed to the runner.</summary>
    [Fact]
    public async Task Daily_WithinGrace_IsNotMarkedLate()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Job job = this.Add(JobRecurrence.Daily(new TimeOnly(9, 0)));
        await this.scheduler.TickAsync(ct);

        this.clock.UtcNow = Monday0800.AddMinutes(61.5);
        await this.scheduler.TickAsync(ct);

        Assert.Equal([(DateTimeOffset?)null], this.runner.LateDues);
        Assert.Equal(JobOutcome.Delivered, this.states.Get(job.Id)?.LastOutcome);
    }

    /// <summary>A failing late run is tried on the next ticks, stops after three tries, and the next due time runs normally with a clean count.</summary>
    [Fact]
    public async Task LateRun_Failing_StopsAfterThreeTriesAndNextTimeRuns()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Job job = this.Add(JobRecurrence.Daily(new TimeOnly(9, 0)));
        await this.scheduler.TickAsync(ct);
        this.runner.Script.Enqueue(null);
        this.runner.Script.Enqueue(null);
        this.runner.Script.Enqueue(null);

        for (int i = 0; i < 5; i++)
        {
            this.clock.UtcNow = Monday0800.AddDays(3).AddHours(2).AddMinutes(i);
            await this.scheduler.TickAsync(ct);
        }

        Assert.Equal(3, this.runner.LateDues.Count);
        Assert.Equal((0, Monday0800.AddDays(4).AddHours(1)), (this.states.GetTries(job.Id).FailedTries, this.states.Get(job.Id)?.NextRunUtc));

        this.clock.UtcNow = Monday0800.AddDays(4).AddHours(1).AddSeconds(30);
        await this.scheduler.TickAsync(ct);

        Assert.Equal(4, this.runner.LateDues.Count);
        Assert.Equal(JobOutcome.Delivered, this.states.Get(job.Id)?.LastOutcome);
    }

    /// <summary>Seeding a new one-off writes its state at once, and a tick a minute later runs it and removes it.</summary>
    [Fact]
    public async Task Seed_OneOffDueInsideTheMinute_StillRuns()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.clock.UtcNow = Monday0800.AddSeconds(20);
        Job job = this.Add(JobRecurrence.Once(new DateOnly(2026, 11, 2), new TimeOnly(8, 1)));

        this.scheduler.Seed(job);

        Assert.Equal(Monday0800.AddMinutes(1), this.states.Get(job.Id)?.NextRunUtc);
        this.clock.UtcNow = Monday0800.AddSeconds(80);
        await this.scheduler.TickAsync(ct);
        Assert.Equal([(DateTimeOffset?)null], this.runner.LateDues);
        Assert.Null(this.store.Find(job.Id));
    }

    /// <summary>A one-off first seen after its time (never seeded) runs once, late, and is removed.</summary>
    [Fact]
    public async Task OneOff_SeenFirstAfterItsTime_RunsOnceLate()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Job job = this.Add(JobRecurrence.Once(new DateOnly(2026, 11, 2), new TimeOnly(6, 0)));

        await this.scheduler.TickAsync(ct);
        await this.scheduler.TickAsync(ct);

        Assert.Equal([Monday0800.AddHours(-2)], this.runner.LateDues);
        Assert.Null(this.store.Find(job.Id));
    }

    /// <summary>A one-off later than the overdue window gives up: removed without posting and with the trace, whose reason names the window.</summary>
    [Fact]
    public async Task OneOff_PastTheWindow_GivesUpWithATrace()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Job job = this.Add(JobRecurrence.Once(new DateOnly(2026, 11, 2), new TimeOnly(9, 0)));
        await this.scheduler.TickAsync(ct);

        this.clock.UtcNow = Monday0800.AddHours(26);
        await this.scheduler.TickAsync(ct);

        Assert.Empty(this.runner.LateDues);
        Assert.Null(this.store.Find(job.Id));
        Assert.Null(this.states.Get(job.Id));
        Assert.Equal([(job.Id, "it was more than 24 hours late")], this.notice.Notes);
    }

    /// <summary>The next run is saved before the post (ADR-0042): while the post is in flight the stored next run is already the following day's, so a restart there never runs it twice.</summary>
    [Fact]
    public async Task Run_SavesTheNextRunBeforeThePost()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Job job = this.Add(JobRecurrence.Daily(new TimeOnly(9, 0)));
        await this.scheduler.TickAsync(ct);
        DateTimeOffset? duringPost = null;
        this.runner.OnRun = _ => duringPost = this.states.Get(job.Id)?.NextRunUtc;

        this.clock.UtcNow = Monday0800.AddHours(1).AddSeconds(10);
        await this.scheduler.TickAsync(ct);

        Assert.Equal(Monday0800.AddHours(25), duringPost);
    }

    /// <summary>A one-off first seen (never seeded) more than the overdue window after its time gives up at once, with the trace, and does not linger.</summary>
    [Fact]
    public async Task OneOff_FirstSeenPastTheWindow_GivesUpWithATrace()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Job job = this.Add(JobRecurrence.Once(new DateOnly(2026, 11, 1), new TimeOnly(6, 0)));

        await this.scheduler.TickAsync(ct);

        Assert.Empty(this.runner.LateDues);
        Assert.Null(this.store.Find(job.Id));
        Assert.Equal([(job.Id, "it was more than 24 hours late")], this.notice.Notes);
    }

    /// <summary>Deleting a Job, and a one-off giving up, each forget its retry count along with its state.</summary>
    [Fact]
    public async Task DeleteAndGiveUp_RemoveTheRetryRow()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Job deleted = this.Add(JobRecurrence.Daily(new TimeOnly(9, 0)));
        Job gaveUp = this.Add(JobRecurrence.Once(new DateOnly(2026, 11, 2), new TimeOnly(9, 0)));
        await this.scheduler.TickAsync(ct);
        this.states.SetTries(deleted.Id, 2, true);
        this.states.SetTries(gaveUp.Id, 1, true);

        Assert.True(this.scheduler.Delete(deleted.Id));
        this.clock.UtcNow = Monday0800.AddHours(26);
        await this.scheduler.TickAsync(ct);

        Assert.Equal(((0, false), (0, false)), (this.states.GetTries(deleted.Id), this.states.GetTries(gaveUp.Id)));
        Assert.Null(this.store.Find(gaveUp.Id));
    }

    /// <summary>With no overdue window a one-off first seen late runs, and the late text carries its own due time, not the time of the run.</summary>
    [Fact]
    public async Task OneOff_NoWindow_FirstSeenLate_ReportsItsOwnDueTime()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.options.Value.Jobs.OverdueOneOffWithin = TimeSpan.Zero;
        Job job = this.Add(JobRecurrence.Once(new DateOnly(2026, 10, 1), new TimeOnly(6, 0)));

        await this.scheduler.TickAsync(ct);

        Assert.Equal([new DateTimeOffset(2026, 10, 1, 6, 0, 0, TimeSpan.Zero)], this.runner.LateDues);
        Assert.Null(this.store.Find(job.Id));
    }

    /// <summary>With RunOverdue off a one-off first seen past the window is kept as before: no run and no trace.</summary>
    [Fact]
    public async Task OneOff_FirstSeenPastTheWindow_RunOverdueOff_IsKept()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.options.Value.Jobs.RunOverdue = false;
        Job job = this.Add(JobRecurrence.Once(new DateOnly(2026, 11, 1), new TimeOnly(6, 0)));

        await this.scheduler.TickAsync(ct);

        Assert.Empty(this.runner.LateDues);
        Assert.NotNull(this.store.Find(job.Id));
        Assert.Empty(this.notice.Notes);
    }

    /// <summary>A one-off that fails three times is given up with the retries reason, and a failed one-off stays until then.</summary>
    [Fact]
    public async Task OneOff_Failing_StaysThenGivesUpAfterThreeTries()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Job job = this.Add(JobRecurrence.Once(new DateOnly(2026, 11, 2), new TimeOnly(9, 0)));
        await this.scheduler.TickAsync(ct);
        this.runner.Script.Enqueue(null);
        this.runner.Script.Enqueue(null);
        this.runner.Script.Enqueue(null);

        this.clock.UtcNow = Monday0800.AddHours(1).AddSeconds(10);
        await this.scheduler.TickAsync(ct);
        Assert.NotNull(this.store.Find(job.Id));
        Assert.Equal(1, this.states.GetTries(job.Id).FailedTries);

        this.clock.UtcNow = Monday0800.AddHours(1).AddSeconds(40);
        await this.scheduler.TickAsync(ct);
        this.clock.UtcNow = Monday0800.AddHours(1).AddSeconds(70);
        await this.scheduler.TickAsync(ct);
        Assert.NotNull(this.store.Find(job.Id));
        this.clock.UtcNow = Monday0800.AddHours(1).AddSeconds(100);
        await this.scheduler.TickAsync(ct);

        Assert.Equal(3, this.runner.LateDues.Count);
        Assert.Null(this.store.Find(job.Id));
        Assert.Equal([(job.Id, "it failed three times")], this.notice.Notes);
    }

    /// <summary>A one-off whose Agent or Room is gone is removed with the trace and the reason; nothing else removes it.</summary>
    [Theory]
    [InlineData("NoAgent", "its Agent is no longer available")]
    [InlineData("NoRoom", "its Room is gone")]
    public async Task OneOff_NoAgentOrNoRoom_IsRemovedWithATrace(string outcomeName, string reason)
    {
        JobOutcome outcome = Enum.Parse<JobOutcome>(outcomeName);
        CancellationToken ct = TestContext.Current.CancellationToken;
        Job job = this.Add(JobRecurrence.Once(new DateOnly(2026, 11, 2), new TimeOnly(9, 0)));
        await this.scheduler.TickAsync(ct);
        this.runner.Script.Enqueue(outcome);

        this.clock.UtcNow = Monday0800.AddHours(1).AddSeconds(10);
        await this.scheduler.TickAsync(ct);

        Assert.Null(this.store.Find(job.Id));
        Assert.Equal([(job.Id, reason)], this.notice.Notes);
    }

    /// <summary>A recurring Job whose Agent is gone is kept, with no trace: its next time comes as usual.</summary>
    [Fact]
    public async Task Recurring_NoAgent_IsKept()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Job job = this.Add(JobRecurrence.Daily(new TimeOnly(9, 0)));
        await this.scheduler.TickAsync(ct);
        this.runner.Script.Enqueue(JobOutcome.NoAgent);

        this.clock.UtcNow = Monday0800.AddHours(1).AddSeconds(10);
        await this.scheduler.TickAsync(ct);

        Assert.NotNull(this.store.Find(job.Id));
        Assert.Empty(this.notice.Notes);
        Assert.Equal((JobOutcome.NoAgent, Monday0800.AddHours(25)), (this.states.Get(job.Id)?.LastOutcome, this.states.Get(job.Id)?.NextRunUtc));
    }

    /// <summary>A recurring Job whose Budget is spent is done for that time and runs again at its next time.</summary>
    [Fact]
    public async Task Recurring_BudgetSpent_RunsAtItsNextTime()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Job job = this.Add(JobRecurrence.Daily(new TimeOnly(9, 0)));
        await this.scheduler.TickAsync(ct);
        this.runner.Script.Enqueue(JobOutcome.BudgetSpent);

        this.clock.UtcNow = Monday0800.AddHours(1).AddSeconds(10);
        await this.scheduler.TickAsync(ct);
        this.clock.UtcNow = Monday0800.AddHours(1).AddSeconds(70);
        await this.scheduler.TickAsync(ct);
        Assert.Single(this.runner.LateDues);

        this.clock.UtcNow = Monday0800.AddHours(25).AddSeconds(10);
        await this.scheduler.TickAsync(ct);

        Assert.Equal(2, this.runner.LateDues.Count);
        Assert.Equal(JobOutcome.Delivered, this.states.Get(job.Id)?.LastOutcome);
    }

    /// <summary>A one-off whose Room's Budget is spent is held and tried again on the next ticks, and runs when it posts.</summary>
    [Theory]
    [InlineData("BudgetSpent")]
    [InlineData("Held")]
    public async Task OneOff_HeldOrBudgetSpent_IsKeptAndRetried(string firstName)
    {
        JobOutcome first = Enum.Parse<JobOutcome>(firstName);
        CancellationToken ct = TestContext.Current.CancellationToken;
        Job job = this.Add(JobRecurrence.Once(new DateOnly(2026, 11, 2), new TimeOnly(9, 0)));
        await this.scheduler.TickAsync(ct);
        this.runner.Script.Enqueue(first);

        this.clock.UtcNow = Monday0800.AddHours(1).AddSeconds(10);
        await this.scheduler.TickAsync(ct);
        Assert.NotNull(this.store.Find(job.Id));
        Assert.Equal((0, Monday0800.AddHours(1)), (this.states.GetTries(job.Id).FailedTries, this.states.Get(job.Id)?.NextRunUtc));

        this.clock.UtcNow = Monday0800.AddHours(1).AddSeconds(70);
        await this.scheduler.TickAsync(ct);

        Assert.Equal(2, this.runner.LateDues.Count);
        Assert.Null(this.store.Find(job.Id));
        Assert.Empty(this.notice.Notes);
    }

    /// <summary>A held one-off that is still held a day later gives up with the lateness reason.</summary>
    [Fact]
    public async Task OneOff_HeldPastTheWindow_GivesUp()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Job job = this.Add(JobRecurrence.Once(new DateOnly(2026, 11, 2), new TimeOnly(9, 0)));
        await this.scheduler.TickAsync(ct);
        this.runner.Script.Enqueue(JobOutcome.Held);
        this.clock.UtcNow = Monday0800.AddHours(1).AddSeconds(10);
        await this.scheduler.TickAsync(ct);

        this.clock.UtcNow = Monday0800.AddHours(1).AddHours(25);
        await this.scheduler.TickAsync(ct);

        Assert.Null(this.store.Find(job.Id));
        Assert.Equal([(job.Id, "it was more than 24 hours late")], this.notice.Notes);
    }

    /// <summary>While a Job is paused its stored next run moves forward each tick, so resuming it never runs it late.</summary>
    [Fact]
    public async Task Paused_NextRunMovesForward_ResumingDoesNotRunLate()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Job job = this.Add(JobRecurrence.Daily(new TimeOnly(9, 0)));
        await this.scheduler.TickAsync(ct);
        Assert.True(this.store.Update(job with { Paused = true }));

        this.clock.UtcNow = Monday0800.AddDays(2).AddHours(4);
        await this.scheduler.TickAsync(ct);
        Assert.Equal(Monday0800.AddDays(3).AddHours(1), this.states.Get(job.Id)?.NextRunUtc);

        Assert.True(this.store.Update(job with { Paused = false }));
        await this.scheduler.TickAsync(ct);
        Assert.Empty(this.runner.LateDues);
        this.clock.UtcNow = Monday0800.AddDays(3).AddHours(1).AddSeconds(30);
        await this.scheduler.TickAsync(ct);

        Assert.Equal([(DateTimeOffset?)null], this.runner.LateDues);
    }

    /// <summary>With RunOverdue off a run missed by more than the grace is skipped to the next run, as before.</summary>
    [Fact]
    public async Task RunOverdueFalse_RestoresTheSkip()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.options.Value.Jobs.RunOverdue = false;
        Job job = this.Add(JobRecurrence.Daily(new TimeOnly(9, 0)));
        await this.scheduler.TickAsync(ct);

        this.clock.UtcNow = Monday0800.AddMinutes(65);
        await this.scheduler.TickAsync(ct);

        Assert.Empty(this.runner.LateDues);
        Assert.Equal(Monday0800.AddHours(25), this.states.Get(job.Id)?.NextRunUtc);
    }

    /// <summary>With the Turn Queue off, an overdue Agent Job is skipped but the Human's Job still runs late.</summary>
    [Fact]
    public async Task TurnQueueOff_OverdueIsOffForAnAgentJobAndOnForTheHumans()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.options.Value.TurnQueue.Enabled = false;
        Job agentJob = this.Add(JobRecurrence.Daily(new TimeOnly(9, 0)));
        Job humanJob = this.Add(JobRecurrence.Daily(new TimeOnly(9, 0)), JobOwner.Human);
        await this.scheduler.TickAsync(ct);

        this.clock.UtcNow = Monday0800.AddDays(3).AddHours(2);
        await this.scheduler.TickAsync(ct);

        Assert.Equal([humanJob.Id], this.runner.RunIds);
        Assert.Equal(Monday0800.AddDays(4).AddHours(1), this.states.Get(agentJob.Id)?.NextRunUtc);
    }

    /// <summary>A late post says "ran late; due" with the due time; an on-time post is the text it always was.</summary>
    [Fact]
    public void TextOf_LateAndOnTime_ReadExactly()
    {
        Job job = new("job-1", JobOwner.Human, "Digest", "Do it.", JobRecurrence.Daily(new TimeOnly(9, 0)), "UTC", "You", false);

        Assert.Equal("⏰ Digest\n\nDo it.", JobRunner.TextOf(job, null));
        Assert.Equal("⏰ Digest (ran late; due Mon 09:00)\n\nDo it.", JobRunner.TextOf(job, null, Monday0800.AddHours(1)));
    }

    /// <summary>The Job page says "Ran late" for a late delivery, and the NoAgent wording is the one spec text.</summary>
    [Fact]
    public void JobText_RanLateAndNoAgent_ReadExactly()
    {
        ManualTimeProvider text = new(Monday0800, TimeZoneInfo.Utc);

        Assert.Equal("Last ran Mon 2 Nov, 08:00: Ran late", JobText.LastRun(Monday0800, JobOutcome.DeliveredLate, TimeZoneInfo.Utc, text));
        Assert.Equal("\"Ping\" did not run: its Agent is no longer available.", JobText.RanNow("Ping", JobOutcome.NoAgent));
    }

    private Job Add(JobRecurrence when, JobOwner? owner = null) =>
        this.store.Add(new Job(string.Empty, owner ?? JobOwner.Agent("Nova"), "Digest", "Do it.", when, "UTC", "You", false));

    /// <summary>Records each run and answers with the next scripted outcome (a null entry throws), or Delivered when the script is empty.</summary>
    private sealed class ScriptedRunner : IJobRunner
    {
        public Queue<JobOutcome?> Script { get; } = new();

        public List<DateTimeOffset?> LateDues { get; } = [];

        public List<string> RunIds { get; } = [];

        /// <summary>Called as a run starts, before it answers, so a test can look at what is stored while the post is in flight.</summary>
        public Action<Job>? OnRun { get; set; }

        public Task<JobOutcome> RunAsync(Job job, DateTimeOffset ranAt, DateTimeOffset? lateDue, CancellationToken ct)
        {
            this.OnRun?.Invoke(job);
            this.LateDues.Add(lateDue);
            this.RunIds.Add(job.Id);
            if (this.Script.Count == 0)
            {
                return Task.FromResult(lateDue is null ? JobOutcome.Delivered : JobOutcome.DeliveredLate);
            }

            JobOutcome? next = this.Script.Dequeue();
            return next is { } outcome ? Task.FromResult(outcome) : throw new InvalidOperationException("The post failed.");
        }
    }

    /// <summary>Records each give-up trace.</summary>
    private sealed class RecordingNotice : IJobGaveUpNotice
    {
        public List<(string JobId, string Reason)> Notes { get; } = [];

        public Task NoteAsync(Job job, string reason, CancellationToken ct)
        {
            this.Notes.Add((job.Id, reason));
            return Task.CompletedTask;
        }
    }
}

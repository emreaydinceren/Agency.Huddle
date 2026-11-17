using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Jobs;
using Agency.Huddle.Tests.Acp;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Factories;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Jobs;

/// <summary>Pins when <see cref="JobScheduler"/> runs a Job, and that a restart or a stop never runs one twice or late.</summary>
public sealed class JobSchedulerTests : IDisposable
{
    private static readonly DateTimeOffset Monday0800 = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

    private readonly TempDataDir dir = new();
    private readonly IOptions<TeamOptions> options;
    private readonly PersonaStore personas;
    private readonly JobStore store;
    private readonly JobStateStore states;
    private readonly RecordingRunner runner = new();
    private readonly ManualTimeProvider clock = new(Monday0800, TimeZoneInfo.Utc);
    private JobScheduler scheduler;

    /// <summary>Seeds the Agent Nova and opens the stores over it.</summary>
    public JobSchedulerTests()
    {
        this.options = this.dir.Options();
        TestPersonaFiles.Write(new AgentPaths(this.options), "Nova", PersonaSupervisorTestSupport.PersonaText("Nova", "You are Nova."));
        this.personas = this.options.CreatePersonaStore();
        this.store = new JobStore(this.personas, this.options, NullLogger<JobStore>.Instance);
        this.states = new JobStateStore(this.options);
        this.scheduler = this.NewScheduler();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.scheduler.Dispose();
        this.store.Dispose();
        this.personas.Dispose();
        this.dir.Dispose();
    }

    /// <summary>The first look at a Job works out its next run and does not run it.</summary>
    [Fact]
    public async Task FirstSight_SeedsTheNextRunWithoutRunning()
    {
        Job job = this.Add(JobRecurrence.Daily(new TimeOnly(9, 0)));

        await this.scheduler.TickAsync(TestContext.Current.CancellationToken);

        Assert.Empty(this.runner.Runs);
        Assert.Equal(Monday0800.AddHours(1), this.states.Get(job.Id)?.NextRunUtc);
    }

    /// <summary>A due Job runs once, records the run, and moves on to its next run; the same minute again runs nothing.</summary>
    [Fact]
    public async Task Due_RunsOnceAndMovesOn()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Job job = this.Add(JobRecurrence.Daily(new TimeOnly(9, 0)));
        await this.scheduler.TickAsync(ct);

        this.clock.UtcNow = Monday0800.AddMinutes(60.5);
        await this.scheduler.TickAsync(ct);
        await this.scheduler.TickAsync(ct);

        Assert.Equal([job.Id], this.runner.Runs);
        Assert.Equal(new JobState(job.Id, "daily at 09:00|UTC", Monday0800.AddHours(25), this.clock.UtcNow, JobOutcome.Delivered), this.states.Get(job.Id));
    }

    /// <summary>A scheduler started again after a run does not run the Job again.</summary>
    [Fact]
    public async Task Restart_AfterARun_DoesNotRunTwice()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _ = this.Add(JobRecurrence.Daily(new TimeOnly(9, 0)));
        await this.scheduler.TickAsync(ct);
        this.clock.UtcNow = Monday0800.AddMinutes(60.5);
        await this.scheduler.TickAsync(ct);

        this.scheduler.Dispose();
        this.scheduler = this.NewScheduler();
        await this.scheduler.TickAsync(ct);

        Assert.Single(this.runner.Runs);
    }

    /// <summary>With <c>Team:Jobs:RunOverdue</c> off, a run missed by more than the grace period, as when Huddle was stopped, is skipped to the next run.</summary>
    [Fact]
    public async Task MissedWhileStopped_IsSkipped()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.options.Value.Jobs.RunOverdue = false;
        Job job = this.Add(JobRecurrence.Daily(new TimeOnly(9, 0)));
        await this.scheduler.TickAsync(ct);

        this.clock.UtcNow = Monday0800.AddMinutes(65);
        await this.scheduler.TickAsync(ct);

        Assert.Empty(this.runner.Runs);
        Assert.Equal(Monday0800.AddHours(25), this.states.Get(job.Id)?.NextRunUtc);
    }

    /// <summary>A run that throws keeps its due time, so the next minute tries again and succeeds.</summary>
    [Fact]
    public async Task FailedRun_IsTriedAgainNextMinute()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Job job = this.Add(JobRecurrence.Daily(new TimeOnly(9, 0)));
        await this.scheduler.TickAsync(ct);

        this.runner.FailNext = true;
        this.clock.UtcNow = Monday0800.AddMinutes(60.5);
        await this.scheduler.TickAsync(ct);
        Assert.Equal((Monday0800.AddHours(1), JobOutcome.Failed), (this.states.Get(job.Id)?.NextRunUtc, this.states.Get(job.Id)?.LastOutcome));

        this.clock.UtcNow = Monday0800.AddMinutes(61.5);
        await this.scheduler.TickAsync(ct);

        Assert.Equal([job.Id], this.runner.Runs);
        Assert.Equal(JobOutcome.Delivered, this.states.Get(job.Id)?.LastOutcome);
    }

    /// <summary>A paused Job never runs.</summary>
    [Fact]
    public async Task Paused_NeverRuns()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Job job = this.Add(JobRecurrence.Daily(new TimeOnly(9, 0)));
        await this.scheduler.TickAsync(ct);
        Assert.True(this.store.Update(job with { Paused = true }));

        this.clock.UtcNow = Monday0800.AddMinutes(60.5);
        await this.scheduler.TickAsync(ct);

        Assert.Empty(this.runner.Runs);
    }

    /// <summary>A one-off that has run leaves its owner's schedule and its state is forgotten.</summary>
    [Fact]
    public async Task OneOff_LeavesTheScheduleAfterRunning()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Job job = this.Add(JobRecurrence.Once(new DateOnly(2026, 11, 2), new TimeOnly(9, 0)));
        await this.scheduler.TickAsync(ct);

        this.clock.UtcNow = Monday0800.AddMinutes(60.5);
        await this.scheduler.TickAsync(ct);

        Assert.Equal([job.Id], this.runner.Runs);
        Assert.Null(this.store.Find(job.Id));
        Assert.Null(this.states.Get(job.Id));
    }

    /// <summary>A changed timetable is worked out again rather than running at the old time.</summary>
    [Fact]
    public async Task EditedTimetable_IsWorkedOutAgain()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Job job = this.Add(JobRecurrence.Daily(new TimeOnly(9, 0)));
        await this.scheduler.TickAsync(ct);

        Assert.True(this.store.Update(job with { When = JobRecurrence.Daily(new TimeOnly(10, 0)) }));
        await this.scheduler.TickAsync(ct);
        this.clock.UtcNow = Monday0800.AddMinutes(60.5);
        await this.scheduler.TickAsync(ct);

        Assert.Empty(this.runner.Runs);
        Assert.Equal(Monday0800.AddHours(2), this.states.Get(job.Id)?.NextRunUtc);
    }

    /// <summary>With Jobs switched off nothing is looked at or run.</summary>
    [Fact]
    public async Task Disabled_RunsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.options.Value.Jobs.Enabled = false;
        Job job = this.Add(JobRecurrence.EveryHours(1));

        await this.scheduler.TickAsync(ct);

        Assert.Null(this.states.Get(job.Id));
    }

    /// <summary>Run now runs the Job at once and leaves its next run where it was.</summary>
    [Fact]
    public async Task RunNow_RunsAndKeepsTheNextRun()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Job job = this.Add(JobRecurrence.Daily(new TimeOnly(9, 0)));
        await this.scheduler.TickAsync(ct);

        Assert.Equal(JobOutcome.Delivered, await this.scheduler.RunNowAsync(job.Id, ct));

        Assert.Equal([job.Id], this.runner.Runs);
        Assert.Equal(new JobState(job.Id, "daily at 09:00|UTC", Monday0800.AddHours(1), Monday0800, JobOutcome.Delivered), this.states.Get(job.Id));
        Assert.Null(await this.scheduler.RunNowAsync("job-none", ct));
    }

    private Job Add(JobRecurrence when) =>
        this.store.Add(new Job(string.Empty, JobOwner.Agent("Nova"), "Digest", "Do it.", when, "UTC", "You", false));

    private JobScheduler NewScheduler() =>
        new(this.store, this.states, this.runner, this.options, NullLogger<JobScheduler>.Instance, new NoNotice(), this.clock);

    /// <summary>A trace writer that writes nothing: these tests do not look at give-ups.</summary>
    private sealed class NoNotice : IJobGaveUpNotice
    {
        public Task NoteAsync(Job job, string reason, CancellationToken ct) => Task.CompletedTask;
    }

    /// <summary>Records each run, and throws once when asked to.</summary>
    private sealed class RecordingRunner : IJobRunner
    {
        private readonly List<string> runs = [];

        public IReadOnlyList<string> Runs => this.runs;

        public bool FailNext { get; set; }

        public Task<JobOutcome> RunAsync(Job job, DateTimeOffset ranAt, DateTimeOffset? lateDue, CancellationToken ct)
        {
            if (this.FailNext)
            {
                this.FailNext = false;
                throw new InvalidOperationException("The post failed.");
            }

            this.runs.Add(job.Id);
            return Task.FromResult(JobOutcome.Delivered);
        }
    }
}

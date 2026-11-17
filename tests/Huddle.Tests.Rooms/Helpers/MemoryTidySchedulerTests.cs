using System.Globalization;
using Agency.Huddle.App;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Helpers.MemoryTidy;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Elicitation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Helpers;

/// <summary>
/// <see cref="MemoryTidyScheduler"/> (Memory Tidy spec §6.7 and §8.1, "The timetable"): each tick is driven through <c>TickAsync</c> with a fake
/// runner and a manual clock, and every row names the <c>DueDecision</c> it expects.
/// </summary>
public sealed class MemoryTidySchedulerTests : IDisposable
{
    private const string WeeklyWhen = "weekly on sun at 03:00";

    /// <summary>A Saturday, the day before the Sunday 03:00 (01:00 UTC) pass in the test zone.</summary>
    private static readonly DateTimeOffset Saturday = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Sunday0100 = new(2026, 11, 22, 1, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset NextSunday0100 = Sunday0100.AddDays(7);
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.CreateCustomTimeZone("MemoryTidyTestZone", TimeSpan.FromHours(2), "Test", "Test");

    private readonly TempDataDir dir = new();
    private readonly IOptions<TeamOptions> options;
    private readonly MemoryTidyScheduleStore store;
    private readonly FakeRunner runner;
    private readonly RecordingLogger<MemoryTidyScheduler> logger = new();
    private readonly ManualTimeProvider clock = new(Saturday, Zone);

    /// <summary>Opens the store over a temp data folder.</summary>
    public MemoryTidySchedulerTests()
    {
        this.options = this.dir.Options();
        this.store = new MemoryTidyScheduleStore(this.options);
        this.runner = new FakeRunner();
    }

    /// <summary>Deletes the temp folder.</summary>
    public void Dispose() => this.dir.Dispose();

    /// <summary>FirstSight: with no row the tick stores the next run and starts nothing.</summary>
    [Fact]
    public async Task NoRow_FirstSight_StoresOnly()
    {
        MemoryTidyScheduler scheduler = this.NewScheduler(WeeklyWhen);

        await scheduler.TickAsync(TestContext.Current.CancellationToken);

        Assert.Empty(this.runner.Starts);
        MemoryTidyScheduleRow? row = this.store.Get(MemoryTidyScheduleStore.Weekly);
        Assert.Equal(Sunday0100, row?.State.NextRunUtc);
        Assert.Equal(Sunday0100, scheduler.NextRun);
        Assert.False(row?.State.EverRan);
    }

    /// <summary>Run, Late false: a run due within two minutes starts a Timetable pass and moves the next run on a week.</summary>
    [Fact]
    public async Task DueWithinGrace_Run_StartsTimetable()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        MemoryTidyScheduler scheduler = this.NewScheduler(WeeklyWhen);
        await scheduler.TickAsync(ct);

        this.clock.UtcNow = Sunday0100.AddSeconds(90);
        await scheduler.TickAsync(ct);

        Assert.Equal([MemoryTidyStart.Timetable], this.runner.Starts);
        MemoryTidyScheduleRow? row = this.store.Get(MemoryTidyScheduleStore.Weekly);
        Assert.Equal(NextSunday0100, row?.State.NextRunUtc);
        Assert.True(row?.State.EverRan);
    }

    /// <summary>Run, Late true: three weeks overdue runs one pass, started as Overdue, not three.</summary>
    [Fact]
    public async Task ThreeWeeksOverdue_RunLate_StartsOneOverdue()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.clock.UtcNow = Saturday.AddDays(-20);
        MemoryTidyScheduler scheduler = this.NewScheduler(WeeklyWhen);
        await scheduler.TickAsync(ct);

        this.clock.UtcNow = Saturday.AddDays(1);
        await scheduler.TickAsync(ct);
        await scheduler.TickAsync(ct);

        Assert.Equal([MemoryTidyStart.Overdue], this.runner.Starts);
        Assert.Equal(NextSunday0100, this.store.Get(MemoryTidyScheduleStore.Weekly)?.State.NextRunUtc);
    }

    /// <summary>Skip: overdue with the regular run under an hour away starts nothing and stores the moved state.</summary>
    [Fact]
    public async Task OverdueWithRegularRunNear_Skip_StartsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        MemoryTidyScheduler scheduler = this.NewScheduler("daily at 03:00");
        await scheduler.TickAsync(ct);
        DateTimeOffset due = this.store.Get(MemoryTidyScheduleStore.Weekly)?.State.NextRunUtc ?? default;

        this.clock.UtcNow = due.AddDays(1).AddMinutes(-30);
        await scheduler.TickAsync(ct);

        Assert.Empty(this.runner.Starts);
        Assert.Equal(due.AddDays(1), this.store.Get(MemoryTidyScheduleStore.Weekly)?.State.NextRunUtc);
    }

    /// <summary>FirstSight: a changed When is a new baseline, and nothing starts even though the old run was due.</summary>
    [Fact]
    public async Task ChangedWhen_FirstSight_StartsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await this.NewScheduler(WeeklyWhen).TickAsync(ct);
        this.clock.UtcNow = Sunday0100;

        await this.NewScheduler("daily at 03:00").TickAsync(ct);

        Assert.Empty(this.runner.Starts);
        MemoryTidyScheduleRow? row = this.store.Get(MemoryTidyScheduleStore.Weekly);
        Assert.Equal("daily at 03:00|MemoryTidyTestZone", row?.State.WhenKey);
        Assert.Equal(Sunday0100.AddDays(1), row?.State.NextRunUtc);
    }

    /// <summary>Run: what the decision carries is stored before the pass does anything, and the result is stored after.</summary>
    [Fact]
    public async Task Run_StoresBeforeThePassStarts()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        MemoryTidyScheduler scheduler = this.NewScheduler(WeeklyWhen);
        await scheduler.TickAsync(ct);
        this.clock.UtcNow = Sunday0100.AddSeconds(30);
        TaskCompletionSource<MemoryTidyPassEnd> pass = new(TaskCreationOptions.RunContinuationsAsynchronously);
        this.runner.Next = pass.Task;

        Task tick = scheduler.TickAsync(ct);

        Assert.False(tick.IsCompleted);
        MemoryTidyScheduleRow? during = this.store.Get(MemoryTidyScheduleStore.Weekly);
        Assert.Equal(NextSunday0100, during?.State.NextRunUtc);
        Assert.False(during?.State.EverRan);
        pass.SetResult(MemoryTidyPassEnd.Done);
        await tick;
        Assert.True(this.store.Get(MemoryTidyScheduleStore.Weekly)?.State.EverRan);
    }

    /// <summary>A busy runner (null answer) stores nothing, and the next tick decides again.</summary>
    [Fact]
    public async Task BusyRunner_StoresNothing_NextTickDecidesAgain()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        MemoryTidyScheduler scheduler = this.NewScheduler(WeeklyWhen);
        await scheduler.TickAsync(ct);
        MemoryTidyScheduleRow? before = this.store.Get(MemoryTidyScheduleStore.Weekly);
        this.clock.UtcNow = Sunday0100.AddSeconds(30);
        this.runner.Busy = true;

        await scheduler.TickAsync(ct);
        this.clock.Advance(TimeSpan.FromMinutes(1));
        await scheduler.TickAsync(ct);

        Assert.Equal(2, this.runner.Starts.Count);
        Assert.Equal(before, this.store.Get(MemoryTidyScheduleStore.Weekly));
    }

    /// <summary>Done, Cap and ThreeFailures all end as Ran: the failed tries are zero and the pass is marked as having run.</summary>
    [Theory]
    [InlineData("Done")]
    [InlineData("Cap")]
    [InlineData("ThreeFailures")]
    public async Task PassEnds_StoreAfterRunWithRan(string endName)
    {
        MemoryTidyPassEnd end = Enum.Parse<MemoryTidyPassEnd>(endName);
        CancellationToken ct = TestContext.Current.CancellationToken;
        MemoryTidyScheduler scheduler = this.NewScheduler(WeeklyWhen);
        await scheduler.TickAsync(ct);
        this.clock.UtcNow = Sunday0100.AddSeconds(30);
        this.runner.Next = Task.FromResult(end);

        await scheduler.TickAsync(ct);

        MemoryTidyScheduleRow? row = this.store.Get(MemoryTidyScheduleStore.Weekly);
        Assert.True(row?.State.EverRan);
        Assert.Equal(0, row?.State.FailedTries);
        Assert.Equal(this.clock.UtcNow, row?.LastRunUtc);
        Assert.Equal(end == MemoryTidyPassEnd.Cap, this.store.Get(MemoryTidyScheduleStore.Resume) is not null);
    }

    /// <summary>Threw is stored as Failed, and the next tick is GiveUp: it stores, starts nothing and logs a Warning.</summary>
    [Fact]
    public async Task Threw_StoresFailed_NextTickGivesUp()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        MemoryTidyScheduler scheduler = this.NewScheduler(WeeklyWhen);
        await scheduler.TickAsync(ct);
        this.clock.UtcNow = Sunday0100.AddSeconds(30);
        this.runner.Next = Task.FromResult(MemoryTidyPassEnd.Threw);
        await scheduler.TickAsync(ct);
        MemoryTidyScheduleRow? failed = this.store.Get(MemoryTidyScheduleStore.Weekly);
        Assert.Equal(1, failed?.State.FailedTries);
        Assert.False(failed?.State.EverRan);

        this.clock.Advance(TimeSpan.FromMinutes(1));
        await scheduler.TickAsync(ct);

        Assert.Single(this.runner.Starts);
        MemoryTidyScheduleRow? gaveUp = this.store.Get(MemoryTidyScheduleStore.Weekly);
        Assert.Equal(0, gaveUp?.State.FailedTries);
        Assert.Equal(NextSunday0100, gaveUp?.State.NextRunUtc);
        Assert.Single(this.logger.AtExactly(LogLevel.Warning));
    }

    /// <summary>Stopped stores nothing more: the row from before the pass stays.</summary>
    [Fact]
    public async Task Stopped_StoresNothingMore()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        MemoryTidyScheduler scheduler = this.NewScheduler(WeeklyWhen);
        await scheduler.TickAsync(ct);
        this.clock.UtcNow = Sunday0100.AddSeconds(30);
        this.runner.Next = Task.FromResult(MemoryTidyPassEnd.Stopped);

        await scheduler.TickAsync(ct);

        MemoryTidyScheduleRow? row = this.store.Get(MemoryTidyScheduleStore.Weekly);
        Assert.Equal(NextSunday0100, row?.State.NextRunUtc);
        Assert.False(row?.State.EverRan);
        Assert.Null(row?.LastRunUtc);
        Assert.Null(this.store.Get(MemoryTidyScheduleStore.Resume));
    }

    /// <summary>Cap from a pass the scheduler started writes the resume row; after local midnight its Run starts Continuation and the row is gone.</summary>
    [Fact]
    public async Task Cap_WritesResume_ContinuationRunsAfterMidnight_AndRowIsGone()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        MemoryTidyScheduler scheduler = this.NewScheduler(WeeklyWhen);
        await scheduler.TickAsync(ct);
        this.clock.UtcNow = Sunday0100.AddSeconds(30);
        this.runner.Next = Task.FromResult(MemoryTidyPassEnd.Cap);
        await scheduler.TickAsync(ct);
        DateTimeOffset localMidnight = new(2026, 11, 22, 22, 0, 0, TimeSpan.Zero);
        MemoryTidyScheduleRow? resume = this.store.Get(MemoryTidyScheduleStore.Resume);
        Assert.Equal(localMidnight, resume?.State.NextRunUtc);
        Assert.Equal("resume|" + localMidnight.ToString("O", CultureInfo.InvariantCulture), resume?.State.WhenKey);
        Assert.False(resume?.State.EverRan);

        this.clock.UtcNow = localMidnight.AddHours(-1);
        await scheduler.TickAsync(ct);
        Assert.Single(this.runner.Starts);

        this.clock.UtcNow = localMidnight.AddSeconds(30);
        this.runner.Next = Task.FromResult(MemoryTidyPassEnd.Done);
        await scheduler.TickAsync(ct);

        Assert.Equal([MemoryTidyStart.Timetable, MemoryTidyStart.Continuation], this.runner.Starts);
        Assert.Null(this.store.Get(MemoryTidyScheduleStore.Resume));
    }

    /// <summary>A continuation that meets the cap again writes a new resume row for the day after.</summary>
    [Fact]
    public async Task Continuation_CapAgain_WritesTheNextDay()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        MemoryTidyScheduler scheduler = this.NewScheduler(WeeklyWhen);
        await scheduler.TickAsync(ct);
        this.clock.UtcNow = Sunday0100.AddSeconds(30);
        this.runner.Next = Task.FromResult(MemoryTidyPassEnd.Cap);
        await scheduler.TickAsync(ct);
        DateTimeOffset localMidnight = new(2026, 11, 22, 22, 0, 0, TimeSpan.Zero);

        this.clock.UtcNow = localMidnight.AddSeconds(30);
        await scheduler.TickAsync(ct);

        Assert.Equal(localMidnight.AddDays(1), this.store.Get(MemoryTidyScheduleStore.Resume)?.State.NextRunUtc);
    }

    /// <summary>A pass that ends Done writes no resume row, and a tick with nothing due writes none either (Tidy now is never the scheduler's).</summary>
    [Fact]
    public async Task NoCap_WritesNoResume()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        MemoryTidyScheduler scheduler = this.NewScheduler(WeeklyWhen);
        await scheduler.TickAsync(ct);
        this.clock.UtcNow = Sunday0100.AddSeconds(30);

        await scheduler.TickAsync(ct);

        Assert.Single(this.runner.Starts);
        Assert.Null(this.store.Get(MemoryTidyScheduleStore.Resume));
    }

    /// <summary>Every When the timetable does not accept schedules nothing and logs one Error, however many ticks follow.</summary>
    [Theory]
    [InlineData("once on 2026-12-01 at 03:00")]
    [InlineData("every 4 hours")]
    [InlineData("weekdays at 09:00")]
    [InlineData("monthly on 15 at 09:00")]
    [InlineData("whenever you like")]
    public async Task UnacceptedWhen_SchedulesNothing_LogsOneError(string when)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        MemoryTidyScheduler scheduler = this.NewScheduler(when);

        await scheduler.TickAsync(ct);
        this.clock.UtcNow = Sunday0100.AddDays(30);
        await scheduler.TickAsync(ct);

        Assert.Empty(this.runner.Starts);
        Assert.Null(this.store.Get(MemoryTidyScheduleStore.Weekly));
        Assert.Single(this.logger.AtExactly(LogLevel.Error));
    }

    private MemoryTidyScheduler NewScheduler(string when)
    {
        this.options.Value.Memory.Tidy.When = when;
        return new MemoryTidyScheduler(this.runner, this.store, this.options, this.logger, this.clock);
    }

    /// <summary>A runner that records what it was asked and answers from <see cref="Next"/> or with a busy null.</summary>
    private sealed class FakeRunner : IMemoryTidyRunner
    {
        /// <summary>What every <c>TryStart</c> was asked.</summary>
        public List<MemoryTidyStart> Starts { get; } = [];

        /// <summary>Whether to answer null, as a Tidy now pass in progress does.</summary>
        public bool Busy { get; set; }

        /// <summary>The pass's end; Done by default.</summary>
        public Task<MemoryTidyPassEnd> Next { get; set; } = Task.FromResult(MemoryTidyPassEnd.Done);

        /// <inheritdoc />
        public Task<MemoryTidyPassEnd>? TryStart(MemoryTidyStart start)
        {
            this.Starts.Add(start);
            return this.Busy ? null : this.Next;
        }
    }
}

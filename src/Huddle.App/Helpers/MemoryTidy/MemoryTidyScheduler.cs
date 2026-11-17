using System.Globalization;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Jobs;
using Agency.Huddle.App.Scheduling;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Helpers.MemoryTidy;

/// <summary>
/// Starts Memory Tidy's weekly pass when it is due, and goes on the next day after a pass that ended at the Helper's daily cap (spec §6.7,
/// "The timetable"). It holds no due-time rule: <see cref="DueCheck"/> decides each tick, and this class stores what the decision carries
/// before a pass starts, so a restart never starts one twice.
/// </summary>
/// <param name="runner">Runs a pass.</param>
/// <param name="store">Remembers the <c>weekly</c> and <c>resume</c> rows.</param>
/// <param name="options">Supplies <c>When</c>.</param>
/// <param name="logger">Reports a timetable it cannot use and a due time it gave up on.</param>
/// <param name="timeProvider">The clock, its local time zone and the timer's clock; defaults to the system clock.</param>
internal sealed class MemoryTidyScheduler(
    IMemoryTidyRunner runner,
    MemoryTidyScheduleStore store,
    IOptions<TeamOptions> options,
    ILogger<MemoryTidyScheduler> logger,
    TimeProvider? timeProvider = null) : BackgroundService
{
    /// <summary>How long after start-up the first tick waits, so a pass does not compete with Agents connecting.</summary>
    internal static readonly TimeSpan FirstTickDelay = TimeSpan.FromMinutes(5);

    private const string ResumePrefix = "resume|";

    private static readonly TimeSpan Period = TimeSpan.FromMinutes(1);
    private static readonly DuePolicy WeeklyPolicy = new(TimeSpan.FromMinutes(2), true, null, TimeSpan.FromHours(1), 1);
    private static readonly DuePolicy ResumePolicy = WeeklyPolicy with { MergeWindow = TimeSpan.Zero };

    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private bool parsed;
    private string whenText = string.Empty;
    private JobRecurrence? recurrence;

    /// <summary>The weekly timetable's next run, for the card; <see langword="null"/> when none is scheduled yet.</summary>
    public DateTimeOffset? NextRun => store.Get(MemoryTidyScheduleStore.Weekly)?.State.NextRunUtc;

    /// <summary>Runs one look at both timetables; the timer calls it each minute.</summary>
    /// <param name="ct">Stops the tick.</param>
    internal async Task TickAsync(CancellationToken ct)
    {
        this.EnsureParsed();
        if (this.recurrence is not null)
        {
            await this.TickWeeklyAsync(this.recurrence, ct).ConfigureAwait(false);
        }

        await this.TickResumeAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this.EnsureParsed();
        await Task.Delay(FirstTickDelay, this.clock, stoppingToken).ConfigureAwait(false);
        using PeriodicTimer timer = new(Period, this.clock);
        do
        {
            try
            {
                await this.TickAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The supervisor loop must not die: log and try again on the next minute.
                logger.LogError(ex, "A Memory Tidy timetable tick failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    private void EnsureParsed()
    {
        if (this.parsed)
        {
            return;
        }

        this.parsed = true;
        string when = options.Value.Memory.Tidy.When;
        if (!JobRecurrence.TryParse(when, out JobRecurrence? parsedRecurrence, out string? error))
        {
            logger.LogError("Memory Tidy's When '{When}' cannot be read, so no pass is scheduled: {Error}", when, error);
            return;
        }

        if (parsedRecurrence.Cadence is not (JobCadence.Weekly or JobCadence.Daily))
        {
            logger.LogError("Memory Tidy's When '{When}' must be weekly or daily, so no pass is scheduled.", when);
            return;
        }

        this.whenText = when;
        this.recurrence = parsedRecurrence;
    }

    private async Task TickWeeklyAsync(JobRecurrence timetable, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        TimeZoneInfo zone = this.clock.LocalTimeZone;
        DateTimeOffset now = this.clock.GetUtcNow();
        MemoryTidyScheduleRow? row = store.Get(MemoryTidyScheduleStore.Weekly);
        string key = this.whenText + "|" + zone.Id;
        DueDecision decision = DueCheck.Next(row?.State, key, t => timetable.NextAfter(t, zone), false, now, WeeklyPolicy);
        await this.ApplyAsync(MemoryTidyScheduleStore.Weekly, row, decision, false, ct).ConfigureAwait(false);
    }

    private async Task TickResumeAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        MemoryTidyScheduleRow? row = store.Get(MemoryTidyScheduleStore.Resume);
        if (row is null)
        {
            return;
        }

        if (row.State.NextRunUtc is null || !TryReadResumeTime(row.State.WhenKey, out DateTimeOffset resumeUtc))
        {
            // A continuation a stop interrupted (its run was stored first) or a row nothing can read: it is not started again.
            store.Delete(MemoryTidyScheduleStore.Resume);
            return;
        }

        DateTimeOffset now = this.clock.GetUtcNow();
        DueDecision decision = DueCheck.Next(row.State, row.State.WhenKey, t => t < resumeUtc ? resumeUtc : null, true, now, ResumePolicy);
        await this.ApplyAsync(MemoryTidyScheduleStore.Resume, row, decision, true, ct).ConfigureAwait(false);
    }

    private async Task ApplyAsync(string name, MemoryTidyScheduleRow? row, DueDecision decision, bool oneOff, CancellationToken ct)
    {
        switch (decision)
        {
            case DueDecision.FirstSight first:
                store.Put(new MemoryTidyScheduleRow(name, first.Save, row?.LastRunUtc));
                break;
            case DueDecision.Skip skip:
                store.Put(new MemoryTidyScheduleRow(name, skip.Save, row?.LastRunUtc));
                break;
            case DueDecision.GiveUp giveUp:
                logger.LogWarning("Memory Tidy gave up on the '{Timetable}' pass that was due after it failed; the next regular time stands.", name);
                this.StoreOrRemove(name, giveUp.Save, row?.LastRunUtc, giveUp.Remove);
                break;
            case DueDecision.Run run:
                await this.RunAsync(name, row, run, StartFor(run, oneOff), oneOff, ct).ConfigureAwait(false);
                break;
            default:
                break;
        }
    }

    /// <summary>What asks the runner: the resume row always continues; the weekly row is Overdue exactly when the decision is Late.</summary>
    private static MemoryTidyStart StartFor(DueDecision.Run run, bool oneOff)
    {
        if (oneOff)
        {
            return MemoryTidyStart.Continuation;
        }

        return run.Late ? MemoryTidyStart.Overdue : MemoryTidyStart.Timetable;
    }

    private async Task RunAsync(string name, MemoryTidyScheduleRow? row, DueDecision.Run run, MemoryTidyStart start, bool oneOff, CancellationToken ct)
    {
        Task<MemoryTidyPassEnd>? pass = runner.TryStart(start);
        if (pass is null)
        {
            // A Tidy now pass is running or the host is stopping: store nothing, the next tick decides again.
            return;
        }

        store.Put(new MemoryTidyScheduleRow(name, run.Save, row?.LastRunUtc));
        MemoryTidyPassEnd end = await pass.WaitAsync(ct).ConfigureAwait(false);
        if (end == MemoryTidyPassEnd.Stopped)
        {
            return;
        }

        DueRunResult result = end == MemoryTidyPassEnd.Threw ? DueRunResult.Failed : DueRunResult.Ran;
        DueState after = DueCheck.AfterRun(run.Save, row?.State ?? run.Save, result);
        DateTimeOffset ended = this.clock.GetUtcNow();
        this.StoreOrRemove(name, after, ended, oneOff && after.NextRunUtc is null);
        if (end == MemoryTidyPassEnd.Cap)
        {
            this.WriteResume(ended);
        }
    }

    private void StoreOrRemove(string name, DueState state, DateTimeOffset? lastRunUtc, bool remove)
    {
        if (remove)
        {
            store.Delete(name);
            return;
        }

        store.Put(new MemoryTidyScheduleRow(name, state, lastRunUtc));
    }

    /// <summary>Writes the row that goes on at the first minute of the next local day.</summary>
    private void WriteResume(DateTimeOffset ended)
    {
        TimeZoneInfo zone = this.clock.LocalTimeZone;
        DateTime midnight = TimeZoneInfo.ConvertTime(ended, zone).Date.AddDays(1);
        if (zone.IsInvalidTime(midnight))
        {
            midnight = midnight.AddHours(1);
        }

        DateTimeOffset resumeUtc = new(TimeZoneInfo.ConvertTimeToUtc(midnight, zone), TimeSpan.Zero);
        DueState state = new(ResumePrefix + resumeUtc.ToString("O", CultureInfo.InvariantCulture), resumeUtc);
        store.Put(new MemoryTidyScheduleRow(MemoryTidyScheduleStore.Resume, state, null));
    }

    private static bool TryReadResumeTime(string key, out DateTimeOffset resumeUtc)
    {
        resumeUtc = default;
        return key.StartsWith(ResumePrefix, StringComparison.Ordinal)
            && DateTimeOffset.TryParse(key[ResumePrefix.Length..], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out resumeUtc);
    }
}

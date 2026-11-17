using System.Globalization;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Scheduling;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Jobs;

/// <summary>
/// Runs every Job when it is due, checking once a minute like <c>CadenceService</c>. The next run lives in <c>team.db</c>
/// (<see cref="JobStateStore"/>) and is saved before a run posts, so a restart never runs a Job twice. A Job seen for the
/// first time, or whose timetable was changed, only has its next run worked out. The decision is <see cref="DueCheck"/>'s: a run missed by
/// more than <see cref="Grace"/>, because Huddle was stopped or asleep, runs once, late (spec §8), unless <c>Team:Jobs:RunOverdue</c> is off.
/// </summary>
/// <param name="store">Lists the Jobs.</param>
/// <param name="states">Remembers each Job's next and last run.</param>
/// <param name="runner">Runs a Job.</param>
/// <param name="options">Says whether Jobs are enabled.</param>
/// <param name="logger">Records a Job that failed or was skipped.</param>
/// <param name="gaveUp">Leaves a trace when a one-off is removed without posting.</param>
/// <param name="timeProvider">The clock, its local time zone and the timer's clock; defaults to the system clock.</param>
internal sealed partial class JobScheduler(
    JobStore store,
    JobStateStore states,
    IJobRunner runner,
    IOptions<TeamOptions> options,
    ILogger<JobScheduler> logger,
    IJobGaveUpNotice gaveUp,
    TimeProvider? timeProvider = null) : BackgroundService
{
    /// <summary>How late a run may still happen: two ticks, so a run is never lost to timer jitter but never sent long after.</summary>
    internal static readonly TimeSpan Grace = TimeSpan.FromMinutes(2);

    /// <summary>A late recurring run whose next regular run is closer than this is skipped in its favour.</summary>
    private static readonly TimeSpan MergeWindow = TimeSpan.FromHours(1);

    private static readonly TimeSpan Period = TimeSpan.FromMinutes(1);

    private const int MaxFailedTries = 3;

    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    /// <summary>The zone a Job's times are in when it names none: this machine's.</summary>
    /// <param name="job">The Job.</param>
    /// <returns>The zone.</returns>
    public TimeZoneInfo ZoneOf(Job job)
    {
        ArgumentNullException.ThrowIfNull(job);
        return JobTimeZones.Resolve(job.TimeZoneId, this.clock.LocalTimeZone);
    }

    /// <summary>A Job's firing state, or <see langword="null"/> when it has not been seen yet.</summary>
    /// <param name="job">The Job.</param>
    /// <returns>The state.</returns>
    public JobState? StateOf(Job job)
    {
        ArgumentNullException.ThrowIfNull(job);
        return states.Get(job.Id);
    }

    /// <summary>When a Job runs next: its stored next run while its timetable is unchanged, otherwise worked out from now.</summary>
    /// <param name="job">The Job.</param>
    /// <returns>The next run, or <see langword="null"/> when it is paused or a one-off whose time has passed.</returns>
    public DateTimeOffset? NextRunOf(Job job)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (job.Paused)
        {
            return null;
        }

        JobState? state = states.Get(job.Id);
        DateTimeOffset now = this.clock.GetUtcNow();
        return state is not null && string.Equals(state.WhenKey, KeyOf(job), StringComparison.Ordinal) && state.NextRunUtc > now
            ? state.NextRunUtc
            : job.When.NextAfter(now, this.ZoneOf(job));
    }

    /// <summary>Writes a new Job's state (and a zero retry count) so a one-off due inside the minute is not lost.</summary>
    /// <param name="job">The new Job.</param>
    public void Seed(Job job)
    {
        ArgumentNullException.ThrowIfNull(job);
        states.Set(new JobState(job.Id, KeyOf(job), job.When.NextAfter(this.clock.GetUtcNow(), this.ZoneOf(job)), null, null));
        states.SetTries(job.Id, 0, false);
    }

    /// <summary>Runs a Job now, outside its timetable, leaving its next run as it was.</summary>
    /// <param name="jobId">The Job's id.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>What the run did, or <see langword="null"/> when there is no such Job.</returns>
    public async Task<JobOutcome?> RunNowAsync(string jobId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(jobId);
        if (store.Find(jobId) is not { } job)
        {
            return null;
        }

        DateTimeOffset now = this.clock.GetUtcNow();
        JobOutcome outcome = await runner.RunAsync(job, now, ct).ConfigureAwait(false);
        JobState current = states.Get(job.Id) ?? new JobState(job.Id, KeyOf(job), job.When.NextAfter(now, this.ZoneOf(job)), null, null);
        states.Set(current with { LastRunUtc = now, LastOutcome = outcome });
        return outcome;
    }

    /// <summary>Deletes a Job and forgets its state. Its Room stays, with its history.</summary>
    /// <param name="jobId">The Job's id.</param>
    /// <returns><see langword="true"/> when the Job existed.</returns>
    public bool Delete(string jobId)
    {
        ArgumentNullException.ThrowIfNull(jobId);
        if (store.Find(jobId) is not { } job || !store.Remove(job.Id))
        {
            return false;
        }

        states.Remove(job.Id);
        return true;
    }

    /// <summary>Runs one pass over every Job; the timer calls it each minute.</summary>
    /// <param name="ct">Stops the pass.</param>
    internal async Task TickAsync(CancellationToken ct)
    {
        if (!options.Value.Jobs.Enabled)
        {
            return;
        }

        DateTimeOffset now = this.clock.GetUtcNow();
        foreach (Job job in store.Jobs)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await this.TickJobAsync(job, now, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One Job's failure (a refused file, a lost Room) must not stop the others: log it and go on.
                LogTickFailed(logger, ex, job.Id);
            }
        }
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
                LogPassFailed(logger, ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    /// <summary>What a stored next run was worked out from; a change means the timetable was edited. The seeder stores the same key.</summary>
    /// <param name="job">The Job.</param>
    /// <returns>The key kept in <c>job_state.when_key</c>.</returns>
    internal static string KeyOf(Job job) => job.When + "|" + (job.TimeZoneId ?? string.Empty);

    private async Task TickJobAsync(Job job, DateTimeOffset now, CancellationToken ct)
    {
        if (job.Paused)
        {
            this.MovePaused(job, now);
            return;
        }

        TimeZoneInfo zone = this.ZoneOf(job);
        string key = KeyOf(job);
        JobState? stored = states.Get(job.Id);
        DueState? before = DueStateOf(stored, job.Id);
        bool oneOff = job.When.Cadence == JobCadence.Once;
        DuePolicy policy = this.PolicyOf(job);
        DueDecision decision = DueCheck.Next(before, key, after => job.When.NextAfter(after, zone), oneOff, now, policy);
        switch (decision)
        {
            case DueDecision.FirstSight { Save.NextRunUtc: null } when oneOff && policy.RunLate:
                // A one-off first seen later than the overdue window has no next run: it gives up now instead of sitting in schedule.md forever.
                await this.RemoveWithTraceAsync(job, "it was more than " + Describe(policy.MaxLateness ?? TimeSpan.Zero) + " late", ct).ConfigureAwait(false);
                break;
            case DueDecision.FirstSight first:
                this.Save(job, stored, first.Save, null, null);
                break;
            case DueDecision.Skip skip:
                LogMissed(logger, job.Id, before?.NextRunUtc ?? now);
                this.Save(job, stored, skip.Save, null, null);
                break;
            case DueDecision.GiveUp giveUp:
                await this.GiveUpAsync(job, stored, giveUp, before, policy, ct).ConfigureAwait(false);
                break;
            case DueDecision.Run run:
                await this.RunDueAsync(job, stored, before, run, now, ct).ConfigureAwait(false);
                break;
            default:
                // NotDue: nothing to do and nothing to save.
                break;
        }
    }

    /// <summary>The limits a Job's due times are judged by. With the Turn Queue off an Agent's overdue post would be lost to an offline Agent, so only the Human's Job runs late (spec §8.4).</summary>
    private DuePolicy PolicyOf(Job job)
    {
        TeamOptions team = options.Value;
        bool runLate = team.Jobs.RunOverdue && (team.TurnQueue.Enabled || job.Owner.IsHuman);
        TimeSpan? within = team.Jobs.OverdueOneOffWithin > TimeSpan.Zero ? team.Jobs.OverdueOneOffWithin : null;
        return new DuePolicy(Grace, runLate, within, MergeWindow, MaxFailedTries);
    }

    private DueState? DueStateOf(JobState? stored, string jobId)
    {
        if (stored is null)
        {
            return null;
        }

        (int failedTries, bool everRan) = states.GetTries(jobId);
        return new DueState(stored.WhenKey, stored.NextRunUtc, failedTries, everRan);
    }

    private void Save(Job job, JobState? stored, DueState save, DateTimeOffset? lastRun, JobOutcome? outcome)
    {
        states.Set(new JobState(job.Id, save.WhenKey, save.NextRunUtc, lastRun ?? stored?.LastRunUtc, outcome ?? stored?.LastOutcome));
        states.SetTries(job.Id, save.FailedTries, save.EverRan);
    }

    /// <summary>While a Job is paused its stored next run follows the clock, so resuming it never finds a past due time (spec §8.5).</summary>
    private void MovePaused(Job job, DateTimeOffset now)
    {
        JobState? stored = states.Get(job.Id);
        DateTimeOffset? next = job.When.NextAfter(now, this.ZoneOf(job));
        if (stored is not null && string.Equals(stored.WhenKey, KeyOf(job), StringComparison.Ordinal) && stored.NextRunUtc == next)
        {
            return;
        }

        states.Set(new JobState(job.Id, KeyOf(job), next, stored?.LastRunUtc, stored?.LastOutcome));
    }

    private async Task GiveUpAsync(Job job, JobState? stored, DueDecision.GiveUp giveUp, DueState? before, DuePolicy policy, CancellationToken ct)
    {
        if (!giveUp.Remove)
        {
            this.Save(job, stored, giveUp.Save, null, null);
            return;
        }

        string reason = before is not null && before.FailedTries >= policy.MaxFailedTries
            ? "it failed three times"
            : "it was more than " + Describe(policy.MaxLateness ?? TimeSpan.Zero) + " late";
        await this.RemoveWithTraceAsync(job, reason, ct).ConfigureAwait(false);
    }

    private async Task RunDueAsync(Job job, JobState? stored, DueState? before, DueDecision.Run run, DateTimeOffset now, CancellationToken ct)
    {
        TimeZoneInfo zone = this.ZoneOf(job);
        bool oneOff = job.When.Cadence == JobCadence.Once;
        DateTimeOffset due = before is not null && string.Equals(before.WhenKey, run.Save.WhenKey, StringComparison.Ordinal) && before.NextRunUtc is { } stamped
            ? stamped
            : job.When.NextAfter(this.LookBackFrom(now, oneOff), zone) ?? now;
        DateTimeOffset? lateDue = run.Late ? TimeZoneInfo.ConvertTime(due, zone) : null;
        DueState beforeRun = before is not null && string.Equals(before.WhenKey, run.Save.WhenKey, StringComparison.Ordinal)
            ? before
            : new DueState(run.Save.WhenKey, due, 0, before?.EverRan ?? false);

        // Saved before the post (ADR-0042): a restart in the middle of a run never runs it twice.
        this.Save(job, stored, run.Save, now, run.Late ? JobOutcome.DeliveredLate : JobOutcome.Delivered);
        JobOutcome outcome = await this.PostAsync(job, now, lateDue, ct).ConfigureAwait(false);
        (JobOutcome shown, DueRunResult result) = Classify(outcome, oneOff, run.Late);
        this.Save(job, stored, DueCheck.AfterRun(run.Save, beforeRun, result), now, shown);

        if (oneOff && shown is JobOutcome.Delivered or JobOutcome.DeliveredLate)
        {
            // A one-off that posted is done: it leaves its owner's schedule.
            _ = this.Delete(job.Id);
        }
        else if (oneOff && shown is JobOutcome.NoAgent or JobOutcome.NoRoom)
        {
            await this.RemoveWithTraceAsync(job, shown == JobOutcome.NoAgent ? "its Agent is no longer available" : "its Room is gone", ct).ConfigureAwait(false);
        }
    }

    /// <summary>The instant to ask a timetable for its own time from: a one-off's time is found from the far past, so it is right whatever the window.</summary>
    private DateTimeOffset LookBackFrom(DateTimeOffset now, bool oneOff) =>
        oneOff ? DateTimeOffset.MinValue : now - options.Value.Jobs.OverdueOneOffWithin;

    private async Task<JobOutcome> PostAsync(Job job, DateTimeOffset now, DateTimeOffset? lateDue, CancellationToken ct)
    {
        try
        {
            return await runner.RunAsync(job, now, lateDue, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The due time is kept, so the next minute tries again until the retries are spent.
            LogRunFailed(logger, ex, job.Id);
            return JobOutcome.Failed;
        }
    }

    /// <summary>How a run ended as the Job shows it and as the timetable sees it (spec §8.1): only a one-off can be held.</summary>
    private static (JobOutcome Shown, DueRunResult Result) Classify(JobOutcome outcome, bool oneOff, bool late) => outcome switch
    {
        JobOutcome.Failed => (JobOutcome.Failed, DueRunResult.Failed),
        JobOutcome.BudgetSpent or JobOutcome.Held when oneOff => (JobOutcome.Held, DueRunResult.Held),
        JobOutcome.Delivered or JobOutcome.DeliveredLate => (late ? JobOutcome.DeliveredLate : JobOutcome.Delivered, DueRunResult.Ran),
        _ => (outcome, DueRunResult.Ran),
    };

    private async Task RemoveWithTraceAsync(Job job, string reason, CancellationToken ct)
    {
        _ = this.Delete(job.Id);
        await gaveUp.NoteAsync(job, reason, ct).ConfigureAwait(false);
    }

    private static string Describe(TimeSpan span)
    {
        if (span >= TimeSpan.FromHours(1) && span.Ticks % TimeSpan.TicksPerHour == 0)
        {
            int hours = (int)span.TotalHours;
            return hours == 1 ? "1 hour" : string.Create(CultureInfo.InvariantCulture, $"{hours} hours");
        }

        int minutes = Math.Max(1, (int)Math.Round(span.TotalMinutes));
        return minutes == 1 ? "1 minute" : string.Create(CultureInfo.InvariantCulture, $"{minutes} minutes");
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Job {JobId} could not be checked.")]
    private static partial void LogTickFailed(ILogger logger, Exception exception, string jobId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Job pass failed.")]
    private static partial void LogPassFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Job {JobId} failed to run; the next minute tries again.")]
    private static partial void LogRunFailed(ILogger logger, Exception exception, string jobId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Job {JobId} was due at {Due} while Huddle was not running; skipped to its next run.")]
    private static partial void LogMissed(ILogger logger, string jobId, DateTimeOffset due);
}

using Agency.Huddle.App.Data;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Jobs;

/// <summary>
/// Runs every Job when it is due, checking once a minute like <c>CadenceService</c>. The next run lives in <c>team.db</c>
/// (<see cref="JobStateStore"/>) and is saved before a run posts, so a restart never runs a Job twice. A Job seen for the
/// first time, or whose timetable was changed, only has its next run worked out. A run missed by more than
/// <see cref="Grace"/>, because Huddle was stopped or asleep, is skipped rather than run late (ADR-0042's rule).
/// </summary>
/// <param name="store">Lists the Jobs.</param>
/// <param name="states">Remembers each Job's next and last run.</param>
/// <param name="runner">Runs a Job.</param>
/// <param name="options">Says whether Jobs are enabled.</param>
/// <param name="logger">Records a Job that failed or was skipped.</param>
/// <param name="timeProvider">The clock, its local time zone and the timer's clock; defaults to the system clock.</param>
internal sealed partial class JobScheduler(
    JobStore store,
    JobStateStore states,
    IJobRunner runner,
    IOptions<TeamOptions> options,
    ILogger<JobScheduler> logger,
    TimeProvider? timeProvider = null) : BackgroundService
{
    /// <summary>How late a run may still happen: two ticks, so a run is never lost to timer jitter but never sent long after.</summary>
    internal static readonly TimeSpan Grace = TimeSpan.FromMinutes(2);

    private static readonly TimeSpan Period = TimeSpan.FromMinutes(1);

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
            if (job.Paused)
            {
                continue;
            }

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

    /// <summary>What a stored next run was worked out from; a change means the timetable was edited.</summary>
    private static string KeyOf(Job job) => job.When + "|" + (job.TimeZoneId ?? string.Empty);

    private async Task TickJobAsync(Job job, DateTimeOffset now, CancellationToken ct)
    {
        TimeZoneInfo zone = this.ZoneOf(job);
        string key = KeyOf(job);
        JobState? state = states.Get(job.Id);
        if (state is null || !string.Equals(state.WhenKey, key, StringComparison.Ordinal))
        {
            states.Set(new JobState(job.Id, key, job.When.NextAfter(now, zone), state?.LastRunUtc, state?.LastOutcome));
            return;
        }

        if (state.NextRunUtc is not { } due || due > now)
        {
            return;
        }

        if (now - due > Grace)
        {
            LogMissed(logger, job.Id, due);
            states.Set(state with { NextRunUtc = job.When.NextAfter(now, zone) });
            return;
        }

        DateTimeOffset? following = job.When.NextAfter(due, zone);
        if (following <= now)
        {
            following = job.When.NextAfter(now, zone);
        }

        JobState fired = state with { NextRunUtc = following, LastRunUtc = now, LastOutcome = JobOutcome.Delivered };
        states.Set(fired);
        JobOutcome outcome;
        try
        {
            outcome = await runner.RunAsync(job, now, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Put the due run back so the next minute retries it; once it is older than Grace it is skipped instead.
            states.Set(state with { LastOutcome = JobOutcome.Failed });
            LogRunFailed(logger, ex, job.Id);
            return;
        }

        states.Set(fired with { LastOutcome = outcome });
        if (following is null)
        {
            // A one-off that has run is done: it leaves its owner's schedule.
            _ = this.Delete(job.Id);
        }
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

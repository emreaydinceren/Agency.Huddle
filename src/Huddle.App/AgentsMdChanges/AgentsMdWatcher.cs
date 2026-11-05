using Agency.Huddle.App.Services;
using Agency.Huddle.App.Teams;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.AgentsMdChanges;

/// <summary>
/// Watches the <c>AGENTS.md</c> files of every Team in the catalog (Spec §6.3). A sweep runs as soon as it starts, then one pass
/// per <see cref="AgentsMdOptions.PollSeconds"/>; a pass reads only a file whose size or modified time moved and hands it to
/// <see cref="AgentsMdLedger.Observe"/>, then offers every pending edit to <see cref="AgentsMdLedger.TryStartJudgement"/>. It runs
/// only while <c>Team:Acp:Enabled</c> is true.
/// </summary>
/// <param name="ledger">Observes each moved file and starts the judgements.</param>
/// <param name="catalog">Lists the Teams whose files are watched.</param>
/// <param name="options">Supplies the data folder, the Teams folder, the switch and the poll period.</param>
/// <param name="logger">Reports a failed file or pass.</param>
/// <param name="timeProvider">The timer's clock; defaults to the system clock.</param>
internal sealed class AgentsMdWatcher(
    AgentsMdLedger ledger,
    ITeamCatalog catalog,
    IOptions<TeamOptions> options,
    ILogger<AgentsMdWatcher> logger,
    TimeProvider? timeProvider = null) : IHostedService, IDisposable
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    /// <summary>Cancelled by <see cref="StopAsync"/> and <see cref="Dispose"/>, which the host may run on two threads.</summary>
    private readonly StopSignal stopping = new();

    /// <summary>The size and modified time last seen per file, keyed by relative path; only this service's loop touches it.</summary>
    private readonly Dictionary<string, FileStamp> seen = new(StringComparer.Ordinal);

    private Task loop = Task.CompletedTask;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (options.Value.Acp.Enabled)
        {
            this.loop = this.RunAsync(this.stopping.Token);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        this.stopping.Stop();
        await this.loop.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose() => this.stopping.Dispose();

    /// <summary>Runs one pass over every watched file; the timer calls it every poll period.</summary>
    /// <param name="ct">Stops the pass.</param>
    internal Task TickAsync(CancellationToken ct)
    {
        TeamOptions team = options.Value;
        string teamsRoot = Path.Combine(team.DataDir, team.Teams.Dir);
        List<string> names = [];
        foreach (TeamSummary summary in catalog.Teams)
        {
            names.Add(summary.Name);
        }

        foreach (AgentsMdFile file in AgentsMdLocator.Enumerate(teamsRoot, names))
        {
            ct.ThrowIfCancellationRequested();
            this.CheckFile(file);
        }

        foreach (AgentsMdPendingEdit edit in ledger.PendingEdits())
        {
            ct.ThrowIfCancellationRequested();
            this.Offer(edit.File);
        }

        return Task.CompletedTask;
    }

    private static FileStamp Stamp(string path)
    {
        FileInfo info = new(path);
        return info.Exists ? new FileStamp(info.Length, info.LastWriteTimeUtc) : FileStamp.Absent;
    }

    /// <summary>Asks the ledger to start a judgement once the debounce has passed. A failure is logged and the next pending edit is still offered.</summary>
    /// <param name="file">The file with a pending edit.</param>
    private void Offer(AgentsMdFile file)
    {
        try
        {
            _ = ledger.TryStartJudgement(file, JudgementReason.Debounced);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // One file's failed offer (an unreachable Helper, a locked ledger) must not stop the others: log it and go on.
            logger.LogWarning(ex, "Offering {File} for judgement failed.", file.RelativePath);
        }
    }

    /// <summary>Observes the file when its stamp moved since the last pass. The ledger handles its own I/O failures; anything else ends the pass and the loop retries it.</summary>
    /// <param name="file">The file to check.</param>
    private void CheckFile(AgentsMdFile file)
    {
        FileStamp stamp = Stamp(file.FullPath);
        if (this.seen.TryGetValue(file.RelativePath, out FileStamp last) && last == stamp)
        {
            return;
        }

        if (stamp == FileStamp.Absent && ledger.FirstSightHash(file.RelativePath) is null)
        {
            // A level that never had a file has nothing to compare and spends no ledger entry.
            this.seen[file.RelativePath] = stamp;
            return;
        }

        AgentsMdObservation observation = ledger.Observe(file);
        if (stamp == FileStamp.Absent || observation.Hash.Length > 0)
        {
            this.seen[file.RelativePath] = stamp;
        }
    }

    /// <summary>The loop: a sweep first, then one pass per poll period, until stopped. The yield keeps the sweep off the host's start path.</summary>
    /// <param name="ct">Stops the loop.</param>
    private async Task RunAsync(CancellationToken ct)
    {
        await Task.Yield();
        TimeSpan period = TimeSpan.FromSeconds(Math.Max(1, options.Value.AgentsMd.PollSeconds));
        try
        {
            using PeriodicTimer timer = new(period, this.clock);
            do
            {
                try
                {
                    await this.TickAsync(ct).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // The loop must not die: log and try again on the next period.
                    logger.LogError(ex, "The AGENTS.md watcher pass failed.");
                }
            }
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            // Stopped: the normal end of the loop.
        }
    }

    private readonly record struct FileStamp(long Length, DateTime ModifiedUtc)
    {
        public static FileStamp Absent { get; } = new(-1, DateTime.MinValue);
    }
}

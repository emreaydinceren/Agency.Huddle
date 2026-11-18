using System.Collections.Concurrent;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Goldfish;

/// <summary>
/// Owns the end of a possession (spec §6.5, §6.6) and the small state moves in between. It has no loop and no timer: a
/// possession ends when it is told to, and the start-up pass ends whatever a restart left open. The store is the one source of
/// truth, so every call reads it. Only <see cref="EndAsync"/> takes a lock, one per possession.
/// </summary>
internal sealed class GoldfishSupervisor(
    GoldfishStore store,
    ITeamDirectory team,
    ChatService chat,
    IGoldfishRunnerLink link,
    IOptions<TeamOptions> options,
    ILogger<GoldfishSupervisor> logger,
    TimeProvider? timeProvider = null) : IHostedService, IDisposable, IPossessionLookup
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    private readonly ConcurrentDictionary<string, SemaphoreSlim> locks = new(StringComparer.Ordinal);

    /// <summary>Ends every row a restart left open as Restarted and prunes old ended rows; a failure is logged and never stops Huddle.</summary>
    /// <param name="cancellationToken">Cancels the pass.</param>
    /// <returns>A task that completes when the pass is done.</returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            foreach (Possession row in store.Open())
            {
                await this.RestartOneAsync(row.Id, cancellationToken).ConfigureAwait(false);
            }

            DateTimeOffset cutoff = this.clock.GetUtcNow().AddDays(-options.Value.Goldfish.KeepDays);
            _ = store.PruneBefore(cutoff);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "The goldfish start-up pass failed; Huddle starts without it.");
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public void Dispose()
    {
        // The per-possession semaphores are deliberately not disposed: they hold no unmanaged handle (nothing touches
        // AvailableWaitHandle), and a call still holding one would throw on release if it were disposed under it.
        this.locks.Clear();
    }

    /// <summary>Returns a possession: claims the exit, tells the runner, posts the result, posts the return row, closes the row.</summary>
    /// <param name="possessionId">The possession to end.</param>
    /// <param name="exit">How it ends.</param>
    /// <param name="by">Who chose the exit, or <see langword="null"/>.</param>
    /// <param name="ct">Cancels the work.</param>
    /// <returns><see langword="true"/> when this call did the return; <see langword="false"/> when it was already returning, ended or unknown.</returns>
    /// <param name="result">What the goldfish reported through <c>goldfish_done</c>, or <see langword="null"/>; a Stuck result is posted as partial.</param>
    public async Task<bool> EndAsync(string possessionId, GoldfishExit exit, string? by, CancellationToken ct, GoldfishResult? result = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(possessionId);

        SemaphoreSlim gate = this.locks.GetOrAdd(possessionId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!store.BeginReturn(possessionId, exit, by))
            {
                return false;
            }

            Possession? row = store.Get(possessionId);
            if (row is null)
            {
                return false;
            }

            return await this.ReturnClaimedAsync(row, exit, by, result, ct).ConfigureAwait(false);
        }
        finally
        {
            _ = gate.Release();
        }
    }

    /// <summary>Runs the steps after the claim. The row ends even when a step fails; on cancellation it stays Returning, and the start-up pass ends it Restarted.</summary>
    private async Task<bool> ReturnClaimedAsync(Possession row, GoldfishExit exit, string? by, GoldfishResult? result, CancellationToken ct)
    {
        try
        {
            await this.TellRunnerAsync(row, exit, ct).ConfigureAwait(false);
            await this.PostResultAsync(row, exit, result, ct).ConfigureAwait(false);
            await this.PostReturnRowAsync(row, exit, by, result, ct).ConfigureAwait(false);
        }
        finally
        {
            if (!ct.IsCancellationRequested)
            {
                store.End(row.Id, this.clock.GetUtcNow());
                _ = this.locks.TryRemove(row.Id, out _);
            }
        }

        return true;
    }

    /// <summary>A turn ended: when the Agent is not done, a Starting or Working possession waits. Nothing is sent.</summary>
    /// <param name="possessionId">The possession.</param>
    /// <param name="done">Whether the Agent said it is done.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>A completed task.</returns>
    public Task OnTurnEndedAsync(string possessionId, bool done, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!done)
        {
            _ = store.SetState(possessionId, PossessionState.Waiting, PossessionState.Starting, PossessionState.Working);
        }

        return Task.CompletedTask;
    }

    /// <summary>The Agent was Mentioned in a Room: a waiting possession in that Room works again.</summary>
    /// <param name="agentId">The Agent.</param>
    /// <param name="roomId">The Room of the Mention.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>A completed task.</returns>
    public Task OnMentionAsync(string agentId, string roomId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (this.FindOpen(agentId) is { } open && string.Equals(open.RoomId, roomId, StringComparison.Ordinal))
        {
            _ = store.SetState(open.Id, PossessionState.Working, PossessionState.Waiting);
        }

        return Task.CompletedTask;
    }

    /// <summary>The Agent's runner connection was lost: its open possession ends as Failed.</summary>
    /// <param name="agentId">The Agent.</param>
    /// <param name="ct">Cancels the work.</param>
    /// <returns>A task that completes when the possession has ended, or at once when there is none.</returns>
    public async Task OnRunnerLostAsync(string agentId, CancellationToken ct)
    {
        if (this.FindOpen(agentId) is { } open)
        {
            _ = await this.EndAsync(open.Id, GoldfishExit.Failed, null, ct).ConfigureAwait(false);
        }
    }

    /// <summary>Finds the Agent's open possession.</summary>
    /// <param name="agentId">The Agent.</param>
    /// <returns>The row, or <see langword="null"/> when the Agent is not possessed.</returns>
    public Possession? FindOpen(string agentId) =>
        store.Open().FirstOrDefault(row => string.Equals(row.AgentId, agentId, StringComparison.Ordinal));

    /// <inheritdoc />
    public string? PossessionOf(string agentId, string roomId) =>
        this.FindOpen(agentId) is { State: PossessionState.Working or PossessionState.Waiting or PossessionState.Returning } open
        && string.Equals(open.RoomId, roomId, StringComparison.Ordinal)
            ? open.Id
            : null;

    /// <summary>The open possession in a Room, whatever its state.</summary>
    /// <param name="roomId">The Room.</param>
    /// <returns>The row, or <see langword="null"/> when no Agent is possessed there.</returns>
    public Possession? OpenInRoom(string roomId) =>
        store.Open().FirstOrDefault(row => string.Equals(row.RoomId, roomId, StringComparison.Ordinal));

    /// <summary>Starts a possession: inserts the Starting row, posts the start row quietly and tells the runner. A failure ends it Failed and rethrows.</summary>
    /// <param name="agentId">The Agent to possess.</param>
    /// <param name="roomId">The Room it works in.</param>
    /// <param name="taskId">The Task in hand, or <see langword="null"/>.</param>
    /// <param name="brief">The brief, word for word.</param>
    /// <param name="ct">Cancels the work.</param>
    /// <returns>The row, or <see langword="null"/> when the Agent already has an open possession.</returns>
    public async Task<Possession?> BeginAsync(string agentId, string roomId, string? taskId, string brief, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        ArgumentNullException.ThrowIfNull(brief);

        Possession row = new(
            Guid.NewGuid().ToString("N"), agentId, roomId, taskId, brief, PossessionState.Starting, null, null, this.clock.GetUtcNow(), null, 0, 0m, null, null);
        if (!store.TryInsert(row))
        {
            return null;
        }

        try
        {
            IReadOnlyList<User> members = await team.GetRoomMembersAsync(roomId, ct).ConfigureAwait(false);
            string name = members.FirstOrDefault(member => string.Equals(member.Id, agentId, StringComparison.Ordinal))?.Name ?? agentId;
            _ = await chat.PostAsHuddleQuietAsync(roomId, GoldfishLine.StartRow(name, brief), ct).ConfigureAwait(false);
            await link.SendBeginAsync(agentId, new GoldfishBegin(row.Id, roomId, brief, taskId), ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _ = await this.EndAsync(row.Id, GoldfishExit.Failed, null, ct).ConfigureAwait(false);
            throw;
        }

        return row;
    }

    /// <summary>Applies a runner's report to the Agent's open possession; a report for any other id is ignored.</summary>
    /// <param name="agentId">The reporting Agent.</param>
    /// <param name="report">The report.</param>
    /// <param name="ct">Cancels the work.</param>
    /// <returns>A task that completes when the report is applied.</returns>
    public async Task OnReportAsync(string agentId, GoldfishReport report, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(report);

        if (this.FindOpen(agentId) is not { } open || !string.Equals(open.Id, report.PossessionId, StringComparison.Ordinal))
        {
            return;
        }

        switch (report.Kind)
        {
            case GoldfishReportKind.Opened:
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug("The runner of {AgentId} opened possession {PossessionId}.", agentId, open.Id);
                }

                break;
            case GoldfishReportKind.TurnStarted:
                _ = store.SetState(open.Id, PossessionState.Working, PossessionState.Starting, PossessionState.Waiting);
                break;
            case GoldfishReportKind.TurnEnded:
                if (report.RunningSpend is { } running)
                {
                    store.AddTurn(open.Id, running - open.Spend, report.Currency);
                }
                else
                {
                    store.AddTurn(open.Id, null, report.Currency);
                }

                await this.OnTurnEndedAsync(open.Id, false, ct).ConfigureAwait(false);
                break;
            case GoldfishReportKind.Failed:
                _ = await this.EndAsync(open.Id, GoldfishExit.Failed, null, ct).ConfigureAwait(false);
                break;
            default:
                break;
        }
    }

    private async Task RestartOneAsync(string possessionId, CancellationToken ct)
    {
        try
        {
            _ = await this.EndAsync(possessionId, GoldfishExit.Restarted, null, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Possession {PossessionId} could not be ended by the start-up pass; the rest continue.", possessionId);
        }
    }

    private async Task TellRunnerAsync(Possession row, GoldfishExit exit, CancellationToken ct)
    {
        try
        {
            await link.SendEndAsync(row.AgentId, row.Id, exit, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The runner of {AgentId} could not be told possession {PossessionId} ended; the return goes on.", row.AgentId, row.Id);
        }
    }

    private async Task PostResultAsync(Possession row, GoldfishExit exit, GoldfishResult? result, CancellationToken ct)
    {
        if (row.ResultMessageId is not null)
        {
            return;
        }

        try
        {
            string? findings = result is { Findings.Count: > 0 } ? string.Join('\n', result.Findings.Select(finding => "- " + finding)) : null;
            string text = GoldfishLine.ResultText(row.Brief, result?.Text, findings, exit == GoldfishExit.Stuck);
            ChatMessage posted = await chat.PostAsHuddleQuietAsync(row.RoomId, text, ct).ConfigureAwait(false);
            store.SetResultMessage(row.Id, posted.Id);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The result of possession {PossessionId} could not be posted to Room {RoomId}.", row.Id, row.RoomId);
        }
    }

    private async Task PostReturnRowAsync(Possession row, GoldfishExit exit, string? by, GoldfishResult? result, CancellationToken ct)
    {
        try
        {
            IReadOnlyList<User> members = await team.GetRoomMembersAsync(row.RoomId, ct).ConfigureAwait(false);
            User? agent = members.FirstOrDefault(member => string.Equals(member.Id, row.AgentId, StringComparison.Ordinal));
            TimeSpan elapsed = this.clock.GetUtcNow() - row.StartedAt;
            string? firstLine = result?.Text.Split('\n', 2)[0].Trim();
            string text = GoldfishLine.ReturnLine(agent?.Name ?? row.AgentId, exit, by, elapsed, agent is not null, result?.Findings.Count ?? 0, firstLine);
            _ = await chat.PostAsHuddleAsync(row.RoomId, text, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The return row of possession {PossessionId} could not be posted to Room {RoomId}.", row.Id, row.RoomId);
        }
    }
}

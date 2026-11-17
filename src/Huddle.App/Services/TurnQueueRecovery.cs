using Agency.Huddle.App.Data;
using Agency.Huddle.Contracts;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Services;

/// <summary>
/// The Turn Queue's start-up pass (spec §5.7): rows left open by the last run are closed, deleted, set back to waiting or left
/// alone by what the Transcript says, <c>done</c> rows past their keep time are pruned, and one line per Room says what was
/// picked up and what was not. It runs in <see cref="StartAsync"/> before any Agent connects and before any poster, and a
/// failure of it is logged and never stops Huddle starting.
/// </summary>
internal sealed class TurnQueueRecovery(
    TurnQueueStore store,
    ITeamDirectory team,
    IChatStore transcript,
    ChatService chat,
    Func<string, bool> agentExists,
    IOptions<TeamOptions> options,
    ILogger<TurnQueueRecovery> logger,
    TimeProvider? timeProvider = null) : IHostedService
{
    private const int FirstTail = 50;
    private const int MaxAttempts = 2;

    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        TurnQueueOptions queue = options.Value.TurnQueue;
        if (!queue.Enabled)
        {
            return;
        }

        try
        {
            await this.RunAsync(queue, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "The Turn Queue start-up pass failed; Huddle starts without it.");
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task RunAsync(TurnQueueOptions queue, CancellationToken ct)
    {
        // Taken first: a row written after this moment has an updated_utc at or after it and is never read as an orphan.
        DateTimeOffset start = this.clock.GetUtcNow();
        List<(string RoomId, List<RestartLine.Entry> Entries)> lines = [];
        foreach (IGrouping<string, TurnQueueRow> room in store.OpenUpdatedBefore(start).GroupBy(static row => row.RoomId, StringComparer.Ordinal))
        {
            try
            {
                List<RestartLine.Entry> entries = await this.HandleRoomAsync(room.Key, [.. room], queue, start, ct).ConfigureAwait(false);
                lines.Add((room.Key, entries));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One Room's failure leaves its rows as they were and must not cost the other Rooms their pass: it is logged and the loop goes on.
                logger.LogError(ex, "The Turn Queue start-up pass failed for Room {RoomId}; its rows are left as they were.", room.Key);
            }
        }

        _ = store.PruneDoneBefore(start.AddDays(-queue.KeepDays));
        foreach ((string roomId, List<RestartLine.Entry> entries) in lines)
        {
            await this.PostLineAsync(roomId, entries, queue.ResumeWithin, ct).ConfigureAwait(false);
        }
    }

    private async Task<List<RestartLine.Entry>> HandleRoomAsync(
        string roomId, List<TurnQueueRow> rows, TurnQueueOptions queue, DateTimeOffset start, CancellationToken ct)
    {
        Room? room = await team.GetRoomAsync(roomId, ct).ConfigureAwait(false);
        IReadOnlyList<User> members = room is null ? [] : await team.GetRoomMembersAsync(roomId, ct).ConfigureAwait(false);
        List<TurnQueueRow> open = [];
        Dictionary<string, string> names = new(StringComparer.Ordinal);
        foreach (TurnQueueRow row in rows)
        {
            User? agent = members.FirstOrDefault(member => string.Equals(member.Id, row.AgentId, StringComparison.Ordinal));
            if (agent is null || !agentExists(agent.Name))
            {
                store.Set(row.Seq, TurnQueueStatus.Done, TurnQueueOutcome.Gone, start);
                continue;
            }

            names[row.AgentId] = agent.Name;
            open.Add(row);
        }

        if (open.Count == 0)
        {
            return [];
        }

        HashSet<string> present = await this.ReadPresentIdsAsync(roomId, open, ct).ConfigureAwait(false);
        Dictionary<string, int[]> tally = new(StringComparer.Ordinal);
        foreach (TurnQueueRow row in open)
        {
            int slot = this.Decide(row, present, queue, start);
            if (slot >= 0)
            {
                if (!tally.TryGetValue(row.AgentId, out int[]? counts))
                {
                    counts = new int[3];
                    tally[row.AgentId] = counts;
                }

                counts[slot]++;
            }
        }

        return [.. tally.Select(pair => new RestartLine.Entry(names[pair.Key], pair.Value[0], pair.Value[1], pair.Value[2]))];
    }

    /// <summary>Applies the §5.7 table to one row of a present Agent.</summary>
    /// <returns>0 when the row is picked up, 1 when it expired, 2 when it was abandoned, -1 when it says nothing.</returns>
    private int Decide(TurnQueueRow row, HashSet<string> present, TurnQueueOptions queue, DateTimeOffset start)
    {
        if (row.Status == TurnQueueStatus.Waiting)
        {
            if (!present.Contains(row.MessageId))
            {
                store.Delete(row.Seq);
                return -1;
            }

            if (TurnQueue.IsTooOld(row.PostedUtc, start, queue.ResumeWithin))
            {
                store.Set(row.Seq, TurnQueueStatus.Done, TurnQueueOutcome.Expired, start);
                return 1;
            }

            return 0;
        }

        if (row.ReplyMessageId is { } replyId && present.Contains(replyId))
        {
            store.Set(row.Seq, TurnQueueStatus.Done, TurnQueueOutcome.Replied, start);
            return -1;
        }

        if (row.Attempts < MaxAttempts)
        {
            store.Set(row.Seq, TurnQueueStatus.Waiting, null, start);
            return 0;
        }

        store.Set(row.Seq, TurnQueueStatus.Done, TurnQueueOutcome.Abandoned, start);
        return 2;
    }

    /// <summary>The ids of the Messages the rows are about, read from the end of the Transcript: the count doubles until the oldest open Message is in it or the whole file was read.</summary>
    private async Task<HashSet<string>> ReadPresentIdsAsync(string roomId, List<TurnQueueRow> rows, CancellationToken ct)
    {
        string oldest = rows.MinBy(static row => row.PostedUtc)?.MessageId ?? string.Empty;
        int count = FirstTail;
        while (true)
        {
            IReadOnlyList<ChatMessage> tail = await transcript.ReadTailAsync(roomId, count, ct).ConfigureAwait(false);
            HashSet<string> ids = new(tail.Select(static message => message.Id), StringComparer.Ordinal);
            if (ids.Contains(oldest) || tail.Count < count || count > int.MaxValue / 2)
            {
                return ids;
            }

            count *= 2;
        }
    }

    private async Task PostLineAsync(string roomId, List<RestartLine.Entry> entries, TimeSpan resumeWithin, CancellationToken ct)
    {
        string? text = RestartLine.Build(entries, resumeWithin);
        if (text is null)
        {
            return;
        }

        try
        {
            _ = await chat.PostAsHuddleAsync(roomId, text, ct).ConfigureAwait(false);
        }
        catch (ChatException ex)
        {
            logger.LogWarning(ex, "The restart line for Room {RoomId} could not be posted.", roomId);
        }
    }
}

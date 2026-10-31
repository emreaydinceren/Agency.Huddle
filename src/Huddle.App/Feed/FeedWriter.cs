using System.Threading.Channels;
using Agency.Huddle.App.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Feed;

/// <summary>
/// The Feed's single writer (spec 6.6): reads every command already queued, up to <c>Team:Feed:BatchSize</c>, and
/// applies them in one transaction in order; raises <see cref="FeedEvents"/> after the commit; and cleans up
/// expired rows on start and then every <c>Team:Feed:CleanupInterval</c>.
/// </summary>
/// <param name="queue">The queue to read.</param>
/// <param name="store">The table to write.</param>
/// <param name="events">The hub raised after each commit.</param>
/// <param name="directory">Where a Room's name and Team are read from.</param>
/// <param name="options">Supplies the Feed options.</param>
/// <param name="logger">Receives the warnings for dropped batches.</param>
/// <param name="timeProvider">The clock the clean-up cutoffs and timer use; the system clock when omitted.</param>
internal sealed class FeedWriter(
    FeedQueue queue,
    FeedStore store,
    FeedEvents events,
    ITeamDirectory directory,
    IOptions<TeamOptions> options,
    ILogger<FeedWriter> logger,
    TimeProvider? timeProvider = null) : BackgroundService
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    /// <summary>Deletes the rows the retention windows have expired, now. A failure is logged and the next run tries again.</summary>
    internal void RunCleanup()
    {
        FeedOptions feed = options.Value.Feed;
        DateTimeOffset now = this.clock.GetUtcNow();
        DateTimeOffset? dismissedCutoff = feed.DismissedRetentionDays > 0 ? now.AddDays(-feed.DismissedRetentionDays) : null;
        DateTimeOffset? activityCutoff = feed.ActivityRetentionDays > 0 ? now.AddDays(-feed.ActivityRetentionDays) : null;
        if (dismissedCutoff is null && activityCutoff is null)
        {
            return;
        }

        try
        {
            _ = store.DeleteExpired(dismissedCutoff, activityCutoff);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The Feed can never take the app down: whatever went wrong, the rows are still there and the next interval tries again.
            logger.LogWarning(ex, "Feed clean-up failed; it runs again at the next interval.");
        }
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        await Task.WhenAll(this.CommandLoopAsync(stoppingToken), this.CleanupLoopAsync(stoppingToken)).ConfigureAwait(false);
    }

    private async Task CommandLoopAsync(CancellationToken ct)
    {
        ChannelReader<FeedCommand> reader = queue.Reader;
        int batchSize = Math.Max(1, options.Value.Feed.BatchSize);
        List<FeedCommand> batch = [];
        try
        {
            while (await reader.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                batch.Clear();
                while (batch.Count < batchSize && reader.TryRead(out FeedCommand? command))
                {
                    batch.Add(command);
                }

                await this.ApplySafelyAsync(batch, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The host is stopping: leaving the loop is the whole job.
        }
    }

    private async Task ApplySafelyAsync(List<FeedCommand> batch, CancellationToken ct)
    {
        try
        {
            await this.ApplyBatchAsync(batch, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The one general catch: this is the supervisor loop of a background service, and the Feed is a
            // convenience that must never take the app down. The batch is lost, the failure is logged, and the
            // next batch gets a fresh start.
            logger.LogError(ex, "The Feed writer dropped a batch after an unexpected failure.");
        }
    }

    private async Task ApplyBatchAsync(List<FeedCommand> batch, CancellationToken ct)
    {
        FeedApplyResult result;
        try
        {
            List<FeedCommand> prepared = await this.FillInRoomsAsync(batch, ct).ConfigureAwait(false);
            result = store.Apply(prepared);
        }
        catch (SqliteException ex)
        {
            logger.LogWarning(ex, "The Feed database refused a batch; the batch is dropped.");
            return;
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "The Feed database could not be reached for a batch; the batch is dropped.");
            return;
        }

        if (result.Added.Count > 0)
        {
            events.RaiseItemsAdded(result.Added);
        }

        if (result.Changed)
        {
            events.RaiseItemsChanged();
        }
    }

    /// <summary>Reads a Room for the fill-in. The fill-in only adds facts, so a failed lookup keeps the nulls rather than dropping the batch.</summary>
    /// <param name="roomId">The Room.</param>
    /// <param name="ct">Cancels the lookup.</param>
    private async Task<Room?> TryGetRoomAsync(string roomId, CancellationToken ct)
    {
        try
        {
            return await directory.GetRoomAsync(roomId, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Adding a name to an item is a courtesy; losing every other producer's items in the batch is not.
            logger.LogWarning(ex, "The Feed could not read Room {RoomId} to fill in its name.", roomId);
            return null;
        }
    }

    private async Task<List<FeedCommand>> FillInRoomsAsync(List<FeedCommand> batch, CancellationToken ct)
    {
        Dictionary<string, Room?> rooms = new(StringComparer.Ordinal);
        List<FeedCommand> prepared = new(batch.Count);
        foreach (FeedCommand command in batch)
        {
            if (command is PostCommand { Message.Payload: RoomWaitPayload { RoomName: null } or RoomWaitPayload { Team: null } } post)
            {
                RoomWaitPayload wait = (RoomWaitPayload)post.Message.Payload;
                if (!rooms.TryGetValue(wait.RoomId, out Room? room))
                {
                    room = await this.TryGetRoomAsync(wait.RoomId, ct).ConfigureAwait(false);
                    rooms[wait.RoomId] = room;
                }

                prepared.Add(room is null ? post : post with { Message = post.Message with { Payload = wait with { RoomName = wait.RoomName ?? room.Name, Team = wait.Team ?? room.Team } } });
            }
            else
            {
                prepared.Add(command);
            }
        }

        return prepared;
    }

    private async Task CleanupLoopAsync(CancellationToken ct)
    {
        this.RunCleanup();
        TimeSpan interval = options.Value.Feed.CleanupInterval;
        if (interval <= TimeSpan.Zero)
        {
            return;
        }

        using PeriodicTimer timer = new(interval, this.clock);
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                this.RunCleanup();
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The host is stopping: leaving the loop is the whole job.
        }
    }
}

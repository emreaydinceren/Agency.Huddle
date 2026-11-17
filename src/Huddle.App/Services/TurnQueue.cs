using Agency.Huddle.App.Data;
using Agency.Huddle.Contracts;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Services;

/// <summary>
/// The Turn Queue: the server's memory of which Turns Agents owe. Later tasks add the acknowledgement, control and
/// view interfaces to this one class.
/// </summary>
internal sealed class TurnQueue : ITurnLedger, ITurnAcks, ITurnQueueControl
{
    private readonly TurnQueueStore store;
    private readonly IOptions<TeamOptions> options;
    private readonly ILogger<TurnQueue> logger;
    private readonly IHostApplicationLifetime lifetime;
    private readonly TimeProvider timeProvider;
    private readonly RoomEvents? events;

    /// <summary>Creates the queue over its store.</summary>
    /// <param name="store">The <c>turn_queue</c> tables.</param>
    /// <param name="options">Supplies <c>Team:TurnQueue:Enabled</c>.</param>
    /// <param name="logger">Receives diagnostics.</param>
    /// <param name="lifetime">Tells <see cref="CloseRunning"/> whether the host is stopping.</param>
    /// <param name="timeProvider">The clock; <see langword="null"/> uses <see cref="TimeProvider.System"/>.</param>
    /// <param name="events">Told when a Room's open rows change; <see langword="null"/> tells nobody.</param>
    public TurnQueue(
        TurnQueueStore store,
        IOptions<TeamOptions> options,
        ILogger<TurnQueue> logger,
        IHostApplicationLifetime lifetime,
        TimeProvider? timeProvider = null,
        RoomEvents? events = null)
    {
        this.events = events;
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(lifetime);
        this.store = store;
        this.options = options;
        this.logger = logger;
        this.lifetime = lifetime;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public void SetSubscribed(string agentId, bool durableQueue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);

        // The flag is the Agent's choice, so it is kept even while the queue is switched off: switching it on again then finds it.
        if (durableQueue)
        {
            this.store.Subscribe(agentId);
        }
        else
        {
            this.store.Unsubscribe(agentId);
        }
    }

    /// <inheritdoc />
    public void Apply(string connectionId, string agentId, QueueAck ack)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentNullException.ThrowIfNull(ack);

        if (!this.options.Value.TurnQueue.Enabled)
        {
            return;
        }

        DateTimeOffset now = this.timeProvider.GetUtcNow();
        switch (ack.Kind)
        {
            case QueueAckKind.Started when ack.ReplyMessageId is { } replyId:
                this.store.Started(agentId, ack.RoomId, ack.MessageIds, replyId, connectionId, now, now);
                this.Changed(ack.RoomId);
                break;
            case QueueAckKind.Finished:
                _ = this.store.Finish(agentId, ack.RoomId, ack.MessageIds, ToStored(ack.Outcome), now);
                this.Changed(ack.RoomId);
                break;
            default:
                // Held changes nothing: the rows stay waiting. A Started without a reply id cannot name its Turn.
                break;
        }
    }

    /// <inheritdoc />
    public void MarkReplied(string agentId, string roomId, IReadOnlyList<string> inReplyTo)
    {
        ArgumentNullException.ThrowIfNull(inReplyTo);

        if (this.options.Value.TurnQueue.Enabled)
        {
            _ = this.store.MarkReplied(agentId, roomId, inReplyTo, this.timeProvider.GetUtcNow());
            this.Changed(roomId);
        }
    }

    /// <inheritdoc />
    public void MarkRefused(string agentId, string roomId, IReadOnlyList<string> inReplyTo)
    {
        ArgumentNullException.ThrowIfNull(inReplyTo);

        if (this.options.Value.TurnQueue.Enabled)
        {
            _ = this.store.Finish(agentId, roomId, inReplyTo, TurnQueueOutcome.Failed, this.timeProvider.GetUtcNow());
            this.Changed(roomId);
        }
    }

    /// <inheritdoc />
    public void CloseRunning(string connectionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

        // A host that is going down drops its connections too; those Turns were not stopped, they are resumed by the next start (spec §5.6).
        if (!this.options.Value.TurnQueue.Enabled || this.lifetime.ApplicationStopping.IsCancellationRequested)
        {
            return;
        }

        IReadOnlyList<string> rooms = this.store.RunningRoomsOf(connectionId);
        _ = this.store.CloseRunning(connectionId, TurnQueueOutcome.Stopped, this.timeProvider.GetUtcNow());
        foreach (string roomId in rooms)
        {
            this.Changed(roomId);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<WaitingTurn> TakeWaiting(string agentId, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);

        TurnQueueOptions limits = this.options.Value.TurnQueue;
        if (!limits.Enabled)
        {
            return [];
        }

        List<WaitingTurn> kept = [];
        HashSet<string> expiredIn = new(StringComparer.Ordinal);
        foreach (TurnQueueRow row in this.store.Open(agentId))
        {
            if (row.Status != TurnQueueStatus.Waiting)
            {
                continue;
            }

            if (IsTooOld(row.PostedUtc, now, limits.ResumeWithin))
            {
                this.store.Set(row.Seq, TurnQueueStatus.Done, TurnQueueOutcome.Expired, now);
                _ = expiredIn.Add(row.RoomId);
                continue;
            }

            kept.Add(new WaitingTurn(row, row.Attempts >= 1));
        }

        foreach (string roomId in expiredIn)
        {
            this.Changed(roomId);
        }

        return kept;
    }

    /// <inheritdoc />
    public void CloseGone(long seq, DateTimeOffset now)
    {
        string? roomId = this.store.RoomOf(seq);
        this.store.Set(seq, TurnQueueStatus.Done, TurnQueueOutcome.Gone, now);
        if (roomId is not null)
        {
            this.Changed(roomId);
        }
    }

    /// <inheritdoc />
    public bool IsSubscribed(string agentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        return this.store.Subscribers().Contains(agentId);
    }

    /// <inheritdoc />
    public void CloseForStop(string agentId, string roomId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);

        if (!this.options.Value.TurnQueue.Enabled)
        {
            return;
        }

        // The bound is read once, here: a Message recorded while the stop is in flight is a new Turn the Human has not stopped.
        long maxSeq = this.store.MaxSeq(agentId, roomId);
        _ = this.store.CloseUpTo(agentId, roomId, maxSeq, TurnQueueOutcome.Stopped, this.timeProvider.GetUtcNow());
        this.Changed(roomId);
    }

    /// <inheritdoc />
    public void CloseForAgent(string agentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);

        // The subscription goes even while the queue is switched off, like the flag it mirrors in SetSubscribed.
        this.store.Unsubscribe(agentId);
        if (!this.options.Value.TurnQueue.Enabled)
        {
            return;
        }

        List<string> rooms = [.. this.store.Open(agentId).Select(static row => row.RoomId).Distinct(StringComparer.Ordinal)];
        _ = this.store.CloseForAgent(agentId, TurnQueueOutcome.Gone, this.timeProvider.GetUtcNow());
        foreach (string roomId in rooms)
        {
            this.Changed(roomId);
        }
    }

    /// <summary>Tells the Room view that the open rows of a Room changed. Called only outside any lock.</summary>
    /// <param name="roomId">The Room.</param>
    private void Changed(string roomId) => this.events?.PublishTurnQueueChanged(roomId);

    /// <summary>The one age rule: a Message older than <paramref name="resumeWithin"/> is not handed out; zero or less means no limit.</summary>
    /// <param name="postedUtc">The Message's own time.</param>
    /// <param name="now">The current time.</param>
    /// <param name="resumeWithin">The limit.</param>
    internal static bool IsTooOld(DateTimeOffset postedUtc, DateTimeOffset now, TimeSpan resumeWithin) =>
        resumeWithin > TimeSpan.Zero && now - postedUtc > resumeWithin;

    /// <summary>Maps the outcome a client reports to the stored one; a Finished ack without an outcome counts as a failed Turn.</summary>
    /// <param name="outcome">The wire outcome.</param>
    private static TurnQueueOutcome ToStored(QueueOutcome? outcome) => outcome switch
    {
        QueueOutcome.Silent => TurnQueueOutcome.Silent,
        QueueOutcome.Stopped => TurnQueueOutcome.Stopped,
        QueueOutcome.Declined => TurnQueueOutcome.Declined,
        QueueOutcome.Dropped => TurnQueueOutcome.Dropped,
        _ => TurnQueueOutcome.Failed,
    };

    /// <inheritdoc />
    public void Record(ChatMessage message, string roomId, IReadOnlyList<string> owedAgentIds)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(owedAgentIds);

        if (!this.options.Value.TurnQueue.Enabled || owedAgentIds.Count == 0)
        {
            return;
        }

        IReadOnlySet<string> subscribers = this.store.Subscribers();
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        bool recorded = false;
        foreach (string agentId in owedAgentIds)
        {
            if (subscribers.Contains(agentId))
            {
                _ = this.store.Record(agentId, message.Id, roomId, message.Timestamp, now);
                recorded = true;
            }
        }

        if (recorded)
        {
            this.Changed(roomId);
        }
    }

    /// <inheritdoc />
    public void Forget(string messageId)
    {
        int removed = this.store.Forget(messageId);
        if (removed > 0 && this.logger.IsEnabled(LogLevel.Debug))
        {
            this.logger.LogDebug("Forgot the owed Turn rows of Message {MessageId}.", messageId);
        }
    }
}

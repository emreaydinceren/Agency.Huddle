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

    /// <summary>Creates the queue over its store.</summary>
    /// <param name="store">The <c>turn_queue</c> tables.</param>
    /// <param name="options">Supplies <c>Team:TurnQueue:Enabled</c>.</param>
    /// <param name="logger">Receives diagnostics.</param>
    /// <param name="lifetime">Tells <see cref="CloseRunning"/> whether the host is stopping.</param>
    /// <param name="timeProvider">The clock; <see langword="null"/> uses <see cref="TimeProvider.System"/>.</param>
    public TurnQueue(
        TurnQueueStore store, IOptions<TeamOptions> options, ILogger<TurnQueue> logger, IHostApplicationLifetime lifetime, TimeProvider? timeProvider = null)
    {
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
                break;
            case QueueAckKind.Finished:
                _ = this.store.Finish(agentId, ack.RoomId, ack.MessageIds, ToStored(ack.Outcome), now);
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
        }
    }

    /// <inheritdoc />
    public void MarkRefused(string agentId, string roomId, IReadOnlyList<string> inReplyTo)
    {
        ArgumentNullException.ThrowIfNull(inReplyTo);

        if (this.options.Value.TurnQueue.Enabled)
        {
            _ = this.store.Finish(agentId, roomId, inReplyTo, TurnQueueOutcome.Failed, this.timeProvider.GetUtcNow());
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

        _ = this.store.CloseRunning(connectionId, TurnQueueOutcome.Stopped, this.timeProvider.GetUtcNow());
    }

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
        foreach (string agentId in owedAgentIds)
        {
            if (subscribers.Contains(agentId))
            {
                _ = this.store.Record(agentId, message.Id, roomId, message.Timestamp, now);
            }
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

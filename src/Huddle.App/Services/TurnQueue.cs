using Agency.Huddle.App.Data;
using Agency.Huddle.Contracts;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Services;

/// <summary>
/// The Turn Queue: the server's memory of which Turns Agents owe. Later tasks add the acknowledgement, control and
/// view interfaces to this one class.
/// </summary>
internal sealed class TurnQueue : ITurnLedger
{
    private readonly TurnQueueStore store;
    private readonly IOptions<TeamOptions> options;
    private readonly ILogger<TurnQueue> logger;
    private readonly TimeProvider timeProvider;

    /// <summary>Creates the queue over its store.</summary>
    /// <param name="store">The <c>turn_queue</c> tables.</param>
    /// <param name="options">Supplies <c>Team:TurnQueue:Enabled</c>.</param>
    /// <param name="logger">Receives diagnostics.</param>
    /// <param name="timeProvider">The clock; <see langword="null"/> uses <see cref="TimeProvider.System"/>.</param>
    public TurnQueue(TurnQueueStore store, IOptions<TeamOptions> options, ILogger<TurnQueue> logger, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        this.store = store;
        this.options = options;
        this.logger = logger;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

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

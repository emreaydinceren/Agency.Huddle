using System.Collections.Concurrent;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Goldfish;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Pipes;

/// <summary>
/// Registry of live Agent connections keyed by Agent id. Subscribes to <see cref="RoomEvents.MessagePosted"/>
/// and fans messages out to every online Agent member except the sender. See Team-Specifications.md §6.2, §8.6.
/// </summary>
internal sealed class AgentGateway : IAgentGateway, IDisposable
{
    private readonly RoomEvents events;
    private readonly Drafts drafts;
    private readonly ILogger<AgentGateway> logger;
    private readonly ConcurrentDictionary<string, AgentConnection> connections = new(StringComparer.Ordinal);
    private readonly Action<MessagePostedEvent> onMessagePosted;

    /// <summary>The goldfish supervisor told when an Agent's runner connection is lost or replaced; set by the composition root.</summary>
    internal GoldfishSupervisor? Goldfish { get; set; }

    public AgentGateway(RoomEvents events,Drafts drafts, ILogger<AgentGateway> logger)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(drafts);
        ArgumentNullException.ThrowIfNull(logger);

        this.events = events;
        this.drafts = drafts;
        this.logger = logger;

        // A re-delivery takes exactly the same path: it is the same Message, and an Agent is not told
        // it has seen it before. The sender is still skipped, which is right - it already knows.
        this.onMessagePosted = e => _ = this.DeliverAsync(e);
        this.events.MessagePosted += this.onMessagePosted;
        this.events.MessageRedelivered += this.onMessagePosted;
    }

    /// <inheritdoc />
    public event Action? PresenceChanged;

    public bool IsOnline(string agentId) => this.connections.ContainsKey(agentId);

    /// <inheritdoc />
    public async Task StopTurnAsync(string agentId, string roomId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);

        // A missing connection is the case where there is nothing to stop - the Agent is already
        // gone - so this is a silent no-op rather than an error.
        if (!this.connections.TryGetValue(agentId, out var connection))
        {
            return;
        }

        await connection.SendAsync(new StopTurn(roomId), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task StartAfreshAsync(string agentId, string roomId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);

        // An offline Agent has no session to forget in this process, so this is a silent no-op rather than an error.
        if (!this.connections.TryGetValue(agentId, out var connection))
        {
            return;
        }

        await connection.SendAsync(new StartAfresh(roomId), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Sends one message to an Agent's runner; a silent no-op when the Agent is offline.</summary>
    /// <param name="agentId">The Agent to send to.</param>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>A task that completes when the message is sent or there was no connection.</returns>
    internal async Task SendAsync(string agentId, ProtocolMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentNullException.ThrowIfNull(message);

        if (!this.connections.TryGetValue(agentId, out var connection))
        {
            return;
        }

        await connection.SendAsync(message, cancellationToken).ConfigureAwait(false);
    }

    internal void Register(AgentConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var agentId = connection.Agent?.Id
            ?? throw new InvalidOperationException("A connection can only be registered after the handshake has assigned an Agent.");

        AgentConnection? stale = null;
        this.connections.AddOrUpdate(
            agentId,
            connection,
            (_, existing) =>
            {
                stale = existing;
                return connection;
            });

        if (stale is not null && !ReferenceEquals(stale, connection))
        {
            stale.Close();
            this.ReportRunnerLost(agentId);
        }

        this.RaisePresenceChanged();
    }

    internal void Unregister(AgentConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var agentId = connection.Agent?.Id;
        if (agentId is null)
        {
            return;
        }

        if (!this.connections.TryRemove(new KeyValuePair<string, AgentConnection>(agentId, connection)))
        {
            // A stale connection losing a reconnect race unregisters too, but the registry already
            // moved on to the new one - nothing here actually changed, so there is nothing to clear
            // or announce.
            return;
        }

        // The Agent's session ended without a terminator for whatever Turn was in flight. Left
        // behind, that Draft would freeze on screen forever - the exact failure streaming exists to
        // remove.
        foreach (var roomId in this.drafts.ClearForAgent(agentId))
        {
            this.events.PublishDraftChanged(roomId);
        }

        this.ReportRunnerLost(agentId);
        this.RaisePresenceChanged();
    }

    /// <summary>The Agent's runner connection was lost or replaced: its open possession ends Failed (E-3). A failure is logged and never reaches the caller.</summary>
    private void ReportRunnerLost(string agentId)
    {
        if (this.Goldfish is { } supervisor)
        {
            _ = this.EndLostPossessionAsync(supervisor, agentId);
        }
    }

    private async Task EndLostPossessionAsync(GoldfishSupervisor supervisor, string agentId)
    {
        try
        {
            await supervisor.OnRunnerLostAsync(agentId, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Ending the possession of {AgentId} after its runner was lost failed.", agentId);
        }
    }

    internal async Task DeliverAsync(MessagePostedEvent e, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(e);

        var sends = new List<Task>();
        foreach (var member in e.Members)
        {
            if (!MessagePostedEvent.IsRecipient(member, e.Message.SenderId))
            {
                continue;
            }

            // The Human's answer to a form this Agent opened: its Turn is open and gets the answer as
            // the tool's result, so a delivery would only queue a second Turn.
            if (string.Equals(member.Id, e.WithheldFromAgentId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!this.connections.TryGetValue(member.Id, out var connection))
            {
                continue;
            }

            var payload = ToPayload(e, member.Id);

            sends.Add(this.SendSafeAsync(connection, payload, ct));
        }

        await Task.WhenAll(sends).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task DeliverToAsync(string agentId, MessagePostedEvent e, Redelivery redelivery, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);

        return this.connections.TryGetValue(agentId, out var connection)
            ? this.SendSafeAsync(connection, ToPayload(e, agentId) with { Redelivery = redelivery }, ct)
            : Task.CompletedTask;
    }

    /// <summary>The one place a <see cref="MessagePosted"/> is built, for a live delivery and a re-delivery alike.</summary>
    /// <param name="e">The Message with its Room's Members, Mentions and Budget.</param>
    /// <param name="memberId">The Agent it is built for.</param>
    private static MessagePosted ToPayload(MessagePostedEvent e, string memberId) =>
        new(
            e.Room.Id,
            e.Room.Name,
            e.Message,
            e.Mentions.Any(m => m.Id == memberId),
            e.Mentions.Select(ToMemberInfo).ToList(),
            e.Members.Select(ToMemberInfo).ToList(),
            e.Budget.Used,
            e.Budget.Granted,
            e.Room.Team);

    public Task CloseAllAsync()
    {
        foreach (var connection in this.connections.Values)
        {
            connection.Close();
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        this.events.MessagePosted -= this.onMessagePosted;
        this.events.MessageRedelivered -= this.onMessagePosted;
    }

    private async Task SendSafeAsync(AgentConnection connection, MessagePosted payload, CancellationToken ct)
    {
        try
        {
            await connection.SendAsync(payload, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Failed to deliver a message to an agent connection.");
        }
    }

    private void RaisePresenceChanged()
    {
        EventRaise.Isolated(this.PresenceChanged, ex => this.logger.LogWarning(ex, "A PresenceChanged handler threw and was skipped."));
    }

    private static MemberInfo ToMemberInfo(User user) => new(user.Id, user.Name, user.Kind);
}
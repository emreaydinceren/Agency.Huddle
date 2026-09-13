using System.Collections.Concurrent;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Pipes;

/// <summary>
/// Registry of live Agent connections keyed by Agent id. Subscribes to <see cref="RoomEvents.MessagePosted"/>
/// and fans messages out to every online Agent member except the sender. See Team-Specifications.md §6.2, §8.6.
/// </summary>
public sealed class AgentGateway : IAgentGateway, IDisposable
{
    private readonly RoomEvents events;
    private readonly ILogger<AgentGateway> logger;
    private readonly ConcurrentDictionary<string, AgentConnection> connections = new(StringComparer.Ordinal);
    private readonly Action<MessagePostedEvent> onMessagePosted;

    public AgentGateway(RoomEvents events, ILogger<AgentGateway> logger)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(logger);

        this.events = events;
        this.logger = logger;

        // A re-delivery takes exactly the same path: it is the same Message, and an Agent is not told
        // it has seen it before. The sender is still skipped, which is right - it already knows.
        this.onMessagePosted = e => _ = this.DeliverAsync(e);
        this.events.MessagePosted += this.onMessagePosted;
        this.events.MessageRedelivered += this.onMessagePosted;
    }

    public bool IsOnline(string agentId) => this.connections.ContainsKey(agentId);

    public IReadOnlyCollection<string> OnlineAgentIds => this.connections.Keys.ToArray();

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
        }
    }

    internal void Unregister(AgentConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var agentId = connection.Agent?.Id;
        if (agentId is null)
        {
            return;
        }

        this.connections.TryRemove(new KeyValuePair<string, AgentConnection>(agentId, connection));
    }

    internal async Task DeliverAsync(MessagePostedEvent e, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(e);

        var sends = new List<Task>();
        foreach (var member in e.Members)
        {
            if (member.Kind != UserKind.Agent || member.Id == e.Message.SenderId)
            {
                continue;
            }

            if (!this.connections.TryGetValue(member.Id, out var connection))
            {
                continue;
            }

            var payload = new MessagePosted(
                e.Room.Id,
                e.Room.Name,
                e.Message,
                e.Mentions.Any(m => m.Id == member.Id),
                e.Mentions.Select(ToMemberInfo).ToList(),
                e.Members.Select(ToMemberInfo).ToList(),
                e.Budget.Used,
                e.Budget.Granted);

            sends.Add(this.SendSafeAsync(connection, payload, ct));
        }

        await Task.WhenAll(sends);
    }

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
            await connection.SendAsync(payload, ct);
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Failed to deliver a message to an agent connection.");
        }
    }

    private static MemberInfo ToMemberInfo(User user) => new(user.Id, user.Name, user.Kind);
}
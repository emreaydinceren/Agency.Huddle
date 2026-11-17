using Agency.Huddle.App.Data;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.Contracts;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Services;

/// <summary>
/// Sends the Turns an Agent still owes to its connection again (Turn Queue spec, sections 5.4 and 5.7): the waiting ones after a durable
/// client's Welcome, and the held ones of a Room when its pause ends. Every Message is rebuilt from the Transcript with the Room's current
/// Members, Mentions and Budget, and goes out through <see cref="IAgentGateway.DeliverToAsync"/>.
/// </summary>
internal sealed class TurnRedelivery : IDisposable
{
    private const int FirstTail = 50;

    private readonly ITurnQueueControl control;
    private readonly IAgentGateway gateway;
    private readonly ITeamDirectory team;
    private readonly IChatStore transcript;
    private readonly ChatService chat;
    private readonly IMentionAliasSource aliases;
    private readonly RoomEvents events;
    private readonly IOptions<TeamOptions> options;
    private readonly IHostApplicationLifetime lifetime;
    private readonly ILogger<TurnRedelivery> logger;
    private readonly TimeProvider clock;
    private readonly Action<string> onUnpaused;

    /// <summary>Creates the service and subscribes it to the end of a Room's pause.</summary>
    /// <param name="control">Hands out the waiting rows and closes the ones that are gone.</param>
    /// <param name="gateway">Sends one Message to one connection.</param>
    /// <param name="team">Reads the Room and its Members.</param>
    /// <param name="transcript">Reads the Message of a row back.</param>
    /// <param name="chat">Supplies the Room's current Budget.</param>
    /// <param name="aliases">Supplies the Mention aliases.</param>
    /// <param name="events">Raises the end of a Room's pause.</param>
    /// <param name="options">Supplies <c>Team:TurnQueue:Enabled</c>.</param>
    /// <param name="lifetime">Stops a send the host abandons.</param>
    /// <param name="logger">Receives failures.</param>
    /// <param name="timeProvider">The clock; <see langword="null"/> uses <see cref="TimeProvider.System"/>.</param>
    public TurnRedelivery(
        ITurnQueueControl control,
        IAgentGateway gateway,
        ITeamDirectory team,
        IChatStore transcript,
        ChatService chat,
        IMentionAliasSource aliases,
        RoomEvents events,
        IOptions<TeamOptions> options,
        IHostApplicationLifetime lifetime,
        ILogger<TurnRedelivery> logger,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(team);
        ArgumentNullException.ThrowIfNull(transcript);
        ArgumentNullException.ThrowIfNull(chat);
        ArgumentNullException.ThrowIfNull(aliases);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(lifetime);
        ArgumentNullException.ThrowIfNull(logger);
        this.control = control;
        this.gateway = gateway;
        this.team = team;
        this.transcript = transcript;
        this.chat = chat;
        this.aliases = aliases;
        this.events = events;
        this.options = options;
        this.lifetime = lifetime;
        this.logger = logger;
        this.clock = timeProvider ?? TimeProvider.System;
        this.onUnpaused = roomId => _ = this.SendHeldAsync(roomId, this.lifetime.ApplicationStopping);
        events.BudgetUnpaused += this.onUnpaused;
    }

    /// <summary>Sends every waiting Turn of one Agent, oldest first.</summary>
    /// <param name="agentId">The Agent that just connected.</param>
    /// <param name="ct">Cancels the sends.</param>
    public async Task SendWaitingAsync(string agentId, CancellationToken ct)
    {
        if (!this.options.Value.TurnQueue.Enabled)
        {
            return;
        }

        try
        {
            await this.SendAsync(agentId, null, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            this.logger.LogError(ex, "Re-delivering the waiting Turns of agent {AgentId} failed.", agentId);
        }
    }

    /// <summary>Sends the waiting Turns of every connected, subscribed Agent in one Room, after its pause ended.</summary>
    /// <param name="roomId">The Room.</param>
    /// <param name="ct">Cancels the sends.</param>
    public async Task SendHeldAsync(string roomId, CancellationToken ct)
    {
        if (!this.options.Value.TurnQueue.Enabled)
        {
            return;
        }

        try
        {
            IReadOnlyList<User> members = await this.team.GetRoomMembersAsync(roomId, ct).ConfigureAwait(false);
            foreach (User agent in members.Where(member => member.Kind == UserKind.Agent && this.gateway.IsOnline(member.Id) && this.control.IsSubscribed(member.Id)))
            {
                await this.SendAsync(agent.Id, roomId, ct).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            this.logger.LogError(ex, "Re-delivering the held Turns of room {RoomId} failed.", roomId);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.events.BudgetUnpaused -= this.onUnpaused;
    }

    private async Task SendAsync(string agentId, string? onlyRoomId, CancellationToken ct)
    {
        foreach (WaitingTurn turn in this.control.TakeWaiting(agentId, this.clock.GetUtcNow()))
        {
            if (onlyRoomId is null || string.Equals(turn.Row.RoomId, onlyRoomId, StringComparison.Ordinal))
            {
                await this.SendRowAsync(agentId, turn, ct).ConfigureAwait(false);
            }
        }
    }

    private async Task SendRowAsync(string agentId, WaitingTurn turn, CancellationToken ct)
    {
        TurnQueueRow row = turn.Row;
        Room? room = await this.team.GetRoomAsync(row.RoomId, ct).ConfigureAwait(false);
        IReadOnlyList<User> members = room is null ? [] : await this.team.GetRoomMembersAsync(row.RoomId, ct).ConfigureAwait(false);
        if (room is null || !members.Any(member => string.Equals(member.Id, agentId, StringComparison.Ordinal)))
        {
            this.control.CloseGone(row.Seq, this.clock.GetUtcNow());
            return;
        }

        ChatMessage? message = await this.FindMessageAsync(row.RoomId, row.MessageId, ct).ConfigureAwait(false);
        if (message is null)
        {
            return;
        }

        IReadOnlyList<User> mentions = MentionParser.Parse(message.Text, members, this.aliases.Aliases);
        MessagePostedEvent posted = new(room, message, members, mentions, this.chat.GetBudget(row.RoomId));
        await this.gateway.DeliverToAsync(agentId, posted, turn.CutOff ? Redelivery.CutOff : Redelivery.Waiting, ct).ConfigureAwait(false);
    }

    /// <summary>Finds a Message by id from the end of the Transcript: the count doubles until it is in the tail or the whole file was read.</summary>
    private async Task<ChatMessage?> FindMessageAsync(string roomId, string messageId, CancellationToken ct)
    {
        int count = FirstTail;
        while (true)
        {
            IReadOnlyList<ChatMessage> tail = await this.transcript.ReadTailAsync(roomId, count, ct).ConfigureAwait(false);
            ChatMessage? found = tail.FirstOrDefault(message => string.Equals(message.Id, messageId, StringComparison.Ordinal));
            if (found is not null || tail.Count < count || count > int.MaxValue / 2)
            {
                return found;
            }

            count *= 2;
        }
    }
}

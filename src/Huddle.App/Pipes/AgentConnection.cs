using System.IO.Pipes;
using System.Text.Json;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Pipes;

/// <summary>
/// Per-connection state machine for a single Agent's pipe: handshake, then a read loop that
/// translates <see cref="PostMessage"/> traffic into <see cref="ChatService.PostAsync"/> calls.
/// See Team-Specifications.md §6.2 and §8.1 for the normative flow.
/// </summary>
internal sealed class AgentConnection
{
    private static readonly TimeSpan HelloTimeout = TimeSpan.FromSeconds(5);

    private readonly NamedPipeServerStream pipe;
    private readonly ITeamDirectory teamDirectory;
    private readonly ChatService chat;
    private readonly AgentGateway gateway;
    private readonly Drafts drafts;
    private readonly RoomEvents roomEvents;
    private readonly ILogger<AgentConnection> logger;

    private JsonLineStream? stream;
    private int closed;

    // The membership decision for the most recently checked Draft, keyed by its MessageId. A Draft
    // is one Turn's worth of MessageDelta/ToolActivity envelopes - often one per token of model
    // output - so re-checking membership through teamDirectory (a SQLite query) for every single one
    // would be a query per token. A Draft's Room and sender never change mid-Turn, so one cached
    // entry, replaced whenever the MessageId changes, is sufficient.
    private (string RoomId, string MessageId, ProtocolError? Error)? membershipCache;

    public AgentConnection(
        NamedPipeServerStream pipe,
        ITeamDirectory teamDirectory,
        ChatService chat,
        AgentGateway gateway,
        Drafts drafts,
        RoomEvents roomEvents,
        ILogger<AgentConnection> logger)
    {
        ArgumentNullException.ThrowIfNull(pipe);
        ArgumentNullException.ThrowIfNull(teamDirectory);
        ArgumentNullException.ThrowIfNull(chat);
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(drafts);
        ArgumentNullException.ThrowIfNull(roomEvents);
        ArgumentNullException.ThrowIfNull(logger);

        this.pipe = pipe;
        this.teamDirectory = teamDirectory;
        this.chat = chat;
        this.gateway = gateway;
        this.drafts = drafts;
        this.roomEvents = roomEvents;
        this.logger = logger;
    }

    public string ConnectionId { get; } = Guid.CreateVersion7().ToString("N");

    public User? Agent { get; private set; }

    public DateTimeOffset ConnectedAt { get; } = DateTimeOffset.UtcNow;

    public async Task RunAsync(CancellationToken ct)
    {
        await using var lineStream = new JsonLineStream(this.pipe);
        this.stream = lineStream;

        try
        {
            var hello = await ReadHelloAsync(lineStream, ct);
            if (hello is null)
            {
                return;
            }

            if (!NameRules.IsValidAgentName(hello.Name))
            {
                await SendBestEffortAsync(lineStream, new ProtocolError(ErrorCodes.InvalidName, $"'{hello.Name}' is not a valid agent name."), ct);
                return;
            }

            var user = await this.teamDirectory.UpsertAgentUserAsync(hello.Name, hello.Description, ct);
            if (user is null)
            {
                await SendBestEffortAsync(lineStream, new ProtocolError(ErrorCodes.NameReserved, $"'{hello.Name}' is reserved."), ct);
                return;
            }

            this.Agent = user;
            await this.chat.EnsureRoomForAsync(user, ct);
            this.gateway.Register(this);

            var welcome = await this.BuildWelcomeAsync(user, ct);
            await lineStream.WriteAsync(welcome, ct);

            await this.ReadLoopAsync(lineStream, ct);
        }
        catch (IOException ex)
        {
            var connectionId = this.ConnectionId;
            this.logger.LogWarning(ex, "Agent connection {ConnectionId} ended.", connectionId);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown: host stopping or the connection was closed from elsewhere.
        }
        catch (ObjectDisposedException)
        {
            // Normal shutdown: the pipe was disposed while an operation was in flight.
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Agent connection {ConnectionId} failed unexpectedly.", this.ConnectionId);
        }
        finally
        {
            this.gateway.Unregister(this);

            if (this.logger.IsEnabled(LogLevel.Information))
            {
                if (this.Agent is { } agent)
                {
                    this.logger.LogInformation("Agent connection {ConnectionId} for agent {AgentName} disconnected.", this.ConnectionId, agent.Name);
                }
                else
                {
                    this.logger.LogInformation("Agent connection {ConnectionId} disconnected before registration.", this.ConnectionId);
                }
            }

            this.pipe.Dispose();
        }
    }

    public async Task SendAsync(ProtocolMessage message, CancellationToken ct)
    {
        var current = this.stream;
        if (current is null)
        {
            return;
        }

        try
        {
            await current.WriteAsync(message, ct);
        }
        catch (IOException)
        {
            this.Close();
        }
        catch (ObjectDisposedException)
        {
            this.Close();
        }
    }

    public void Close()
    {
        if (Interlocked.Exchange(ref this.closed, 1) == 0)
        {
            this.pipe.Dispose();
        }
    }

    private static async Task<Hello?> ReadHelloAsync(JsonLineStream lineStream, CancellationToken ct)
    {
        using var helloCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        helloCts.CancelAfter(HelloTimeout);

        ProtocolMessage? first;
        try
        {
            first = await lineStream.ReadAsync(helloCts.Token);
        }
        catch (OperationCanceledException)
        {
            await SendBestEffortAsync(lineStream, new ProtocolError(ErrorCodes.ExpectedHello, "Timed out waiting for 'hello'."), ct);
            return null;
        }
        catch (JsonException ex)
        {
            await SendBestEffortAsync(lineStream, new ProtocolError(ErrorCodes.ExpectedHello, ex.Message), ct);
            return null;
        }

        if (first is not Hello hello)
        {
            await SendBestEffortAsync(lineStream, new ProtocolError(ErrorCodes.ExpectedHello, "Expected 'hello' as the first message."), ct);
            return null;
        }

        return hello;
    }

    private async Task<Welcome> BuildWelcomeAsync(User user, CancellationToken ct)
    {
        var rooms = await this.teamDirectory.GetRoomsForUserAsync(user.Id, ct);
        var roomInfos = new List<RoomInfo>(rooms.Count);
        foreach (var room in rooms)
        {
            var members = await this.teamDirectory.GetRoomMembersAsync(room.Id, ct);
            roomInfos.Add(new RoomInfo(room.Id, room.Name, members.Select(ToMemberInfo).ToList()));
        }

        return new Welcome(user.Id, user.Name, roomInfos);
    }

    private async Task ReadLoopAsync(JsonLineStream lineStream, CancellationToken ct)
    {
        while (true)
        {
            ProtocolMessage? message;
            try
            {
                message = await lineStream.ReadAsync(ct);
            }
            catch (JsonException ex)
            {
                await lineStream.WriteAsync(new ProtocolError(ErrorCodes.BadMessage, ex.Message), ct);
                continue;
            }

            if (message is null)
            {
                return;
            }

            switch (message)
            {
                case PostMessage post:
                await this.HandlePostMessageAsync(lineStream, post, ct);
                break;
                case MessageDelta delta:
                await this.HandleMessageDeltaAsync(lineStream, delta, ct);
                break;
                case ToolActivity activity:
                await this.HandleToolActivityAsync(lineStream, activity, ct);
                break;
                case Hello:
                await lineStream.WriteAsync(new ProtocolError(ErrorCodes.BadMessage, "already registered"), ct);
                break;
                default:
                await lineStream.WriteAsync(new ProtocolError(ErrorCodes.BadMessage, "Unexpected message type."), ct);
                break;
            }
        }
    }

    private async Task HandlePostMessageAsync(JsonLineStream lineStream, PostMessage post, CancellationToken ct)
    {
        if (post.MessageId is not null && !NameRules.IsValidId(post.MessageId))
        {
            await lineStream.WriteAsync(new ProtocolError(ErrorCodes.BadMessage, "Invalid message id.", post.MessageId), ct);
            return;
        }

        try
        {
            await this.chat.PostAsync(post.RoomId, this.Agent!.Id, post.Text, post.MessageId, ct);
        }
        catch (ChatException ex)
        {
            await lineStream.WriteAsync(new ProtocolError(ex.Code, ex.Message, post.MessageId), ct);
            return;
        }

        // The real Message now exists under this id, so the Draft that led up to it is gone the
        // instant the Room view has something better to show.
        if (post.MessageId is not null)
        {
            this.drafts.Complete(post.MessageId);
            this.roomEvents.PublishDraftChanged(post.RoomId);
        }
    }

    private async Task HandleMessageDeltaAsync(JsonLineStream lineStream, MessageDelta delta, CancellationToken ct)
    {
        if (!NameRules.IsValidId(delta.MessageId))
        {
            await lineStream.WriteAsync(new ProtocolError(ErrorCodes.BadMessage, "Invalid message id.", delta.MessageId), ct);
            return;
        }

        var membershipError = await this.CheckMembershipAsync(delta.RoomId, delta.MessageId, ct);
        if (membershipError is not null)
        {
            await lineStream.WriteAsync(membershipError, ct);
            return;
        }

        if (delta.IsFinal)
        {
            this.drafts.Complete(delta.MessageId);
        }
        else
        {
            this.drafts.Append(delta.MessageId, delta.RoomId, this.Agent!.Id, this.Agent!.Name, delta.Text);
        }

        this.roomEvents.PublishDraftChanged(delta.RoomId);
    }

    private async Task HandleToolActivityAsync(JsonLineStream lineStream, ToolActivity activity, CancellationToken ct)
    {
        if (!NameRules.IsValidId(activity.MessageId))
        {
            await lineStream.WriteAsync(new ProtocolError(ErrorCodes.BadMessage, "Invalid message id.", activity.MessageId), ct);
            return;
        }

        var membershipError = await this.CheckMembershipAsync(activity.RoomId, activity.MessageId, ct);
        if (membershipError is not null)
        {
            await lineStream.WriteAsync(membershipError, ct);
            return;
        }

        this.drafts.Activity(
            activity.MessageId, activity.ToolCallId, activity.RoomId, this.Agent!.Id, this.Agent!.Name, activity.Title, activity.Status);

        this.roomEvents.PublishDraftChanged(activity.RoomId);
    }

    /// <summary>
    /// Resolves whether <see cref="Agent"/> may write a Draft into <paramref name="roomId"/> under
    /// <paramref name="messageId"/>, reusing the last decision when <paramref name="messageId"/> is
    /// unchanged from the previous call. See <see cref="membershipCache"/> for why one cached entry
    /// is enough.
    /// </summary>
    /// <returns>
    /// <see langword="null"/> when the Agent is a Member of the Room; otherwise the
    /// <see cref="ProtocolError"/> to send back.
    /// </returns>
    private async Task<ProtocolError?> CheckMembershipAsync(string roomId, string messageId, CancellationToken ct)
    {
        // Keyed on the Room AND the Message id, never the Message id alone. This connection is the
        // validation boundary for anything that dials the pipe, not just for PersonaRunner: a client
        // is free to reuse one message id across two Rooms, and a cache keyed only on the id would
        // then hand Room A's verdict to Room B and let an Agent write a Draft into a Room it is not
        // a Member of.
        if (this.membershipCache is { } cached
            && string.Equals(cached.RoomId, roomId, StringComparison.Ordinal)
            && string.Equals(cached.MessageId, messageId, StringComparison.Ordinal))
        {
            return cached.Error;
        }

        var error = await this.ResolveMembershipErrorAsync(roomId, messageId, ct);
        this.membershipCache = (roomId, messageId, error);
        return error;
    }

    private async Task<ProtocolError?> ResolveMembershipErrorAsync(string roomId, string messageId, CancellationToken ct)
    {
        var room = await this.teamDirectory.GetRoomAsync(roomId, ct);
        if (room is null)
        {
            return new ProtocolError(ErrorCodes.UnknownRoom, $"Unknown room '{roomId}'.", messageId);
        }

        var members = await this.teamDirectory.GetRoomMembersAsync(roomId, ct);
        var isMember = members.Any(m => m.Id == this.Agent!.Id);
        if (!isMember)
        {
            return new ProtocolError(ErrorCodes.NotMember, $"'{this.Agent!.Id}' is not a member of room '{roomId}'.", messageId);
        }

        return null;
    }

    private static async Task SendBestEffortAsync(JsonLineStream lineStream, ProtocolError error, CancellationToken ct)
    {
        try
        {
            await lineStream.WriteAsync(error, ct);
        }
        catch (IOException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static MemberInfo ToMemberInfo(User user) => new(user.Id, user.Name, user.Kind);
}
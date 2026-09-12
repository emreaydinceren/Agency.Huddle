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
public sealed class AgentConnection
{
    private static readonly TimeSpan HelloTimeout = TimeSpan.FromSeconds(5);

    private readonly NamedPipeServerStream pipe;
    private readonly ITeamDirectory teamDirectory;
    private readonly ChatService chat;
    private readonly AgentGateway gateway;
    private readonly ILogger<AgentConnection> logger;

    private JsonLineStream? stream;
    private int closed;

    public AgentConnection(
        NamedPipeServerStream pipe, ITeamDirectory teamDirectory, ChatService chat, AgentGateway gateway, ILogger<AgentConnection> logger)
    {
        ArgumentNullException.ThrowIfNull(pipe);
        ArgumentNullException.ThrowIfNull(teamDirectory);
        ArgumentNullException.ThrowIfNull(chat);
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(logger);

        this.pipe = pipe;
        this.teamDirectory = teamDirectory;
        this.chat = chat;
        this.gateway = gateway;
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
                await lineStream.WriteAsync(
                    new ProtocolError(ErrorCodes.NotSupported, "messageDelta is reserved for a later protocol version", delta.MessageId), ct);
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
        }
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
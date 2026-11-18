using System.Globalization;
using System.IO.Pipes;
using System.Text.Json;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using Microsoft.Data.Sqlite;

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
    private readonly IChatStore chatStore;
    private readonly AgentGateway gateway;
    private readonly Drafts drafts;
    private readonly RoomEvents roomEvents;
    private readonly FreshStartNotes freshStartNotes;
    private readonly ITurnAcks acks;
    private readonly ITurnQueueControl control;
    private readonly TurnRedelivery redelivery;
    private readonly ILogger<AgentConnection> logger;
    private readonly TimeProvider clock;

    private JsonLineStream? stream;
    private int closed;

    // Where the handshake had got to, and why it stopped there. Read only when the connection ends before its
    // Welcome is written, so a client that loses the handshake (CIPipeline.md: the RoomSessionResumeTests broken
    // pipe and connection reset) leaves one Warning saying which step the server was on and what ended it.
    private string handshakeStep = "waiting for 'hello'";
    private string? handshakeEnd;
    private Exception? handshakeFault;
    private bool welcomed;

    // Completed once the Welcome is on the wire (or the connection ends without one). RunAsync
    // registers with the gateway BEFORE building the Welcome, so no Message can fall between the
    // Welcome's snapshot and live delivery - but that also makes this connection a delivery target
    // while the handshake is still in progress. The Welcome carries no history, so a MessagePosted
    // sent in that window must be held rather than dropped; SendAsync awaits this so it can never
    // overtake the Welcome, which the runner requires as the first envelope it reads.
    private readonly TaskCompletionSource welcomeSent = new(TaskCreationOptions.RunContinuationsAsynchronously);

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
        IChatStore chatStore,
        AgentGateway gateway,
        Drafts drafts,
        RoomEvents roomEvents,
        FreshStartNotes freshStartNotes,
        ITurnAcks acks,
        ITurnQueueControl control,
        TurnRedelivery redelivery,
        ILogger<AgentConnection> logger,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(redelivery);
        this.redelivery = redelivery;
        ArgumentNullException.ThrowIfNull(freshStartNotes);
        ArgumentNullException.ThrowIfNull(acks);
        ArgumentNullException.ThrowIfNull(control);
        this.freshStartNotes = freshStartNotes;
        this.acks = acks;
        this.control = control;
        ArgumentNullException.ThrowIfNull(pipe);
        ArgumentNullException.ThrowIfNull(teamDirectory);
        ArgumentNullException.ThrowIfNull(chat);
        ArgumentNullException.ThrowIfNull(chatStore);
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(drafts);
        ArgumentNullException.ThrowIfNull(roomEvents);
        ArgumentNullException.ThrowIfNull(logger);

        this.pipe = pipe;
        this.teamDirectory = teamDirectory;
        this.chat = chat;
        this.chatStore = chatStore;
        this.gateway = gateway;
        this.drafts = drafts;
        this.roomEvents = roomEvents;
        this.logger = logger;
        this.clock = timeProvider ?? TimeProvider.System;
        this.ConnectedAt = this.clock.GetUtcNow();
    }

    public string ConnectionId { get; } = Guid.CreateVersion7().ToString("N");

    public User? Agent { get; private set; }

    public DateTimeOffset ConnectedAt { get; }

    public async Task RunAsync(CancellationToken ct)
    {
        var lineStream = new JsonLineStream(this.pipe);
        await using var lineStreamScope = lineStream.ConfigureAwait(false);
        this.stream = lineStream;
        if (this.logger.IsEnabled(LogLevel.Debug))
        {
            this.logger.LogDebug("Agent connection {ConnectionId} opened.", this.ConnectionId);
        }

        try
        {
            if (!await this.HandshakeAsync(lineStream, ct).ConfigureAwait(false))
            {
                return;
            }

            await this.ReadLoopAsync(lineStream, ct).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            if (this.welcomed)
            {
                var connectionId = this.ConnectionId;
                this.logger.LogWarning(ex, "Agent connection {ConnectionId} ended.", connectionId);
            }
            else
            {
                this.RecordHandshakeFault(ex);
            }
        }
        catch (OperationCanceledException ex)
        {
            // Normal shutdown: host stopping or the connection was closed from elsewhere.
            this.RecordHandshakeFault(ex);
        }
        catch (ObjectDisposedException ex)
        {
            // Normal shutdown: the pipe was disposed while an operation was in flight.
            this.RecordHandshakeFault(ex);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Agent connection {ConnectionId} failed unexpectedly.", this.ConnectionId);
        }
        finally
        {
            // Releases any send held for a Welcome that will now never be written; it then meets
            // the disposed pipe and fails quietly in SendAsync, instead of waiting forever.
            this.welcomeSent.TrySetResult();
            this.gateway.Unregister(this);
            this.CloseRunningTurns();
            this.LogDisconnect();

            await this.pipe.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Closes the rows this connection started, once an Agent was registered. Not in <c>AgentGateway.Unregister</c>: a stale
    /// connection unregisters after its replacement registered, and this closes only the rows this connection's own id started (spec §5.6).
    /// </summary>
    private void CloseRunningTurns()
    {
        if (this.Agent is not null)
        {
            this.GuardQueue(() => this.control.CloseRunning(this.ConnectionId), "closing the running Turns of an ended connection");
        }
    }

    /// <summary>Records why the handshake ended, when it ended on an exception before the Welcome was written.</summary>
    /// <param name="ex">The exception that ended it.</param>
    private void RecordHandshakeFault(Exception ex)
    {
        if (!this.welcomed)
        {
            this.handshakeFault = ex;
            this.handshakeEnd ??= Volatile.Read(ref this.closed) == 1
                ? $"the app closed the connection ({ex.GetType().Name})"
                : $"{ex.GetType().Name}: {ex.Message}";
        }
    }

    /// <summary>
    /// Logs the end of the connection: one Information line once the Agent was welcomed, and one Warning naming the
    /// handshake step, its reason and the time since the connection opened when it ended before the Welcome was written.
    /// </summary>
    private void LogDisconnect()
    {
        if (!this.welcomed)
        {
            string elapsedMs = (this.clock.GetUtcNow() - this.ConnectedAt).TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture);
            this.logger.LogWarning(
                this.handshakeFault,
                "Agent connection {ConnectionId} ended before its Welcome was sent, {ElapsedMs} ms after it opened, while {HandshakeStep}: {HandshakeEnd}.",
                this.ConnectionId,
                elapsedMs,
                this.handshakeStep,
                this.handshakeEnd ?? "no reason was recorded");
        }
        else if (this.logger.IsEnabled(LogLevel.Information) && this.Agent is { } agent)
        {
            this.logger.LogInformation("Agent connection {ConnectionId} for agent {AgentName} disconnected.", this.ConnectionId, agent.Name);
        }
    }

    /// <summary>Reads the Hello, registers the agent and writes the Welcome.</summary>
    /// <param name="lineStream">The connection's line stream.</param>
    /// <param name="ct">Cancels the handshake.</param>
    /// <returns><see langword="true"/> when the agent is registered and welcomed; <see langword="false"/> when the connection must end.</returns>
    private async Task<bool> HandshakeAsync(JsonLineStream lineStream, CancellationToken ct)
    {
        var hello = await this.ReadHelloAsync(lineStream, ct).ConfigureAwait(false);
        if (hello is null)
        {
            return false;
        }

        this.handshakeStep = $"registering '{hello.Name}'";
        if (!NameRules.IsValidAgentName(hello.Name))
        {
            this.handshakeEnd = "the name is not a valid agent name";
            await SendBestEffortAsync(lineStream, new ProtocolError(ErrorCodes.InvalidName, $"'{hello.Name}' is not a valid agent name."), ct).ConfigureAwait(false);
            return false;
        }

        var user = await this.teamDirectory.UpsertAgentUserAsync(hello.Name, hello.Description, ct).ConfigureAwait(false);
        if (user is null)
        {
            this.handshakeEnd = "the name is reserved";
            await SendBestEffortAsync(lineStream, new ProtocolError(ErrorCodes.NameReserved, $"'{hello.Name}' is reserved."), ct).ConfigureAwait(false);
            return false;
        }

        this.Agent = user;
        await this.chat.EnsureRoomForAsync(user, FeedActor.Huddle, ct).ConfigureAwait(false);
        this.GuardQueue(() => this.acks.SetSubscribed(user.Id, hello.DurableQueue == true), "recording the Hello's durable queue flag");
        this.gateway.Register(this);

        this.handshakeStep = $"building the Welcome for '{hello.Name}'";
        var welcome = await this.BuildWelcomeAsync(user, ct).ConfigureAwait(false);
        this.handshakeStep = $"writing the Welcome for '{hello.Name}'";
        await lineStream.WriteAsync(welcome, ct).ConfigureAwait(false);
        if (hello.DurableQueue == true)
        {
            // Sent after the Welcome (SendAsync waits for it) and never awaited: the read loop must start at once.
            _ = this.RedeliverWaitingAsync(user.Id, ct);
        }

        this.welcomed = true;
        this.welcomeSent.TrySetResult();
        return true;
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
            await this.welcomeSent.Task.WaitAsync(ct).ConfigureAwait(false);
            await current.WriteAsync(message, ct).ConfigureAwait(false);
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

    private async Task<Hello?> ReadHelloAsync(JsonLineStream lineStream, CancellationToken ct)
    {
        using var helloCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        helloCts.CancelAfter(HelloTimeout);

        ProtocolMessage? first;
        try
        {
            first = await lineStream.ReadAsync(helloCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            this.handshakeEnd = ct.IsCancellationRequested ? "the host is stopping" : "no 'hello' arrived within the timeout";
            await SendBestEffortAsync(lineStream, new ProtocolError(ErrorCodes.ExpectedHello, "Timed out waiting for 'hello'."), ct).ConfigureAwait(false);
            return null;
        }
        catch (JsonException ex)
        {
            this.handshakeEnd = $"the first line is not a message: {ex.Message}";
            await SendBestEffortAsync(lineStream, new ProtocolError(ErrorCodes.ExpectedHello, ex.Message), ct).ConfigureAwait(false);
            return null;
        }

        if (first is not Hello hello)
        {
            this.handshakeEnd = first is null
                ? "the client closed its end before sending anything"
                : $"the first message is a {first.GetType().Name}, not a Hello";
            await SendBestEffortAsync(lineStream, new ProtocolError(ErrorCodes.ExpectedHello, "Expected 'hello' as the first message."), ct).ConfigureAwait(false);
            return null;
        }

        return hello;
    }

    private async Task<Welcome> BuildWelcomeAsync(User user, CancellationToken ct)
    {
        var rooms = await this.teamDirectory.GetRoomsForUserAsync(user.Id, ct).ConfigureAwait(false);
        var roomInfos = new List<RoomInfo>(rooms.Count);
        foreach (var room in rooms)
        {
            var members = await this.teamDirectory.GetRoomMembersAsync(room.Id, ct).ConfigureAwait(false);
            var isEmpty = !await this.chatStore.HasMessagesAsync(room.Id, ct).ConfigureAwait(false);
            roomInfos.Add(new RoomInfo(room.Id, room.Name, members.Select(ToMemberInfo).ToList(), isEmpty, room.Team));
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
                message = await lineStream.ReadAsync(ct).ConfigureAwait(false);
            }
            catch (JsonException ex)
            {
                await lineStream.WriteAsync(new ProtocolError(ErrorCodes.BadMessage, ex.Message), ct).ConfigureAwait(false);
                continue;
            }

            if (message is null)
            {
                return;
            }

            switch (message)
            {
                case PostMessage post:
                await this.HandlePostMessageAsync(lineStream, post, ct).ConfigureAwait(false);
                break;
                case MessageDelta delta:
                await this.HandleMessageDeltaAsync(lineStream, delta, ct).ConfigureAwait(false);
                break;
                case ToolActivity activity:
                await this.HandleToolActivityAsync(lineStream, activity, ct).ConfigureAwait(false);
                break;
                case FreshStartNote note:
                await this.HandleFreshStartNoteAsync(lineStream, note, ct).ConfigureAwait(false);
                break;
                case Hello:
                await lineStream.WriteAsync(new ProtocolError(ErrorCodes.BadMessage, "already registered"), ct).ConfigureAwait(false);
                break;
                case ReadTranscript read:
                await this.HandleReadTranscriptAsync(lineStream, read, ct).ConfigureAwait(false);
                break;
                case QueueAck ack:
                await this.HandleQueueAckAsync(lineStream, ack, ct).ConfigureAwait(false);
                break;
                case GoldfishReport report:
                await this.HandleGoldfishReportAsync(report, ct).ConfigureAwait(false);
                break;
                default:
                await lineStream.WriteAsync(new ProtocolError(ErrorCodes.BadMessage, "Unexpected message type."), ct).ConfigureAwait(false);
                break;
            }
        }
    }

    /// <summary>Hands a runner's goldfish report to the supervisor; a failing store is logged and never ends the connection.</summary>
    private async Task HandleGoldfishReportAsync(GoldfishReport report, CancellationToken ct)
    {
        if (this.gateway.Goldfish is not { } supervisor || this.Agent is not { } agent)
        {
            return;
        }

        try
        {
            await supervisor.OnReportAsync(agent.Id, report, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            this.logger.LogError(ex, "Applying a goldfish report failed on connection {ConnectionId}.", this.ConnectionId);
        }
    }

    private async Task HandlePostMessageAsync(JsonLineStream lineStream, PostMessage post, CancellationToken ct)
    {
        if (post.MessageId is not null && !NameRules.IsValidId(post.MessageId))
        {
            await lineStream.WriteAsync(new ProtocolError(ErrorCodes.BadMessage, "Invalid message id.", post.MessageId), ct).ConfigureAwait(false);
            return;
        }

        User agent = this.Agent ?? throw new InvalidOperationException("A PostMessage was handled before the handshake set the Agent.");
        string agentId = agent.Id;
        List<string> inReplyTo = ValidIds(post.InReplyTo);
        try
        {
            await this.chat.PostAsync(post.RoomId, agentId, post.Text, post.MessageId, ct).ConfigureAwait(false);
        }
        catch (ChatException ex)
        {
            if (inReplyTo.Count > 0)
            {
                this.GuardQueue(() => this.acks.MarkRefused(agentId, post.RoomId, inReplyTo), "closing the Turns of a refused reply");
            }

            await lineStream.WriteAsync(new ProtocolError(ex.Code, ex.Message, post.MessageId), ct).ConfigureAwait(false);
            return;
        }

        if (inReplyTo.Count > 0)
        {
            this.GuardQueue(() => this.acks.MarkReplied(agentId, post.RoomId, inReplyTo), "closing the Turns a reply answers");
        }

        // The real Message now exists under this id, so the Draft that led up to it is gone the
        // instant the Room view has something better to show.
        if (post.MessageId is not null)
        {
            this.drafts.Complete(post.MessageId);
            this.roomEvents.PublishDraftChanged(post.RoomId);
        }
    }

    /// <summary>
    /// Applies a <see cref="QueueAck"/>: refuses a Room the Agent is not a Member of exactly as <see cref="ReadTranscript"/> does,
    /// drops every id that is not a valid id, and hands what is left to the Turn Queue.
    /// </summary>
    private async Task HandleQueueAckAsync(JsonLineStream lineStream, QueueAck ack, CancellationToken ct)
    {
        if (this.Agent is not { } agent)
        {
            return;
        }

        // The first Message id stands in as the error's related id; the Room id when the ack names none.
        string related = ack.MessageIds is [string first, ..] ? first : ack.RoomId;
        var membershipError = await this.ResolveMembershipErrorAsync(ack.RoomId, related, ct).ConfigureAwait(false);
        if (membershipError is not null)
        {
            await lineStream.WriteAsync(membershipError, ct).ConfigureAwait(false);
            return;
        }

        List<string> ids = ValidIds(ack.MessageIds);
        if (ids.Count == 0)
        {
            return;
        }

        string? replyId = NameRules.IsValidId(ack.ReplyMessageId) ? ack.ReplyMessageId : null;
        QueueAck clean = ack with { MessageIds = ids, ReplyMessageId = replyId };
        this.GuardQueue(() => this.acks.Apply(this.ConnectionId, agent.Id, clean), "applying a queue ack");
    }

    /// <summary>The ids of <paramref name="ids"/> that pass <see cref="NameRules.IsValidId"/>, in order; empty for <see langword="null"/>.</summary>
    /// <param name="ids">The ids as received.</param>
    private static List<string> ValidIds(IReadOnlyList<string>? ids) =>
        ids is null ? [] : [.. ids.Where(id => NameRules.IsValidId(id))];

    /// <summary>Sends the Turns this Agent still owes after its Welcome; a failure is logged and ends only this task.</summary>
    /// <param name="agentId">The Agent that connected.</param>
    /// <param name="ct">The connection's token.</param>
    private async Task RedeliverWaitingAsync(string agentId, CancellationToken ct)
    {
        try
        {
            await this.redelivery.SendWaitingAsync(agentId, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The connection or the host ended first; nothing is owed to a closed pipe.
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Re-delivering the waiting Turns failed on connection {ConnectionId}.", this.ConnectionId);
        }
    }

    /// <summary>
    /// Runs a call into the Turn Queue so that a failing store is logged and never ends the connection: the general catch in
    /// <see cref="RunAsync"/> would close it (spec §5.5).
    /// </summary>
    /// <param name="call">The call.</param>
    /// <param name="what">What the call was for, for the log.</param>
    private void GuardQueue(Action call, string what)
    {
        try
        {
            call();
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            this.logger.LogError(ex, "The Turn Queue failed while {What} on connection {ConnectionId}.", what, this.ConnectionId);
        }
    }

    private async Task HandleMessageDeltaAsync(JsonLineStream lineStream, MessageDelta delta, CancellationToken ct)
    {
        if (!NameRules.IsValidId(delta.MessageId))
        {
            await lineStream.WriteAsync(new ProtocolError(ErrorCodes.BadMessage, "Invalid message id.", delta.MessageId), ct).ConfigureAwait(false);
            return;
        }

        var membershipError = await this.CheckMembershipAsync(delta.RoomId, delta.MessageId, ct).ConfigureAwait(false);
        if (membershipError is not null)
        {
            await lineStream.WriteAsync(membershipError, ct).ConfigureAwait(false);
            return;
        }

        if (delta.IsFinal)
        {
            this.drafts.Complete(delta.MessageId);
        }
        else
        {
            User agent = this.Agent ?? throw new InvalidOperationException("A MessageDelta was handled before the handshake set the Agent.");
            this.drafts.Append(delta.MessageId, delta.RoomId, agent.Id, agent.Name, delta.Text);
        }

        this.roomEvents.PublishDraftChanged(delta.RoomId);
    }

    private async Task HandleFreshStartNoteAsync(JsonLineStream lineStream, FreshStartNote note, CancellationToken ct)
    {
        if (!NameRules.IsValidId(note.RoomId))
        {
            await lineStream.WriteAsync(new ProtocolError(ErrorCodes.BadMessage, "Invalid room id."), ct).ConfigureAwait(false);
            return;
        }

        // The note has no Message id; the Room id stands in for it in the membership check and its error.
        var membershipError = await this.CheckMembershipAsync(note.RoomId, note.RoomId, ct).ConfigureAwait(false);
        if (membershipError is not null)
        {
            await lineStream.WriteAsync(membershipError, ct).ConfigureAwait(false);
            return;
        }

        if (this.Agent is not { } agent)
        {
            return;
        }

        // Counts are display data: clamp rather than refuse, as a ToolActivity is sanitised.
        FreshStartNote clean = note with
        {
            MessageCount = Math.Max(0, note.MessageCount),
            EstimatedTokens = Math.Max(0, note.EstimatedTokens),
        };
        this.freshStartNotes.Set(clean.RoomId, agent.Name, clean);
        this.roomEvents.PublishFreshStartNotesChanged(clean.RoomId);
    }

    private async Task HandleToolActivityAsync(JsonLineStream lineStream, ToolActivity activity, CancellationToken ct)
    {
        if (!NameRules.IsValidId(activity.MessageId))
        {
            await lineStream.WriteAsync(new ProtocolError(ErrorCodes.BadMessage, "Invalid message id.", activity.MessageId), ct).ConfigureAwait(false);
            return;
        }

        var membershipError = await this.CheckMembershipAsync(activity.RoomId, activity.MessageId, ct).ConfigureAwait(false);
        if (membershipError is not null)
        {
            await lineStream.WriteAsync(membershipError, ct).ConfigureAwait(false);
            return;
        }

        // The pipe is a validation boundary, so the display-only members are sanitised, never
        // trusted. A violation never rejects the activity: a ProtocolError here would throw away a
        // status change because a preview was large, which is the wrong trade for display data.
        string? title = ToolActivityLimits.Clip(activity.Title, ToolActivityLimits.MaxTitleLength, out _);
        string? path = activity.Path is { Length: > 0 and <= ToolActivityLimits.MaxPathLength } ? activity.Path : null;
        int? line = activity.Line is >= 1 ? activity.Line : null;
        EditChange? edit = SanitiseEdit(activity.Edit);
        string? arguments = ToolActivityLimits.Clip(activity.Arguments, ToolActivityLimits.MaxArgumentsLength, out _);
        string? result = ToolActivityLimits.Clip(activity.Result, ToolActivityLimits.MaxResultLength, out _);
        // Unreachable in practice - the membership check above already needed an Agent - but it is what
        // lets the rest of this method read the Agent without a null-forgiving operator.
        if (this.Agent is not { } agent)
        {
            return;
        }

        if ((path != activity.Path || line != activity.Line) && this.logger.IsEnabled(LogLevel.Debug))
        {
            this.logger.LogDebug(
                "Dropped an out-of-range path or line on a tool activity for room {RoomId} from agent {AgentId}.",
                activity.RoomId,
                agent.Id);
        }

        this.drafts.Activity(
            activity.MessageId, activity.ToolCallId, activity.RoomId, agent.Id, agent.Name, title, activity.Status, path, line, edit, arguments, result);

        this.roomEvents.PublishDraftChanged(activity.RoomId);
    }

    /// <summary>
    /// Clips each side of <paramref name="edit"/> to <see cref="ToolActivityLimits.MaxEditSideLength"/>,
    /// marks it truncated when either side was cut, and floors <see cref="EditChange.OmittedChanges"/>
    /// at zero. The runner already clips, but a peer on this pipe is not trusted to.
    /// </summary>
    /// <param name="edit">The change as received, or <see langword="null"/>.</param>
    private static EditChange? SanitiseEdit(EditChange? edit)
    {
        if (edit is null)
        {
            return null;
        }

        string? oldText = ToolActivityLimits.Clip(edit.OldText, ToolActivityLimits.MaxEditSideLength, out bool oldClipped);
        string? newText = ToolActivityLimits.Clip(edit.NewText, ToolActivityLimits.MaxEditSideLength, out bool newClipped);
        return new EditChange(oldText, newText, edit.Truncated || oldClipped || newClipped, Math.Max(edit.OmittedChanges, 0));
    }

    /// <summary>
    /// Answers a <see cref="ReadTranscript"/> (RS §6.5), for a Room Session's first Turn: slices the
    /// Room's Transcript between <see cref="ReadTranscript.AfterMessageId"/> (exclusive) and
    /// <see cref="ReadTranscript.BeforeMessageId"/> (exclusive), takes at most the latest
    /// <see cref="ReadTranscript.Max"/> of that range, and trims further if the answer would not fit
    /// the wire (finding P-21). Sent only to this connection - never broadcast - per ADR-0003: the
    /// server labels, the client decides.
    /// </summary>
    private async Task HandleReadTranscriptAsync(JsonLineStream lineStream, ReadTranscript read, CancellationToken ct)
    {
        if (read.Max < 1)
        {
            await lineStream.WriteAsync(
                new ProtocolError(ErrorCodes.BadMessage, "max must be at least 1.", read.RequestId), ct).ConfigureAwait(false);
            return;
        }

        // ResolveMembershipErrorAsync directly, never CheckMembershipAsync: that cache is keyed on a
        // Message id, and RequestId is not one - reusing it would risk handing this Room's verdict to
        // an unrelated cache entry, or vice versa.
        var membershipError = await this.ResolveMembershipErrorAsync(read.RoomId, read.RequestId, ct).ConfigureAwait(false);
        if (membershipError is not null)
        {
            await lineStream.WriteAsync(membershipError, ct).ConfigureAwait(false);
            return;
        }

        var transcript = await this.chatStore.ReadAllAsync(read.RoomId, ct).ConfigureAwait(false);

        var beforeIndex = IndexOf(transcript, read.BeforeMessageId); // An id not in the Transcript reads to the end; a re-sent Turn relies on it (Turn Queue spec 5.8).
        var end = beforeIndex >= 0 ? beforeIndex : transcript.Count;
        var afterIndex = read.AfterMessageId is null ? -1 : IndexOf(transcript, read.AfterMessageId);
        var start = afterIndex >= 0 && afterIndex < end ? afterIndex + 1 : 0;

        var rangeCount = Math.Max(0, end - start);
        var takeCount = Math.Min(rangeCount, read.Max);
        var windowStart = end - takeCount;
        var omitted = rangeCount - takeCount;

        List<ChatMessage> messages = new(takeCount);
        for (var i = windowStart; i < end; i++)
        {
            messages.Add(transcript[i]);
        }

        // Drops the oldest of what is left until the serialised answer fits under half of
        // JsonLineStream.MaxLineBytes (finding P-21), stopping at an empty list if one Message alone
        // is still too long (correction item 21).
        while (messages.Count > 0
            && ProtocolJson.Serialize(new TranscriptTail(read.RequestId, read.RoomId, messages, omitted)).Length > JsonLineStream.MaxLineBytes / 2)
        {
            messages.RemoveAt(0);
            omitted++;
        }

        await lineStream.WriteAsync(new TranscriptTail(read.RequestId, read.RoomId, messages, omitted), ct).ConfigureAwait(false);

        static int IndexOf(IReadOnlyList<ChatMessage> messages, string messageId)
        {
            for (var i = 0; i < messages.Count; i++)
            {
                if (string.Equals(messages[i].Id, messageId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }
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

        var error = await this.ResolveMembershipErrorAsync(roomId, messageId, ct).ConfigureAwait(false);
        this.membershipCache = (roomId, messageId, error);
        return error;
    }

    private async Task<ProtocolError?> ResolveMembershipErrorAsync(string roomId, string messageId, CancellationToken ct)
    {
        var room = await this.teamDirectory.GetRoomAsync(roomId, ct).ConfigureAwait(false);
        if (room is null)
        {
            return new ProtocolError(ErrorCodes.UnknownRoom, $"Unknown room '{roomId}'.", messageId);
        }

        var members = await this.teamDirectory.GetRoomMembersAsync(roomId, ct).ConfigureAwait(false);
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
            await lineStream.WriteAsync(error, ct).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // Best effort: the stream is already closed, so the error cannot be delivered.
        }
        catch (ObjectDisposedException)
        {
            // Best effort: the stream is already closed, so the error cannot be delivered.
        }
    }

    private static MemberInfo ToMemberInfo(User user) => new(user.Id, user.Name, user.Kind);
}
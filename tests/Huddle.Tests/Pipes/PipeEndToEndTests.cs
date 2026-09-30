using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO.Pipes;
using System.Text;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.Pipes;

public sealed class PipeEndToEndTests
{
    [Fact]
    public async Task Agent_Hello_Post_Reply_RoundTrip()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();
        var events = fixture.Services.GetRequiredService<RoomEvents>();
        var store = fixture.Services.GetRequiredService<IChatStore>();
        var gateway = fixture.Services.GetRequiredService<IAgentGateway>();

        await using var client = await fixture.ConnectClientAsync(ct);
        await client.WriteAsync(new Hello("echo", "test agent"), ct);

        var welcomeMessage = await client.ReadAsync(ct);
        var welcome = Assert.IsType<Welcome>(welcomeMessage);
        Assert.False(string.IsNullOrEmpty(welcome.AgentId));
        var room = Assert.Single(welcome.Rooms);
        Assert.Equal("echo", room.Name);
        Assert.Equal(2, room.Members.Count);

        await chat.PostAsync(room.Id, KnownIds.Human, "hi @echo", ct: ct);

        var deliveredMessage = await client.ReadAsync(ct);
        var delivered = Assert.IsType<MessagePosted>(deliveredMessage);
        Assert.Equal("hi @echo", delivered.Message.Text);
        Assert.True(delivered.Mentioned);
        Assert.Contains(delivered.Mentions, m => m.Id == welcome.AgentId);
        Assert.Equal("echo", delivered.RoomName);

        var replyReceived = new TaskCompletionSource<MessagePostedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        events.MessagePosted += e =>
        {
            if (e.Message.SenderId == welcome.AgentId)
            {
                replyReceived.TrySetResult(e);
            }
        };

        await client.WriteAsync(new PostMessage(room.Id, "reply-1", "pong"), ct);

        using var replyRegistration = ct.Register(() => replyReceived.TrySetCanceled(ct));
        var replyEvent = await replyReceived.Task;
        Assert.Equal(welcome.AgentId, replyEvent.Message.SenderId);

        var history = await store.ReadAllAsync(room.Id, ct);
        Assert.Equal(2, history.Count);
        Assert.Equal("reply-1", history[^1].Id);
        Assert.Equal("echo", history[^1].SenderName);
        Assert.True(gateway.IsOnline(welcome.AgentId));

        using var noEchoCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        noEchoCts.CancelAfter(TimeSpan.FromMilliseconds(500));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.ReadAsync(noEchoCts.Token));
    }

    [Fact]
    public async Task TwoAgents_OnlyMentionedOneGetsFlag_AndRoomIsRenamed()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();

        await using var alpha = await fixture.ConnectClientAsync(ct);
        await alpha.WriteAsync(new Hello("alpha", null), ct);
        var alphaWelcome = Assert.IsType<Welcome>(await alpha.ReadAsync(ct));
        var alphaRoomId = Assert.Single(alphaWelcome.Rooms).Id;

        await using var beta = await fixture.ConnectClientAsync(ct);
        await beta.WriteAsync(new Hello("beta", null), ct);
        var betaWelcome = Assert.IsType<Welcome>(await beta.ReadAsync(ct));

        await chat.SubmitFromComposerAsync(alphaRoomId, KnownIds.Human, "/invite @beta", ct);
        await chat.PostAsync(alphaRoomId, KnownIds.Human, "@beta hi", ct: ct);

        var alphaDelivered = Assert.IsType<MessagePosted>(await alpha.ReadAsync(ct));
        Assert.False(alphaDelivered.Mentioned);
        Assert.Equal("alpha, beta", alphaDelivered.RoomName);

        var betaDelivered = Assert.IsType<MessagePosted>(await beta.ReadAsync(ct));
        Assert.True(betaDelivered.Mentioned);
        Assert.Equal("alpha, beta", betaDelivered.RoomName);

        Assert.NotEqual(alphaWelcome.AgentId, betaWelcome.AgentId);
    }

    [Fact]
    public async Task FirstMessageNotHello_GetsError_AndDisconnect()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);

        await using var client = await fixture.ConnectClientAsync(ct);
        await client.WriteAsync(new PostMessage("room", null, "hi"), ct);

        var error = Assert.IsType<ProtocolError>(await client.ReadAsync(ct));
        Assert.Equal(ErrorCodes.ExpectedHello, error.Code);

        Assert.Null(await client.ReadAsync(ct));
    }

    [Fact]
    public async Task InvalidAgentName_GetsError_AndDisconnect()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);

        await using var client = await fixture.ConnectClientAsync(ct);
        // A path separator, not a space: a Name may contain spaces, but it is still the filename
        // of a Persona, so anything that could reach out of that directory is refused at hello.
        await client.WriteAsync(new Hello("bad/name", null), ct);

        var error = Assert.IsType<ProtocolError>(await client.ReadAsync(ct));
        Assert.Equal(ErrorCodes.InvalidName, error.Code);

        Assert.Null(await client.ReadAsync(ct));
    }

    [Fact]
    public async Task NameWithSpaces_IsAccepted()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);

        await using var client = await fixture.ConnectClientAsync(ct);
        await client.WriteAsync(new Hello("Emily Lee", null), ct);

        var welcome = Assert.IsType<Welcome>(await client.ReadAsync(ct));
        Assert.Equal("Emily Lee", welcome.Name);
    }

    [Fact]
    public async Task HumanName_GetsNameReserved()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);

        await using var client = await fixture.ConnectClientAsync(ct);
        await client.WriteAsync(new Hello("You", null), ct);

        var error = Assert.IsType<ProtocolError>(await client.ReadAsync(ct));
        Assert.Equal(ErrorCodes.NameReserved, error.Code);

        Assert.Null(await client.ReadAsync(ct));
    }

    [Fact]
    public async Task Post_ToUnknownRoom_GetsErrorWithRelatedId_ConnectionStaysOpen()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var events = fixture.Services.GetRequiredService<RoomEvents>();

        await using var client = await fixture.ConnectClientAsync(ct);
        await client.WriteAsync(new Hello("echo", null), ct);
        var welcome = Assert.IsType<Welcome>(await client.ReadAsync(ct));
        var roomId = Assert.Single(welcome.Rooms).Id;

        await client.WriteAsync(new PostMessage("nope", "m1", "x"), ct);
        var error = Assert.IsType<ProtocolError>(await client.ReadAsync(ct));
        Assert.Equal(ErrorCodes.UnknownRoom, error.Code);
        Assert.Equal("m1", error.RelatedMessageId);

        var posted = new TaskCompletionSource<MessagePostedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        events.MessagePosted += e =>
        {
            if (e.Message.Id == "m2")
            {
                posted.TrySetResult(e);
            }
        };

        await client.WriteAsync(new PostMessage(roomId, "m2", "hello"), ct);

        using var registration = ct.Register(() => posted.TrySetCanceled(ct));
        var evt = await posted.Task;
        Assert.Equal("m2", evt.Message.Id);
    }

    /// <summary>A Member's <see cref="MessageDelta"/> grows that Message id's Draft and publishes <see cref="RoomEvents.DraftChanged"/>.</summary>
    [Fact]
    public async Task MessageDelta_FromAMember_IsAccepted()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var events = fixture.Services.GetRequiredService<RoomEvents>();
        var drafts = fixture.Services.GetRequiredService<Drafts>();

        await using var client = await fixture.ConnectClientAsync(ct);
        await client.WriteAsync(new Hello("echo", null), ct);
        var welcome = Assert.IsType<Welcome>(await client.ReadAsync(ct));
        var roomId = Assert.Single(welcome.Rooms).Id;

        var changed = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        events.DraftChanged += room => changed.TrySetResult(room);

        await client.WriteAsync(new MessageDelta(roomId, "m-delta-1", "Hel", false), ct);

        using var registration = ct.Register(() => changed.TrySetCanceled(ct));
        var changedRoomId = await changed.Task;
        Assert.Equal(roomId, changedRoomId);

        var draft = Assert.Single(drafts.ForRoom(roomId));
        Assert.Equal("Hel", draft.Text);
    }

    /// <summary>An Agent that is not a Member of the target Room is refused, and no Draft is created.</summary>
    [Fact]
    public async Task MessageDelta_FromANonMember_IsRefused()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var drafts = fixture.Services.GetRequiredService<Drafts>();

        await using var echo = await fixture.ConnectClientAsync(ct);
        await echo.WriteAsync(new Hello("echo", null), ct);
        var echoWelcome = Assert.IsType<Welcome>(await echo.ReadAsync(ct));
        var echoRoomId = Assert.Single(echoWelcome.Rooms).Id;

        await using var alpha = await fixture.ConnectClientAsync(ct);
        await alpha.WriteAsync(new Hello("alpha", null), ct);
        Assert.IsType<Welcome>(await alpha.ReadAsync(ct));

        await alpha.WriteAsync(new MessageDelta(echoRoomId, "m-delta-2", "Hi", false), ct);

        var error = Assert.IsType<ProtocolError>(await alpha.ReadAsync(ct));
        Assert.Equal(ErrorCodes.NotMember, error.Code);
        Assert.Empty(drafts.ForRoom(echoRoomId));
    }

    /// <summary>
    /// Reusing one MessageId across two Rooms re-checks membership for the second Room. The
    /// per-connection membership cache exists so a Draft does not cost one SQLite round trip per
    /// token of model output, and keying it on the MessageId alone would let an Agent bank an
    /// allowed verdict in its own Room and then spend it on a Room it is not a Member of.
    /// </summary>
    [Fact]
    public async Task MessageDelta_ReusingAMessageIdInAnotherRoom_IsStillRefused()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var drafts = fixture.Services.GetRequiredService<Drafts>();

        await using var echo = await fixture.ConnectClientAsync(ct);
        await echo.WriteAsync(new Hello("echo", null), ct);
        var echoWelcome = Assert.IsType<Welcome>(await echo.ReadAsync(ct));
        var echoRoomId = Assert.Single(echoWelcome.Rooms).Id;

        await using var alpha = await fixture.ConnectClientAsync(ct);
        await alpha.WriteAsync(new Hello("alpha", null), ct);
        var alphaWelcome = Assert.IsType<Welcome>(await alpha.ReadAsync(ct));
        var alphaRoomId = Assert.Single(alphaWelcome.Rooms).Id;

        // Allowed: alpha's own Room, which caches an "is a Member" verdict under this MessageId.
        const string messageId = "m-delta-shared";
        await alpha.WriteAsync(new MessageDelta(alphaRoomId, messageId, "mine", false), ct);

        // An accepted delta draws no reply, so there is nothing to await: poll until the server has
        // processed it, bounded so a regression fails rather than hangs.
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (drafts.ForRoom(alphaRoomId).Count == 0 && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(25, ct);
        }

        Assert.Single(drafts.ForRoom(alphaRoomId));

        // The same MessageId, a Room alpha is not a Member of. The cached verdict must not carry over.
        await alpha.WriteAsync(new MessageDelta(echoRoomId, messageId, "not mine", false), ct);

        var error = Assert.IsType<ProtocolError>(await alpha.ReadAsync(ct));
        Assert.Equal(ErrorCodes.NotMember, error.Code);
        Assert.Empty(drafts.ForRoom(echoRoomId));
    }

    /// <summary>A <see cref="MessageDelta"/> whose MessageId fails <c>NameRules.IsValidId</c> is refused as a bad message.</summary>
    [Fact]
    public async Task MessageDelta_WithAnInvalidMessageId_IsRefused()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);

        await using var client = await fixture.ConnectClientAsync(ct);
        await client.WriteAsync(new Hello("echo", null), ct);
        var welcome = Assert.IsType<Welcome>(await client.ReadAsync(ct));
        var roomId = Assert.Single(welcome.Rooms).Id;

        await client.WriteAsync(new MessageDelta(roomId, "bad/id", "Hi", false), ct);

        var error = Assert.IsType<ProtocolError>(await client.ReadAsync(ct));
        Assert.Equal(ErrorCodes.BadMessage, error.Code);
    }

    /// <summary>A Draft is never delivered to another Agent - only the Room view subscribes to <see cref="RoomEvents.DraftChanged"/>.</summary>
    [Fact]
    public async Task Drafts_AreNeverDeliveredToAnotherAgent()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();

        await using var alpha = await fixture.ConnectClientAsync(ct);
        await alpha.WriteAsync(new Hello("alpha", null), ct);
        var alphaWelcome = Assert.IsType<Welcome>(await alpha.ReadAsync(ct));
        var alphaRoomId = Assert.Single(alphaWelcome.Rooms).Id;

        await using var beta = await fixture.ConnectClientAsync(ct);
        await beta.WriteAsync(new Hello("beta", null), ct);
        Assert.IsType<Welcome>(await beta.ReadAsync(ct));

        await chat.SubmitFromComposerAsync(alphaRoomId, KnownIds.Human, "/invite @beta", ct);

        await alpha.WriteAsync(new MessageDelta(alphaRoomId, "m-delta-3", "secret draft text", false), ct);

        using var noDraftCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        noDraftCts.CancelAfter(TimeSpan.FromMilliseconds(500));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => beta.ReadAsync(noDraftCts.Token));
    }

    [Fact]
    public async Task BadJsonLine_GetsBadMessage_ConnectionStaysOpen()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);

        var pipe = new NamedPipeClientStream(".", fixture.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(ct);
        await using var client = new JsonLineStream(pipe, leaveOpen: true);

        await client.WriteAsync(new Hello("echo", null), ct);
        var welcome = Assert.IsType<Welcome>(await client.ReadAsync(ct));
        var roomId = Assert.Single(welcome.Rooms).Id;

        var badLine = Encoding.UTF8.GetBytes("{not json\n");
        await pipe.WriteAsync(badLine, ct);
        await pipe.FlushAsync(ct);

        var error = Assert.IsType<ProtocolError>(await client.ReadAsync(ct));
        Assert.Equal(ErrorCodes.BadMessage, error.Code);

        await client.WriteAsync(new PostMessage(roomId, "m3", "still works"), ct);
        await pipe.DisposeAsync();
    }

    [Fact]
    public async Task Reconnect_SameName_ReplacesConnection_KeepsAgentId()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);

        await using var first = await fixture.ConnectClientAsync(ct);
        await first.WriteAsync(new Hello("echo", null), ct);
        var firstWelcome = Assert.IsType<Welcome>(await first.ReadAsync(ct));

        await using var second = await fixture.ConnectClientAsync(ct);
        await second.WriteAsync(new Hello("echo", null), ct);
        var secondWelcome = Assert.IsType<Welcome>(await second.ReadAsync(ct));

        Assert.Equal(firstWelcome.AgentId, secondWelcome.AgentId);
        Assert.Null(await first.ReadAsync(ct));
    }

    /// <summary>A new Agent's two-Member Room with the Human has taken no Messages yet, so its Welcome reports it empty.</summary>
    [Fact]
    public async Task Welcome_NewAgentRoom_IsEmptyTrue()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);

        await using var client = await fixture.ConnectClientAsync(ct);
        await client.WriteAsync(new Hello("echo", null), ct);
        var welcome = Assert.IsType<Welcome>(await client.ReadAsync(ct));
        var room = Assert.Single(welcome.Rooms);

        Assert.True(room.IsEmpty);
    }

    /// <summary>
    /// Once a Human Message lands in a Room, a later Welcome for that Room from a fresh connection
    /// reports it not empty, proving the flag comes from the Transcript rather than from connection
    /// state.
    /// </summary>
    [Fact]
    public async Task Welcome_AfterOneMessage_ReconnectIsEmptyFalse()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();

        await using var first = await fixture.ConnectClientAsync(ct);
        await first.WriteAsync(new Hello("echo", null), ct);
        var firstWelcome = Assert.IsType<Welcome>(await first.ReadAsync(ct));
        var firstRoom = Assert.Single(firstWelcome.Rooms);
        Assert.True(firstRoom.IsEmpty);

        await chat.PostAsync(firstRoom.Id, KnownIds.Human, "hi", ct: ct);

        await using var second = await fixture.ConnectClientAsync(ct);
        await second.WriteAsync(new Hello("echo", null), ct);
        var secondWelcome = Assert.IsType<Welcome>(await second.ReadAsync(ct));
        var secondRoom = Assert.Single(secondWelcome.Rooms);

        Assert.False(secondRoom.IsEmpty);
    }

    [Fact]
    public async Task Disconnect_UnregistersAgent()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var gateway = fixture.Services.GetRequiredService<IAgentGateway>();

        var client = await fixture.ConnectClientAsync(ct);
        await client.WriteAsync(new Hello("echo", null), ct);
        var welcome = Assert.IsType<Welcome>(await client.ReadAsync(ct));
        Assert.True(gateway.IsOnline(welcome.AgentId));

        await client.DisposeAsync();

        var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (gateway.IsOnline(welcome.AgentId) && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(50, ct);
        }

        Assert.False(gateway.IsOnline(welcome.AgentId));
    }

    /// <summary>An Agent's open Drafts are cleared when its connection drops, so a crashed Agent leaves no frozen Draft on screen.</summary>
    [Fact]
    public async Task Disconnect_ClearsTheAgentsDrafts()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var gateway = fixture.Services.GetRequiredService<IAgentGateway>();
        var drafts = fixture.Services.GetRequiredService<Drafts>();
        var events = fixture.Services.GetRequiredService<RoomEvents>();

        var client = await fixture.ConnectClientAsync(ct);
        await client.WriteAsync(new Hello("echo", null), ct);
        var welcome = Assert.IsType<Welcome>(await client.ReadAsync(ct));
        var roomId = Assert.Single(welcome.Rooms).Id;

        var changed = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        events.DraftChanged += room => changed.TrySetResult(room);

        await client.WriteAsync(new MessageDelta(roomId, "m-delta-4", "partial", false), ct);

        using var registration = ct.Register(() => changed.TrySetCanceled(ct));
        await changed.Task;
        Assert.Single(drafts.ForRoom(roomId));

        await client.DisposeAsync();

        var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (gateway.IsOnline(welcome.AgentId) && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(50, ct);
        }

        Assert.False(gateway.IsOnline(welcome.AgentId));
        Assert.Empty(drafts.ForRoom(roomId));
    }

    [Fact]
    public async Task MessagePosted_CarriesEveryRoomMember()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();

        await using var echo = await fixture.ConnectClientAsync(ct);
        await echo.WriteAsync(new Hello("echo", null), ct);
        var echoWelcome = Assert.IsType<Welcome>(await echo.ReadAsync(ct));
        var roomId = Assert.Single(echoWelcome.Rooms).Id;

        await chat.PostAsync(roomId, KnownIds.Human, "hi @echo", ct: ct);
        var firstDelivered = Assert.IsType<MessagePosted>(await echo.ReadAsync(ct));
        Assert.Equal(2, firstDelivered.Members.Count);
        Assert.Contains(firstDelivered.Members, m => m.Id == KnownIds.Human);
        Assert.Contains(firstDelivered.Members, m => m.Id == echoWelcome.AgentId);

        await using var alpha = await fixture.ConnectClientAsync(ct);
        await alpha.WriteAsync(new Hello("alpha", null), ct);
        Assert.IsType<Welcome>(await alpha.ReadAsync(ct));

        await chat.SubmitFromComposerAsync(roomId, KnownIds.Human, "/invite @alpha", ct);
        await chat.PostAsync(roomId, KnownIds.Human, "hi again @echo", ct: ct);

        var secondDelivered = Assert.IsType<MessagePosted>(await echo.ReadAsync(ct));
        Assert.Equal(3, secondDelivered.Members.Count);
        Assert.Contains(secondDelivered.Members, m => m.Id == KnownIds.Human);
        Assert.Contains(secondDelivered.Members, m => m.Id == echoWelcome.AgentId);
    }

    // The contract a third-party pipe client sees. The code is new, so an old client will not know it;
    // ProtocolError already carries an arbitrary code string, which is why this needs no version bump.
    [Fact]
    public async Task AgentPostOverBudget_ReturnsABudgetExhaustedError()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:AgentMessageBudget"] = "1" }, ct);

        await using var client = await fixture.ConnectClientAsync(ct);
        await client.WriteAsync(new Hello("echo", null), ct);
        var welcome = Assert.IsType<Welcome>(await client.ReadAsync(ct));
        var roomId = Assert.Single(welcome.Rooms).Id;

        await client.WriteAsync(new PostMessage(roomId, "m1", "spends the budget"), ct);
        await client.WriteAsync(new PostMessage(roomId, "m2", "one too many"), ct);

        var error = Assert.IsType<ProtocolError>(await client.ReadAsync(ct));
        Assert.Equal(ErrorCodes.BudgetExhausted, error.Code);
        Assert.Equal("m2", error.RelatedMessageId);
        Assert.Contains("Do not retry", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A refused post must not deadlock the connection. The server answers a refusal by writing from
    /// inside its read loop, so a client that posts and goes straight on to its next envelope - which
    /// is well-behaved, and exactly what <c>DemoAgentHost</c> and <c>PersonaRunner</c> do when they
    /// send a post followed by its terminator - once met a server blocked writing while it was blocked
    /// writing, with the pipe's buffers left at their default of 0. Neither side read again: the Draft
    /// never completed and that Agent went deaf in every Room with its pipe still open and its tile
    /// still green. Every assertion here would hang rather than fail if it returned, which is what the
    /// fixture's timeout is for.
    /// </summary>
    [Fact]
    public async Task AgentKeepsWritingAfterARefusedPost_ConnectionStaysAlive()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:AgentMessageBudget"] = "1" }, ct);

        await using var client = await fixture.ConnectClientAsync(ct);
        await client.WriteAsync(new Hello("echo", null), ct);
        var welcome = Assert.IsType<Welcome>(await client.ReadAsync(ct));
        var roomId = Assert.Single(welcome.Rooms).Id;

        await client.WriteAsync(new PostMessage(roomId, "m1", "spends the budget"), ct);

        // The refused post, then its terminator, written back to back without reading in between -
        // the exact window the deadlock lived in.
        await client.WriteAsync(new PostMessage(roomId, "m2", "one too many"), ct);
        await client.WriteAsync(new MessageDelta(roomId, "m2", string.Empty, IsFinal: true), ct);

        var error = Assert.IsType<ProtocolError>(await client.ReadAsync(ct));
        Assert.Equal(ErrorCodes.BudgetExhausted, error.Code);

        // Still being read from: the read loop consumed the terminator above and answered this too.
        // Without that, the Agent is deaf rather than merely capped.
        await client.WriteAsync(new PostMessage(roomId, "m3", "still refused, still answered"), ct);
        var second = Assert.IsType<ProtocolError>(await client.ReadAsync(ct));
        Assert.Equal(ErrorCodes.BudgetExhausted, second.Code);
        Assert.Equal("m3", second.RelatedMessageId);
    }

    [Fact]
    public async Task TwoClients_ConnectConcurrently()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);

        await using var first = await fixture.ConnectClientAsync(ct);
        await using var second = await fixture.ConnectClientAsync(ct);

        var firstHello = first.WriteAsync(new Hello("echo", null), ct);
        var secondHello = second.WriteAsync(new Hello("alpha", null), ct);
        await Task.WhenAll(firstHello, secondHello);

        var firstWelcomeTask = first.ReadAsync(ct);
        var secondWelcomeTask = second.ReadAsync(ct);
        await Task.WhenAll(firstWelcomeTask, secondWelcomeTask);

        Assert.IsType<Welcome>(await firstWelcomeTask);
        Assert.IsType<Welcome>(await secondWelcomeTask);
    }

    /// <summary>A successful <see cref="PostMessage"/> completes the Draft that shares its MessageId.</summary>
    [Fact]
    public async Task PostMessage_CompletesTheDraftWithTheSameId()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var drafts = fixture.Services.GetRequiredService<Drafts>();
        var events = fixture.Services.GetRequiredService<RoomEvents>();

        await using var client = await fixture.ConnectClientAsync(ct);
        await client.WriteAsync(new Hello("echo", null), ct);
        var welcome = Assert.IsType<Welcome>(await client.ReadAsync(ct));
        var roomId = Assert.Single(welcome.Rooms).Id;

        var changed = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        events.DraftChanged += room => changed.TrySetResult(room);
        await client.WriteAsync(new MessageDelta(roomId, "reply-9", "partial reply", false), ct);

        using var registration = ct.Register(() => changed.TrySetCanceled(ct));
        await changed.Task;
        Assert.Single(drafts.ForRoom(roomId));

        await client.WriteAsync(new PostMessage(roomId, "reply-9", "full reply"), ct);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (drafts.ForRoom(roomId).Count > 0 && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(50, ct);
        }

        Assert.Empty(drafts.ForRoom(roomId));
    }

    /// <summary><see cref="IAgentGateway.StopTurnAsync"/> writes a <see cref="StopTurn"/> Envelope to the Agent's own connection.</summary>
    [Fact]
    public async Task StopTurnAsync_ReachesTheAgentsConnection()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var gateway = fixture.Services.GetRequiredService<IAgentGateway>();

        await using var client = await fixture.ConnectClientAsync(ct);
        await client.WriteAsync(new Hello("echo", null), ct);
        var welcome = Assert.IsType<Welcome>(await client.ReadAsync(ct));
        var roomId = Assert.Single(welcome.Rooms).Id;

        // The read is started BEFORE the stop is awaited, and that ordering is the test, not a
        // stylistic choice. JsonLineStream.WriteAsync ends in FlushAsync, which on a Windows named
        // pipe is FlushFileBuffers: it does not return until the other end drains the buffer. Await
        // the stop first and nothing is left to read it, so the write blocks until the token fires.
        // In the real system the Agent's read loop is always parked in ReadAsync, which is exactly
        // what this ordering reproduces.
        var reading = client.ReadAsync(ct);

        await gateway.StopTurnAsync(welcome.AgentId, roomId, ct);

        var stop = Assert.IsType<StopTurn>(await reading);
        Assert.Equal(roomId, stop.RoomId);
    }

    /// <summary>Stopping an Agent that is not connected is a silent no-op, not an error.</summary>
    [Fact]
    public async Task StopTurnAsync_ForAnOfflineAgent_IsHarmless()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var gateway = fixture.Services.GetRequiredService<IAgentGateway>();

        await gateway.StopTurnAsync("no-such-agent", "no-such-room", ct);

        Assert.False(gateway.IsOnline("no-such-agent"));
    }

    /// <summary>A Member's <see cref="ToolActivity"/> updates that Message id's Draft and publishes <see cref="RoomEvents.DraftChanged"/>.</summary>
    [Fact]
    public async Task ToolActivity_FromAMember_IsAccepted()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var events = fixture.Services.GetRequiredService<RoomEvents>();
        var drafts = fixture.Services.GetRequiredService<Drafts>();

        await using var client = await fixture.ConnectClientAsync(ct);
        await client.WriteAsync(new Hello("echo", null), ct);
        var welcome = Assert.IsType<Welcome>(await client.ReadAsync(ct));
        var roomId = Assert.Single(welcome.Rooms).Id;

        var changed = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        events.DraftChanged += room => changed.TrySetResult(room);

        await client.WriteAsync(new ToolActivity(roomId, "m-tool-1", "call-1", "Searching", ToolActivityStatus.InProgress), ct);

        using var registration = ct.Register(() => changed.TrySetCanceled(ct));
        await changed.Task;

        var draft = Assert.Single(drafts.ForRoom(roomId));
        Assert.Equal("Searching", draft.ToolTitle);
    }

    /// <summary>An activity carrying a path, a line and an Edit reaches the Draft's row with all three.</summary>
    [Fact]
    public async Task ToolActivity_WithEdit_ReachesTheDraft()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var (client, roomId) = await ConnectEchoAsync(fixture, ct);
        await using var _ = client;

        ToolCallDetail call = await SendToolActivityAsync(
            fixture, client, roomId, "Edit notes.md", "E:\\work\\notes.md", 12, new EditChange("one", "two"), ct);

        Assert.Equal(new ToolCallDetail("call-1", "Edit notes.md", ToolActivityStatus.InProgress, "E:\\work\\notes.md", 12, new EditChange("one", "two")), call);
    }

    /// <summary>An Agent that is not a Member of the Room cannot put a tool call, or an Edit preview, on its Draft.</summary>
    [Fact]
    public async Task ToolActivity_FromANonMember_IsRefused()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var drafts = fixture.Services.GetRequiredService<Drafts>();
        var (echo, echoRoomId) = await ConnectEchoAsync(fixture, ct);
        await using var _ = echo;

        await using var alpha = await fixture.ConnectClientAsync(ct);
        await alpha.WriteAsync(new Hello("alpha", null), ct);
        Assert.IsType<Welcome>(await alpha.ReadAsync(ct));

        await alpha.WriteAsync(new ToolActivity(echoRoomId, "m-tool-9", "call-1", "Edit", ToolActivityStatus.InProgress, "a.txt", 1, new EditChange("1", "2")), ct);

        var error = Assert.IsType<ProtocolError>(await alpha.ReadAsync(ct));
        Assert.Equal(ErrorCodes.NotMember, error.Code);
        Assert.Empty(drafts.ForRoom(echoRoomId));
    }

    /// <summary>An Edit side over the limit is cut to it and marked truncated, because the pipe is a validation boundary and a runner's clip is not trusted.</summary>
    [Fact]
    public async Task ToolActivity_OversizeEdit_IsClipped()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var (client, roomId) = await ConnectEchoAsync(fixture, ct);
        await using var _ = client;
        string huge = new('x', ToolActivityLimits.MaxEditSideLength + 10);

        ToolCallDetail call = await SendToolActivityAsync(fixture, client, roomId, "Write", "a.txt", null, new EditChange(huge, huge), ct);

        Assert.NotNull(call.Edit);
        Assert.Equal(ToolActivityLimits.MaxEditSideLength, call.Edit.OldText?.Length);
        Assert.Equal(ToolActivityLimits.MaxEditSideLength, call.Edit.NewText?.Length);
        Assert.True(call.Edit.Truncated);
    }

    /// <summary>A line below 1 is dropped, and the rest of the activity still lands.</summary>
    [Fact]
    public async Task ToolActivity_InvalidLine_IsDropped()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var (client, roomId) = await ConnectEchoAsync(fixture, ct);
        await using var _ = client;

        ToolCallDetail call = await SendToolActivityAsync(fixture, client, roomId, "Edit", "a.txt", 0, null, ct);

        Assert.Null(call.Line);
        Assert.Equal("a.txt", call.Path);
    }

    /// <summary>A path over the limit is dropped rather than shown cut, because a cut path names a file that does not exist.</summary>
    [Fact]
    public async Task ToolActivity_OverlongPath_IsDropped()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var (client, roomId) = await ConnectEchoAsync(fixture, ct);
        await using var _ = client;

        ToolCallDetail call = await SendToolActivityAsync(fixture, client, roomId, "Edit", new string('p', ToolActivityLimits.MaxPathLength + 1), 3, null, ct);

        Assert.Null(call.Path);
        Assert.Equal(3, call.Line);
    }

    /// <summary>A title over the limit is cut to it.</summary>
    [Fact]
    public async Task ToolActivity_OverlongTitle_IsClipped()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var (client, roomId) = await ConnectEchoAsync(fixture, ct);
        await using var _ = client;

        ToolCallDetail call = await SendToolActivityAsync(fixture, client, roomId, new string('t', ToolActivityLimits.MaxTitleLength + 50), null, null, null, ct);

        Assert.Equal(ToolActivityLimits.MaxTitleLength, call.Title?.Length);
    }

    /// <summary>A runner's own truncation flag survives, so a preview the runner already cut still says it was shortened.</summary>
    [Fact]
    public async Task ToolActivity_RunnerMarkedTruncated_StaysTruncated()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var (client, roomId) = await ConnectEchoAsync(fixture, ct);
        await using var _ = client;

        ToolCallDetail call = await SendToolActivityAsync(fixture, client, roomId, "Edit", "a.txt", null, new EditChange("1", "2", true), ct);

        Assert.True(call.Edit?.Truncated);
    }

    /// <summary>A negative omitted-changes count is set to zero, so the view never says "-2 more changes".</summary>
    [Fact]
    public async Task ToolActivity_NegativeOmittedChanges_IsSetToZero()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var (client, roomId) = await ConnectEchoAsync(fixture, ct);
        await using var _ = client;

        ToolCallDetail call = await SendToolActivityAsync(fixture, client, roomId, "Edit", "a.txt", null, new EditChange("1", "2", false, -2), ct);

        Assert.Equal(0, call.Edit?.OmittedChanges);
    }

    /// <summary>Connects the built-in echo Agent and returns its client with the id of its direct Room.</summary>
    private static async Task<(JsonLineStream Client, string RoomId)> ConnectEchoAsync(PipeHostFixture fixture, CancellationToken ct)
    {
        var client = await fixture.ConnectClientAsync(ct);
        await client.WriteAsync(new Hello("echo", null), ct);
        var welcome = Assert.IsType<Welcome>(await client.ReadAsync(ct));
        return (client, Assert.Single(welcome.Rooms).Id);
    }

    /// <summary>Sends one <see cref="ToolActivity"/> for call-1 and returns the Draft row the server built once it has published the change.</summary>
    private static async Task<ToolCallDetail> SendToolActivityAsync(
        PipeHostFixture fixture,
        JsonLineStream client,
        string roomId,
        string? title,
        string? path,
        int? line,
        EditChange? edit,
        CancellationToken ct)
    {
        var events = fixture.Services.GetRequiredService<RoomEvents>();
        var drafts = fixture.Services.GetRequiredService<Drafts>();
        var changed = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        events.DraftChanged += room => changed.TrySetResult(room);

        await client.WriteAsync(new ToolActivity(roomId, "m-tool-1", "call-1", title, ToolActivityStatus.InProgress, path, line, edit), ct);

        using var registration = ct.Register(() => changed.TrySetCanceled(ct));
        await changed.Task;

        return Assert.Single(Assert.Single(drafts.ForRoom(roomId)).ToolCalls);
    }

    /// <summary>
    /// A client that closes its end cleanly - what Ctrl+C or killing the client process both do - still reaches
    /// end of stream on <see cref="AgentConnection.RunAsync"/>'s read loop rather than an <see cref="IOException"/>,
    /// so the only trace of the disconnect is the Information line in the <c>finally</c> block. That line is the
    /// entire fix for T2: before it existed, a clean goodbye left no log line at all.
    /// </summary>
    [Fact]
    public async Task Disconnect_CleanClose_LogsExactlyOneInformationLine()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var gateway = fixture.Services.GetRequiredService<IAgentGateway>();

        var client = await fixture.ConnectClientAsync(ct);
        await client.WriteAsync(new Hello("echo", null), ct);
        var welcome = Assert.IsType<Welcome>(await client.ReadAsync(ct));

        await client.DisposeAsync();

        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (gateway.IsOnline(welcome.AgentId) && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(50, ct);
        }

        Assert.False(gateway.IsOnline(welcome.AgentId));

        // AgentConnection.RunAsync unregisters from the gateway BEFORE it writes the disconnect line, so
        // "offline" does not yet mean "logged": wait for the line itself rather than reading the log once.
        List<CapturedLogEntry> connectionEntries = [];
        while (!connectionEntries.Exists(e => e.Level == LogLevel.Information) && DateTimeOffset.UtcNow < deadline)
        {
            connectionEntries = fixture.LogEntries
                .Where(e => string.Equals(e.Category, typeof(AgentConnection).FullName, StringComparison.Ordinal))
                .ToList();

            if (!connectionEntries.Exists(e => e.Level == LogLevel.Information))
            {
                await Task.Delay(20, ct);
            }
        }

        var informationEntry = Assert.Single(connectionEntries, e => e.Level == LogLevel.Information);
        Assert.Contains("echo", informationEntry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(connectionEntries, e => e.Level == LogLevel.Warning);
    }

    /// <summary>
    /// A pipe that breaks - the reading end going away while the server still has bytes to write, rather than a
    /// graceful shutdown - still reaches <see cref="AgentConnection.RunAsync"/>'s <see cref="IOException"/> catch
    /// and logs the pre-existing faulted-connection Warning. Dropping the raw <see cref="NamedPipeClientStream"/>
    /// right after sending <c>hello</c>, before reading the reply, leaves no reader on the pipe by the time the
    /// server tries to write the <see cref="Welcome"/> back, so that write fails with "the pipe is broken" instead
    /// of the clean end-of-stream a graceful close produces.
    /// </summary>
    [Fact]
    public async Task Disconnect_AbruptClose_StillLogsTheFaultedConnectionWarning()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);

        var pipe = new NamedPipeClientStream(".", fixture.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(ct);
        await using (var client = new JsonLineStream(pipe, leaveOpen: true))
        {
            await client.WriteAsync(new Hello("echo", null), ct);
        }

        pipe.Dispose();

        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        List<CapturedLogEntry> warnings = [];
        while (warnings.Count == 0 && DateTimeOffset.UtcNow < deadline)
        {
            warnings = fixture.LogEntries
                .Where(e => string.Equals(e.Category, typeof(AgentConnection).FullName, StringComparison.Ordinal) && e.Level == LogLevel.Warning)
                .ToList();

            if (warnings.Count == 0)
            {
                await Task.Delay(50, ct);
            }
        }

        var warning = Assert.Single(warnings);
        Assert.Contains("ended", warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("disconnected", warning.Message, StringComparison.Ordinal);
    }
}
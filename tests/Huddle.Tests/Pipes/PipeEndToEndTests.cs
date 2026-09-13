using Microsoft.Extensions.DependencyInjection;
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

    [Fact]
    public async Task MessageDelta_GetsNotSupported()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);

        await using var client = await fixture.ConnectClientAsync(ct);
        await client.WriteAsync(new Hello("echo", null), ct);
        var welcome = Assert.IsType<Welcome>(await client.ReadAsync(ct));
        var roomId = Assert.Single(welcome.Rooms).Id;

        await client.WriteAsync(new MessageDelta(roomId, "m-7", "par", false), ct);

        var error = Assert.IsType<ProtocolError>(await client.ReadAsync(ct));
        Assert.Equal(ErrorCodes.NotSupported, error.Code);
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
}
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.IO.Pipes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Hooks;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Pipes;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Exercises <see cref="PersonaRunner"/> end to end over a real pipe (via <see cref="PipeHostFixture"/>)
/// against a <see cref="FakeAgentHostFactory"/>, so no test spends a token or launches <c>node</c>.
/// </summary>
public sealed class PersonaRunnerTests
{
    /// <summary>
    /// The five real chat tools' names, each carrying its full <c>mcp__team__</c> prefix, in the same
    /// order <see cref="DotAcpAgentHostFactory"/> builds them in.
    /// </summary>
    private static readonly IReadOnlyList<string> ToolNames =
    [
        "mcp__team__get_help",
        "mcp__team__list_agents",
        "mcp__team__create_room",
        "mcp__team__invite_agent",
        "mcp__team__post_message",
    ];

    [Fact]
    public async Task Start_RegistersAgentAndCreatesDirectRoom()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);

        var (userId, roomId) = await WaitForDirectRoomAsync(fixture, "nova", ct);

        Assert.Single(factory.Calls);
        Assert.Equal(userId, factory.Calls[0].AgentId);
        Assert.NotNull(roomId);
    }

    /// <summary>
    /// Regression test for the description being built from the raw file's first line, which is
    /// always "---" once a Persona has frontmatter: BuildDescription must skip the frontmatter
    /// block and use the body's first line instead.
    /// </summary>
    [Fact]
    public async Task Start_PersonaWithFrontmatter_RegistersDescriptionFromTheBodyNotTheDelimiter()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        var persona = new Persona("nova", "---\nrole: 'Router'\n---\nYou are Nova, the router.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);

        await WaitForDirectRoomAsync(fixture, "nova", ct);
        var directory = fixture.Services.GetRequiredService<ITeamDirectory>();
        var user = await directory.FindUserByNameAsync("nova", ct);

        Assert.NotNull(user);
        Assert.Equal("You are Nova, the router.", user.Description);
    }

    [Fact]
    public async Task DirectRoom_RepliesWithoutMention()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReply("pong");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);

        var (agentId, roomId) = await WaitForDirectRoomAsync(fixture, "nova", ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();

        await chat.PostAsync(roomId, KnownIds.Human, "hello there", ct: ct);

        var history = await WaitForHistoryCountAsync(store, roomId, 2, ct);

        Assert.Equal("pong", history[^1].Text);
        Assert.Equal(agentId, history[^1].SenderId);
    }

    [Fact]
    public async Task GroupRoom_DoesNotReplyWithoutMention()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReply("should not appear");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);

        var (novaId, _) = await WaitForDirectRoomAsync(fixture, "nova", ct);

        await using var friend = await fixture.ConnectClientAsync(ct);
        await friend.WriteAsync(new Hello("friend", null), ct);
        var friendWelcome = Assert.IsType<Welcome>(await friend.ReadAsync(ct));

        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();
        var room = await chat.CreateRoomForAsync([novaId, friendWelcome.AgentId], ct);

        await chat.PostAsync(room.Id, KnownIds.Human, "hello group", ct: ct);

        // Bounded wait: give a (misbehaving) unmentioned agent every chance to reply before asserting it
        // did not (docs/ChatRoom.md records a test that logged 4299 messages in two seconds).
        await Task.Delay(TimeSpan.FromSeconds(1), ct);

        var history = await store.ReadAllAsync(room.Id, ct);
        Assert.Single(history);
    }

    [Fact]
    public async Task GroupRoom_RepliesWhenMentioned()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReply("mentioned reply");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);

        var (novaId, _) = await WaitForDirectRoomAsync(fixture, "nova", ct);

        await using var friend = await fixture.ConnectClientAsync(ct);
        await friend.WriteAsync(new Hello("friend", null), ct);
        var friendWelcome = Assert.IsType<Welcome>(await friend.ReadAsync(ct));

        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();
        var room = await chat.CreateRoomForAsync([novaId, friendWelcome.AgentId], ct);

        await chat.PostAsync(room.Id, KnownIds.Human, "@nova hello", ct: ct);

        var history = await WaitForHistoryCountAsync(store, room.Id, 2, ct);

        Assert.Equal("mentioned reply", history[^1].Text);
        Assert.Equal(novaId, history[^1].SenderId);
    }

    [Fact]
    public async Task Reply_IsTheConcatenationOfMessageChunks()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReply("Hello, ", "world!");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);

        var (_, roomId) = await WaitForDirectRoomAsync(fixture, "nova", ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();

        await chat.PostAsync(roomId, KnownIds.Human, "hi", ct: ct);

        var history = await WaitForHistoryCountAsync(store, roomId, 2, ct);

        Assert.Equal("Hello, world!", history[^1].Text);
    }

    [Fact]
    public async Task ConcurrentMessages_AreProcessedOneAtATime()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueDelayedReply(TimeSpan.FromMilliseconds(300), "first");
        factory.Session.EnqueueDelayedReply(TimeSpan.FromMilliseconds(300), "second");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);

        var (_, roomId) = await WaitForDirectRoomAsync(fixture, "nova", ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();

        var post1 = chat.PostAsync(roomId, KnownIds.Human, "one", ct: ct);
        var post2 = chat.PostAsync(roomId, KnownIds.Human, "two", ct: ct);
        await Task.WhenAll(post1, post2);

        var history = await WaitForHistoryCountAsync(store, roomId, 4, ct);

        Assert.False(factory.Session.OverlapDetected);
        Assert.Equal(2, factory.Session.Prompts.Count);
        Assert.Contains(history, m => m.Text == "first");
        Assert.Contains(history, m => m.Text == "second");
    }

    [Fact]
    public async Task PromptFailure_DoesNotKillTheReadLoop()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueFailure(new InvalidOperationException("boom"));
        factory.Session.EnqueueReply("recovered");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);

        var (agentId, roomId) = await WaitForDirectRoomAsync(fixture, "nova", ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();

        await chat.PostAsync(roomId, KnownIds.Human, "first message", ct: ct);
        await chat.PostAsync(roomId, KnownIds.Human, "second message", ct: ct);

        var history = await WaitForHistoryCountAsync(store, roomId, 3, ct);

        Assert.Equal("recovered", history[^1].Text);
        Assert.Equal(agentId, history[^1].SenderId);
    }

    /// <summary>
    /// Regression test for the crashed-agent hang (roadmap item 3): before <c>RunEventReaderAsync</c>
    /// grew its <c>finally</c>, faulting the event stream mid-turn left <c>ProcessWorkItemAsync</c>
    /// awaiting a <c>TurnCompleted</c> that could now never arrive, so the single consumer never
    /// drained another <see cref="PersonaRunner.WorkItem"/> - the Agent went deaf in every Room,
    /// permanently. A second message posted after the fault is only ever attempted (recorded in
    /// <see cref="FakeAgentSession.Prompts"/>) if the consumer actually moved past the faulted turn.
    /// </summary>
    [Fact]
    public async Task EventStreamFaulting_UnblocksTheInFlightTurn()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueDelayedReply(TimeSpan.FromMilliseconds(300), "never posted");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);

        var (_, roomId) = await WaitForDirectRoomAsync(fixture, "nova", ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();

        await chat.PostAsync(roomId, KnownIds.Human, "first message", ct: ct);

        // Give the first turn's PromptAsync a moment to start its 300ms delay before the fault lands,
        // so the fault genuinely arrives mid-turn rather than before the turn even started.
        await Task.Delay(TimeSpan.FromMilliseconds(50), ct);
        factory.Session.FaultEvents(new IOException("adapter process died"));

        await chat.PostAsync(roomId, KnownIds.Human, "second message", ct: ct);

        // Bounded: if the finally were removed, the consumer would stay parked on the first turn's
        // completion source forever, this count would never reach 2, and the test fails on timeout
        // instead of hanging the suite.
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (factory.Session.Prompts.Count < 2 && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(50, ct);
        }

        Assert.Equal(2, factory.Session.Prompts.Count);
    }

    /// <summary>
    /// Companion to <see cref="EventStreamFaulting_UnblocksTheInFlightTurn"/>: the same fault must also
    /// be logged, not merely survived, so a crashed agent process leaves a trace somewhere. Uses a
    /// capturing logger rather than <see cref="NullLogger{T}"/> because this is the one behaviour
    /// <see cref="NullLogger{T}"/> cannot observe.
    /// </summary>
    [Fact]
    public async Task EventStreamFaulting_IsLogged()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueDelayedReply(TimeSpan.FromMilliseconds(300), "never posted");
        var persona = new Persona("nova", "You are Nova.");
        var logger = new RecordingLogger<PersonaRunner>();

        await using var agentHost = CreateHost(fixture, persona, factory, logger);
        await agentHost.StartAsync(ct);

        var (_, roomId) = await WaitForDirectRoomAsync(fixture, "nova", ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();

        await chat.PostAsync(roomId, KnownIds.Human, "first message", ct: ct);
        await Task.Delay(TimeSpan.FromMilliseconds(50), ct);
        factory.Session.FaultEvents(new IOException("adapter process died"));

        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (!logger.Entries.Exists(entry => entry.Level == LogLevel.Warning) && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(50, ct);
        }

        Assert.Contains(
            logger.Entries,
            entry => entry.Level == LogLevel.Warning && entry.Message.Contains(persona.Name, StringComparison.Ordinal));
    }

    /// <summary>
    /// Proves item 2 of task T2.1: <c>ProcessWorkItemAsync</c> mints a Message id and carries it on
    /// <see cref="PostMessage"/> rather than sending <see langword="null"/>. Talks to a hand-rolled
    /// server rather than the usual <see cref="PipeHostFixture"/> so it can inspect the raw envelope
    /// the runner writes, not only whatever id a full <see cref="ChatService"/> would persist - which
    /// would look identical whether or not the runner supplied one, since <c>ChatService.PostAsync</c>
    /// mints its own id when <c>messageId</c> is null.
    /// </summary>
    [Fact]
    public async Task PostMessage_CarriesTheMintedMessageId()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var server = new FakePersonaServer();
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReply("hi back");
        var persona = new Persona("nova", "You are Nova.");
        var options = Options.Create(new TeamOptions { PipeName = server.PipeName });

        await using var runner = new PersonaRunner(persona, options, factory, new FakeHookSource(), NullLogger<PersonaRunner>.Instance);
        await server.HandshakeAsync(runner, ct);

        await server.SendAsync(
            new MessagePosted(
                "room-1",
                "Room",
                new ChatMessage("human-message", DateTimeOffset.UtcNow, "human", "You", "hi"),
                Mentioned: true,
                Mentions: [],
                Members: [],
                AgentMessagesSinceHuman: 0,
                Budget: 0),
            ct);

        var posted = await server.ReceiveAsync<PostMessage>(ct);

        Assert.NotNull(posted.MessageId);
        Assert.True(NameRules.IsValidId(posted.MessageId));
    }

    /// <summary>
    /// Proves item 5 of task T2.1: a Turn that ends in <see cref="StopReason.Refusal"/> posts nothing,
    /// even though text arrived before the stop - a refusal is not a successful reply.
    /// </summary>
    [Fact]
    public async Task TurnEndingInRefusal_PostsNoMessage()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReplyEndingIn(StopReason.Refusal, "I can't help with that.");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);

        var (_, roomId) = await WaitForDirectRoomAsync(fixture, "nova", ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();

        await chat.PostAsync(roomId, KnownIds.Human, "hi", ct: ct);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (factory.Session.Prompts.Count < 1 && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(50, ct);
        }

        Assert.Single(factory.Session.Prompts);

        // Bounded grace period for a (misbehaving) refusal to be posted anyway.
        await Task.Delay(TimeSpan.FromMilliseconds(500), ct);

        var history = await store.ReadAllAsync(roomId, ct);
        Assert.Single(history);
    }

    [Fact]
    public async Task BlankReply_PostsNothing()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReply("   ");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);

        var (_, roomId) = await WaitForDirectRoomAsync(fixture, "nova", ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();

        await chat.PostAsync(roomId, KnownIds.Human, "hi", ct: ct);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (factory.Session.Prompts.Count < 1 && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(50, ct);
        }

        Assert.Single(factory.Session.Prompts);

        // Bounded grace period for a (misbehaving) blank reply to be posted anyway.
        await Task.Delay(TimeSpan.FromMilliseconds(500), ct);

        var history = await store.ReadAllAsync(roomId, ct);
        Assert.Single(history);
    }

    [Fact]
    public async Task Untagged_MessagesAreNeverSubmitted()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);

        var (novaId, _) = await WaitForDirectRoomAsync(fixture, "nova", ct);

        await using var friend = await fixture.ConnectClientAsync(ct);
        await friend.WriteAsync(new Hello("friend", null), ct);
        var friendWelcome = Assert.IsType<Welcome>(await friend.ReadAsync(ct));

        var chat = fixture.Services.GetRequiredService<ChatService>();
        var room = await chat.CreateRoomForAsync([novaId, friendWelcome.AgentId], ct);

        await chat.PostAsync(room.Id, KnownIds.Human, "unmentioned one", ct: ct);
        await chat.PostAsync(room.Id, KnownIds.Human, "unmentioned two", ct: ct);
        await chat.PostAsync(room.Id, KnownIds.Human, "unmentioned three", ct: ct);

        // Bounded wait: give a (misbehaving) unmentioned agent every chance to reply before asserting it
        // did not (docs/ChatRoom.md records a test that logged 4299 messages in two seconds).
        await Task.Delay(TimeSpan.FromSeconds(1), ct);

        Assert.Empty(factory.Session.Prompts);
    }

    [Fact]
    public async Task Tagged_PromptIncludesMissedMessages()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReply("caught up");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);

        var (novaId, _) = await WaitForDirectRoomAsync(fixture, "nova", ct);

        await using var friend = await fixture.ConnectClientAsync(ct);
        await friend.WriteAsync(new Hello("friend", null), ct);
        var friendWelcome = Assert.IsType<Welcome>(await friend.ReadAsync(ct));

        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();
        var room = await chat.CreateRoomForAsync([novaId, friendWelcome.AgentId], ct);

        await chat.PostAsync(room.Id, KnownIds.Human, "missed one", ct: ct);
        await chat.PostAsync(room.Id, KnownIds.Human, "missed two", ct: ct);
        await chat.PostAsync(room.Id, KnownIds.Human, "missed three", ct: ct);
        await chat.PostAsync(room.Id, KnownIds.Human, "@nova wake up", ct: ct);

        await WaitForHistoryCountAsync(store, room.Id, 5, ct);

        var prompt = Assert.Single(factory.Session.Prompts);
        Assert.Contains("missed one", prompt, StringComparison.Ordinal);
        Assert.Contains("missed two", prompt, StringComparison.Ordinal);
        Assert.Contains("missed three", prompt, StringComparison.Ordinal);
        Assert.Contains("wake up", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CatchUp_IsClearedAfterUse()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReply("first catch up");
        factory.Session.EnqueueReply("second catch up");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);

        var (novaId, _) = await WaitForDirectRoomAsync(fixture, "nova", ct);

        await using var friend = await fixture.ConnectClientAsync(ct);
        await friend.WriteAsync(new Hello("friend", null), ct);
        var friendWelcome = Assert.IsType<Welcome>(await friend.ReadAsync(ct));

        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();
        var room = await chat.CreateRoomForAsync([novaId, friendWelcome.AgentId], ct);

        await chat.PostAsync(room.Id, KnownIds.Human, "batch one missed", ct: ct);
        await chat.PostAsync(room.Id, KnownIds.Human, "@nova first tag", ct: ct);
        await WaitForHistoryCountAsync(store, room.Id, 3, ct);

        await chat.PostAsync(room.Id, KnownIds.Human, "@nova second tag", ct: ct);
        await WaitForHistoryCountAsync(store, room.Id, 5, ct);

        Assert.Equal(2, factory.Session.Prompts.Count);
        var secondPrompt = factory.Session.Prompts[1];
        Assert.DoesNotContain("batch one missed", secondPrompt, StringComparison.Ordinal);
        Assert.Contains("second tag", secondPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CatchUp_IsPerRoom()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReply("reply for room b");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);

        var (novaId, _) = await WaitForDirectRoomAsync(fixture, "nova", ct);

        await using var friend = await fixture.ConnectClientAsync(ct);
        await friend.WriteAsync(new Hello("friend", null), ct);
        var friendWelcome = Assert.IsType<Welcome>(await friend.ReadAsync(ct));

        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();
        var roomA = await chat.CreateRoomForAsync([novaId, friendWelcome.AgentId], ct);
        var roomB = await chat.CreateRoomForAsync([novaId, friendWelcome.AgentId], ct);

        await chat.PostAsync(roomA.Id, KnownIds.Human, "room a secret missed message", ct: ct);
        await chat.PostAsync(roomB.Id, KnownIds.Human, "@nova hello in room b", ct: ct);

        await WaitForHistoryCountAsync(store, roomB.Id, 2, ct);

        var prompt = Assert.Single(factory.Session.Prompts);
        Assert.DoesNotContain("room a secret missed message", prompt, StringComparison.Ordinal);
        Assert.Contains("hello in room b", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CatchUp_IsBoundedToTheConfiguredCount()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        var additionalConfig = new Dictionary<string, string?>
        {
            ["Team:Acp:CatchUpMessages"] = "2",
        };
        await using var fixture = await PipeHostFixture.StartAsync(additionalConfig, ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReply("bounded reply");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);

        var (novaId, _) = await WaitForDirectRoomAsync(fixture, "nova", ct);

        await using var friend = await fixture.ConnectClientAsync(ct);
        await friend.WriteAsync(new Hello("friend", null), ct);
        var friendWelcome = Assert.IsType<Welcome>(await friend.ReadAsync(ct));

        var chat = fixture.Services.GetRequiredService<ChatService>();
        var room = await chat.CreateRoomForAsync([novaId, friendWelcome.AgentId], ct);

        await chat.PostAsync(room.Id, KnownIds.Human, "oldest missed", ct: ct);
        await chat.PostAsync(room.Id, KnownIds.Human, "middle missed", ct: ct);
        await chat.PostAsync(room.Id, KnownIds.Human, "newest missed", ct: ct);
        await chat.PostAsync(room.Id, KnownIds.Human, "@nova tag", ct: ct);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (factory.Session.Prompts.Count < 1 && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(50, ct);
        }

        var prompt = Assert.Single(factory.Session.Prompts);
        Assert.DoesNotContain("oldest missed", prompt, StringComparison.Ordinal);
        Assert.Contains("middle missed", prompt, StringComparison.Ordinal);
        Assert.Contains("newest missed", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DirectRoom_HasNoCatchUp()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReply("pong");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);

        var (_, roomId) = await WaitForDirectRoomAsync(fixture, "nova", ct);
        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();

        await chat.PostAsync(roomId, KnownIds.Human, "hello there", ct: ct);

        await WaitForHistoryCountAsync(store, roomId, 2, ct);

        var prompt = Assert.Single(factory.Session.Prompts);

        // "context only" used to be a literal in BuildPrompt; it now lives entirely inside the
        // turn.catchUpHeader hook default. Deriving the expected phrase from HookCatalog rather than
        // typing it here means a reworded default still fails this test loudly when the header leaks
        // into a no-catch-up prompt, instead of silently asserting against wording nobody owns anymore.
        // Do not "simplify" this back to a literal — see task T1.11.
        var catchUpHeaderDefault = HookCatalog.Get("turn.catchUpHeader").Default;
        var distinctivePortion = HookRenderer
            .Render(catchUpHeaderDefault, new Dictionary<string, string> { ["{{roomLabel}}"] = string.Empty })
            .Trim();

        Assert.DoesNotContain(distinctivePortion, prompt, StringComparison.Ordinal);
    }

    // The mcp__team__ five-name pin and the orientation-ordering pin both moved to
    // Hooks/HookDefaultsTests.cs (task T1.11): rendered against a caller-supplied toolNames argument,
    // they proved only that Compose's own argument came back out of its own output, not anything about
    // the product's shipped wording. HookDefaultsTests re-anchors both against HookCatalog's actual
    // defaults, which is the thing that can vary now.

    [Fact]
    public void SystemPromptComposer_IncludesPersonaText()
    {
        var persona = new Persona("nova", "# Nova\nYou are a helpful assistant named Nova.");

        var prompt = SystemPromptComposer.Compose(persona, new FakeHookSource(), "mcp__team__get_help", ToolNames);

        Assert.Contains("You are a helpful assistant named Nova.", prompt, StringComparison.Ordinal);
    }

    /// <summary>
    /// Proves the feature this task adds: a configured override for a hook's text reaches the composed
    /// prompt in place of <see cref="HookCatalog"/>'s default.
    /// </summary>
    [Fact]
    public void SystemPromptComposer_HookOverride_ReachesTheComposedPrompt()
    {
        var persona = new Persona("nova", "You are Nova.");
        var hooks = new FakeHookSource();
        hooks.SetOverride("systemPrompt.identity", "You are, unusually, called \"{{personaName}}\" here.");

        var prompt = SystemPromptComposer.Compose(persona, hooks, "mcp__team__get_help", ToolNames);

        Assert.Contains("You are, unusually, called \"nova\" here.", prompt, StringComparison.Ordinal);
    }

    /// <summary>
    /// Proves the same feature for the per-turn prompt: a configured override for the Room label hook
    /// reaches the text <see cref="PersonaRunner.BuildPrompt"/> produces, in place of the catalog
    /// default.
    /// </summary>
    [Fact]
    public void BuildPrompt_HookOverride_ReachesTheTurnPrompt()
    {
        var hooks = new FakeHookSource();
        hooks.SetOverride("turn.roomLabel", "<<{{roomName}}/{{roomId}}>>");
        var item = new PersonaRunner.WorkItem("room-9", "Nova & You", "You", "hello", []);

        var prompt = PersonaRunner.BuildPrompt(item, hooks);

        Assert.Contains("<<Nova & You/room-9>>", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GroupRoom_AtBudget_MentionedAgentTakesNoTurn()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:AgentMessageBudget"] = "1" }, ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReply("should not appear");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);
        var (novaId, _) = await WaitForDirectRoomAsync(fixture, "nova", ct);

        var (chat, store, room, friendId) = await CreateGroupWithFriendAsync(fixture, novaId, ct);

        // The friend's message is itself the first - and only - agent message this Room allows, so by
        // the time it reaches Nova the Room is already spent.
        await chat.PostAsync(room.Id, friendId, "@nova hello", ct: ct);

        // Bounded wait: give a misbehaving agent every chance to reply before asserting it did not.
        await Task.Delay(TimeSpan.FromSeconds(1), ct);

        var history = await store.ReadAllAsync(room.Id, ct);
        Assert.Single(history);
    }

    // The payoff of the three-state gate. A Catch-up Message was missed and rides along later; a
    // Message declined for Budget is being held for re-delivery, so buffering it would show it to the
    // model twice in one prompt.
    [Fact]
    public async Task BudgetExhausted_DoesNotBufferAsCatchUp()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:AgentMessageBudget"] = "1" }, ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReply("woken by the human");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);
        var (novaId, _) = await WaitForDirectRoomAsync(fixture, "nova", ct);

        var (chat, store, room, friendId) = await CreateGroupWithFriendAsync(fixture, novaId, ct);
        await chat.PostAsync(room.Id, friendId, "@nova declined for budget", ct: ct);
        await Task.Delay(TimeSpan.FromSeconds(1), ct);

        await chat.PostAsync(room.Id, KnownIds.Human, "@nova carry on", ct: ct);
        await WaitForHistoryCountAsync(store, room.Id, 3, ct);

        var prompt = Assert.Single(factory.Session.Prompts);
        Assert.DoesNotContain("declined for budget", prompt, StringComparison.Ordinal);
    }

    // Continue is only meaningful if it wakes the exchange it paused: a Turn never starts on its own.
    [Fact]
    public async Task Redelivery_AfterExtend_ProducesATurn()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:AgentMessageBudget"] = "1" }, ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReply("resumed");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);
        var (novaId, _) = await WaitForDirectRoomAsync(fixture, "nova", ct);

        var (chat, store, room, friendId) = await CreateGroupWithFriendAsync(fixture, novaId, ct);
        await chat.PostAsync(room.Id, friendId, "@nova hello", ct: ct);
        await Task.Delay(TimeSpan.FromSeconds(1), ct);
        Assert.Single(await store.ReadAllAsync(room.Id, ct));

        var extended = await chat.ExtendBudgetAsync(room.Id, ct);

        Assert.True(extended);
        var history = await WaitForHistoryCountAsync(store, room.Id, 2, ct);
        Assert.Equal("resumed", history[^1].Text);
        Assert.Equal(novaId, history[^1].SenderId);
    }

    [Fact]
    public async Task TokenBudget_WhenExhausted_StopsTakingTurns()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        // The message Budget is off so only the token Budget can stop anything.
        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?>
            {
                ["Team:AgentMessageBudget"] = "0",
                ["Team:Acp:TokenBudget"] = "100",
            },
            ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReplyWithUsage([150], "first reply");
        factory.Session.EnqueueReply("second reply");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);
        var (novaId, _) = await WaitForDirectRoomAsync(fixture, "nova", ct);

        var (chat, store, room, friendId) = await CreateGroupWithFriendAsync(fixture, novaId, ct);
        await chat.PostAsync(room.Id, friendId, "@nova one", ct: ct);
        await WaitForHistoryCountAsync(store, room.Id, 2, ct);

        await chat.PostAsync(room.Id, friendId, "@nova two", ct: ct);
        await Task.Delay(TimeSpan.FromSeconds(1), ct);

        // Two posts from the friend and one reply: the second turn never ran.
        var history = await store.ReadAllAsync(room.Id, ct);
        Assert.Equal(3, history.Count);
        Assert.DoesNotContain(history, m => m.Text == "second reply");
    }

    // UsageUpdated.Used is a level, so a compaction makes it fall. Levels 100, 150, 20, 60 are 190
    // tokens of work; summing the levels themselves would read 330 and trip a 200 budget that should
    // not trip.
    [Fact]
    public async Task TokenBudget_IgnoresDropsInUsed()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?>
            {
                ["Team:AgentMessageBudget"] = "0",
                ["Team:Acp:TokenBudget"] = "200",
            },
            ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReplyWithUsage([100, 150, 20, 60], "first reply");
        factory.Session.EnqueueReply("second reply");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);
        var (novaId, _) = await WaitForDirectRoomAsync(fixture, "nova", ct);

        var (chat, store, room, friendId) = await CreateGroupWithFriendAsync(fixture, novaId, ct);
        await chat.PostAsync(room.Id, friendId, "@nova one", ct: ct);
        await WaitForHistoryCountAsync(store, room.Id, 2, ct);

        await chat.PostAsync(room.Id, friendId, "@nova two", ct: ct);
        var history = await WaitForHistoryCountAsync(store, room.Id, 4, ct);

        Assert.Contains(history, m => m.Text == "second reply");
    }

    [Fact]
    public async Task TokenBudget_ResetsOnAHumanMessage()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?>
            {
                ["Team:AgentMessageBudget"] = "0",
                ["Team:Acp:TokenBudget"] = "100",
            },
            ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReplyWithUsage([150], "first reply");
        factory.Session.EnqueueReply("after the human spoke");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);
        var (novaId, _) = await WaitForDirectRoomAsync(fixture, "nova", ct);

        var (chat, store, room, friendId) = await CreateGroupWithFriendAsync(fixture, novaId, ct);
        await chat.PostAsync(room.Id, friendId, "@nova one", ct: ct);
        await WaitForHistoryCountAsync(store, room.Id, 2, ct);

        await chat.PostAsync(room.Id, KnownIds.Human, "@nova carry on", ct: ct);
        var history = await WaitForHistoryCountAsync(store, room.Id, 4, ct);

        Assert.Equal("after the human spoke", history[^1].Text);
    }

    [Fact]
    public async Task TokenBudget_ZeroDisablesTheCap()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?>
            {
                ["Team:AgentMessageBudget"] = "0",
                ["Team:Acp:TokenBudget"] = "0",
            },
            ct);
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReplyWithUsage([999_999], "first reply");
        factory.Session.EnqueueReply("second reply");
        var persona = new Persona("nova", "You are Nova.");

        await using var agentHost = CreateHost(fixture, persona, factory);
        await agentHost.StartAsync(ct);
        var (novaId, _) = await WaitForDirectRoomAsync(fixture, "nova", ct);

        var (chat, store, room, friendId) = await CreateGroupWithFriendAsync(fixture, novaId, ct);
        await chat.PostAsync(room.Id, friendId, "@nova one", ct: ct);
        await WaitForHistoryCountAsync(store, room.Id, 2, ct);

        await chat.PostAsync(room.Id, friendId, "@nova two", ct: ct);
        var history = await WaitForHistoryCountAsync(store, room.Id, 4, ct);

        Assert.Contains(history, m => m.Text == "second reply");
    }

    /// <summary>
    /// Puts Nova in a Room with a second Agent, so a test can post agent-authored Messages - the only
    /// kind that spends Budget. The friend is a bare pipe client: it never replies, which keeps every
    /// Message in these Rooms one the test asked for.
    /// </summary>
    /// <param name="fixture">The running host.</param>
    /// <param name="novaId">The Agent id of the Persona under test.</param>
    /// <param name="ct">Cancels the handshake and the Room creation.</param>
    /// <returns>The chat service, the store, the Room, and the friend's Agent id.</returns>
    private static async Task<(ChatService Chat, IChatStore Store, Room Room, string FriendId)> CreateGroupWithFriendAsync(
        PipeHostFixture fixture, string novaId, CancellationToken ct)
    {
        var friend = await fixture.ConnectClientAsync(ct);
        await friend.WriteAsync(new Hello("friend", null), ct);
        var friendWelcome = Assert.IsType<Welcome>(await friend.ReadAsync(ct));

        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();
        var room = await chat.CreateRoomForAsync([novaId, friendWelcome.AgentId], ct);
        return (chat, store, room, friendWelcome.AgentId);
    }

    private static PersonaRunner CreateHost(
        PipeHostFixture fixture, Persona persona, FakeAgentHostFactory factory, ILogger<PersonaRunner>? logger = null)
    {
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        return new PersonaRunner(persona, options, factory, new FakeHookSource(), logger ?? NullLogger<PersonaRunner>.Instance);
    }

    private static async Task<(string AgentId, string RoomId)> WaitForDirectRoomAsync(
        PipeHostFixture fixture, string agentName, CancellationToken ct)
    {
        var directory = fixture.Services.GetRequiredService<ITeamDirectory>();
        var gateway = fixture.Services.GetRequiredService<IAgentGateway>();

        while (true)
        {
            var user = await directory.FindUserByNameAsync(agentName, ct);
            if (user is not null && gateway.IsOnline(user.Id))
            {
                var room = await directory.FindRoomWithExactMembersAsync(KnownIds.Human, user.Id, ct);
                if (room is not null)
                {
                    return (user.Id, room.Id);
                }
            }

            await Task.Delay(50, ct);
        }
    }

    private static async Task<IReadOnlyList<ChatMessage>> WaitForHistoryCountAsync(
        IChatStore store, string roomId, int count, CancellationToken ct)
    {
        while (true)
        {
            var history = await store.ReadAllAsync(roomId, ct);
            if (history.Count >= count)
            {
                return history;
            }

            await Task.Delay(50, ct);
        }
    }

    /// <summary>
    /// A capturing <see cref="ILogger{TCategoryName}"/> fake, for the one test in this file that must
    /// assert against a log call rather than a chat side effect. Every other test in this file uses
    /// <see cref="NullLogger{T}"/>, which by design cannot be asserted against.
    /// </summary>
    /// <typeparam name="T">The logger's category type.</typeparam>
    private sealed class RecordingLogger<T> : ILogger<T>
    {
        /// <summary>Every call made to this logger so far, in call order.</summary>
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        /// <summary>Not used by this fake: scoping is irrelevant to the one test that needs it, so this returns a no-op.</summary>
        /// <typeparam name="TState">The scope state type.</typeparam>
        /// <param name="state">The scope state.</param>
        /// <returns>A no-op <see cref="IDisposable"/>.</returns>
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        /// <summary>Always enabled, so every call this fake receives is actually recorded.</summary>
        /// <param name="logLevel">The level being checked.</param>
        /// <returns><see langword="true"/>, always.</returns>
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <summary>Records one log call's level and formatted message.</summary>
        /// <typeparam name="TState">The state type carrying this call's structured values.</typeparam>
        /// <param name="logLevel">The call's severity.</param>
        /// <param name="eventId">Unused by this fake.</param>
        /// <param name="state">The call's structured state, passed to <paramref name="formatter"/>.</param>
        /// <param name="exception">The call's exception, if any.</param>
        /// <param name="formatter">Formats <paramref name="state"/> and <paramref name="exception"/> into the message text.</param>
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            this.Entries.Add((logLevel, formatter(state, exception)));
        }
    }

    /// <summary>
    /// A hand-rolled, single-connection ACP server, used only by
    /// <see cref="PostMessage_CarriesTheMintedMessageId"/> to read the raw <see cref="PostMessage"/>
    /// envelope a <see cref="PersonaRunner"/> writes. Every other test in this file runs the runner
    /// against a real <see cref="PipeHostFixture"/> composition root instead, because most behaviour is
    /// easiest to observe through <see cref="ChatService"/> and <see cref="IChatStore"/> - but the id a
    /// full <see cref="ChatService.PostAsync"/> ultimately persists looks identical whether or not the
    /// runner supplied one, since it mints its own when the supplied id is <see langword="null"/>. Only
    /// the wire envelope itself can prove the runner sent one.
    /// </summary>
    private sealed class FakePersonaServer : IAsyncDisposable
    {
        private readonly NamedPipeServerStream pipe;

        private JsonLineStream? stream;

        /// <summary>Creates the server-side pipe instance, unconnected, under a unique per-test name.</summary>
        public FakePersonaServer()
        {
            this.PipeName = "persona-runner-tests-" + Guid.NewGuid().ToString("N");
            this.pipe = new NamedPipeServerStream(
                this.PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        }

        /// <summary>The named pipe this server listens on; hand it to the runner's <see cref="TeamOptions.PipeName"/>.</summary>
        public string PipeName { get; }

        /// <summary>Starts <paramref name="runner"/>, accepts its connection, and completes the Hello/Welcome handshake.</summary>
        /// <param name="runner">The runner to start against this server.</param>
        /// <param name="ct">Cancels the accept, the handshake, and the runner's own start.</param>
        public async Task HandshakeAsync(PersonaRunner runner, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(runner);

            var startTask = runner.StartAsync(ct);
            await this.pipe.WaitForConnectionAsync(ct);

            var lineStream = new JsonLineStream(this.pipe);
            this.stream = lineStream;

            var hello = Assert.IsType<Hello>(await lineStream.ReadAsync(ct));
            await lineStream.WriteAsync(new Welcome("agent-1", hello.Name, []), ct);

            await startTask;
        }

        /// <summary>Writes one envelope to the connected runner.</summary>
        /// <param name="message">The envelope to send.</param>
        /// <param name="ct">Cancels the write.</param>
        public Task SendAsync(ProtocolMessage message, CancellationToken ct)
        {
            return this.stream!.WriteAsync(message, ct);
        }

        /// <summary>Reads the next envelope the runner writes and asserts its concrete type.</summary>
        /// <typeparam name="T">The expected envelope type.</typeparam>
        /// <param name="ct">Cancels the read.</param>
        /// <returns>The envelope, typed as <typeparamref name="T"/>.</returns>
        public async Task<T> ReceiveAsync<T>(CancellationToken ct)
            where T : ProtocolMessage
        {
            var message = await this.stream!.ReadAsync(ct);
            return Assert.IsType<T>(message);
        }

        /// <summary>Disposes the line stream if the handshake reached it, otherwise the bare pipe.</summary>
        public async ValueTask DisposeAsync()
        {
            if (this.stream is not null)
            {
                await this.stream.DisposeAsync();
            }
            else
            {
                await this.pipe.DisposeAsync();
            }
        }
    }
}
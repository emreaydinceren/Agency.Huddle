namespace Agency.Huddle.Tests.Acp.Sessions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Sessions.Fakes;
using Agency.Huddle.Tests.Pipes;

/// <summary>
/// Pins <see cref="RoomSession"/>'s Transcript Catch-up (RS §6.5): the first Turn after any open
/// reads it, later Turns on the same open use the in-memory buffer instead, and a refused or
/// unanswered read (RS §9 E-3) never blocks the Turn - only the 10-second wait <see cref="FakeRoomSessionOwner"/>
/// stands in for at this level, rather than a real 10-second wait.
/// </summary>
public sealed class TranscriptCatchUpTests
{
    private static readonly Persona Nova = new("nova", "You are Nova.");

    /// <summary>The first Turn after an open reads the Transcript with <c>after=null</c> (fresh open) and <c>max</c> from <see cref="AcpOptions.TranscriptCatchUpMessages"/>, and its rendered Messages replace the missed-messages buffer on that Turn.</summary>
    [Fact]
    public async Task FirstTurn_ReadsTranscript_RendersHeaderAndMessages()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReply("hi back");
        FakeRoomSessionOwner owner = new();
        owner.ReadTranscriptHandler = (roomId, _, _, _, _) => Task.FromResult<TranscriptTail?>(
            new TranscriptTail(
                "req",
                roomId,
                [
                    new ChatMessage("m1", DateTimeOffset.UtcNow, "hu", "Human", "three options please"),
                    new ChatMessage("m2", DateTimeOffset.UtcNow, "nova-id", "Nova", "here are three options"),
                ],
                Omitted: 10));
        AcpOptions options = new() { TranscriptCatchUpMessages = 20 };
        var (room, runCts) = CreateSession(owner, FixedOpen(session), options);
        try
        {
            room.Enqueue(new QueuedWork(1, new WorkItem("room-1", "Room 1", "Human", "go with 2", [], TriggerMessageId: "trig-1")));

            await WaitUntilAsync(() => owner.Written.OfType<PostMessage>().Any(), ct);

            var call = Assert.Single(owner.TranscriptReadCalls);
            Assert.Equal("room-1", call.RoomId);
            Assert.Null(call.AfterMessageId);
            Assert.Equal("trig-1", call.BeforeMessageId);
            Assert.Equal(20, call.Max);

            var prompt = Assert.Single(session.Prompts);
            Assert.Contains("This is a new session for this Room", prompt, StringComparison.Ordinal);
            Assert.Contains("…10 earlier Messages are not shown.", prompt, StringComparison.Ordinal);
            Assert.Contains("Human: three options please", prompt, StringComparison.Ordinal);
            Assert.Contains("Nova: here are three options", prompt, StringComparison.Ordinal);
            Assert.Contains("go with 2", prompt, StringComparison.Ordinal);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>Only the first Turn after an open reads the Transcript; a second Turn on the same open never reads it again (D24 correction 19).</summary>
    [Fact]
    public async Task SecondTurn_UsesBufferNotTranscript()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReply("first reply");
        session.EnqueueReply("second reply");
        FakeRoomSessionOwner owner = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session));
        try
        {
            room.Enqueue(new QueuedWork(1, new WorkItem("room-1", "Room 1", "Human", "hello", [], TriggerMessageId: "trig-1")));
            await WaitUntilAsync(() => owner.Written.OfType<PostMessage>().Count() == 1, ct);

            room.Enqueue(new QueuedWork(2, new WorkItem("room-1", "Room 1", "Human", "again", [], TriggerMessageId: "trig-2")));
            await WaitUntilAsync(() => owner.Written.OfType<PostMessage>().Count() == 2, ct);

            Assert.Single(owner.TranscriptReadCalls);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>
    /// A command as a session's first Turn does not use up its Transcript read: it sends a bare command,
    /// so the Messages the session missed are still unseen, and the next ordinary Turn must still read them.
    /// </summary>
    [Fact]
    public async Task CommandFirst_LeavesTheTranscriptReadForTheNextOrdinaryTurn()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReply();
        session.EnqueueReply("ok");
        FakeRoomSessionOwner owner = new();
        owner.ReadTranscriptHandler = (roomId, _, _, _, _) => Task.FromResult<TranscriptTail?>(
            new TranscriptTail("req", roomId, [new ChatMessage("m1", DateTimeOffset.UtcNow, "hu", "Human", "an earlier remark")], Omitted: 0));
        var (room, runCts) = CreateSession(owner, FixedOpen(session));
        try
        {
            room.Enqueue(new QueuedWork(1, new WorkItem("room-1", "Room 1", "Human", "@nova /compact", [], WorkItemKind.Command, TriggerMessageId: "trig-1", Command: new AdapterCommandCall("compact", string.Empty))));
            await WaitUntilAsync(() => session.Prompts.Count == 1, ct);
            room.Enqueue(new QueuedWork(2, new WorkItem("room-1", "Room 1", "Human", "go on", [], TriggerMessageId: "trig-2")));

            await WaitUntilAsync(() => owner.Written.OfType<PostMessage>().Any(post => post.Text == "ok"), ct);

            var call = Assert.Single(owner.TranscriptReadCalls);
            Assert.Equal("trig-2", call.BeforeMessageId);
            Assert.Equal("/compact", session.Prompts[0]);
            Assert.Equal(
                RoomSession.BuildPrompt(
                    new WorkItem(
                        "room-1",
                        "Room 1",
                        "Human",
                        "go on",
                        [],
                        Transcript: new TranscriptCatchUp(false, [new ChatMessage("m1", DateTimeOffset.UtcNow, "hu", "Human", "an earlier remark")], 0)),
                    new FakePromptSource()),
                session.Prompts[1]);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>A Room's first Turn ever - the range before the trigger holds no Messages - leaves the prompt byte-identical to today's, with no Transcript block at all.</summary>
    [Fact]
    public async Task RoomsFirstTurnEver_NoBlock_ByteIdenticalToToday()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReply("hi there");
        FakeRoomSessionOwner owner = new();
        owner.ReadTranscriptHandler = (roomId, _, _, _, _) => Task.FromResult<TranscriptTail?>(new TranscriptTail("req", roomId, [], Omitted: 0));
        var (room, runCts) = CreateSession(owner, FixedOpen(session));
        try
        {
            room.Enqueue(new QueuedWork(1, new WorkItem("room-1", "Nova & You", "You", "hello there", [], TriggerMessageId: "trig-1")));

            await WaitUntilAsync(() => owner.Written.OfType<PostMessage>().Any(), ct);

            var prompt = Assert.Single(session.Prompts);
            var expected = RoomSession.BuildPrompt(new WorkItem("room-1", "Nova & You", "You", "hello there", []), new FakePromptSource());
            Assert.Equal(expected, prompt);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>RS §9 E-3: a refused or unanswered Transcript read (<see langword="null"/>) lets the Turn go ahead without the block, rather than failing it.</summary>
    [Fact]
    public async Task TranscriptRefusedOrSilent_TurnGoesAheadWithWarning()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReply("hi there");
        FakeRoomSessionOwner owner = new();
        owner.ReadTranscriptHandler = static (_, _, _, _, _) => Task.FromResult<TranscriptTail?>(null);
        var (room, runCts) = CreateSession(owner, FixedOpen(session));
        try
        {
            room.Enqueue(new QueuedWork(1, new WorkItem("room-1", "Nova & You", "You", "hello there", [], TriggerMessageId: "trig-1")));

            await WaitUntilAsync(() => owner.Written.OfType<PostMessage>().Any(), ct);

            var posted = Assert.Single(owner.Written.OfType<PostMessage>());
            Assert.Equal("hi there", posted.Text);
            var prompt = Assert.Single(session.Prompts);
            Assert.DoesNotContain("This is a new session", prompt, StringComparison.Ordinal);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>A Greeting reads no Transcript (it has no <see cref="WorkItem.TriggerMessageId"/>), and still consumes the "first Turn after open" opportunity: the ordinary Message right after it does not read one either.</summary>
    [Fact]
    public async Task Greeting_ConsumesFirstTurnFlag_WithoutReadingTranscript()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReply("hello!");
        session.EnqueueReply("hi back");
        FakeRoomSessionOwner owner = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session));
        try
        {
            room.Enqueue(new QueuedWork(1, new WorkItem("room-1", "Room 1", string.Empty, string.Empty, [], WorkItemKind.Greeting)));
            await WaitUntilAsync(() => owner.Written.OfType<PostMessage>().Count() == 1, ct);

            room.Enqueue(new QueuedWork(2, new WorkItem("room-1", "Room 1", "Human", "hi", [], TriggerMessageId: "trig-1")));
            await WaitUntilAsync(() => owner.Written.OfType<PostMessage>().Count() == 2, ct);

            Assert.Empty(owner.TranscriptReadCalls);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>A shared session (<c>RoomId</c> null) never reads a Transcript, unchanged from before D24.</summary>
    [Fact]
    public async Task SharedSession_NeverReadsTranscript()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReply("hi there");
        FakeRoomSessionOwner owner = new();
        CancellationTokenSource runCts = new();
        RoomSession room = new(
            roomId: null,
            open: FixedOpen(session),
            owner: owner,
            scheduler: new ImmediateTurnScheduler(),
            prompts: new FakePromptSource(),
            options: new AcpOptions(),
            fileChanges: null,
            declaredWatches: [],
            logger: NullLogger.Instance,
            runToken: runCts.Token);
        try
        {
            room.Enqueue(new QueuedWork(1, new WorkItem("room-1", "Room 1", "Human", "hi", [], TriggerMessageId: "trig-1")));

            await WaitUntilAsync(() => owner.Written.OfType<PostMessage>().Any(), ct);

            Assert.Empty(owner.TranscriptReadCalls);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>
    /// RS principle 4, U10, D-7: the Transcript range is fixed to the sequence's own trigger the
    /// moment it is enqueued, not to whatever is latest by the time the Turn actually runs. Nova's
    /// own Room Session is held busy by a gated Turn in a DIFFERENT Room first (the global
    /// <c>TurnGate</c>'s default <c>MaxConcurrentTurns</c> of 1 makes this cross-Room queueing, per
    /// finding P-4); the group Room's Mention is enqueued - and its trigger id fixed - while that
    /// gate is still held. Two more Messages land in the group Room, after the trigger, before the
    /// gate is released and Nova's Turn there is finally admitted. Its Transcript must show neither
    /// of them.
    /// </summary>
    [Fact]
    public async Task Panellist_QueuedBehindOthers_TranscriptEndsBeforeTrigger()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var roomSessions = fixture.Services.GetRequiredService<RoomSessionStore>();
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        var logger = fixture.Services.GetRequiredService<ILogger<PersonaRunner>>();

        var novaFactory = new FakeAgentHostFactory { SessionPerRoom = true };
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        novaFactory.Session.EnqueueGatedReply(gate.Task, "held reply");

        await using PersonaRunner novaRunner = new(
            new Persona("nova", "You are Nova."), options, novaFactory, new FakePromptSource(), new RoomFollows(), logger, roomSessions: roomSessions);
        await novaRunner.StartAsync(ct);

        var (novaId, roomXId) = await WaitForDirectRoomAsync(fixture, "nova", ct);
        var friendId = await RegisterLightweightAgentAsync(fixture, "friend", ct);

        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();
        var roomY = await chat.CreateRoomForAsync([novaId, friendId], ct);

        // Nova's own direct Room, RoomX: this Turn takes the global gate's only slot and hangs there
        // until gate is released below.
        await chat.PostAsync(roomXId, KnownIds.Human, "hold this room", ct: ct);
        await WaitUntilAsync(() => novaFactory.Session.Prompts.Count == 1, ct);

        // The trigger: enqueued (and its own Message id fixed as the Transcript's beforeMessageId)
        // while RoomX's Turn still holds the gate - RoomY's own Turn cannot even open its session
        // yet, let alone read the Transcript.
        await chat.PostAsync(roomY.Id, KnownIds.Human, "@nova please review, option 2", ct: ct);
        await WaitForHistoryCountAsync(store, roomY.Id, 1, ct);

        // Two more Messages land in RoomY, after the trigger, while Nova's Turn there still waits on
        // the gate - not yet admitted, so it has not read the Transcript yet.
        await chat.PostAsync(roomY.Id, KnownIds.Human, "no wait, actually option 3", ct: ct);
        await chat.PostAsync(roomY.Id, friendId, "I would go with option 3 too", ct: ct);
        await WaitForHistoryCountAsync(store, roomY.Id, 3, ct);

        gate.SetResult();

        await WaitUntilAsync(() => novaFactory.Host!.Sessions.Count == 2 && novaFactory.Host.Sessions[1].Prompts.Count == 1, ct);

        var prompt = novaFactory.Host!.Sessions[1].Prompts[0];
        Assert.Contains("please review, option 2", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("no wait, actually option 3", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("I would go with option 3 too", prompt, StringComparison.Ordinal);
    }

    /// <summary>
    /// RS §6.5 U4, over a real pipe, <see cref="ChatService"/> and <see cref="Agency.Huddle.App.Pipes.AgentConnection"/>:
    /// a group Room (3 Members, so a reply needs an explicit Mention) takes 30 Messages nobody is
    /// Mentioned in, then a 31st Mentioning Nova. Its first-Turn prompt carries the header, the
    /// omitted-count line for the 10 left out, the 20 before the trigger, and the trigger only once
    /// as the message line.
    /// </summary>
    [Fact]
    public async Task FirstMention_InRoomWith30Messages_PromptCarriesThe20Before()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var roomSessions = fixture.Services.GetRequiredService<RoomSessionStore>();
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        var logger = fixture.Services.GetRequiredService<ILogger<PersonaRunner>>();

        var novaFactory = new FakeAgentHostFactory { SessionPerRoom = true };
        var friendFactory = new FakeAgentHostFactory { SessionPerRoom = true };

        await using PersonaRunner novaRunner = new(
            new Persona("nova", "You are Nova."), options, novaFactory, new FakePromptSource(), new RoomFollows(), logger, roomSessions: roomSessions);
        await using PersonaRunner friendRunner = new(
            new Persona("friend", "You are Friend."), options, friendFactory, new FakePromptSource(), new RoomFollows(), logger, roomSessions: roomSessions);
        await novaRunner.StartAsync(ct);
        await friendRunner.StartAsync(ct);

        // Per-Room mode already opened one session each for nova's and friend's own Human Room at
        // start (RS §6.2's exception); the group Room below gets a distinct, later-opened session -
        // this is what novaFactory.Session (the FIRST-opened one) is NOT, so every assertion below
        // reads whichever session opens second.
        var (novaId, _) = await WaitForDirectRoomAsync(fixture, "nova", ct);
        var (friendId, _) = await WaitForDirectRoomAsync(fixture, "friend", ct);
        Assert.Single(novaFactory.Host!.Sessions);

        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();
        var room = await chat.CreateRoomForAsync([novaId, friendId], ct);

        for (var i = 0; i < 30; i++)
        {
            await chat.PostAsync(room.Id, KnownIds.Human, $"note {i}", ct: ct);
        }

        await WaitForHistoryCountAsync(store, room.Id, 30, ct);

        await chat.PostAsync(room.Id, KnownIds.Human, "@nova go with option 2", ct: ct);

        await WaitUntilAsync(() => novaFactory.Host.Sessions.Count == 2 && novaFactory.Host.Sessions[1].Prompts.Count == 1, ct);

        var prompt = novaFactory.Host.Sessions[1].Prompts[0];
        Assert.Contains("…10 earlier Messages are not shown.", prompt, StringComparison.Ordinal);
        Assert.Contains("You: note 10", prompt, StringComparison.Ordinal);
        Assert.Contains("You: note 29", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("You: note 9\n", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("note 0", prompt, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(prompt, "go with option 2"));
    }

    /// <summary>RS §9 E-11: an Agent Mentioned in a Room it was just invited to gets a fresh session whose Catch-up shows what was said before it joined.</summary>
    [Fact]
    public async Task InvitedToRoom_FirstMention_SeesWhatWasSaidBeforeItJoined()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var roomSessions = fixture.Services.GetRequiredService<RoomSessionStore>();
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        var logger = fixture.Services.GetRequiredService<ILogger<PersonaRunner>>();

        var friendFactory = new FakeAgentHostFactory { SessionPerRoom = true };
        friendFactory.Session.EnqueueReply("hi from before");
        var novaFactory = new FakeAgentHostFactory { SessionPerRoom = true };

        await using PersonaRunner friendRunner = new(
            new Persona("friend", "You are Friend."), options, friendFactory, new FakePromptSource(), new RoomFollows(), logger, roomSessions: roomSessions);
        await using PersonaRunner novaRunner = new(
            new Persona("nova", "You are Nova."), options, novaFactory, new FakePromptSource(), new RoomFollows(), logger, roomSessions: roomSessions);
        await friendRunner.StartAsync(ct);
        await novaRunner.StartAsync(ct);

        var (friendId, _) = await WaitForDirectRoomAsync(fixture, "friend", ct);

        // Waits only for Nova's own registration - its id is never needed directly here, since the
        // Invitation below addresses it by Name. Per-Room mode already opened nova's own Human Room
        // session at start (RS §6.2's exception), consuming novaFactory.Session - the Room it joins
        // below gets a distinct, second session.
        await WaitForDirectRoomAsync(fixture, "nova", ct);
        Assert.Single(novaFactory.Host!.Sessions);

        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();

        // A Room holding the Human and Friend only (friend's own existing direct Room), with talk
        // before Nova ever joins.
        var room = await chat.CreateRoomForAsync([friendId], ct);
        await chat.PostAsync(room.Id, KnownIds.Human, "let's plan the trip", ct: ct);
        await WaitForHistoryCountAsync(store, room.Id, 2, ct);

        await chat.InviteAsync(room.Id, "nova", ct);
        await chat.PostAsync(room.Id, KnownIds.Human, "@nova welcome, what do you think?", ct: ct);

        await WaitUntilAsync(() => novaFactory.Host.Sessions.Count == 2 && novaFactory.Host.Sessions[1].Prompts.Count == 1, ct);

        var prompt = novaFactory.Host.Sessions[1].Prompts[0];
        Assert.Contains("let's plan the trip", prompt, StringComparison.Ordinal);
        Assert.Contains("welcome, what do you think?", prompt, StringComparison.Ordinal);
    }

    /// <summary>How many non-overlapping times <paramref name="needle"/> occurs in <paramref name="haystack"/>.</summary>
    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    /// <summary>Waits until <paramref name="agentName"/> is registered and Online, then returns its id and its direct Room with the Human.</summary>
    private static async Task<(string AgentId, string RoomId)> WaitForDirectRoomAsync(PipeHostFixture fixture, string agentName, CancellationToken ct)
    {
        var directory = fixture.Services.GetRequiredService<ITeamDirectory>();
        var gateway = fixture.Services.GetRequiredService<Agency.Huddle.App.Pipes.IAgentGateway>();

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

    /// <summary>Waits until <paramref name="roomId"/>'s Transcript holds at least <paramref name="count"/> Messages.</summary>
    private static async Task WaitForHistoryCountAsync(IChatStore store, string roomId, int count, CancellationToken ct)
    {
        while ((await store.ReadAllAsync(roomId, ct)).Count < count)
        {
            await Task.Delay(50, ct);
        }
    }

    /// <summary>
    /// Registers <paramref name="agentName"/> as an Agent over a bare Hello/Welcome handshake on its
    /// own pipe connection, with no <see cref="PersonaRunner"/> behind it - the same lightweight
    /// pattern <c>PersonaRunnerTests.CreateGroupWithFriendAsync</c> uses for a Room's third Member
    /// that a test never needs to prompt or assert against, only to exist and be a Member.
    /// </summary>
    /// <param name="fixture">The fixture whose pipe to connect to.</param>
    /// <param name="agentName">The Name to register.</param>
    /// <param name="ct">Cancels the connect and the handshake.</param>
    /// <returns>The registered Agent's id.</returns>
    private static async Task<string> RegisterLightweightAgentAsync(PipeHostFixture fixture, string agentName, CancellationToken ct)
    {
        var client = await fixture.ConnectClientAsync(ct);
        await client.WriteAsync(new Hello(agentName, null), ct);
        var welcome = Assert.IsType<Welcome>(await client.ReadAsync(ct));
        return welcome.AgentId;
    }

    /// <summary>Builds a per-Room <see cref="RoomSession"/> with fakes standing in for every collaborator except resume, which these tests do not exercise.</summary>
    private static (RoomSession Session, CancellationTokenSource RunCts) CreateSession(
        FakeRoomSessionOwner owner, Func<CancellationToken, Task<IAgentSession>> open, AcpOptions? options = null)
    {
        CancellationTokenSource runCts = new();
        RoomSession session = new(
            roomId: "room-1",
            open: open,
            owner: owner,
            scheduler: new ImmediateTurnScheduler(),
            prompts: new FakePromptSource(),
            options: options ?? new AcpOptions(),
            fileChanges: null,
            declaredWatches: [],
            logger: NullLogger.Instance,
            runToken: runCts.Token,
            persona: Nova);
        return (session, runCts);
    }

    /// <summary>An open delegate that always returns the same session.</summary>
    private static Func<CancellationToken, Task<IAgentSession>> FixedOpen(FakeAgentSession session) =>
        _ => Task.FromResult<IAgentSession>(session);

    private static async Task DisposeSessionAsync(RoomSession session, CancellationTokenSource runCts)
    {
        await runCts.CancelAsync();
        await session.DisposeAsync();
        runCts.Dispose();
    }

    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken ct, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("Condition was not met before the test's timeout.");
            }

            await Task.Delay(10, ct);
        }
    }
}

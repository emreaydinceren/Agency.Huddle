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
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Sessions.Fakes;
using Agency.Huddle.Tests.Pipes;

/// <summary>
/// Pins <see cref="RoomSession"/>'s resume decision and <see cref="RoomSessionStore"/> read/write
/// (RS §6.1 "Opening", §6.6, §9 E-1/E-2), and <see cref="Agency.Huddle.App.Acp.PersonaRunner"/>'s own
/// start-up pruning and shared-mode "store untouched" rule (finding P-15).
/// </summary>
public sealed class RoomSessionResumeTests
{
    private static readonly Persona Nova = new("nova", "You are Nova.", Model: "m1", Effort: "e1");

    /// <summary>At Turn end (RS §6.6), the store holds the opened session's id, this Persona's Adapter/Model/Effort, and the reply's own minted id as <c>LastMessageId</c>.</summary>
    [Fact]
    public async Task TurnEnd_StoresEntry_LastMessageIdIsReplyIdOrTrigger()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using var dataDir = new TempDataDir();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
        FakeAgentSession session = new();
        session.EnqueueReply("hi back");
        FakePersonaHost host = new(session, ClaudeProfile());
        FakeRoomSessionOwner owner = new();
        var (room, runCts) = CreateRoomSession(owner, host, store);
        try
        {
            room.Enqueue(new QueuedWork(1, new WorkItem("room-1", "Room 1", "Human", "hi", [], TriggerMessageId: "trig-1")));

            await WaitUntilAsync(() => owner.Written.OfType<PostMessage>().Any(), ct);

            var posted = Assert.Single(owner.Written.OfType<PostMessage>());
            var entry = store.Get("nova", "room-1");
            Assert.NotNull(entry);
            Assert.Equal(session.SessionId, entry.SessionId);
            Assert.Equal("claude", entry.AdapterId);
            Assert.Equal("m1", entry.Model);
            Assert.Equal("e1", entry.Effort);
            Assert.Equal(posted.MessageId, entry.LastMessageId);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>
    /// Finding P-17: a Turn stopped after it showed activity still stores an entry, even though
    /// <c>PromptAsync</c> never returned - the store write is gated on <c>promptReturned ||
    /// turn.SawActivity</c>, not on <c>promptReturned</c> alone.
    /// </summary>
    [Fact]
    public async Task TurnStoppedAfterActivity_StillStoresEntry()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using var dataDir = new TempDataDir();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
        FakeAgentSession session = new();

        // The first chunk lands (and is observed via a MessageDelta below) after a short delay that
        // itself respects the Turn's own token; the Stop below arrives while PromptAsync is still
        // awaiting the delay before the SECOND chunk, so PromptAsync throws
        // OperationCanceledException and never returns - promptReturned stays false - while
        // turn.SawActivity has already been latched true by the first chunk's MessageChunk event.
        session.EnqueueDripFedReply(TimeSpan.FromMilliseconds(150), "chunk one", "chunk two never sent");
        FakePersonaHost host = new(session, ClaudeProfile());
        FakeRoomSessionOwner owner = new();
        var (room, runCts) = CreateRoomSession(owner, host, store);
        try
        {
            room.Enqueue(new QueuedWork(1, new WorkItem("room-1", "Room 1", "Human", "go", [], TriggerMessageId: "trig-6")));

            await WaitUntilAsync(() => owner.Written.OfType<MessageDelta>().Any(delta => !delta.IsFinal), ct);

            await room.StopAsync("room-1", mark: 0, ct);

            await WaitUntilAsync(() => owner.Written.OfType<MessageDelta>().Any(delta => delta.IsFinal), ct);

            Assert.Empty(owner.Written.OfType<PostMessage>());
            var entry = store.Get("nova", "room-1");
            Assert.NotNull(entry);
            Assert.Equal("trig-6", entry.LastMessageId);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>
    /// D22 correction 9: on a resume, the FIRST <see cref="UsageUpdated"/> is the baseline - it
    /// reports a whole restored context, not tokens this Turn spent - and is not counted against the
    /// Budget; a later rise within the same Turn is counted as usual.
    /// </summary>
    [Fact]
    public async Task Resumed_FirstUsageUpdatedIsBaseline_LaterRiseCounted()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using var dataDir = new TempDataDir();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
        store.Put("nova", "room-1", new RoomSessionEntry("stored-session-id", "claude", "m1", "e1", "msg-abc", DateTimeOffset.UtcNow));

        FakeAgentSession freshSession = new();
        FakeAgentSession resumedSession = new();

        // The first level (1000) is the resumed session's restored-context baseline and must not be
        // added; the second (1500) is a genuine rise of 500 during this Turn and must be.
        resumedSession.EnqueueReplyWithUsage([1000, 1500], "resumed reply");
        FakePersonaHost host = new(freshSession, ClaudeProfile()) { CanResume = true };
        host.ResumeHandler = id => string.Equals(id, "stored-session-id", StringComparison.Ordinal) ? resumedSession : null;

        FakeRoomSessionOwner owner = new();
        var (room, runCts) = CreateRoomSession(owner, host, store);
        try
        {
            room.Enqueue(new QueuedWork(1, new WorkItem("room-1", "Room 1", "Human", "go", [], TriggerMessageId: "trig-7")));

            await WaitUntilAsync(() => owner.Written.OfType<PostMessage>().Any(), ct);

            Assert.Equal(500, owner.TokensAdded);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>A stored entry whose Adapter, Model and Effort all match, with a resume-capable host, resumes by id and reads only the Messages after the stored <c>LastMessageId</c>.</summary>
    [Fact]
    public async Task Reopen_EntryMatches_ResumesById()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using var dataDir = new TempDataDir();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
        store.Put("nova", "room-1", new RoomSessionEntry("stored-session-id", "claude", "m1", "e1", "msg-abc", DateTimeOffset.UtcNow));

        FakeAgentSession freshSession = new();
        FakeAgentSession resumedSession = new();
        resumedSession.EnqueueReply("resumed reply");
        FakePersonaHost host = new(freshSession, ClaudeProfile()) { CanResume = true };
        host.ResumeHandler = id => string.Equals(id, "stored-session-id", StringComparison.Ordinal) ? resumedSession : null;

        FakeRoomSessionOwner owner = new();
        owner.ReadTranscriptHandler = (roomId, _, _, _, _) => Task.FromResult<TranscriptTail?>(
            new TranscriptTail("req", roomId, [new ChatMessage("m9", DateTimeOffset.UtcNow, "hu", "Human", "any update?")], Omitted: 0));

        var (room, runCts) = CreateRoomSession(owner, host, store);
        try
        {
            room.Enqueue(new QueuedWork(1, new WorkItem("room-1", "Room 1", "Human", "go", [], TriggerMessageId: "trig-2")));

            await WaitUntilAsync(() => owner.Written.OfType<PostMessage>().Any(), ct);

            Assert.Contains("stored-session-id", host.ResumeCalls);
            Assert.Single(owner.TranscriptReadCalls);
            Assert.Equal("msg-abc", owner.TranscriptReadCalls[0].AfterMessageId);
            var prompt = Assert.Single(resumedSession.Prompts);
            Assert.Contains("While this session was closed", prompt, StringComparison.Ordinal);
            Assert.DoesNotContain("This is a new session", prompt, StringComparison.Ordinal);
            Assert.Empty(freshSession.Prompts);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>RS §9 E-1: when the Adapter answers "not found" for a resumable entry, the session opens fresh and reads the latest Messages instead.</summary>
    [Fact]
    public async Task Reopen_ResumeNotFound_FreshWithLatest()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using var dataDir = new TempDataDir();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
        store.Put("nova", "room-1", new RoomSessionEntry("stored-session-id", "claude", "m1", "e1", "msg-abc", DateTimeOffset.UtcNow));

        FakeAgentSession freshSession = new();
        freshSession.EnqueueReply("fresh reply");
        FakePersonaHost host = new(freshSession, ClaudeProfile()) { CanResume = true };
        host.ResumeHandler = static _ => null;

        FakeRoomSessionOwner owner = new();
        owner.ReadTranscriptHandler = (roomId, _, _, _, _) => Task.FromResult<TranscriptTail?>(
            new TranscriptTail("req", roomId, [new ChatMessage("m9", DateTimeOffset.UtcNow, "hu", "Human", "earlier text")], Omitted: 0));

        var (room, runCts) = CreateRoomSession(owner, host, store);
        try
        {
            room.Enqueue(new QueuedWork(1, new WorkItem("room-1", "Room 1", "Human", "go", [], TriggerMessageId: "trig-3")));

            await WaitUntilAsync(() => owner.Written.OfType<PostMessage>().Any(), ct);

            Assert.Contains("stored-session-id", host.ResumeCalls);
            Assert.Null(owner.TranscriptReadCalls[0].AfterMessageId);
            var prompt = Assert.Single(freshSession.Prompts);
            Assert.Contains("This is a new session", prompt, StringComparison.Ordinal);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>RS §9 E-2: a resume that throws is a Turn failure, and the stored entry is kept unchanged, not overwritten or forgotten.</summary>
    [Fact]
    public async Task Reopen_ResumeThrows_TurnFailsEntryKept()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using var dataDir = new TempDataDir();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
        var original = new RoomSessionEntry("stored-session-id", "claude", "m1", "e1", "msg-abc", DateTimeOffset.UtcNow);
        store.Put("nova", "room-1", original);

        FakeAgentSession freshSession = new();
        FakePersonaHost host = new(freshSession, ClaudeProfile()) { CanResume = true };
        host.ResumeHandler = static _ => throw new InvalidOperationException("adapter refused to resume");

        FakeRoomSessionOwner owner = new();
        var (room, runCts) = CreateRoomSession(owner, host, store);
        try
        {
            room.Enqueue(new QueuedWork(1, new WorkItem("room-1", "Room 1", "Human", "go", [], TriggerMessageId: "trig-4")));

            await WaitUntilAsync(() => owner.ReportCalls.Contains("ReportTurnFailure"), ct);

            Assert.Empty(owner.Written.OfType<PostMessage>());
            var kept = store.Get("nova", "room-1");
            Assert.Equal(original, kept);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>A changed Model, Effort or Adapter id never resumes (RS §6.1's match), even when the host is resume-capable and holds the stored id.</summary>
    [Theory]
    [InlineData("different-model", "e1", "claude")]
    [InlineData("m1", "different-effort", "claude")]
    [InlineData("m1", "e1", "different-adapter")]
    public async Task Reopen_ModelEffortOrAdapterChanged_NeverResumes(string model, string effort, string adapterId)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using var dataDir = new TempDataDir();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
        store.Put("nova", "room-1", new RoomSessionEntry("stored-session-id", "claude", "m1", "e1", "msg-abc", DateTimeOffset.UtcNow));

        FakeAgentSession freshSession = new();
        freshSession.EnqueueReply("fresh reply");
        FakePersonaHost host = new(freshSession, ClaudeProfile() with { Id = adapterId }) { CanResume = true };
        host.ResumeHandler = static id => new FakeAgentSession();

        FakeRoomSessionOwner owner = new();
        var persona = new Persona("nova", "You are Nova.", Model: model, Effort: effort);
        var (room, runCts) = CreateRoomSession(owner, host, store, persona);
        try
        {
            room.Enqueue(new QueuedWork(1, new WorkItem("room-1", "Room 1", "Human", "go", [], TriggerMessageId: "trig-5")));

            await WaitUntilAsync(() => owner.Written.OfType<PostMessage>().Any(), ct);

            Assert.Empty(host.ResumeCalls);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>RS §6.6 "Pruned at start": a stored entry for a Room the Agent is no longer a Member of is dropped before the runner opens anything.</summary>
    [Fact]
    public async Task Start_PrunesEntriesForRoomsNotInWelcome()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var roomSessions = fixture.Services.GetRequiredService<RoomSessionStore>();
        roomSessions.Put("nova", "stale-room-id", new RoomSessionEntry("s1", "claude", null, null, null, DateTimeOffset.UtcNow));

        var factory = new FakeAgentHostFactory { SessionPerRoom = true };
        factory.Session.EnqueueReply("ok");
        var persona = new Persona("nova", "You are Nova.");
        var logger = fixture.Services.GetRequiredService<ILogger<PersonaRunner>>();

        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        await using PersonaRunner runner = new(
            persona, options, factory, new FakePromptSource(), new RoomFollows(), logger, roomSessions: roomSessions);
        await runner.StartAsync(ct);

        Assert.Null(roomSessions.Get("nova", "stale-room-id"));
    }

    /// <summary>Finding P-15: in shared mode the store is never touched - no file appears under <c>room-sessions/</c>, even after a Turn runs.</summary>
    [Fact]
    public async Task SharedMode_StoreUntouched()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(ct);
        var roomSessions = fixture.Services.GetRequiredService<RoomSessionStore>();
        var options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        var factory = new FakeAgentHostFactory();
        factory.Session.EnqueueReply("ok");
        var persona = new Persona("nova", "You are Nova.");
        var logger = fixture.Services.GetRequiredService<ILogger<PersonaRunner>>();

        await using PersonaRunner runner = new(
            persona, options, factory, new FakePromptSource(), new RoomFollows(), logger, roomSessions: roomSessions);
        await runner.StartAsync(ct);

        var directory = fixture.Services.GetRequiredService<ITeamDirectory>();
        var gateway = fixture.Services.GetRequiredService<IAgentGateway>();
        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();

        string? roomId = null;
        while (roomId is null)
        {
            var user = await directory.FindUserByNameAsync("nova", ct);
            if (user is not null && gateway.IsOnline(user.Id))
            {
                var room = await directory.FindRoomWithExactMembersAsync(KnownIds.Human, user.Id, ct);
                roomId = room?.Id;
            }

            if (roomId is null)
            {
                await Task.Delay(20, ct);
            }
        }

        await chat.PostAsync(roomId, KnownIds.Human, "hi", ct: ct);
        while ((await store.ReadAllAsync(roomId, ct)).Count < 2)
        {
            await Task.Delay(20, ct);
        }

        Assert.False(Directory.Exists(Path.Combine(options.Value.DataDir, "room-sessions")));
    }

    /// <summary>Builds a <see cref="RoomSession"/> in per-Room mode with a real <paramref name="store"/> and a resume-capable <paramref name="host"/>.</summary>
    private static (RoomSession Session, CancellationTokenSource RunCts) CreateRoomSession(
        FakeRoomSessionOwner owner, FakePersonaHost host, RoomSessionStore store, Persona? persona = null)
    {
        CancellationTokenSource runCts = new();
        RoomSession session = new(
            roomId: "room-1",
            open: host.OpenAsync,
            owner: owner,
            scheduler: new ImmediateTurnScheduler(),
            prompts: new FakePromptSource(),
            options: new AcpOptions(),
            fileChanges: null,
            declaredWatches: [],
            logger: NullLogger.Instance,
            runToken: runCts.Token,
            persona: persona ?? Nova,
            roomSessions: store,
            host: host);
        return (session, runCts);
    }

    private static AdapterProfile ClaudeProfile() => new(
        Id: "claude",
        DisplayName: "Claude",
        Description: null,
        Command: "claude-agent-acp",
        Args: null,
        AdapterPath: null,
        UsesToolNamePrefix: true,
        EnvironmentOverrides: null,
        ReadsFiles: true,
        IsolateUserSettings: true,
        SessionPerRoom: true);

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

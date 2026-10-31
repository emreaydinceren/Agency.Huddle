namespace Agency.Huddle.Tests.Acp.Sessions;

using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Sessions.Fakes;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.Waiting;

/// <summary>
/// Pins how a <see cref="StartAfresh"/> reaches the Room Session pool (Spec LR-R15, LR-R19, LR-R21,
/// LR-E5): per-Room mode hands it to that Room's session only, a Room with no session forgets what is
/// stored and is remembered as note-pending, shared mode ignores it, and the runner read loop routes
/// the Envelope to the pool. 5.4.t extends this class with the session's own behaviour.
/// </summary>
public sealed class StartAfreshTests
{
    private const string AgentId = "agent-1";

    private static readonly Persona Nova = new("nova", "You are Nova.", Model: "m1", Effort: "e1");

    /// <summary>In per-Room mode the pool hands a start afresh to that Room's session only; the other Room's session and stored entry are untouched.</summary>
    [Fact]
    public async Task StartAfreshAsync_PerRoomMode_ReachesThatRoomsSessionOnly()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
        store.Put("nova", "B", Entry());
        using CancellationTokenSource runCts = new();
        await using RoomSessionPool pool = CreatePool(sessionPerRoom: true, store, contextFill: null, runCts.Token);
        using CancelOnDispose cancelRun = new(runCts);
        pool.Enqueue(Work(1, "A"));
        pool.Enqueue(Work(2, "B"));

        await pool.StartAfreshAsync("A", ct);

        RoomSession sessionA = Assert.IsType<RoomSession>(pool.SessionFor("A"));
        RoomSession sessionB = Assert.IsType<RoomSession>(pool.SessionFor("B"));
        Assert.True(sessionA.FreshStartPending);
        Assert.False(sessionB.FreshStartPending);
        Assert.NotNull(store.Get("nova", "B"));
    }

    /// <summary>A Room with no live session yet has no session created, but its stored entry and context reading are forgotten and the Room is remembered as note-pending (correction D5 row 2).</summary>
    [Fact]
    public async Task StartAfreshAsync_RoomWithNoSession_ForgetsStoredStateAndRemembersNotePending()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
        ContextFill contextFill = new();
        store.Put("nova", "C", Entry());
        contextFill.Report(AgentId, "C", used: 500, size: 1000);
        using CancellationTokenSource runCts = new();
        await using RoomSessionPool pool = CreatePool(sessionPerRoom: true, store, contextFill, runCts.Token);
        using CancelOnDispose cancelRun = new(runCts);

        await pool.StartAfreshAsync("C", ct);

        Assert.Null(pool.SessionFor("C"));
        Assert.Null(store.Get("nova", "C"));
        Assert.Null(contextFill.Get(AgentId, "C"));

        pool.Enqueue(Work(1, "C"));
        pool.Enqueue(Work(2, "D"));

        RoomSession sessionC = Assert.IsType<RoomSession>(pool.SessionFor("C"));
        RoomSession sessionD = Assert.IsType<RoomSession>(pool.SessionFor("D"));
        Assert.True(sessionC.FreshStartNotePending);
        Assert.False(sessionD.FreshStartNotePending);
    }

    /// <summary>In shared mode a start afresh is a no-op: the shared session, the store and the context reading are untouched (LR-E5).</summary>
    [Fact]
    public async Task StartAfreshAsync_SharedMode_IsANoOp()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
        ContextFill contextFill = new();
        store.Put("nova", "A", Entry());
        contextFill.Report(AgentId, "A", used: 500, size: 1000);
        using CancellationTokenSource runCts = new();
        await using RoomSessionPool pool = CreatePool(sessionPerRoom: false, store, contextFill, runCts.Token);
        using CancelOnDispose cancelRun = new(runCts);

        await pool.StartAfreshAsync("A", ct);

        RoomSession shared = Assert.IsType<RoomSession>(pool.SessionFor("A"));
        Assert.False(shared.FreshStartPending);
        Assert.False(shared.FreshStartNotePending);
        Assert.NotNull(store.Get("nova", "A"));
        Assert.NotNull(contextFill.Get(AgentId, "A"));
    }

    /// <summary>The runner's read loop routes a <see cref="StartAfresh"/> Envelope to the pool: a Room with no session has its stored entry forgotten.</summary>
    [Fact]
    public async Task ReadLoop_StartAfresh_RoutesToThePool()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
        await using PipeRunnerServer server = new("start-afresh-tests", recordReceived: false);
        FakeAgentHostFactory factory = new() { SessionPerRoom = true };
        RoomInfo humanRoom = new("room-a", "Direct", [new MemberInfo("human", "Human", UserKind.Human), new MemberInfo(AgentId, "nova", UserKind.Agent)]);
        RoomInfo groupRoom = new("room-b", "Room B", [new MemberInfo("human", "Human", UserKind.Human), new MemberInfo(AgentId, "nova", UserKind.Agent), new MemberInfo("friend", "Friend", UserKind.Agent)]);
        await using PersonaRunner runner = new(
            TestPersonas.Nova,
            TestOptions.Create(opts => opts.PipeName = server.PipeName),
            factory,
            new FakePromptSource(),
            new RoomFollows(),
            NullLogger<PersonaRunner>.Instance,
            roomSessions: store);
        await server.HandshakeAsync(runner, [humanRoom, groupRoom], ct);
        store.Put(TestPersonas.Nova.Name, "room-b", Entry());

        await server.SendAsync(new StartAfresh("room-b"), ct);

        await Eventually.UntilAsync(() => store.Get(TestPersonas.Nova.Name, "room-b") is null, ct, timeout: TestBudgets.Standard);
        Assert.Null(store.Get(TestPersonas.Nova.Name, "room-b"));
    }

    private static RoomSessionEntry Entry() => new("session-1", "claude", "m1", "e1", "msg-1", DateTimeOffset.UnixEpoch);

    private static QueuedWork Work(long sequence, string roomId) =>
        new(sequence, new WorkItem(roomId, roomId, "Human", "hi", [], TriggerMessageId: "trig-" + roomId));

    private static RoomSessionPool CreatePool(bool sessionPerRoom, RoomSessionStore store, ContextFill? contextFill, CancellationToken runToken)
    {
        FakePersonaHost host = new(new FakeAgentSession(), Profile(sessionPerRoom));
        return new RoomSessionPool(
            host,
            new FakeRoomSessionOwner(),
            Nova,
            new FakePromptSource(),
            new AcpOptions(),
            TimeProvider.System,
            fileChanges: null,
            declaredWatches: [],
            NullLogger.Instance,
            runToken,
            roomSessions: store,
            agentId: AgentId,
            contextFill: contextFill);
    }

    private static AdapterProfile Profile(bool sessionPerRoom) => new(
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
        SessionPerRoom: sessionPerRoom);

    /// <summary>Cancels the pool run token when disposed. Declared after the pool, so it runs before the pool is disposed: the session consumers end only when the run token is cancelled.</summary>
    private sealed class CancelOnDispose(CancellationTokenSource source) : IDisposable
    {
        /// <inheritdoc/>
        public void Dispose() => source.Cancel();
    }
}

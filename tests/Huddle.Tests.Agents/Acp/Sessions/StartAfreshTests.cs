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
        TaskCompletionSource openGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using RoomSessionPool pool = CreatePool(sessionPerRoom: true, store, contextFill: null, runCts.Token, openGate);
        using CancelOnDispose cancelRun = new(runCts);
        pool.Enqueue(Work(1, "A"));
        pool.Enqueue(Work(2, "B"));
        await Eventually.UntilAsync(() => pool.SessionFor("A")?.State == RoomSessionState.Opening, ct, timeout: TestBudgets.Standard);

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

    /// <summary>An idle session asked to start afresh disposes its agent session, forgets the stored entry and the context reading, and the next item opens a NEW session (no resume) whose first Turn carries Catch-up (LR-R19, LR-R20).</summary>
    [Fact]
    public async Task RequestFreshStart_IdleSession_TearsDownForgetsAndOpensNewWithCatchUp()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using FreshRig rig = FreshRig.Create();
        await rig.RunFirstTurnAsync(ct);
        rig.ContextFill.Report(AgentId, "room-1", used: 500, size: 1000);

        await rig.Room.RequestFreshStartAsync(ct);

        await Eventually.UntilAsync(() => rig.First.Disposed, ct, timeout: TestBudgets.Standard);
        Assert.Null(rig.Store.Get("nova", "room-1"));
        Assert.Null(rig.ContextFill.Get(AgentId, "room-1"));

        rig.Room.Enqueue(Item(2));
        await Eventually.UntilAsync(() => rig.PostCount == 2, ct, timeout: TestBudgets.Standard);

        Assert.Equal(2, rig.Host.Sessions.Count);
        Assert.Empty(rig.Host.ResumeCalls);
        Assert.Equal(2, rig.Owner.TranscriptReadCalls.Count);
        string prompt = Assert.Single(rig.Host.Sessions[1].Prompts);
        Assert.Contains("This is a new session for this Room", prompt, StringComparison.Ordinal);
    }

    /// <summary>A busy session asked to start afresh finishes its live Turn normally; the fresh start happens before the next item is served (LR-R18).</summary>
    [Fact]
    public async Task RequestFreshStart_BusySession_CompletesTurnThenStartsAfreshBeforeNextItem()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using FreshRig rig = FreshRig.Create();
        rig.First.EnqueueGatedReply(gate.Task, "one");
        rig.Room.Enqueue(Item(1));
        await Eventually.UntilAsync(() => rig.First.Prompts.Count == 1, ct, timeout: TestBudgets.Standard);

        await rig.Room.RequestFreshStartAsync(ct);
        rig.Room.Enqueue(Item(2));

        Assert.True(rig.Room.FreshStartPending);
        Assert.Equal(RoomSessionState.Busy, rig.Room.State);
        Assert.False(rig.First.Disposed);
        gate.SetResult();
        await Eventually.UntilAsync(() => rig.PostCount == 2, ct, timeout: TestBudgets.Standard);

        Assert.Equal(2, rig.Host.Sessions.Count);
        Assert.True(rig.First.Disposed);
        Assert.Single(rig.First.Prompts);
        Assert.Single(rig.Host.Sessions[1].Prompts);
        Assert.Empty(rig.Host.ResumeCalls);
    }

    /// <summary>While a user-input lease is held on the live Turn nothing is torn down; after the lease is released and the Turn ends, the fresh start happens before the next item (LR-R18).</summary>
    [Fact]
    public async Task RequestFreshStart_LeaseHeld_NoTeardownUntilReleasedThenBeforeNextItem()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using FreshRig rig = FreshRig.Create();
        rig.First.EnqueueGatedReply(gate.Task, "one");
        rig.Room.Enqueue(Item(1));
        await Eventually.UntilAsync(() => rig.First.Prompts.Count == 1, ct, timeout: TestBudgets.Standard);
        UserInputLease? lease = rig.Room.TryBeginUserInput();
        Assert.NotNull(lease);

        await rig.Room.RequestFreshStartAsync(ct);

        Assert.True(rig.Room.FreshStartPending);
        Assert.Equal(RoomSessionState.Busy, rig.Room.State);
        Assert.False(rig.First.Disposed);
        Assert.Single(rig.Host.Sessions);
        lease.Dispose();
        rig.Room.Enqueue(Item(2));
        gate.SetResult();
        await Eventually.UntilAsync(() => rig.PostCount == 2, ct, timeout: TestBudgets.Standard);

        Assert.Equal(2, rig.Host.Sessions.Count);
        Assert.True(rig.First.Disposed);
        Assert.Single(rig.First.Prompts);
    }

    /// <summary>A fresh start is not a failure: it reports no Turn failure and writes no Message of its own (LR-R20).</summary>
    [Fact]
    public async Task RequestFreshStart_IsNotAFailureAndWritesNoMessage()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using FreshRig rig = FreshRig.Create();
        await rig.RunFirstTurnAsync(ct);

        await rig.Room.RequestFreshStartAsync(ct);
        await Eventually.UntilAsync(() => rig.First.Disposed, ct, timeout: TestBudgets.Standard);
        rig.Room.Enqueue(Item(2));
        await Eventually.UntilAsync(() => rig.PostCount == 2, ct, timeout: TestBudgets.Standard);

        Assert.DoesNotContain("ReportTurnFailure", rig.Owner.ReportCalls);
        Assert.Null(rig.Owner.LastFailureReason);
        Assert.Equal(2, rig.PostCount);
    }

    /// <summary>Two requests before the next item cause one fresh start: one dispose and one new open (LR-R15).</summary>
    [Fact]
    public async Task RequestFreshStart_TwiceBeforeNextItem_StartsAfreshOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using FreshRig rig = FreshRig.Create();
        await rig.RunFirstTurnAsync(ct);

        await rig.Room.RequestFreshStartAsync(ct);
        await rig.Room.RequestFreshStartAsync(ct);
        rig.Room.Enqueue(Item(2));
        rig.Room.Enqueue(Item(3));
        await Eventually.UntilAsync(() => rig.PostCount == 3, ct, timeout: TestBudgets.Standard);

        Assert.Equal(2, rig.Host.Sessions.Count);
        Assert.True(rig.First.Disposed);
        Assert.False(rig.Host.Sessions[1].Disposed);
        Assert.Equal(2, rig.Host.Sessions[1].Prompts.Count);
    }

    /// <summary>Disposing the session while an idle fresh-start teardown is still pending completes, and the old agent session ends Disposed.</summary>
    [Fact]
    public async Task DisposeAsync_WhileFreshStartTeardownPending_CompletesAndDisposesOldSession()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FreshRig rig = FreshRig.Create();
        await rig.RunFirstTurnAsync(ct);

        await rig.Room.RequestFreshStartAsync(ct);
        await rig.DisposeAsync();

        Assert.True(rig.First.Disposed);
    }

    /// <summary>Only the fresh session's first Turn owes the note: it is owed right after the start afresh is applied and stays owed until the Turn that writes it consumes it (5.6).</summary>
    [Fact]
    public async Task RequestFreshStart_TwiceBeforeNextItem_NotePendingUntilFirstTurnConsumesIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using FreshRig rig = FreshRig.Create();
        await rig.RunFirstTurnAsync(ct);

        await rig.Room.RequestFreshStartAsync(ct);
        await rig.Room.RequestFreshStartAsync(ct);
        rig.Room.Enqueue(Item(2));
        await Eventually.UntilAsync(() => rig.PostCount == 2, ct, timeout: TestBudgets.Standard);

        Assert.True(rig.Room.FreshStartNotePending);
        Assert.False(rig.Room.FreshStartPending);
    }

    /// <summary>A Stop during the fresh session's first Turn is an ordinary Stop: the Turn ends without a reply and the new session stays open (LR-E8).</summary>
    [Fact]
    public async Task Stop_DuringFreshSessionsFirstTurn_IsOrdinaryAndSessionStays()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using FreshRig rig = FreshRig.Create(static second => second.EnqueueReplyThenHang("chunk"));
        await rig.RunFirstTurnAsync(ct);
        await rig.Room.RequestFreshStartAsync(ct);
        rig.Room.Enqueue(Item(2));
        await Eventually.UntilAsync(() => rig.Host.Sessions.Count == 2 && rig.Host.Sessions[1].Prompts.Count == 1, ct, timeout: TestBudgets.Standard);

        await rig.Room.StopAsync("room-1", mark: 0, ct);
        await Eventually.UntilAsync(() => rig.Owner.Written.OfType<MessageDelta>().Count(static delta => delta.IsFinal) == 2, ct, timeout: TestBudgets.Standard);

        Assert.Equal(2, rig.Host.Sessions.Count);
        Assert.False(rig.Host.Sessions[1].Disposed);
        Assert.Equal(1, rig.PostCount);
        Assert.DoesNotContain("ReportTurnFailure", rig.Owner.ReportCalls);
    }

    private static QueuedWork Item(long sequence) =>
        new(sequence, new WorkItem("room-1", "Room 1", "Human", "message " + sequence, [], TriggerMessageId: "trig-" + sequence));

    private static RoomSessionEntry Entry() => new("session-1", "claude", "m1", "e1", "msg-1", DateTimeOffset.UnixEpoch);

    private static QueuedWork Work(long sequence, string roomId) =>
        new(sequence, new WorkItem(roomId, roomId, "Human", "hi", [], TriggerMessageId: "trig-" + roomId));

    private static RoomSessionPool CreatePool(bool sessionPerRoom, RoomSessionStore store, ContextFill? contextFill, CancellationToken runToken, TaskCompletionSource? openGate = null)
    {
        FakePersonaHost host = new(new FakeAgentSession(), Profile(sessionPerRoom)) { OpenGate = openGate };
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

    /// <summary>One per-Room <see cref="RoomSession"/> over a resume-capable fake host and a real store, torn down in the only order that ends: the harness cancels its run token first.</summary>
    private sealed class FreshRig : IAsyncDisposable
    {
        private readonly TempDataDir dataDir;
        private readonly RoomSessionHarness harness;

        private FreshRig(TempDataDir dataDir, RoomSessionHarness harness, FakePersonaHost host, FakeRoomSessionOwner owner, RoomSessionStore store, ContextFill contextFill, FakeAgentSession first)
        {
            this.dataDir = dataDir;
            this.harness = harness;
            this.Host = host;
            this.Owner = owner;
            this.Store = store;
            this.ContextFill = contextFill;
            this.First = first;
        }

        public RoomSession Room => this.harness.Room;

        public FakePersonaHost Host { get; }

        public FakeRoomSessionOwner Owner { get; }

        public RoomSessionStore Store { get; }

        public ContextFill ContextFill { get; }

        public FakeAgentSession First { get; }

        public int PostCount => this.Owner.Written.OfType<PostMessage>().Count();

        /// <summary>Builds the rig; <paramref name="scriptLater"/> scripts every session after the first as it opens.</summary>
        /// <param name="scriptLater">Scripts a later session, or <see langword="null"/> for none.</param>
        /// <returns>The rig, to be disposed with <c>await using</c>.</returns>
        public static FreshRig Create(Action<FakeAgentSession>? scriptLater = null)
        {
            TempDataDir dataDir = new();
            RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
            ContextFill contextFill = new();
            FakeAgentSession first = new();
            FakePersonaHost host = new(first, Profile(sessionPerRoom: true))
            {
                CanResume = true,
                ResumeHandler = _ => first,
                OnOpen = session =>
                {
                    if (!ReferenceEquals(session, first))
                    {
                        scriptLater?.Invoke(session);
                    }
                },
            };
            FakeRoomSessionOwner owner = new()
            {
                ReadTranscriptHandler = static (roomId, _, _, _, _) => Task.FromResult<TranscriptTail?>(
                    new TranscriptTail("req", roomId, [new ChatMessage("m0", DateTimeOffset.UnixEpoch, "hu", "Human", "earlier")], Omitted: 0)),
            };
            RoomSessionHarness harness = RoomSessionHarness.Create(
                owner,
                host.OpenAsync,
                new RoomSessionHarnessOptions
                {
                    Persona = Nova,
                    AgentId = AgentId,
                    ContextFill = contextFill,
                    RoomSessions = store,
                    Host = host,
                });
            return new FreshRig(dataDir, harness, host, owner, store, contextFill, first);
        }

        /// <summary>Serves item 1 to completion and waits until the session is idle with its entry stored.</summary>
        /// <param name="ct">The test's cancellation token.</param>
        /// <returns>A task that completes when the session is idle.</returns>
        public async Task RunFirstTurnAsync(CancellationToken ct)
        {
            this.First.EnqueueReply("one");
            this.Room.Enqueue(Item(1));
            await Eventually.UntilAsync(() => this.PostCount == 1 && this.Store.Get("nova", "room-1") is not null && this.Room.State == RoomSessionState.Idle, ct, timeout: TestBudgets.Standard);
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            await this.harness.DisposeAsync();
            this.dataDir.Dispose();
        }
    }

    /// <summary>Cancels the pool run token when disposed. Declared after the pool, so it runs before the pool is disposed: the session consumers end only when the run token is cancelled.</summary>
    private sealed class CancelOnDispose(CancellationTokenSource source) : IDisposable
    {
        /// <inheritdoc/>
        public void Dispose() => source.Cancel();
    }
}

namespace Agency.Huddle.Tests.Acp.Sessions;

using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Waiting;
using Agency.Huddle.Tests.Factories;

/// <summary>
/// Pins <see cref="Agency.Huddle.App.Acp.Sessions.RoomSessionPool"/> (RS §6.2), findings P-4, P-5 and
/// P-20, over a real <see cref="PersonaRunner"/> talking to a hand-scripted pipe server
/// (<see cref="PipeRunnerServer"/>), which gives full control over the Welcome's Room list without needing
/// a real <c>ChatService</c> — every registered agent there gets a direct Human Room automatically,
/// which would make <see cref="Start_NoHumanRoom_NothingOpens"/> unconstructible.
/// </summary>
public sealed class RoomSessionPoolTests
{
    /// <summary>The headline test (RS §10): two Rooms, two Room Sessions, each session's prompts hold only its own Room's Messages.</summary>
    [Fact]
    public async Task TwoRooms_TwoSessions_EachHoldsOnlyItsOwnRoomsMessages()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var ct = cts.Token;

        await using var server = new PipeRunnerServer("room-session-pool-tests", recordReceived: false);
        var factory = new FakeAgentHostFactory { SessionPerRoom = true };
        var persona = TestPersonas.Nova;
        var roomA = HumanRoom("room-a");
        var roomB = GroupRoom("room-b", "Room B");

        await using var runner = CreateRunner(server, persona, factory);
        await server.HandshakeAsync(runner, [roomA, roomB], ct);

        Assert.Single(factory.Host!.Sessions);

        for (var i = 0; i < 6; i++)
        {
            var room = i % 2 == 0 ? roomA : roomB;
            var label = i % 2 == 0 ? "A" : "B";
            await server.SendAsync(Posted(room, $"to {label} #{i}"), ct);
            await server.ReceiveUntilAsync<PostMessage>(null, ct);
        }

        Assert.Equal(2, factory.Host.Sessions.Count);
        var sessionA = factory.Host.Sessions[0];
        var sessionB = factory.Host.Sessions[1];

        Assert.NotEmpty(sessionA.Prompts);
        Assert.NotEmpty(sessionB.Prompts);
        Assert.All(sessionA.Prompts, p => Assert.False(p.Contains(roomB.Id, StringComparison.Ordinal) || p.Contains("to B", StringComparison.Ordinal)));
        Assert.All(sessionB.Prompts, p => Assert.False(p.Contains(roomA.Id, StringComparison.Ordinal) || p.Contains("to A", StringComparison.Ordinal)));
    }

    /// <summary>Only the Room with the Human opens at start; a group Room stays Closed until Mentioned.</summary>
    [Fact]
    public async Task Start_OnlyTheRoomWithTheHumanOpens()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var server = new PipeRunnerServer("room-session-pool-tests", recordReceived: false);
        var factory = new FakeAgentHostFactory { SessionPerRoom = true };
        var persona = TestPersonas.Nova;
        var roomA = HumanRoom("room-a");
        var roomB = GroupRoom("room-b", "Room B");

        await using var runner = CreateRunner(server, persona, factory);
        await server.HandshakeAsync(runner, [roomB, roomA], ct);

        Assert.Single(factory.Host!.Sessions);
    }

    /// <summary>A start-up failure opening the Human Room's session fails <see cref="PersonaRunner.StartAsync"/>, as today.</summary>
    [Fact]
    public async Task Start_HumanRoomOpenFails_ThrowsAsToday()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var server = new PipeRunnerServer("room-session-pool-tests", recordReceived: false);
        var factory = new FakeAgentHostFactory { SessionPerRoom = true };
        factory.OnHostStarted = host => host.FailNextOpenWith(new InvalidOperationException("boom"));
        var persona = TestPersonas.Nova;
        var roomA = HumanRoom("room-a");

        await using var runner = CreateRunner(server, persona, factory);

        await Assert.ThrowsAsync<InvalidOperationException>(() => server.HandshakeAsync(runner, [roomA], ct));
    }

    /// <summary>With no Room holding exactly the Human and this Agent, nothing opens at start.</summary>
    [Fact]
    public async Task Start_NoHumanRoom_NothingOpens()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var server = new PipeRunnerServer("room-session-pool-tests", recordReceived: false);
        var factory = new FakeAgentHostFactory { SessionPerRoom = true };
        var persona = TestPersonas.Nova;
        var roomB = GroupRoom("room-b", "Room B");

        await using var runner = CreateRunner(server, persona, factory);
        await server.HandshakeAsync(runner, [roomB], ct);

        Assert.Empty(factory.Host!.Sessions);
    }

    /// <summary>
    /// D24 correction 23: the Model-not-in-catalog warning is not tied to <c>OpenAtStartAsync</c>'s
    /// own open - a per-Room Persona with no Human Room, whose FIRST open ever is the lazy one its
    /// first Mention triggers, still gets it, reported once per runner (D23 correction 17).
    /// </summary>
    [Fact]
    public async Task NoHumanRoom_FirstMention_StillWarnsOnModelNotInCatalog()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var server = new PipeRunnerServer("room-session-pool-tests", recordReceived: false);
        var factory = new FakeAgentHostFactory { SessionPerRoom = true };
        factory.Session.Models = [new AgentModelOption("some-other-model", "Other", null)];
        var persona = new Persona("nova", "You are Nova.", Model: "missing-model");
        var roomB = GroupRoom("room-b", "Room B");

        StatusRecorder statuses = new();
        await using var runner = CreateRunner(server, persona, factory);
        runner.StatusChanged += statuses.Record;
        await server.HandshakeAsync(runner, [roomB], ct);

        Assert.Empty(factory.Host!.Sessions);

        await server.SendAsync(Posted(roomB, "hi"), ct);
        await server.ReceiveUntilAsync<PostMessage>(null, ct);

        await statuses.AssertContainsEventuallyAsync(
            status => status.State == PersonaState.Degraded && status.Reason is not null
                && status.Reason.Contains("missing-model", StringComparison.Ordinal),
            ct);
    }

    /// <summary>In shared mode (finding P-9), every Room routes through the one shared session.</summary>
    [Fact]
    public async Task SharedMode_OneSessionForAllRooms()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var ct = cts.Token;

        await using var server = new PipeRunnerServer("room-session-pool-tests", recordReceived: false);
        var factory = new FakeAgentHostFactory { SessionPerRoom = false };
        var persona = TestPersonas.Nova;
        var roomA = HumanRoom("room-a");
        var roomB = GroupRoom("room-b", "Room B");

        await using var runner = CreateRunner(server, persona, factory);
        await server.HandshakeAsync(runner, [roomA, roomB], ct);

        await server.SendAsync(Posted(roomA, "to A"), ct);
        await server.ReceiveUntilAsync<PostMessage>(null, ct);
        await server.SendAsync(Posted(roomB, "to B"), ct);
        await server.ReceiveUntilAsync<PostMessage>(null, ct);

        Assert.Single(factory.Host!.Sessions);
    }

    /// <summary>The idle sweep closes an empty-queue Idle Room Session whose <c>LastActivity</c> is older than the configured threshold.</summary>
    [Fact]
    public async Task SweepIdle_ClosesIdleOlderThanThreshold()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var ct = cts.Token;

        var time = new ManualTimeProvider();
        var acp = new AcpOptions { SessionIdleMinutes = 5 };
        await using var server = new PipeRunnerServer("room-session-pool-tests", recordReceived: false);
        var factory = new FakeAgentHostFactory { SessionPerRoom = true };
        var persona = TestPersonas.Nova;
        var roomA = HumanRoom("room-a");
        var roomB = GroupRoom("room-b", "Room B");

        await using var runner = CreateRunner(server, persona, factory, acp, time);
        await server.HandshakeAsync(runner, [roomA, roomB], ct);

        await server.SendAsync(Posted(roomB, "hi"), ct);

        // The reply's own PostMessage arrives well before the Turn's finally block finishes writing
        // the terminating (IsFinal) MessageDelta, committing File Changes (off here) and storing a
        // Room Session entry (off here too) - only after ALL of that does the consumer's own finally
        // stamp Room B's Idle state and LastActivity. Waiting for PostMessage alone raced that
        // stamp against the Advance below; waiting for the terminator instead closes almost all of
        // that gap, and SweepUntilAsync's retry closes what (theoretically) remains.
        await server.ReceiveUntilAsync<MessageDelta>(delta => delta.RoomId == roomB.Id && delta.IsFinal, ct);

        await SweepUntilAsync(runner, time, TimeSpan.FromMinutes(6), () => factory.Host!.Sessions[1].Disposed, ct);

        Assert.True(factory.Host!.Sessions[0].Disposed);
        Assert.True(factory.Host.Sessions[1].Disposed);
    }

    /// <summary><c>SessionIdleMinutes</c> zero or less never closes an idle Room Session.</summary>
    [Fact]
    public async Task SweepIdle_ZeroMinutes_NeverCloses()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var ct = cts.Token;

        var time = new ManualTimeProvider();
        var acp = new AcpOptions { SessionIdleMinutes = 0 };
        await using var server = new PipeRunnerServer("room-session-pool-tests", recordReceived: false);
        var factory = new FakeAgentHostFactory { SessionPerRoom = true };
        var persona = TestPersonas.Nova;
        var roomA = HumanRoom("room-a");

        await using var runner = CreateRunner(server, persona, factory, acp, time);
        await server.HandshakeAsync(runner, [roomA], ct);

        time.Advance(TimeSpan.FromDays(1));
        await runner.SweepIdleSessionsAsync();

        Assert.False(factory.Host!.Sessions[0].Disposed);
    }

    /// <summary>Finding P-20: the sweep never runs in shared mode, since closing the one shared session would lose the whole conversation.</summary>
    [Fact]
    public async Task SharedMode_SweepNeverCloses()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var ct = cts.Token;

        var time = new ManualTimeProvider();
        var acp = new AcpOptions { SessionIdleMinutes = 1 };
        await using var server = new PipeRunnerServer("room-session-pool-tests", recordReceived: false);
        var factory = new FakeAgentHostFactory { SessionPerRoom = false };
        var persona = TestPersonas.Nova;
        var roomA = HumanRoom("room-a");

        await using var runner = CreateRunner(server, persona, factory, acp, time);
        await server.HandshakeAsync(runner, [roomA], ct);

        time.Advance(TimeSpan.FromDays(1));
        await runner.SweepIdleSessionsAsync();

        Assert.False(factory.Host!.Sessions[0].Disposed);
    }

    /// <summary>Opening a fourth Room beyond <c>MaxLiveSessions</c> closes the least recently used Idle one.</summary>
    [Fact]
    public async Task LiveCap_FourthRoom_ClosesLeastRecentlyUsedIdle()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var ct = cts.Token;

        var acp = new AcpOptions { MaxLiveSessions = 3 };
        await using var server = new PipeRunnerServer("room-session-pool-tests", recordReceived: false);
        var factory = new FakeAgentHostFactory { SessionPerRoom = true };
        var persona = TestPersonas.Nova;
        var roomA = HumanRoom("room-a");
        var roomB = GroupRoom("room-b", "Room B");
        var roomC = GroupRoom("room-c", "Room C");
        var roomD = GroupRoom("room-d", "Room D");

        await using var runner = CreateRunner(server, persona, factory, acp);
        await server.HandshakeAsync(runner, [roomA, roomB, roomC, roomD], ct);

        foreach (var room in new[] { roomB, roomC })
        {
            await server.SendAsync(Posted(room, "hi"), ct);
            await server.ReceiveUntilAsync<PostMessage>(null, ct);
        }

        await server.SendAsync(Posted(roomD, "hi"), ct);
        await server.ReceiveUntilAsync<PostMessage>(null, ct);

        Assert.Equal(4, factory.Host!.Sessions.Count);
        Assert.True(factory.Host.Sessions[0].Disposed);
        Assert.False(factory.Host.Sessions[1].Disposed);
        Assert.False(factory.Host.Sessions[2].Disposed);
        Assert.False(factory.Host.Sessions[3].Disposed);
    }

    /// <summary>Eviction prefers an Idle Room Session with an empty queue over an older one whose queue still holds work.</summary>
    [Fact]
    public async Task LiveCap_PrefersIdleWithEmptyQueue()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var ct = cts.Token;

        var acp = new AcpOptions { MaxConcurrentTurns = 1, MaxLiveSessions = 3 };
        await using var server = new PipeRunnerServer("room-session-pool-tests", recordReceived: false);
        var factory = new FakeAgentHostFactory { SessionPerRoom = true };
        var persona = TestPersonas.Nova;
        var roomA = HumanRoom("room-a");
        var roomC = GroupRoom("room-c", "Room C");
        var roomG = GroupRoom("room-g", "Room G");
        var roomD = GroupRoom("room-d", "Room D");

        await using var runner = CreateRunner(server, persona, factory, acp);
        await server.HandshakeAsync(runner, [roomA, roomC, roomG, roomD], ct);

        // Room C opens and completes normally: Idle, empty queue, newer than Room A (opened at start).
        await server.SendAsync(Posted(roomC, "hi"), ct);
        await server.ReceiveUntilAsync<PostMessage>(null, ct);

        // Room G opens and holds the one concurrency slot forever, so nothing else can ever be
        // admitted while it stays gated - this makes the next steps fully deterministic rather than
        // racy: an un-admitted ticket simply never runs, it does not "usually" not run.
        var releaseG = new TaskCompletionSource();
        factory.Host!.OnOpen = session => session.EnqueueGatedReply(releaseG.Task, "g reply");
        await server.SendAsync(Posted(roomG, "hi"), ct);
        await Eventually.UntilAsync(() => factory.Host.Sessions.Count == 3, ct, timeout: TestBudgets.Standard);
        factory.Host.OnOpen = null;

        // Room D's ticket is offered next (lower sequence than Room A's second message below), so
        // when Room G eventually releases the sole slot, D - not A - is admitted.
        await server.SendAsync(Posted(roomD, "hi"), ct);

        // Room A's second message: its ticket cannot be admitted while Room G holds the sole slot,
        // so Room A (already Idle from its start-open) becomes Idle with a non-empty queue. The read
        // loop processes envelopes strictly in arrival order, one at a time, so by the time this send
        // returns, Room D's ticket (sent just above) is already the lower, earlier-offered one - a
        // short settle delay is enough for both sends to have reached the read loop before Room G's
        // gate is released below.
        await server.SendAsync(Posted(roomA, "second"), ct);
        await Task.Delay(200, ct);

        releaseG.SetResult();

        // Room D opens (the 4th live Room Session while the cap is 3): eviction must prefer Room C
        // (Idle, empty queue) over Room A (Idle, but its queue still holds the second message), even
        // though Room A is the older of the two.
        await Eventually.UntilAsync(() => factory.Host.Sessions.Count == 4 && factory.Host.Sessions[1].Disposed, ct, TimeSpan.FromSeconds(10));

        Assert.True(factory.Host.Sessions[1].Disposed); // Room C: evicted.
        Assert.False(factory.Host.Sessions[0].Disposed); // Room A: protected, still has queued work.
        Assert.False(factory.Host.Sessions[2].Disposed); // Room G: Idle again after releasing, but younger.
    }

    /// <summary>
    /// Finding P-5: three Rooms are already open and busy (their concurrency slots held) when a
    /// fourth Room's open is admitted past the configured <c>MaxLiveSessions</c> - a cap below
    /// <c>MaxConcurrentTurns</c> is raised to it (RS §6.2), so the four together never actually
    /// exceed the effective cap, and the fourth opens cleanly rather than waiting on - or being
    /// blocked by - the three still-busy ones.
    /// </summary>
    [Fact]
    public async Task LiveCap_AllOpenHaveQueuedWork_StillOpensWithoutDeadlock()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(20));
        var ct = cts.Token;

        var acp = new AcpOptions { MaxConcurrentTurns = 4, MaxLiveSessions = 3 };
        await using var server = new PipeRunnerServer("room-session-pool-tests", recordReceived: false);
        var factory = new FakeAgentHostFactory { SessionPerRoom = true };
        var persona = TestPersonas.Nova;
        var roomB = GroupRoom("room-b", "Room B");
        var roomC = GroupRoom("room-c", "Room C");
        var roomE = GroupRoom("room-e", "Room E");
        var roomF = GroupRoom("room-f", "Room F");

        await using var runner = CreateRunner(server, persona, factory, acp);
        await server.HandshakeAsync(runner, [roomB, roomC, roomE, roomF], ct);

        var neverReleased = new TaskCompletionSource();
        factory.Host!.OnOpen = session => session.EnqueueGatedReply(neverReleased.Task, "busy");

        foreach (var room in new[] { roomB, roomC, roomE })
        {
            await server.SendAsync(Posted(room, "hi"), ct);
        }

        await Eventually.UntilAsync(() => factory.Host.Sessions.Count == 3, ct, timeout: TestBudgets.Standard);
        factory.Host.OnOpen = null;

        await server.SendAsync(Posted(roomF, "hi"), ct);
        await server.ReceiveUntilAsync<PostMessage>(null, ct);

        Assert.Equal(4, factory.Host.Sessions.Count);
        Assert.False(factory.Host.Sessions[0].Disposed);
        Assert.False(factory.Host.Sessions[1].Disposed);
        Assert.False(factory.Host.Sessions[2].Disposed);

        // Releases Room B, C and E's Turns, which are gated forever until now, so runner disposal at
        // the end of this test does not hang waiting for their consumers to end.
        neverReleased.SetResult();
    }

    /// <summary>Finding P-4: at <c>MaxConcurrentTurns</c> 1, Turns across two Rooms run in the order they were sent, never interleaved.</summary>
    [Fact]
    public async Task ArrivalOrder_Max1_AcrossRooms()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var ct = cts.Token;

        await using var server = new PipeRunnerServer("room-session-pool-tests", recordReceived: false);
        var factory = new FakeAgentHostFactory { SessionPerRoom = true };
        var persona = TestPersonas.Nova;
        var roomA = HumanRoom("room-a");
        var roomB = GroupRoom("room-b", "Room B");

        // Cross-Room arrival order is the subject, so A1 and A2 must stay two Turns: folding is off.
        await using var runner = CreateRunner(server, persona, factory, new AcpOptions { FoldOneToOne = false });
        await server.HandshakeAsync(runner, [roomA, roomB], ct);

        var log = new List<string>();
        var gate = new Lock();
        void Record(string text)
        {
            lock (gate)
            {
                log.Add(text);
            }
        }

        factory.Host!.Sessions[0].OnPrompt = Record;
        factory.Host.OnOpen = session => session.OnPrompt = Record;

        await server.SendAsync(Posted(roomA, "A1"), ct);
        await server.SendAsync(Posted(roomA, "A2"), ct);
        await server.SendAsync(Posted(roomB, "B1"), ct);

        for (var i = 0; i < 3; i++)
        {
            await server.ReceiveUntilAsync<PostMessage>(null, ct);
        }

        List<string> observed;
        lock (gate)
        {
            observed = [.. log];
        }

        Assert.Equal(3, observed.Count);
        Assert.Contains("A1", observed[0], StringComparison.Ordinal);
        Assert.Contains("A2", observed[1], StringComparison.Ordinal);
        Assert.Contains("B1", observed[2], StringComparison.Ordinal);
    }

    /// <summary>At <c>MaxConcurrentTurns</c> 2, two Room Sessions' Turns are genuinely in flight at once.</summary>
    [Fact]
    public async Task Overlap_Max2_TwoRoomsRunConcurrently()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var ct = cts.Token;

        var acp = new AcpOptions { MaxConcurrentTurns = 2 };
        await using var server = new PipeRunnerServer("room-session-pool-tests", recordReceived: false);
        var factory = new FakeAgentHostFactory { SessionPerRoom = true };
        var persona = TestPersonas.Nova;
        var roomA = HumanRoom("room-a");
        var roomB = GroupRoom("room-b", "Room B");

        await using var runner = CreateRunner(server, persona, factory, acp);
        await server.HandshakeAsync(runner, [roomA, roomB], ct);

        var releaseA = new TaskCompletionSource();
        var releaseB = new TaskCompletionSource();
        var aStarted = new TaskCompletionSource();
        var bStarted = new TaskCompletionSource();

        factory.Host!.Sessions[0].OnPrompt = _ => aStarted.TrySetResult();
        factory.Host.Sessions[0].EnqueueGatedReply(releaseA.Task, "a reply");
        factory.Host.OnOpen = session =>
        {
            session.OnPrompt = _ => bStarted.TrySetResult();
            session.EnqueueGatedReply(releaseB.Task, "b reply");
        };

        await server.SendAsync(Posted(roomA, "hi a"), ct);
        await server.SendAsync(Posted(roomB, "hi b"), ct);

        await aStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        await bStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);

        releaseA.SetResult();
        releaseB.SetResult();

        PostMessage firstPost = await server.ReceiveUntilAsync<PostMessage>(null, ct);
        PostMessage secondPost = await server.ReceiveUntilAsync<PostMessage>(null, ct);

        Assert.NotEqual(firstPost.RoomId, secondPost.RoomId);
    }

    /// <summary>A <c>MaxLiveSessions</c> below <c>MaxConcurrentTurns</c> is raised to it, so both Rooms it needs stay open.</summary>
    [Fact]
    public async Task CapBelowConcurrency_RaisedWithWarning()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var ct = cts.Token;

        var acp = new AcpOptions { MaxLiveSessions = 1, MaxConcurrentTurns = 2 };
        await using var server = new PipeRunnerServer("room-session-pool-tests", recordReceived: false);
        var factory = new FakeAgentHostFactory { SessionPerRoom = true };
        var persona = TestPersonas.Nova;
        var roomA = HumanRoom("room-a");
        var roomB = GroupRoom("room-b", "Room B");

        await using var runner = CreateRunner(server, persona, factory, acp);
        await server.HandshakeAsync(runner, [roomA, roomB], ct);

        await server.SendAsync(Posted(roomB, "hi"), ct);
        await server.ReceiveUntilAsync<PostMessage>(null, ct);

        Assert.Equal(2, factory.Host!.Sessions.Count);
        Assert.False(factory.Host.Sessions[0].Disposed);
        Assert.False(factory.Host.Sessions[1].Disposed);
    }

    /// <summary>Finding P-5 invariant 3: three Rooms opening at once, with <c>MaxLiveSessions</c> 2, never has more than two sessions open simultaneously.</summary>
    [Fact(Timeout = 10000)]
    public async Task Max2_ConcurrentOpens_NeverExceedCap()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(9));
        var ct = cts.Token;

        var acp = new AcpOptions { MaxConcurrentTurns = 2, MaxLiveSessions = 2 };
        await using var server = new PipeRunnerServer("room-session-pool-tests", recordReceived: false);
        var factory = new FakeAgentHostFactory { SessionPerRoom = true };
        factory.OnHostStarted = host => host.OpenDelay = TimeSpan.FromMilliseconds(150);
        var persona = TestPersonas.Nova;
        var roomB = GroupRoom("room-b", "Room B");
        var roomC = GroupRoom("room-c", "Room C");
        var roomD = GroupRoom("room-d", "Room D");

        await using var runner = CreateRunner(server, persona, factory, acp);
        await server.HandshakeAsync(runner, [roomB, roomC, roomD], ct);

        await server.SendAsync(Posted(roomB, "hi"), ct);
        await server.SendAsync(Posted(roomC, "hi"), ct);
        await server.SendAsync(Posted(roomD, "hi"), ct);

        for (var i = 0; i < 3; i++)
        {
            await server.ReceiveUntilAsync<PostMessage>(null, ct);
        }

        Assert.True(factory.Host!.LiveSessionHighWaterMark <= 2);
    }

    /// <summary>
    /// The deterministic form of the race <see cref="Max2_ConcurrentOpens_NeverExceedCap"/> hit about
    /// once in twenty loaded runs: a session opened lazily INSIDE its first Turn must read Busy for
    /// that whole Turn. It used to publish Idle when the open finished, so a third Room opening at the
    /// live cap picked it as the eviction victim - it had the oldest <c>LastActivity</c> - and closed
    /// it mid-Turn, losing that Turn's reply.
    /// </summary>
    [Fact]
    public async Task LazyOpenedSession_MidFirstTurn_IsNeverTheEvictionVictim()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        var acp = new AcpOptions { MaxConcurrentTurns = 2, MaxLiveSessions = 2 };
        await using var server = new PipeRunnerServer("room-session-pool-tests", recordReceived: false);
        var factory = new FakeAgentHostFactory { SessionPerRoom = true };
        var releaseC = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // No Room holds only the Human, so nothing opens at start and Room C's lazy open is the
        // host's first - which hands back factory.Session, scripted here to hold its Turn in flight.
        factory.Session.EnqueueGatedReply(releaseC.Task, "c reply");
        var persona = TestPersonas.Nova;
        var roomB = GroupRoom("room-b", "Room B");
        var roomC = GroupRoom("room-c", "Room C");
        var roomD = GroupRoom("room-d", "Room D");

        await using var runner = CreateRunner(server, persona, factory, acp);
        await server.HandshakeAsync(runner, [roomB, roomC, roomD], ct);

        try
        {
            await server.SendAsync(Posted(roomC, "hi"), ct);
            await Eventually.UntilAsync(() => factory.Session.Prompts.Count == 1, ct, timeout: TestBudgets.Standard);

            // Room B opens and finishes a whole Turn while C's is still in flight, so B's LastActivity
            // is newer than C's: were C Idle, it - not B - would be the least recently used candidate.
            await server.SendAsync(Posted(roomB, "hi"), ct);
            await server.ReceiveUntilAsync<PostMessage>(post => string.Equals(post.RoomId, roomB.Id, StringComparison.Ordinal), ct);

            // Room D takes the slot B freed and opens at the cap: one of B and C must be closed.
            await server.SendAsync(Posted(roomD, "hi"), ct);
            await server.ReceiveUntilAsync<PostMessage>(post => string.Equals(post.RoomId, roomD.Id, StringComparison.Ordinal), ct);

            Assert.False(factory.Session.Disposed);

            releaseC.SetResult();
            var cPost = await server.ReceiveUntilAsync<PostMessage>(post => string.Equals(post.RoomId, roomC.Id, StringComparison.Ordinal), ct);
            Assert.Equal("c reply", cPost.Text);
        }
        finally
        {
            // factory.Session never completes its event stream on dispose, so a C Turn evicted
            // mid-flight would otherwise wait on it forever and hang the runner's disposal - a
            // failing assertion above must fail the test, not hang it.
            releaseC.TrySetResult();
        }
    }

    private static RoomInfo HumanRoom(string id) =>
        new(id, "Direct", [new MemberInfo("human", "Human", UserKind.Human), new MemberInfo("agent-1", "nova", UserKind.Agent)]);

    private static RoomInfo GroupRoom(string id, string name) =>
        new(
            id,
            name,
            [
                new MemberInfo("human", "Human", UserKind.Human),
                new MemberInfo("agent-1", "nova", UserKind.Agent),
                new MemberInfo("friend", "Friend", UserKind.Agent),
            ]);

    private static MessagePosted Posted(RoomInfo room, string text) =>
        new(
            room.Id,
            room.Name,
            new ChatMessage(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, "human", "Human", text),
            Mentioned: true,
            Mentions: [],
            Members: room.Members,
            AgentMessagesSinceHuman: 0,
            Budget: 0);

    private static PersonaRunner CreateRunner(
        PipeRunnerServer server, Persona persona, FakeAgentHostFactory factory, AcpOptions? acp = null, TimeProvider? timeProvider = null)
    {
        var options = TestOptions.Create(opts =>
        {
            opts.PipeName = server.PipeName;
            opts.Acp = acp ?? new AcpOptions();
        });
        return new PersonaRunner(
            persona, options, factory, new FakePromptSource(), new RoomFollows(), NullLogger<PersonaRunner>.Instance, timeProvider: timeProvider);
    }


    /// <summary>
    /// Advances <paramref name="time"/> by <paramref name="step"/> and sweeps, repeating until
    /// <paramref name="condition"/> is met or <paramref name="timeout"/> elapses. A single
    /// advance-then-sweep can race a Turn that has not yet finished stamping its Room Session's
    /// <c>LastActivity</c> at the moment it advances the clock - if that stamp lands using the
    /// already-advanced time, the Room reads as freshly active rather than idle, and one sweep pass
    /// simply misses it. Re-advancing on each retry is what makes this self-correcting rather than
    /// merely re-trying the same race: once the stamp has landed (at whatever moment it actually did),
    /// the NEXT advance necessarily pushes the clock past it, so it becomes overdue on the very next
    /// sweep regardless of the timing that produced it.
    /// </summary>
    /// <param name="runner">Whose idle sweep to invoke.</param>
    /// <param name="time">The runner's own <see cref="ManualTimeProvider"/>.</param>
    /// <param name="step">How far to advance the clock on each attempt.</param>
    /// <param name="condition">What the sweep is expected to have achieved.</param>
    /// <param name="ct">Bounds each sweep call and the delay between attempts.</param>
    /// <param name="timeout">How long to keep retrying before failing the test. Defaults to 5 seconds.</param>
    private static async Task SweepUntilAsync(
        PersonaRunner runner, ManualTimeProvider time, TimeSpan step, Func<bool> condition, CancellationToken ct, TimeSpan? timeout = null)
    {
        _ = await Eventually.ValueAsync(
            async _ =>
            {
                if (condition())
                {
                    return true;
                }

                time.Advance(step);
                await runner.SweepIdleSessionsAsync();
                return condition();
            },
            static met => met,
            ct,
            timeout ?? TestBudgets.Standard,
            TimeSpan.FromMilliseconds(20),
            what: "the idle sweep achieved the expected condition");
    }
}

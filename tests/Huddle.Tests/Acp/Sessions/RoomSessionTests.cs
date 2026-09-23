namespace Agency.Huddle.Tests.Acp.Sessions;

using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Sessions.Fakes;

/// <summary>
/// Pins <see cref="RoomSession"/> (RS §6.1) in isolation, before <see cref="Agency.Huddle.App.Acp.PersonaRunner"/> is
/// split to use it (Task 22.1.t). Every test builds its own <see cref="RoomSession"/> directly, with
/// a hand-written <see cref="FakeRoomSessionOwner"/> standing in for the runner and, where the
/// ticket protocol itself is under test, a <see cref="RecordingTurnScheduler"/> standing in for the
/// pool's <c>TurnGate</c> (not built until D23).
/// </summary>
public sealed class RoomSessionTests
{
    private static readonly WorkItem RoomAItem = new("room-a", "Room A", "Bob", "hello a", []);
    private static readonly WorkItem RoomBItem = new("room-b", "Room B", "Bob", "hello b", []);

    /// <summary>The first enqueued item opens the session exactly once, then prompts and posts the reply.</summary>
    [Fact]
    public async Task Enqueue_FirstItem_OpensOnceThenPromptsAndPostsReply()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReply("hi there");
        var openCalls = 0;
        FakeRoomSessionOwner owner = new();
        var (room, runCts) = CreateSession(owner, CountingOpen(() => session, () => openCalls++));
        try
        {
            room.Enqueue(new QueuedWork(1, RoomAItem));

            await WaitUntilAsync(() => owner.Written.OfType<PostMessage>().Any(), ct);

            Assert.Equal(1, openCalls);
            PostMessage posted = Assert.Single(owner.Written.OfType<PostMessage>());
            Assert.Equal("hi there", posted.Text);
            Assert.Contains(owner.Written, message => message is MessageDelta { IsFinal: true });
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>Two queued items run one after another, never overlapping on the shared session.</summary>
    [Fact]
    public async Task Enqueue_TwoItems_RunSeriallyNoOverlap()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReply("first");
        session.EnqueueReply("second");
        FakeRoomSessionOwner owner = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session));
        try
        {
            room.Enqueue(new QueuedWork(1, RoomAItem));
            room.Enqueue(new QueuedWork(2, RoomAItem));

            await WaitUntilAsync(() => owner.Written.OfType<PostMessage>().Count() == 2, ct);

            Assert.False(session.OverlapDetected);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>A Stop naming a different Room than the one running never cancels the active Turn (finding P-6).</summary>
    [Fact]
    public async Task Stop_OtherRoomInSharedSession_DoesNotCancelActiveTurn()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueDelayedReply(TimeSpan.FromMilliseconds(300), "a reply");
        FakeRoomSessionOwner owner = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session));
        try
        {
            room.Enqueue(new QueuedWork(1, RoomAItem));
            await WaitUntilAsync(() => session.Prompts.Count == 1, ct);

            await room.StopAsync("room-b", mark: 1, ct);

            Assert.Equal(0, session.CancelCallCount);
            await WaitUntilAsync(() => owner.Written.OfType<PostMessage>().Any(), ct);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>A Stop clears only its own Room's queued items; another Room's queued item still runs.</summary>
    [Fact]
    public async Task Stop_SameRoom_ClearsItsQueuedItemsOnly()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueDelayedReply(TimeSpan.FromMilliseconds(200), "a1 reply");
        session.EnqueueReply("b1 reply");
        FakeRoomSessionOwner owner = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session));
        try
        {
            var a1 = RoomAItem with { Text = "a1" };
            var a2 = RoomAItem with { Text = "a2" };
            var b1 = RoomBItem with { Text = "b1" };

            room.Enqueue(new QueuedWork(1, a1));
            await WaitUntilAsync(() => session.Prompts.Count == 1, ct);
            room.Enqueue(new QueuedWork(2, a2));
            room.Enqueue(new QueuedWork(3, b1));

            await room.StopAsync("room-a", mark: 2, ct);

            await WaitUntilAsync(() => owner.Written.OfType<PostMessage>().Any(message => message.Text == "b1 reply"), ct);

            Assert.DoesNotContain(session.Prompts, prompt => prompt.Contains("a2", StringComparison.Ordinal));
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>TRAP 1 at unit level: a Stop latches before it cancels, so the stopped Turn reports no failure.</summary>
    [Fact]
    public async Task Stop_LatchWrittenBeforeCancel_ReportsNoFailure()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueDelayedReply(TimeSpan.FromMilliseconds(500), "never posted");
        FakeRoomSessionOwner owner = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session));
        try
        {
            room.Enqueue(new QueuedWork(1, RoomAItem));
            await WaitUntilAsync(() => session.Prompts.Count == 1, ct);

            await room.StopAsync("room-a", mark: 1, ct);

            await WaitUntilAsync(() => owner.Written.OfType<MessageDelta>().Any(delta => delta.IsFinal), ct);

            Assert.DoesNotContain(owner.ReportCalls, call => call == nameof(IRoomSessionOwner.ReportTurnFailure));
            Assert.DoesNotContain(owner.ReportCalls, call => call == nameof(IRoomSessionOwner.ReportIncompleteStop));
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>
    /// D25 correction 24: strengthens TRAP 1 with the idle-timeout watchdog actually armed, unlike
    /// <see cref="Stop_LatchWrittenBeforeCancel_ReportsNoFailure"/> (<see cref="AcpOptions.TurnIdleTimeoutSeconds"/>
    /// defaults to 0 there, so no watchdog ever starts). Mutation-tested: moving
    /// <c>RoomSession.StopAsync</c>'s <c>MarkStopRequested()</c> call after its <c>Cancel()</c> call
    /// does NOT turn this test red, reliably, across repeated runs - <c>Cancel()</c> runs
    /// synchronously and the catch clause that reads <c>StopRequested</c> only runs once the
    /// exception has propagated back up through the awaited Task, by which time the reordered write
    /// has long since landed. A genuine regression here needs the idle-timeout watchdog's own
    /// <c>MarkTimedOut()</c> to race <em>the same window</em> in real wall-clock time - the identical
    /// difficulty this correction already documents for TRAP 2's <c>CancelAsync</c> callbacks being
    /// deferred. Kept anyway: it still pins that a Stop landing well inside an ARMED bound is
    /// reported as Stopped, not merely as "no bound configured at all".
    /// </summary>
    [Fact]
    public async Task Stop_WhileWatchdogArmed_LatchBeforeCancel_ReportedAsStopped()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueDelayedReply(TimeSpan.FromSeconds(5), "never posted");
        FakeRoomSessionOwner owner = new();
        AcpOptions options = new() { TurnIdleTimeoutSeconds = 5 };
        var (room, runCts) = CreateSession(owner, FixedOpen(session), options: options);
        try
        {
            room.Enqueue(new QueuedWork(1, RoomAItem));
            await WaitUntilAsync(() => session.Prompts.Count == 1, ct);

            await room.StopAsync("room-a", mark: 1, ct);

            await WaitUntilAsync(() => owner.Written.OfType<MessageDelta>().Any(delta => delta.IsFinal), ct);

            Assert.DoesNotContain(owner.ReportCalls, call => call == nameof(IRoomSessionOwner.ReportTurnFailure));
            Assert.DoesNotContain(owner.ReportCalls, call => call == nameof(IRoomSessionOwner.ReportOffline));
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>TRAP 2 at unit level: the idle-timeout watchdog cancels the far side before it cancels its own Turn token.</summary>
    [Fact]
    public async Task IdleTimeout_CancelsFarSideFirst()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueDelayedReply(TimeSpan.FromSeconds(2), "too late");
        FakeRoomSessionOwner owner = new();
        AcpOptions options = new() { TurnIdleTimeoutSeconds = 1 };
        var (room, runCts) = CreateSession(owner, FixedOpen(session), options: options);
        try
        {
            room.Enqueue(new QueuedWork(1, RoomAItem));

            await WaitUntilAsync(() => session.CancelObservedPromptInFlight, ct, TimeSpan.FromSeconds(5));

            Assert.Contains(owner.ReportCalls, call => call == nameof(IRoomSessionOwner.ReportTurnFailure));
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>An item queued while the Persona-wide token Budget is spent drains without ever prompting or opening.</summary>
    [Fact]
    public async Task TokenBudgetSpent_ItemDrainedWithoutPrompt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        var openCalls = 0;
        FakeRoomSessionOwner owner = new() { TokenBudgetSpent = true };
        var (room, runCts) = CreateSession(owner, CountingOpen(() => session, () => openCalls++));
        try
        {
            room.Enqueue(new QueuedWork(1, RoomAItem));

            await WaitUntilAsync(() => owner.ReportCalls.Contains(nameof(IRoomSessionOwner.ReportTokenBudgetSpent)), ct);

            Assert.Empty(session.Prompts);
            Assert.Equal(0, openCalls);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>A closed session reopens on its next enqueued item, calling the opener again.</summary>
    [Fact]
    public async Task Close_ThenEnqueue_OpensAgain()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var openCalls = 0;
        Func<CancellationToken, Task<IAgentSession>> open = _ =>
        {
            openCalls++;
            FakeAgentSession created = new();
            created.EnqueueReply("reply " + openCalls.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return Task.FromResult<IAgentSession>(created);
        };
        FakeRoomSessionOwner owner = new();
        var (room, runCts) = CreateSession(owner, open);
        try
        {
            await room.OpenAsync(ct);
            Assert.Equal(RoomSessionState.Idle, room.State);

            await room.CloseAsync();
            Assert.Equal(RoomSessionState.Closed, room.State);

            room.Enqueue(new QueuedWork(1, RoomAItem));
            await WaitUntilAsync(() => openCalls == 2, ct);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>Closing an Idle session never reports it Offline: the reader's own end is deliberate, not a crash.</summary>
    [Fact]
    public async Task Close_DoesNotReportOffline()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        FakeRoomSessionOwner owner = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session));
        try
        {
            await room.OpenAsync(ct);
            await room.CloseAsync();

            // Give the (already-ended) event reader a moment to have run its finally, if it was ever
            // going to report anything.
            await Task.Delay(50, ct);

            Assert.DoesNotContain(owner.ReportCalls, call => call == nameof(IRoomSessionOwner.ReportLoopEnded));
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>The first item enqueued for an idle session is offered to the scheduler exactly once.</summary>
    [Fact]
    public async Task FirstEnqueue_OffersHeadOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReply("ok");
        FakeRoomSessionOwner owner = new();
        RecordingTurnScheduler scheduler = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session), scheduler: scheduler);
        try
        {
            room.Enqueue(new QueuedWork(1, RoomAItem));
            await WaitUntilAsync(() => owner.Written.OfType<PostMessage>().Any(), ct);

            Assert.Equal(1, scheduler.OfferCount);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>A Stop-marked head is still completed on the scheduler, so no admitted slot leaks.</summary>
    [Fact]
    public async Task StoppedHead_StillCompletedNoLeak()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        FakeRoomSessionOwner owner = new();
        RecordingTurnScheduler scheduler = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session), scheduler: scheduler);
        try
        {
            // The mark is set before the item is even queued, so it is dropped deterministically
            // rather than racing the consumer.
            await room.StopAsync("room-a", mark: 100, ct);
            room.Enqueue(new QueuedWork(1, RoomAItem));

            await WaitUntilAsync(() => scheduler.CompleteCount == 1, ct);

            Assert.Equal(1, scheduler.OfferCount);
            Assert.Empty(session.Prompts);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>The second queued item is offered before the first one's ticket is completed (RS §6.1 rule 4, finding P-4).</summary>
    [Fact]
    public async Task SecondItem_OfferedBeforeFirstCompleted()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueDelayedReply(TimeSpan.FromMilliseconds(200), "first");
        session.EnqueueReply("second");
        FakeRoomSessionOwner owner = new();
        RecordingTurnScheduler scheduler = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session), scheduler: scheduler);
        try
        {
            room.Enqueue(new QueuedWork(1, RoomAItem));
            await WaitUntilAsync(() => session.Prompts.Count == 1, ct);
            room.Enqueue(new QueuedWork(2, RoomAItem));

            await WaitUntilAsync(() => scheduler.CompleteCount == 2, ct);

            List<string> calls = [.. scheduler.Calls];
            var offerSecondIndex = calls.IndexOf("Offer(2)");
            var completeFirstIndex = calls.IndexOf("Complete(1)");
            Assert.True(offerSecondIndex >= 0 && completeFirstIndex >= 0 && offerSecondIndex < completeFirstIndex);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>A Turn whose prompt throws still completes its ticket, so a failing Turn cannot leak a slot.</summary>
    [Fact]
    public async Task TurnThrows_TicketStillCompleted()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueFailure(new InvalidOperationException("boom"));
        FakeRoomSessionOwner owner = new();
        RecordingTurnScheduler scheduler = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session), scheduler: scheduler);
        try
        {
            room.Enqueue(new QueuedWork(1, RoomAItem));

            await WaitUntilAsync(() => scheduler.CompleteCount == 1, ct);

            Assert.Contains(owner.ReportCalls, call => call == nameof(IRoomSessionOwner.ReportTurnFailure));
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>LastActivity moves forward once a Turn ends.</summary>
    [Fact]
    public async Task LastActivity_UpdatedAtTurnEnd()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReply("ok");
        FakeRoomSessionOwner owner = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session));
        try
        {
            var before = room.LastActivity;
            room.Enqueue(new QueuedWork(1, RoomAItem));

            await WaitUntilAsync(() => owner.Written.OfType<PostMessage>().Any(), ct);
            await WaitUntilAsync(() => room.LastActivity > before, ct);

            Assert.True(room.LastActivity > before);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>D23 correction 17 / D24 correction 23: opening a session reports its advertised Models to the owner, so the Model-not-in-catalog warning can fire from any Room Session's open, not only a runner's start-up one.</summary>
    [Fact]
    public async Task Open_ReportsModelsToOwner()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReply("ok");
        FakeRoomSessionOwner owner = new();
        var (room, runCts) = CreateSession(owner, FixedOpen(session));
        try
        {
            room.Enqueue(new QueuedWork(1, RoomAItem));

            await WaitUntilAsync(() => owner.Written.OfType<PostMessage>().Any(), ct);

            var reported = Assert.Single(owner.ReportedModels);
            Assert.Same(session.Models, reported);
        }
        finally
        {
            await DisposeSessionAsync(room, runCts);
        }
    }

    /// <summary>
    /// Builds a <see cref="RoomSession"/> with fakes standing in for every collaborator, and its own
    /// <see cref="CancellationTokenSource"/> as the run token - the consumer's loop only ever ends
    /// when that token is cancelled, exactly as <see cref="Agency.Huddle.App.Acp.PersonaRunner"/>'s
    /// own <c>runCts</c> ends it in production, so a test disposes with
    /// <see cref="DisposeSessionAsync"/> rather than a bare <c>await using</c>.
    /// </summary>
    private static (RoomSession Session, CancellationTokenSource RunCts) CreateSession(
        FakeRoomSessionOwner owner,
        Func<CancellationToken, Task<IAgentSession>> open,
        ITurnScheduler? scheduler = null,
        AcpOptions? options = null)
    {
        CancellationTokenSource runCts = new();
        RoomSession session = new(
            roomId: null,
            open: open,
            owner: owner,
            scheduler: scheduler ?? new ImmediateTurnScheduler(),
            prompts: new FakePromptSource(),
            options: options ?? new AcpOptions(),
            fileChanges: null,
            declaredWatches: [],
            logger: NullLogger.Instance,
            runToken: runCts.Token);
        return (session, runCts);
    }

    /// <summary>
    /// Cancels <paramref name="runCts"/> - the run token <paramref name="session"/>'s consumer and
    /// event-reader loops watch - before disposing <paramref name="session"/>, so teardown ends its
    /// loops instead of hanging on them forever.
    /// </summary>
    private static async Task DisposeSessionAsync(RoomSession session, CancellationTokenSource runCts)
    {
        await runCts.CancelAsync();
        await session.DisposeAsync();
        runCts.Dispose();
    }

    /// <summary>An open delegate that always returns the same session.</summary>
    private static Func<CancellationToken, Task<IAgentSession>> FixedOpen(FakeAgentSession session) =>
        _ => Task.FromResult<IAgentSession>(session);

    /// <summary>An open delegate that counts its own calls through <paramref name="onOpen"/>, returning <paramref name="session"/>'s current value each time.</summary>
    private static Func<CancellationToken, Task<IAgentSession>> CountingOpen(Func<FakeAgentSession> session, Action onOpen) =>
        _ =>
        {
            onOpen();
            return Task.FromResult<IAgentSession>(session());
        };

    /// <summary>Polls <paramref name="condition"/> until it is true, or fails the test after <paramref name="timeout"/>.</summary>
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

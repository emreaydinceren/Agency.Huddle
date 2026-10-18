using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Sessions.Fakes;

namespace Agency.Huddle.Tests.Acp.Sessions;

/// <summary>
/// Pins the user-input lease on a <see cref="RoomSession"/>'s Turn (elicitation bridge, E-3): the idle
/// watchdog pauses while a question is open, restarts from the answer, and a question never outlives
/// its Turn; and <see cref="RoomSession"/> as the <see cref="IElicitationScope"/> that hands each
/// request to the <see cref="IElicitationBridge"/> with the Turn's own Room. Time is a
/// <see cref="TrackedFakeTimeProvider"/> throughout: every test moves it by hand and never sleeps.
/// </summary>
public sealed class RoomSessionUserInputTests
{
    private static readonly WorkItem RoomAItem = new("room-a", "Room A", "Bob", "hello a", []);
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(5);
    private static readonly ElicitationRequest Request = new("session-1", "tool-1", "Which one?", """{"type":"object","properties":{}}""");
    private static readonly Persona Nova = new("nova", "You are Nova.", Model: "m1", Effort: "e1");

    /// <summary>While a question is open the idle watchdog never fires, however many bounds of silence pass.</summary>
    [Fact]
    public async Task IdleWatchdog_OpenRequest_NeverFires()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TrackedFakeTimeProvider clock = new();
        await using Rig rig = new(clock, idleSeconds: 5);
        rig.Session.EnqueueGatedReply(rig.Release.Task);
        await rig.StartTurnAsync(1, expectedTimers: 1, ct);

        using UserInputLease? lease = rig.Room.TryBeginUserInput();
        Assert.NotNull(lease);

        await AdvanceAndSettleAsync(rig, Bound, 1, ct);
        await AdvanceAndSettleAsync(rig, Bound, 1, ct);
        await AdvanceAndSettleAsync(rig, Bound, 1, ct);

        Assert.Equal(0, rig.Session.CancelCallCount);
        Assert.DoesNotContain(rig.Owner.ReportCalls, call => call == nameof(IRoomSessionOwner.ReportTurnFailure));
    }

    /// <summary>
    /// After the answer the silence clock restarts from that moment: nothing fires a hair before one bound
    /// has passed since the release, and it does fire just after, however long the question was open.
    /// </summary>
    [Fact]
    public async Task IdleWatchdog_AfterRelease_RestartsFromTheAnswer()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TrackedFakeTimeProvider clock = new();
        await using Rig rig = new(clock, idleSeconds: 5);
        rig.Session.EnqueueGatedReply(rig.Release.Task);
        await rig.StartTurnAsync(1, expectedTimers: 1, ct);

        UserInputLease? lease = rig.Room.TryBeginUserInput();
        Assert.NotNull(lease);
        await AdvanceAndSettleAsync(rig, Bound, 1, ct);
        clock.Advance(TimeSpan.FromSeconds(2));

        // Released at t = 7 s: a bound later is t = 12 s.
        lease.Dispose();
        await AdvanceAndSettleAsync(rig, TimeSpan.FromSeconds(4.9), 1, ct);

        Assert.Equal(0, rig.Session.CancelCallCount);

        clock.Advance(TimeSpan.FromSeconds(0.3));
        await WaitUntilAsync(() => rig.Session.CancelCallCount == 1, ct);

        Assert.Equal(1, rig.Session.CancelCallCount);
    }

    /// <summary>Two overlapping questions keep the watchdog paused until both are answered, and it restarts from the second answer.</summary>
    [Fact]
    public async Task IdleWatchdog_TwoOverlapping_PausedUntilBothReleased()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TrackedFakeTimeProvider clock = new();
        await using Rig rig = new(clock, idleSeconds: 5);
        rig.Session.EnqueueGatedReply(rig.Release.Task);
        await rig.StartTurnAsync(1, expectedTimers: 1, ct);

        UserInputLease? first = rig.Room.TryBeginUserInput();
        UserInputLease? second = rig.Room.TryBeginUserInput();
        Assert.NotNull(first);
        Assert.NotNull(second);
        await AdvanceAndSettleAsync(rig, Bound, 1, ct);

        first.Dispose();
        await AdvanceAndSettleAsync(rig, Bound, 1, ct);
        await AdvanceAndSettleAsync(rig, Bound, 1, ct);

        Assert.Equal(0, rig.Session.CancelCallCount);

        // The second answer lands at t = 15 s; its bound ends at t = 20 s.
        second.Dispose();
        await AdvanceAndSettleAsync(rig, TimeSpan.FromSeconds(4.9), 1, ct);

        Assert.Equal(0, rig.Session.CancelCallCount);

        clock.Advance(TimeSpan.FromSeconds(0.3));
        await WaitUntilAsync(() => rig.Session.CancelCallCount == 1, ct);
    }

    /// <summary>Releasing the same lease twice counts once, so a double dispose cannot un-pause the watchdog under a second open question.</summary>
    [Fact]
    public async Task Lease_DisposedTwice_CountsOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TrackedFakeTimeProvider clock = new();
        await using Rig rig = new(clock, idleSeconds: 5);
        rig.Session.EnqueueGatedReply(rig.Release.Task);
        await rig.StartTurnAsync(1, expectedTimers: 1, ct);

        UserInputLease? first = rig.Room.TryBeginUserInput();
        using UserInputLease? second = rig.Room.TryBeginUserInput();
        Assert.NotNull(first);
        Assert.NotNull(second);

        first.Dispose();
        first.Dispose();
        await AdvanceAndSettleAsync(rig, Bound, 1, ct);
        await AdvanceAndSettleAsync(rig, Bound, 1, ct);

        Assert.Equal(0, rig.Session.CancelCallCount);
    }

    /// <summary>With the lease open when the Turn ends, the question's token is cancelled, and the next Turn's watchdog is not paused by it.</summary>
    [Fact]
    public async Task TurnEndsWithLeaseOpen_TokenCancelled_NextTurnWatchdogFiresNormally()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TrackedFakeTimeProvider clock = new();
        FakeElicitationBridge bridge = new();
        await using Rig rig = new(clock, idleSeconds: 5, bridge: bridge);
        rig.Session.EnqueueGatedReply(rig.Release.Task);
        rig.Session.EnqueueDelayedReply(TimeSpan.FromHours(1), "second turn");
        await rig.StartTurnAsync(1, expectedTimers: 1, ct);

        Task<ElicitationResult> pending = rig.Room.ElicitAsync(Request, ct);
        FakeElicitationBridge.BridgeCall call = (await bridge.WaitForCallsAsync(1, ct))[0];
        Assert.False(call.Token.IsCancellationRequested);

        rig.Release.SetResult();
        ElicitationResult result = await pending.WaitAsync(TimeSpan.FromSeconds(5), ct);

        Assert.IsType<ElicitationCancelled>(result);
        Assert.True(call.Token.IsCancellationRequested);

        // The second Turn goes silent for a full bound: its own watchdog and the fake session's delay are the two armed timers.
        await rig.StartTurnAsync(2, expectedTimers: 2, ct);
        clock.Advance(Bound);
        await WaitUntilAsync(() => rig.Session.CancelCallCount == 1, ct);
        await WaitUntilAsync(() => rig.Owner.ReportCalls.Contains(nameof(IRoomSessionOwner.ReportTurnFailure)), ct);

        Assert.Equal(1, rig.Session.CancelCallCount);
    }

    /// <summary>A lease outlives nothing: once its Turn ends its token reads as cancelled, and reading it then does not throw.</summary>
    [Fact]
    public async Task Lease_AfterTheTurnEnds_TokenIsCancelledAndReadable()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TrackedFakeTimeProvider clock = new();
        await using Rig rig = new(clock);
        rig.Session.EnqueueGatedReply(rig.Release.Task);
        await rig.StartTurnAsync(1, expectedTimers: 0, ct);

        using UserInputLease? lease = rig.Room.TryBeginUserInput();
        Assert.NotNull(lease);
        Assert.False(lease.Token.IsCancellationRequested);

        rig.Release.SetResult();
        await WaitUntilAsync(() => rig.Owner.Written.OfType<MessageDelta>().Any(delta => delta.IsFinal), ct);

        Assert.True(lease.Token.IsCancellationRequested);
    }

    /// <summary>A Stop cancels the open question at once, even while the Adapter has not yet ended the Turn, and the Turn is still reported as a Stop.</summary>
    [Fact]
    public async Task Stop_CancelsOpenRequestToken_AndIsStillAStop()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TrackedFakeTimeProvider clock = new();
        FakeElicitationBridge bridge = new();
        RecordingLogger logger = new();
        await using Rig rig = new(clock, idleSeconds: 5, bridge: bridge, logger: logger);
        rig.Session.EnqueueGatedReply(rig.Release.Task);
        await rig.StartTurnAsync(1, expectedTimers: 1, ct);

        Task<ElicitationResult> pending = rig.Room.ElicitAsync(Request, ct);
        FakeElicitationBridge.BridgeCall call = (await bridge.WaitForCallsAsync(1, ct))[0];

        // A gated reply never observes the Turn's token, so the Turn is still running when the Stop lands:
        // only the lease's own link to the Turn can cancel the question.
        await rig.Room.StopAsync("room-a", mark: 1, ct);
        ElicitationResult result = await pending.WaitAsync(TimeSpan.FromSeconds(5), ct);

        Assert.IsType<ElicitationCancelled>(result);
        Assert.True(call.Token.IsCancellationRequested);
        Assert.True(rig.Session.LastPromptCancellationToken.IsCancellationRequested);

        rig.Release.SetResult();
        await WaitUntilAsync(() => rig.Owner.Written.OfType<MessageDelta>().Any(delta => delta.IsFinal), ct);

        Assert.Equal(1, rig.Session.CancelCallCount);
        Assert.DoesNotContain(rig.Owner.ReportCalls, reported => reported == nameof(IRoomSessionOwner.ReportTurnFailure));
        Assert.DoesNotContain(rig.Owner.ReportCalls, reported => reported == nameof(IRoomSessionOwner.ReportOffline));

        // Only the bound running out is logged as a dropped question; a Stop is the Human's own doing.
        Assert.DoesNotContain(logger.Snapshot(), entry => entry.Message.Contains("dropped a question", StringComparison.Ordinal));
    }

    /// <summary>A run shutdown cancels the open question without waiting for the Adapter to end the Turn.</summary>
    [Fact]
    public async Task Shutdown_CancelsToken()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TrackedFakeTimeProvider clock = new();
        FakeElicitationBridge bridge = new();
        await using Rig rig = new(clock, bridge: bridge);
        rig.Session.EnqueueGatedReply(rig.Release.Task);
        await rig.StartTurnAsync(1, expectedTimers: 0, ct);

        Task<ElicitationResult> pending = rig.Room.ElicitAsync(Request, ct);
        FakeElicitationBridge.BridgeCall call = (await bridge.WaitForCallsAsync(1, ct))[0];

        await rig.RunCts.CancelAsync();
        ElicitationResult result = await pending.WaitAsync(TimeSpan.FromSeconds(5), ct);

        Assert.IsType<ElicitationCancelled>(result);
        Assert.True(call.Token.IsCancellationRequested);
    }

    /// <summary>
    /// The upper bound answers the question as cancelled, and that is the question's own end: the Turn's
    /// own token is untouched, the Turn goes on to complete and post its reply, and nothing is reported
    /// as a failure.
    /// </summary>
    [Fact]
    public async Task UserInputTimeout_CancelsHandler_AndDoesNotMarkTurnTimedOut()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TrackedFakeTimeProvider clock = new();
        FakeElicitationBridge bridge = new();
        await using Rig rig = new(clock, bridge: bridge, userInputSeconds: 30);
        rig.Session.EnqueueGatedReply(rig.Release.Task, "reply");
        await rig.StartTurnAsync(1, expectedTimers: 0, ct);

        Task<ElicitationResult> pending = rig.Room.ElicitAsync(Request, ct);
        FakeElicitationBridge.BridgeCall call = (await bridge.WaitForCallsAsync(1, ct))[0];
        await WaitForTimersAsync(clock, 1, ct);

        clock.Advance(TimeSpan.FromSeconds(29));
        Assert.False(call.Token.IsCancellationRequested);
        Assert.False(pending.IsCompleted);

        clock.Advance(TimeSpan.FromSeconds(1));
        ElicitationResult result = await pending.WaitAsync(TimeSpan.FromSeconds(5), ct);

        Assert.IsType<ElicitationCancelled>(result);
        Assert.True(call.Token.IsCancellationRequested);
        Assert.False(rig.Session.LastPromptCancellationToken.IsCancellationRequested);

        rig.Release.SetResult();
        await WaitUntilAsync(() => rig.Owner.Written.OfType<MessageDelta>().Any(delta => delta.IsFinal), ct);

        Assert.Equal("reply", Assert.Single(rig.Owner.Written.OfType<PostMessage>()).Text);
        Assert.Contains(rig.Owner.ReportCalls, reported => reported == nameof(IRoomSessionOwner.ReportTurnCompleted));
        Assert.DoesNotContain(rig.Owner.ReportCalls, reported => reported == nameof(IRoomSessionOwner.ReportTurnFailure));
    }

    /// <summary>A question dropped by the upper bound is logged once, as a warning that names the Persona, the Room and the bound.</summary>
    [Fact]
    public async Task UserInputTimeout_LogsAWarningNamingTheRoomAndTheBound()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TrackedFakeTimeProvider clock = new();
        FakeElicitationBridge bridge = new();
        RecordingLogger logger = new();
        await using Rig rig = new(clock, bridge: bridge, userInputSeconds: 30, logger: logger);
        rig.Session.EnqueueGatedReply(rig.Release.Task);
        await rig.StartTurnAsync(1, expectedTimers: 0, ct);

        Task<ElicitationResult> pending = rig.Room.ElicitAsync(Request, ct);
        await bridge.WaitForCallsAsync(1, ct);
        await WaitForTimersAsync(clock, 1, ct);

        clock.Advance(TimeSpan.FromSeconds(30));
        await pending.WaitAsync(TimeSpan.FromSeconds(5), ct);

        (LogLevel Level, string Message) entry = Assert.Single(logger.Snapshot());
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("Persona 'nova' dropped a question in room room-a: no answer after 30 seconds.", entry.Message);
    }

    /// <summary>The bridge receives the Turn's own Room, not the session's (null in shared mode), and the session's Agent id, with the request untouched.</summary>
    [Fact]
    public async Task Scope_RoomId_ComesFromActiveTurn_InSharedMode()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TrackedFakeTimeProvider clock = new();
        FakeElicitationBridge bridge = new();
        await using Rig rig = new(clock, bridge: bridge, agentId: "agent-9", roomId: null);
        rig.Session.EnqueueGatedReply(rig.Release.Task);
        await rig.StartTurnAsync(1, expectedTimers: 0, ct);

        Task<ElicitationResult> pending = rig.Room.ElicitAsync(Request, ct);
        FakeElicitationBridge.BridgeCall call = (await bridge.WaitForCallsAsync(1, ct))[0];

        Assert.Null(rig.Room.RoomId);
        Assert.Equal(new ElicitationContext("room-a", "agent-9"), call.Context);
        Assert.Same(Request, call.Request);
        call.Answer.SetResult(new ElicitationDeclined());
        Assert.IsType<ElicitationDeclined>(await pending.WaitAsync(TimeSpan.FromSeconds(5), ct));
    }

    /// <summary>With no Turn in flight there is nothing to lease, so no lease is handed out.</summary>
    [Fact]
    public async Task TryBegin_NoActiveTurn_ReturnsNull()
    {
        TrackedFakeTimeProvider clock = new();
        await using Rig rig = new(clock);

        Assert.Null(rig.Room.TryBeginUserInput());
    }

    /// <summary>A question that arrives with no Turn in flight is answered as cancelled and never reaches the bridge.</summary>
    [Fact]
    public async Task ElicitAsync_NoActiveTurn_AnswersCancelled_WithoutCallingTheBridge()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TrackedFakeTimeProvider clock = new();
        FakeElicitationBridge bridge = new();
        await using Rig rig = new(clock, bridge: bridge);

        ElicitationResult result = await rig.Room.ElicitAsync(Request, ct);

        Assert.IsType<ElicitationCancelled>(result);
        Assert.Empty(bridge.Calls);
    }

    /// <summary>A question that arrives after its Turn has ended is answered as cancelled and never reaches the bridge.</summary>
    [Fact]
    public async Task ElicitAsync_AfterTheTurnEnded_AnswersCancelled()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TrackedFakeTimeProvider clock = new();
        FakeElicitationBridge bridge = new();
        await using Rig rig = new(clock, bridge: bridge);
        rig.Session.EnqueueGatedReply(rig.Release.Task);
        await rig.StartTurnAsync(1, expectedTimers: 0, ct);
        rig.Release.SetResult();
        await WaitUntilAsync(() => rig.Owner.Written.OfType<MessageDelta>().Any(delta => delta.IsFinal), ct);

        ElicitationResult result = await rig.Room.ElicitAsync(Request, ct);

        Assert.IsType<ElicitationCancelled>(result);
        Assert.Empty(bridge.Calls);
    }

    /// <summary>With no bridge supplied every question is answered as cancelled.</summary>
    [Fact]
    public async Task ElicitAsync_NoBridge_AnswersCancelled()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TrackedFakeTimeProvider clock = new();
        await using Rig rig = new(clock, bridge: null);
        rig.Session.EnqueueGatedReply(rig.Release.Task);
        await rig.StartTurnAsync(1, expectedTimers: 0, ct);

        ElicitationResult result = await rig.Room.ElicitAsync(Request, ct);

        Assert.IsType<ElicitationCancelled>(result);
    }

    /// <summary>A session that does not know its Agent cannot say who is asking, so it answers as cancelled instead of showing the question.</summary>
    [Fact]
    public async Task ElicitAsync_NoAgentId_AnswersCancelled()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TrackedFakeTimeProvider clock = new();
        FakeElicitationBridge bridge = new();
        await using Rig rig = new(clock, bridge: bridge, agentId: null);
        rig.Session.EnqueueGatedReply(rig.Release.Task);
        await rig.StartTurnAsync(1, expectedTimers: 0, ct);

        ElicitationResult result = await rig.Room.ElicitAsync(Request, ct);

        Assert.IsType<ElicitationCancelled>(result);
        Assert.Empty(bridge.Calls);
    }

    /// <summary>The caller's own token (the Adapter's request going away) cancels the question and leaves the Turn running.</summary>
    [Fact]
    public async Task ElicitAsync_CallerTokenCancelled_AnswersCancelled_AndLeavesTheTurnRunning()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TrackedFakeTimeProvider clock = new();
        FakeElicitationBridge bridge = new();
        await using Rig rig = new(clock, bridge: bridge);
        rig.Session.EnqueueGatedReply(rig.Release.Task);
        await rig.StartTurnAsync(1, expectedTimers: 0, ct);
        using CancellationTokenSource caller = new();

        Task<ElicitationResult> pending = rig.Room.ElicitAsync(Request, caller.Token);
        FakeElicitationBridge.BridgeCall call = (await bridge.WaitForCallsAsync(1, ct))[0];
        await caller.CancelAsync();
        ElicitationResult result = await pending.WaitAsync(TimeSpan.FromSeconds(5), ct);

        Assert.IsType<ElicitationCancelled>(result);
        Assert.True(call.Token.IsCancellationRequested);
        Assert.False(rig.Session.LastPromptCancellationToken.IsCancellationRequested);
    }

    /// <summary>A bridge that gives up with its own cancellation exception is answered as cancelled, not surfaced as an error.</summary>
    [Fact]
    public async Task ElicitAsync_BridgeThrowsCancellation_AnswersCancelled()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TrackedFakeTimeProvider clock = new();
        FakeElicitationBridge bridge = new() { Handler = static _ => throw new OperationCanceledException() };
        await using Rig rig = new(clock, bridge: bridge);
        rig.Session.EnqueueGatedReply(rig.Release.Task);
        await rig.StartTurnAsync(1, expectedTimers: 0, ct);

        ElicitationResult result = await rig.Room.ElicitAsync(Request, ct);

        Assert.IsType<ElicitationCancelled>(result);
    }

    /// <summary>An answered question returns exactly what the bridge returned, and releases its lease so the watchdog runs again.</summary>
    [Fact]
    public async Task ElicitAsync_Answered_ReturnsTheBridgesResult_AndReleasesTheLease()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TrackedFakeTimeProvider clock = new();
        ElicitationAccepted accepted = new(new Dictionary<string, object> { ["question_0"] = "Red" });
        FakeElicitationBridge bridge = new() { Handler = _ => Task.FromResult<ElicitationResult>(accepted) };
        await using Rig rig = new(clock, idleSeconds: 5, bridge: bridge);
        rig.Session.EnqueueGatedReply(rig.Release.Task);
        await rig.StartTurnAsync(1, expectedTimers: 1, ct);

        ElicitationResult result = await rig.Room.ElicitAsync(Request, ct);

        Assert.Same(accepted, result);

        // Released at t = 0, so a full bound of silence later the watchdog fires.
        clock.Advance(Bound);
        await WaitUntilAsync(() => rig.Session.CancelCallCount == 1, ct);
    }

    /// <summary>A bridge that throws surfaces its exception unchanged, and the lease is still released so the watchdog runs again.</summary>
    [Fact]
    public async Task ElicitAsync_BridgeThrows_PropagatesAndReleasesTheLease()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TrackedFakeTimeProvider clock = new();
        FakeElicitationBridge bridge = new() { Handler = static _ => throw new InvalidOperationException("form could not be shown") };
        await using Rig rig = new(clock, idleSeconds: 5, bridge: bridge);
        rig.Session.EnqueueGatedReply(rig.Release.Task);
        await rig.StartTurnAsync(1, expectedTimers: 1, ct);

        InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Room.ElicitAsync(Request, ct));

        Assert.Equal("form could not be shown", thrown.Message);
        clock.Advance(Bound);
        await WaitUntilAsync(() => rig.Session.CancelCallCount == 1, ct);
    }

    /// <summary>Opening the lease counts as activity: a Turn that only ever showed a question and was then stopped still stores its session for a resume.</summary>
    [Fact]
    public async Task Lease_Begin_CountsAsActivity_ForTheStoreEntry()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
        TrackedFakeTimeProvider clock = new();
        await using Rig rig = new(clock, roomId: "room-a", store: store);
        rig.Session.EnqueueDelayedReply(TimeSpan.FromHours(1), "never posted");
        await rig.StartTurnAsync(1, expectedTimers: 1, ct);

        using UserInputLease? lease = rig.Room.TryBeginUserInput();
        Assert.NotNull(lease);
        await rig.Room.StopAsync("room-a", mark: 1, ct);

        await WaitUntilAsync(() => store.Get("nova", "room-a") is not null, ct);

        Assert.Equal(rig.Session.SessionId, store.Get("nova", "room-a")?.SessionId);
    }

    /// <summary>A lease on one Room Session never pauses another's watchdog.</summary>
    [Fact]
    public async Task TwoRoomSessions_DoNotShareLeaseState()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TrackedFakeTimeProvider clock = new();
        await using Rig leased = new(clock, idleSeconds: 5);
        await using Rig other = new(clock, idleSeconds: 5);
        leased.Session.EnqueueGatedReply(leased.Release.Task);
        other.Session.EnqueueGatedReply(other.Release.Task);
        await leased.StartTurnAsync(1, expectedTimers: 1, ct);
        await other.StartTurnAsync(1, expectedTimers: 2, ct);

        using UserInputLease? lease = leased.Room.TryBeginUserInput();
        Assert.NotNull(lease);

        clock.Advance(Bound);
        await WaitUntilAsync(() => other.Session.CancelCallCount == 1, ct);
        await WaitUntilAsync(() => clock.PendingTimers >= 1, ct);

        Assert.Equal(0, leased.Session.CancelCallCount);
    }

    /// <summary>The default upper bound on a question is ten minutes.</summary>
    [Fact]
    public void UserInputTimeoutSeconds_DefaultsTo600()
    {
        AcpOptions options = new();

        Assert.Equal(600, options.UserInputTimeoutSeconds);
    }

    /// <summary>With the default options a question is dropped at 600 seconds and not a second before.</summary>
    [Fact]
    public async Task UserInputTimeout_Default_ExpiresAt600Seconds()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TrackedFakeTimeProvider clock = new();
        FakeElicitationBridge bridge = new();
        await using Rig rig = new(clock, bridge: bridge);
        rig.Session.EnqueueGatedReply(rig.Release.Task);
        await rig.StartTurnAsync(1, expectedTimers: 0, ct);
        Task<ElicitationResult> pending = rig.Room.ElicitAsync(Request, ct);
        await bridge.WaitForCallsAsync(1, ct);
        await WaitForTimersAsync(clock, 1, ct);

        clock.Advance(TimeSpan.FromSeconds(599));
        Assert.False(pending.IsCompleted);

        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.IsType<ElicitationCancelled>(await pending.WaitAsync(TimeSpan.FromSeconds(5), ct));
    }

    /// <summary>A configured bound below thirty seconds, zero and negative included, is raised to thirty: a question is never unbounded and never expires before a Human can read it.</summary>
    /// <param name="configured">The configured seconds.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10)]
    public async Task UserInputTimeout_BelowTheMinimum_IsClampedTo30(int configured)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TrackedFakeTimeProvider clock = new();
        FakeElicitationBridge bridge = new();
        await using Rig rig = new(clock, bridge: bridge, userInputSeconds: configured);
        rig.Session.EnqueueGatedReply(rig.Release.Task);
        await rig.StartTurnAsync(1, expectedTimers: 0, ct);
        Task<ElicitationResult> pending = rig.Room.ElicitAsync(Request, ct);
        await bridge.WaitForCallsAsync(1, ct);
        await WaitForTimersAsync(clock, 1, ct);

        clock.Advance(TimeSpan.FromSeconds(29));
        Assert.False(pending.IsCompleted);

        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.IsType<ElicitationCancelled>(await pending.WaitAsync(TimeSpan.FromSeconds(5), ct));
    }

    /// <summary>A configured bound beyond a day is cut to a day, so a typo can neither throw nor effectively disable the bound.</summary>
    [Fact]
    public async Task UserInputTimeout_AboveTheMaximum_IsClampedToADay()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TrackedFakeTimeProvider clock = new();
        FakeElicitationBridge bridge = new();
        await using Rig rig = new(clock, bridge: bridge, userInputSeconds: int.MaxValue);
        rig.Session.EnqueueGatedReply(rig.Release.Task);
        await rig.StartTurnAsync(1, expectedTimers: 0, ct);
        Task<ElicitationResult> pending = rig.Room.ElicitAsync(Request, ct);
        await bridge.WaitForCallsAsync(1, ct);
        await WaitForTimersAsync(clock, 1, ct);

        clock.Advance(TimeSpan.FromSeconds(86_399));
        Assert.False(pending.IsCompleted);

        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.IsType<ElicitationCancelled>(await pending.WaitAsync(TimeSpan.FromSeconds(5), ct));
    }

    /// <summary>The bound is read from the options when a question arrives, so an operator's edit applies to the next question of a running session.</summary>
    [Fact]
    public async Task UserInputTimeout_ReadLive_FromTheOptionsAtRequestTime()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TrackedFakeTimeProvider clock = new();
        FakeElicitationBridge bridge = new();
        await using Rig rig = new(clock, bridge: bridge);
        rig.Session.EnqueueGatedReply(rig.Release.Task);
        await rig.StartTurnAsync(1, expectedTimers: 0, ct);
        rig.Options.UserInputTimeoutSeconds = 45;
        Task<ElicitationResult> pending = rig.Room.ElicitAsync(Request, ct);
        await bridge.WaitForCallsAsync(1, ct);
        await WaitForTimersAsync(clock, 1, ct);

        clock.Advance(TimeSpan.FromSeconds(44));
        Assert.False(pending.IsCompleted);

        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.IsType<ElicitationCancelled>(await pending.WaitAsync(TimeSpan.FromSeconds(5), ct));
    }

    /// <summary>Opening a session binds the Room Session as its elicitation scope before the first prompt is sent.</summary>
    [Fact]
    public async Task Open_BindsTheScope()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TrackedFakeTimeProvider clock = new();
        await using Rig rig = new(clock);
        rig.Session.EnqueueGatedReply(rig.Release.Task);
        int boundAtPrompt = -1;
        rig.Session.OnPrompt = _ => boundAtPrompt = rig.Session.BoundScopes.Count;

        await rig.StartTurnAsync(1, expectedTimers: 0, ct);
        await WaitUntilAsync(() => boundAtPrompt >= 0, ct);

        Assert.Same(rig.Room, Assert.Single(rig.Session.BoundScopes));
        Assert.Equal(1, boundAtPrompt);
    }

    /// <summary>Resuming a stored session binds the Room Session as its elicitation scope, and the session a resume replaced is never bound.</summary>
    [Fact]
    public async Task Resume_BindsTheScope()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
        store.Put("nova", "room-1", new RoomSessionEntry("stored-session-id", "claude", "m1", "e1", "msg-abc", DateTimeOffset.UtcNow, null));
        FakeAgentSession fresh = new();
        FakeAgentSession resumed = new();
        resumed.EnqueueReply("resumed reply");
        FakePersonaHost host = new(fresh, ClaudeProfile()) { CanResume = true };
        host.ResumeHandler = id => string.Equals(id, "stored-session-id", StringComparison.Ordinal) ? resumed : null;
        FakeRoomSessionOwner owner = new();
        using CancellationTokenSource runCts = new();
        RoomSession room = new(
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
            persona: Nova,
            roomSessions: store,
            host: host);
        try
        {
            room.Enqueue(new QueuedWork(1, new WorkItem("room-1", "Room 1", "Human", "go", [], TriggerMessageId: "trig-1")));

            await WaitUntilAsync(() => owner.Written.OfType<PostMessage>().Any(), ct);

            Assert.Equal("stored-session-id", Assert.Single(host.ResumeCalls));
            Assert.Same(room, Assert.Single(resumed.BoundScopes));
            Assert.Empty(fresh.BoundScopes);
        }
        finally
        {
            await runCts.CancelAsync();
            await room.DisposeAsync();
        }
    }

    /// <summary>
    /// Moves <paramref name="rig"/>'s clock by <paramref name="by"/>, then waits until the watchdog has
    /// either armed its next delay (so it has looked at the idle clock and decided not to fire) or fired.
    /// </summary>
    private static async Task AdvanceAndSettleAsync(Rig rig, TimeSpan by, int timersAfter, CancellationToken ct)
    {
        rig.Clock.Advance(by);
        await WaitUntilAsync(() => rig.Clock.PendingTimers >= timersAfter || rig.Session.CancelCallCount > 0, ct);
    }

    /// <summary>
    /// Waits until <paramref name="count"/> timers are armed on <paramref name="clock"/>, or fails the test
    /// after five seconds: a timer the code under test never arms must fail a test, not hang it.
    /// </summary>
    private static async Task WaitForTimersAsync(TrackedFakeTimeProvider clock, int count, CancellationToken ct)
    {
        try
        {
            await clock.WaitForPendingAsync(count, ct).WaitAsync(TimeSpan.FromSeconds(5), ct);
        }
        catch (TimeoutException)
        {
            Assert.Fail("The expected timers were not armed before the test's timeout.");
        }
    }

    /// <summary>Polls <paramref name="condition"/> until it is true, or fails the test after five seconds.</summary>
    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken ct)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("Condition was not met before the test's timeout.");
            }

            await Task.Delay(10, ct);
        }
    }

    /// <summary>The Adapter Profile a per-Room test's host reports: one that can resume.</summary>
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

    /// <summary>
    /// One <see cref="RoomSession"/> over a <see cref="FakeAgentSession"/> whose delays and the session's
    /// own timers run on a shared <see cref="TrackedFakeTimeProvider"/>. A test queues its reply plan on
    /// <see cref="Session"/>; a gated plan is released by <see cref="Release"/>, which disposal always
    /// completes so a Turn held by a token-blind gated reply can end.
    /// </summary>
    private sealed class Rig : IAsyncDisposable
    {
        private long sequence;

        /// <summary>Builds a Room Session, shared-mode unless <paramref name="store"/> makes it per-Room.</summary>
        /// <param name="clock">The clock the session, its watchdog and its fake adapter all use.</param>
        /// <param name="idleSeconds">The idle bound; zero disables the watchdog, so the question timeout is the only timer.</param>
        /// <param name="bridge">The bridge the session answers questions with, or <see langword="null"/> for none.</param>
        /// <param name="agentId">The session's Agent id, or <see langword="null"/> for none.</param>
        /// <param name="roomId">The session's own Room id, or <see langword="null"/> for a shared session.</param>
        /// <param name="logger">Where the session logs.</param>
        /// <param name="store">When given, the session is per-Room with this store, a host and a Persona, so a Turn end can write an entry.</param>
        /// <param name="userInputSeconds">The question timeout, or <see langword="null"/> for the default.</param>
        public Rig(
            TrackedFakeTimeProvider clock,
            int idleSeconds = 0,
            IElicitationBridge? bridge = null,
            string? agentId = "agent-1",
            string? roomId = null,
            ILogger? logger = null,
            RoomSessionStore? store = null,
            int? userInputSeconds = null)
        {
            this.Clock = clock;
            this.Session = new FakeAgentSession(time: clock);
            this.Release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            this.Owner = new FakeRoomSessionOwner();
            this.Options = new AcpOptions { TurnIdleTimeoutSeconds = idleSeconds };
            if (userInputSeconds is { } seconds)
            {
                this.Options.UserInputTimeoutSeconds = seconds;
            }

            this.RunCts = new CancellationTokenSource();
            FakePersonaHost? host = store is null ? null : new FakePersonaHost(this.Session, ClaudeProfile());
            Func<CancellationToken, Task<IAgentSession>> open = host is null
                ? _ => Task.FromResult<IAgentSession>(this.Session)
                : host.OpenAsync;
            this.Room = new RoomSession(
                roomId: roomId,
                open: open,
                owner: this.Owner,
                scheduler: new ImmediateTurnScheduler(),
                prompts: new FakePromptSource(),
                options: this.Options,
                fileChanges: null,
                declaredWatches: [],
                logger: logger ?? NullLogger.Instance,
                runToken: this.RunCts.Token,
                time: clock,
                persona: store is null ? null : Nova,
                roomSessions: store,
                host: host,
                agentId: agentId,
                elicitationBridge: bridge);
        }

        /// <summary>The shared clock.</summary>
        public TrackedFakeTimeProvider Clock { get; }

        /// <summary>The fake Adapter session the Room Session prompts.</summary>
        public FakeAgentSession Session { get; }

        /// <summary>Completed to let a gated reply end its Turn.</summary>
        public TaskCompletionSource Release { get; }

        /// <summary>What the Room Session reported and wrote.</summary>
        public FakeRoomSessionOwner Owner { get; }

        /// <summary>The options the Room Session reads live.</summary>
        public AcpOptions Options { get; }

        /// <summary>The run token's source; cancelling it is a shutdown.</summary>
        public CancellationTokenSource RunCts { get; }

        /// <summary>The Room Session under test.</summary>
        public RoomSession Room { get; }

        /// <summary>
        /// Queues one work item in Room A and waits until its prompt reached the Adapter and
        /// <paramref name="expectedTimers"/> timers are armed on the shared clock.
        /// </summary>
        /// <param name="promptCount">How many prompts the Adapter should have seen once this Turn started.</param>
        /// <param name="expectedTimers">The armed-timer count that means this Turn's watchdog and delays are in place.</param>
        /// <param name="ct">Cancels the wait.</param>
        public async Task StartTurnAsync(int promptCount, int expectedTimers, CancellationToken ct)
        {
            this.sequence++;
            this.Room.Enqueue(new QueuedWork(this.sequence, RoomAItem));
            await WaitUntilAsync(() => this.Session.Prompts.Count == promptCount, ct);
            await WaitForTimersAsync(this.Clock, expectedTimers, ct);
        }

        /// <summary>Releases any gated reply, then ends and disposes the Room Session.</summary>
        public async ValueTask DisposeAsync()
        {
            this.Release.TrySetResult();
            await this.RunCts.CancelAsync();
            await this.Room.DisposeAsync();
            this.RunCts.Dispose();
        }
    }

    /// <summary>An <see cref="ILogger"/> that keeps every entry's level and formatted message.</summary>
    private sealed class RecordingLogger : ILogger
    {
        private readonly Lock gate = new();
        private readonly List<(LogLevel Level, string Message)> entries = [];

        /// <summary>A copy of every entry logged so far.</summary>
        /// <returns>The entries, in order.</returns>
        public IReadOnlyList<(LogLevel Level, string Message)> Snapshot()
        {
            lock (this.gate)
            {
                return [.. this.entries];
            }
        }

        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            lock (this.gate)
            {
                this.entries.Add((logLevel, formatter(state, exception)));
            }
        }
    }
}

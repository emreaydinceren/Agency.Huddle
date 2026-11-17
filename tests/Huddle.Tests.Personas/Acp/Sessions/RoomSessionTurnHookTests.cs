using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Acp.Sessions.TurnHooks;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Sessions.Fakes;
using Agency.Huddle.Tests.Waiting;

namespace Agency.Huddle.Tests.Acp.Sessions;

/// <summary>
/// Pins how <see cref="RoomSession"/> runs a <see cref="TurnHookPipeline"/> around a Turn (Spec 6.7, rows
/// <c>ProcessWorkItemAsync</c> and <c>FinishTurnAsync</c>): Before runs for a Message Turn only, ahead of the
/// watchdog, and its block leads the prompt; After runs only for a Turn that reached the Adapter, with the
/// paths the Turn touched, and its cancellation propagates. A recording hook stands in for the real ones.
/// </summary>
public sealed class RoomSessionTurnHookTests
{
    private const string Block = "HOOK-BLOCK-TEXT";
    private const string RoomId = "room-1";

    private static readonly Persona Nova = new("nova", "You are Nova.", Model: "m1", Effort: "e1");

    private static readonly TurnHookScope Scope = new("nova", ReadsFiles: true, FileChangesOn: false, DeclaredWatches: []);

    /// <summary>A Message Turn runs Before once, with the Room, and the hook's block reaches the Adapter ahead of the Message text.</summary>
    [Fact]
    public async Task MessageTurn_RunsBeforeAndItsBlockIsInThePrompt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RecordingHook hook = new();
        FakeAgentSession session = new();
        session.EnqueueReply("done");
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = Build(owner, session, hook);

        harness.Room.Enqueue(new QueuedWork(1, MessageItem("please summarise")));
        await Eventually.UntilAsync(() => session.Prompts.Count == 1, ct, timeout: TestBudgets.Standard);

        string prompt = session.Prompts[0];
        Assert.Equal(RoomId, Assert.Single(hook.Starting).RoomId);
        // contains-ok: prompt text, not markup; BuildPromptHookBlocksTests pins the exact position and spacing.
        Assert.Contains(Block, prompt, StringComparison.Ordinal);
        Assert.True(
            prompt.IndexOf(Block, StringComparison.Ordinal) < prompt.IndexOf("please summarise", StringComparison.Ordinal),
            "The hook block must lead the Message text.");
    }

    /// <summary>A Turn whose prompt throws before any activity never reached the Adapter, so After does not run for it.</summary>
    [Fact]
    public async Task TurnThatNeverSent_DoesNotRunAfter()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RecordingHook hook = new();
        FakeAgentSession session = new();
        session.EnqueueFailure(new InvalidOperationException("boom"));
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = Build(owner, session, hook);

        harness.Room.Enqueue(new QueuedWork(1, MessageItem("first")));
        await Eventually.UntilAsync(() => owner.ReportCalls.Contains(nameof(IRoomSessionOwner.ReportTurnFailure)), ct, timeout: TestBudgets.Standard);
        await RunMarkerTurnAsync(harness, session, hook, sequence: 2, ct);

        Assert.Equal(2, hook.Starting.Count);
        Assert.Single(hook.Ended);
    }

    /// <summary>A Turn with an Edit tool call runs After once, handed the absolute path the Edit touched.</summary>
    [Fact]
    public async Task TurnWithActivity_RunsAfter_WithTouchedPaths()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RecordingHook hook = new();
        FakeAgentSession session = new();
        string edited = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "turn-hook-a.txt"));
        string rawInput = JsonSerializer.Serialize(new Dictionary<string, string> { ["file_path"] = edited });
        session.EnqueueToolActivity(
            new ToolCallStarted(session.SessionId, "c1", "Write", ToolKind.Edit, ToolCallStatus.Pending, rawInput),
            new ToolCallUpdated(session.SessionId, "c1", "Write", ToolKind.Edit, ToolCallStatus.Completed, null, rawInput));
        session.EnqueueReply("done");
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = Build(owner, session, hook);

        harness.Room.Enqueue(new QueuedWork(1, MessageItem("edit it")));
        await Eventually.UntilAsync(() => hook.Ended.Count == 1, ct, timeout: TestBudgets.Standard);

        TurnEnded ended = Assert.Single(hook.Ended);
        Assert.Equal(RoomId, ended.RoomId);
        Assert.Contains(edited, ended.Touched);
    }

    /// <summary>A Command Turn is not a Message Turn: neither Before nor After runs for it.</summary>
    [Fact]
    public async Task CommandTurn_RunsNeitherHookPoint()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RecordingHook hook = new();
        FakeAgentSession session = new();
        session.EnqueueReply("compacted");
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = Build(owner, session, hook);
        WorkItem command = new(
            RoomId,
            "Room 1",
            "You",
            "@nova /compact",
            [],
            WorkItemKind.Command,
            TriggerMessageId: "trig-1",
            Command: new AdapterCommandCall("compact", string.Empty));

        harness.Room.Enqueue(new QueuedWork(1, command));
        await Eventually.UntilAsync(() => owner.Written.OfType<MessageDelta>().Any(delta => delta.IsFinal), ct, timeout: TestBudgets.Standard);
        await RunMarkerTurnAsync(harness, session, hook, sequence: 2, ct);

        Assert.Single(hook.Starting);
        Assert.Single(hook.Ended);
    }

    /// <summary>A Greeting Turn is not a Message Turn either: neither Before nor After runs for it.</summary>
    [Fact]
    public async Task GreetingTurn_RunsNeitherHookPoint()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RecordingHook hook = new();
        FakeAgentSession session = new();
        session.EnqueueReply("hello there");
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = Build(owner, session, hook);
        WorkItem greeting = new(RoomId, "Room 1", string.Empty, string.Empty, [], WorkItemKind.Greeting);

        harness.Room.Enqueue(new QueuedWork(1, greeting));
        await Eventually.UntilAsync(() => owner.Written.OfType<MessageDelta>().Any(delta => delta.IsFinal), ct, timeout: TestBudgets.Standard);
        await RunMarkerTurnAsync(harness, session, hook, sequence: 2, ct);

        Assert.Single(hook.Starting);
        Assert.Single(hook.Ended);
    }

    /// <summary>
    /// A Before held for longer than the idle bound does not time the Turn out: the watchdog is armed after
    /// the hooks, so the held time is not silence. The reply arrives and no failure is reported.
    /// </summary>
    [Fact]
    public async Task Before_RunsBeforeTheWatchdogIsArmed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeTimeProvider clock = new();
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        RecordingHook hook = new()
        {
            BeforeGate = async token =>
            {
                entered.TrySetResult();
                await release.Task.WaitAsync(token);
            },
        };
        FakeAgentSession session = new(time: clock);
        session.EnqueueReply("late reply");
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = Build(
            owner,
            session,
            hook,
            new() { Acp = new AcpOptions { TurnIdleTimeoutSeconds = 5 }, Time = clock });

        harness.Room.Enqueue(new QueuedWork(1, MessageItem("hold me")));
        await entered.Task.WaitAsync(TestBudgets.Standard, ct);
        clock.Advance(TimeSpan.FromSeconds(30));
        release.TrySetResult();
        await Eventually.UntilAsync(() => owner.Written.OfType<PostMessage>().Any(post => post.Text == "late reply"), ct, timeout: TestBudgets.Standard);

        Assert.DoesNotContain(nameof(IRoomSessionOwner.ReportTurnFailure), owner.ReportCalls);
        Assert.Equal(0, session.CancelCallCount);
    }

    /// <summary>
    /// A cancellation inside After escapes the Turn rather than being swallowed, so the session entry is not
    /// stored (an OperationCanceledException escapes today, and skips the write).
    /// </summary>
    [Fact]
    public async Task AfterTurn_Cancelled_Propagates()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        RecordingHook hook = new()
        {
            AfterGate = async token =>
            {
                entered.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            },
        };
        FakeAgentSession session = new();
        session.EnqueueReply("done");
        FakeRoomSessionOwner owner = new();
        FakePersonaHost host = new(session, ClaudeProfile());
        RoomSessionHarness harness = Build(
            owner,
            session,
            hook,
            new() { Persona = Nova, RoomSessions = store, Host = host },
            host.OpenAsync);

        harness.Room.Enqueue(new QueuedWork(1, MessageItem("go")));
        await entered.Task.WaitAsync(TestBudgets.Standard, ct);
        await harness.DisposeAsync();

        Assert.Single(hook.Ended);
        Assert.Null(store.Get("nova", RoomId));
    }

    /// <summary>The control for the cancelled case: when After completes, the same setup does store the session entry.</summary>
    [Fact]
    public async Task AfterTurn_Completes_StoresTheSessionEntry()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
        RecordingHook hook = new();
        FakeAgentSession session = new();
        session.EnqueueReply("done");
        FakeRoomSessionOwner owner = new();
        FakePersonaHost host = new(session, ClaudeProfile());
        await using RoomSessionHarness harness = Build(
            owner,
            session,
            hook,
            new() { Persona = Nova, RoomSessions = store, Host = host },
            host.OpenAsync);

        harness.Room.Enqueue(new QueuedWork(1, MessageItem("go")));
        await Eventually.UntilAsync(() => store.Get("nova", RoomId) is not null, ct, timeout: TestBudgets.Standard);

        Assert.Single(hook.Ended);
        Assert.Equal(session.SessionId, store.Get("nova", RoomId)?.SessionId);
    }

    /// <summary>The state After returned in one Turn is what the next Turn's Before and After are handed (Spec §6.10, state is per session).</summary>
    [Fact]
    public async Task TwoTurns_SecondSeesTheStateTheFirstAfterReturned()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        StatefulHook hook = new();
        FakeAgentSession session = new();
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = Build(owner, session, hook);

        await RunTurnAsync(harness, session, hook, 1, ct);
        await RunTurnAsync(harness, session, hook, 2, ct);

        Assert.Null(hook.Starting[0].State);
        Assert.Equal("after-of-carry-1", hook.Starting[1].State);
        Assert.Null(hook.Ended[0].State);
        Assert.Equal("after-of-carry-1", hook.Ended[1].State);
    }

    /// <summary>The carry a Turn's Before returned reaches that Turn's After, and no other Turn's.</summary>
    [Fact]
    public async Task TwoTurns_EachAfterGetsItsOwnCarry()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        StatefulHook hook = new();
        FakeAgentSession session = new();
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = Build(owner, session, hook);

        await RunTurnAsync(harness, session, hook, 1, ct);
        await RunTurnAsync(harness, session, hook, 2, ct);

        Assert.Equal(["carry-1", "carry-2"], hook.Carries);
    }

    /// <summary>Two sessions that share one pipeline keep separate state: the second session's first Turn sees none of the first's.</summary>
    [Fact]
    public async Task TwoSessionsOverOnePipeline_StateIsSeparate()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        StatefulHook hook = new();
        TurnHookPipeline pipeline = new([hook], Scope, NullLogger.Instance);
        FakeAgentSession sessionA = new();
        FakeAgentSession sessionB = new();
        await using RoomSessionHarness harnessA = BuildOn(pipeline, new FakeRoomSessionOwner(), sessionA);
        await using RoomSessionHarness harnessB = BuildOn(pipeline, new FakeRoomSessionOwner(), sessionB);

        await RunTurnAsync(harnessA, sessionA, hook, 1, ct);
        await RunTurnAsync(harnessB, sessionB, hook, 1, ct);
        await RunTurnAsync(harnessA, sessionA, hook, 2, ct);
        await RunTurnAsync(harnessB, sessionB, hook, 2, ct);

        Assert.Null(hook.Starting[1].State);
        Assert.Null(hook.Ended[1].State);
        Assert.Equal("after-of-carry-1", hook.Starting[2].State);
        Assert.Equal("after-of-carry-2", hook.Starting[3].State);
    }

    /// <summary>A fresh open runs Opening before it calls the opener: while the open is held, Opening has already run, as Fresh with no state (Spec 8.5, 6.7).</summary>
    [Fact]
    public async Task FreshOpen_RunsOpeningBeforeTheOpen()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        OpeningHook hook = new();
        FakeAgentSession session = new();
        session.EnqueueReply("done");
        TaskCompletionSource openEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseOpen = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = Build(
            owner,
            session,
            hook,
            open: async token =>
            {
                openEntered.TrySetResult();
                await releaseOpen.Task.WaitAsync(TestBudgets.Standard, token);
                return session;
            });

        try
        {
            harness.Room.Enqueue(new QueuedWork(1, MessageItem("go")));
            await openEntered.Task.WaitAsync(TestBudgets.Standard, ct);

            SessionOpening opening = Assert.Single(hook.Openings);
            Assert.Equal(SessionOpenReason.Fresh, opening.Reason);
            Assert.Null(opening.State);
        }
        finally
        {
            releaseOpen.TrySetResult();
        }
    }

    /// <summary>
    /// Opening does not run under the session's gate: a Stop call completes while Opening is held. Opening blocks the
    /// thread it runs on (a held hook that awaits would unwind and release the gate anyway), so only the open's
    /// <c>Task.Yield</c> keeps it off the thread that holds the gate.
    /// </summary>
    [Fact]
    public async Task FreshOpen_DoesNotRunOpeningUnderTheGate()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TaskCompletionSource openingEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using ManualResetEventSlim releaseOpening = new();
        OpeningHook hook = new()
        {
            OpeningGate = token =>
            {
                openingEntered.TrySetResult();
                _ = releaseOpening.Wait(TestBudgets.Slow, token);
                return Task.CompletedTask;
            },
        };
        FakeAgentSession session = new();
        session.EnqueueReply("done");
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = Build(owner, session, hook);

        try
        {
            harness.Room.Enqueue(new QueuedWork(1, MessageItem("go")));
            await openingEntered.Task.WaitAsync(TestBudgets.Standard, ct);

            // On the pool, so a StopAsync that blocks on the gate cannot stall the test thread.
            Task stop = Task.Run(() => harness.Room.StopAsync(RoomId, 1, ct), ct);
            await stop.WaitAsync(TestBudgets.Short, ct);

            Assert.True(stop.IsCompletedSuccessfully);
        }
        finally
        {
            releaseOpening.Set();
        }
    }

    /// <summary>An exception while the Turn is prepared, before the prompt, is reported as Finished(Failed) and never as Started.</summary>
    [Fact]
    public async Task FailureBeforePrompt_SendsFinishedFailed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        FakeRoomSessionOwner owner = new()
        {
            WriteFault = message => message is MessageDelta ? new InvalidOperationException("draft write exploded") : null,
        };
        await using RoomSessionHarness harness = Build(owner, session, new RecordingHook());

        harness.Room.Enqueue(new QueuedWork(1, MessageItem("go")));
        await Eventually.UntilAsync(() => owner.Written.OfType<QueueAck>().Any(), ct, timeout: TestBudgets.Standard);

        QueueAck ack = Assert.Single(owner.Written.OfType<QueueAck>());
        Assert.Equal(QueueAckKind.Finished, ack.Kind);
        Assert.Equal(QueueOutcome.Failed, ack.Outcome);
        Assert.Empty(session.Prompts);
    }

    /// <summary>A Stop that lands while the Before hooks run drops the item unrun and reports it as Finished(Dropped).</summary>
    [Fact]
    public async Task StopDuringBeforeHooks_SendsFinishedDropped()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        RecordingHook hook = new()
        {
            BeforeGate = async token =>
            {
                entered.TrySetResult();
                await release.Task.WaitAsync(token);
            },
        };
        FakeAgentSession session = new();
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = Build(owner, session, hook);

        try
        {
            harness.Room.Enqueue(new QueuedWork(1, MessageItem("hold me")));
            await entered.Task.WaitAsync(TestBudgets.Standard, ct);
            await harness.Room.StopAsync(RoomId, 1, ct);
        }
        finally
        {
            release.TrySetResult();
        }

        await Eventually.UntilAsync(() => owner.Written.OfType<QueueAck>().Any(), ct, timeout: TestBudgets.Standard);

        QueueAck ack = Assert.Single(owner.Written.OfType<QueueAck>());
        Assert.Equal(QueueAckKind.Finished, ack.Kind);
        Assert.Equal(QueueOutcome.Dropped, ack.Outcome);
        Assert.Equal(["trig-hold me"], ack.MessageIds);
        Assert.Empty(session.Prompts);
    }

    /// <summary>A resume hands Opening the stored state of the hook and the reason Resumed.</summary>
    [Fact]
    public async Task Resume_PassesTheEntrysState_WithReasonResumed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
        store.Put(
            "nova",
            RoomId,
            new RoomSessionEntry(
                "stored-id", "claude", "m1", "e1", "msg-1", DateTimeOffset.UtcNow,
                HookState: new Dictionary<string, string?>(StringComparer.Ordinal) { ["opening"] = "stored-state" }));
        OpeningHook hook = new();
        FakeAgentSession fresh = new();
        FakeAgentSession resumed = new();
        resumed.EnqueueReply("done");
        FakePersonaHost host = new(fresh, ClaudeProfile()) { CanResume = true };
        host.ResumeHandler = id => string.Equals(id, "stored-id", StringComparison.Ordinal) ? resumed : null;
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = Build(
            owner,
            fresh,
            hook,
            new() { Persona = Nova, RoomSessions = store, Host = host },
            host.OpenAsync);

        harness.Room.Enqueue(new QueuedWork(1, MessageItem("go")));
        await Eventually.UntilAsync(() => hook.Openings.Count == 1, ct, timeout: TestBudgets.Standard);

        SessionOpening opening = hook.Openings[0];
        Assert.Equal(SessionOpenReason.Resumed, opening.Reason);
        Assert.Equal("stored-state", opening.State);
    }

    /// <summary>A resume the Adapter answers "not found" to opens fresh, and Opening runs once, as Fresh with no stored state.</summary>
    [Fact]
    public async Task ResumeNotFound_RunsOpeningAsFresh()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
        store.Put(
            "nova",
            RoomId,
            new RoomSessionEntry(
                "stored-id", "claude", "m1", "e1", "msg-1", DateTimeOffset.UtcNow,
                HookState: new Dictionary<string, string?>(StringComparer.Ordinal) { ["opening"] = "stored-state" }));
        OpeningHook hook = new();
        FakeAgentSession fresh = new();
        fresh.EnqueueReply("done");
        FakePersonaHost host = new(fresh, ClaudeProfile()) { CanResume = true };
        host.ResumeHandler = static _ => null;
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = Build(
            owner,
            fresh,
            hook,
            new() { Persona = Nova, RoomSessions = store, Host = host },
            host.OpenAsync);

        harness.Room.Enqueue(new QueuedWork(1, MessageItem("go")));
        await Eventually.UntilAsync(() => hook.Openings.Count >= 1, ct, timeout: TestBudgets.Standard);

        SessionOpening opening = Assert.Single(hook.Openings);
        Assert.Equal(SessionOpenReason.Fresh, opening.Reason);
        Assert.Null(opening.State);
    }

    /// <summary>A Greeting runs no Before or After, so the entry it stores carries the state Opening set.</summary>
    [Fact]
    public async Task Greeting_StoresTheStateOpeningSet()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
        OpeningHook hook = new() { OpeningState = "opened-state" };
        FakeAgentSession session = new();
        session.EnqueueReply("hello");
        FakeRoomSessionOwner owner = new();
        FakePersonaHost host = new(session, ClaudeProfile());
        await using RoomSessionHarness harness = Build(
            owner,
            session,
            hook,
            new() { Persona = Nova, RoomSessions = store, Host = host },
            host.OpenAsync);

        harness.Room.Enqueue(new QueuedWork(1, new WorkItem(RoomId, "Room 1", string.Empty, string.Empty, [], WorkItemKind.Greeting)));
        await Eventually.UntilAsync(() => store.Get("nova", RoomId) is not null, ct, timeout: TestBudgets.Standard);

        Assert.Equal("opened-state", store.Get("nova", RoomId)?.HookState?["opening"]);
    }

    /// <summary>A Message Turn stores the state After returned, under the hook's name, in the session entry.</summary>
    [Fact]
    public async Task Message_StoresTheStateAfterSet()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
        StatefulHook hook = new();
        FakeAgentSession session = new();
        session.EnqueueReply("done");
        FakeRoomSessionOwner owner = new();
        FakePersonaHost host = new(session, ClaudeProfile());
        await using RoomSessionHarness harness = Build(
            owner,
            session,
            hook,
            new() { Persona = Nova, RoomSessions = store, Host = host },
            host.OpenAsync);

        harness.Room.Enqueue(new QueuedWork(1, MessageItem("go")));
        await Eventually.UntilAsync(() => store.Get("nova", RoomId) is not null, ct, timeout: TestBudgets.Standard);

        Assert.Equal("after-of-carry-1", store.Get("nova", RoomId)?.HookState?["stateful"]);
    }

    /// <summary>A completed /compact runs Opening with the reason Compacted and writes the hook's state to the stored entry at once (Spec 6.7, "The command path").</summary>
    [Fact]
    public async Task CompletedCompact_RunsOpeningWithCompacted_AndWritesTheStateAtOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        RoomSessionStore store = SeededStore(dataDir);
        OpeningHook hook = new() { OpeningState = "opened" };
        FakeAgentSession session = new();
        session.EnqueueReply("compacted");
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = BuildStored(owner, session, hook, store);

        harness.Room.Enqueue(new QueuedWork(1, CommandItem("compact")));
        await Eventually.UntilAsync(() => StoredOpeningState(store) == "opened", ct, timeout: TestBudgets.Standard);

        Assert.Equal(SessionOpenReason.Compacted, hook.Openings[^1].Reason);
        Assert.Equal(2, hook.Openings.Count);
        Assert.Equal("opened", StoredOpeningState(store));
    }

    /// <summary>The state written after /compact leaves the entry's last Message id alone: a Command Turn stores no entry of its own.</summary>
    [Fact]
    public async Task CompletedCompact_DoesNotChangeLastMessageId()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        RoomSessionStore store = SeededStore(dataDir);
        OpeningHook hook = new() { OpeningState = "opened" };
        FakeAgentSession session = new();
        session.EnqueueReply("compacted");
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = BuildStored(owner, session, hook, store);

        harness.Room.Enqueue(new QueuedWork(1, CommandItem("compact")));
        await Eventually.UntilAsync(() => StoredOpeningState(store) == "opened", ct, timeout: TestBudgets.Standard);

        Assert.Equal("msg-1", store.Get("nova", RoomId)?.LastMessageId);
    }

    /// <summary>A /compact the user stopped changed nothing, so Opening does not run again and the stored state stays as it was.</summary>
    [Fact]
    public async Task StoppedCompact_RunsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        RoomSessionStore store = SeededStore(dataDir);
        OpeningHook hook = new() { OpeningState = "opened" };
        FakeAgentSession session = new();
        session.EnqueueReplyEndingIn(StopReason.Cancelled, "partial");
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = BuildStored(owner, session, hook, store);

        harness.Room.Enqueue(new QueuedWork(1, CommandItem("compact")));
        await RunGreetingMarkerAsync(harness, session, store, sequence: 2, ct);

        Assert.Equal(SessionOpenReason.Fresh, Assert.Single(hook.Openings).Reason);
    }

    /// <summary>A completed Command other than /compact runs no Opening and writes no state.</summary>
    [Fact]
    public async Task OtherCommand_RunsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        RoomSessionStore store = SeededStore(dataDir);
        OpeningHook hook = new() { OpeningState = "opened" };
        FakeAgentSession session = new();
        session.EnqueueReply("status text");
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = BuildStored(owner, session, hook, store);

        harness.Room.Enqueue(new QueuedWork(1, CommandItem("status")));
        await RunGreetingMarkerAsync(harness, session, store, sequence: 2, ct);

        Assert.Equal(SessionOpenReason.Fresh, Assert.Single(hook.Openings).Reason);
    }

    /// <summary>The command name is compared ignoring case: /COMPACT is a compaction too.</summary>
    [Fact]
    public async Task CompactNameIgnoresCase()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        RoomSessionStore store = SeededStore(dataDir);
        OpeningHook hook = new() { OpeningState = "opened" };
        FakeAgentSession session = new();
        session.EnqueueReply("compacted");
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = BuildStored(owner, session, hook, store);

        harness.Room.Enqueue(new QueuedWork(1, CommandItem("COMPACT")));
        await Eventually.UntilAsync(() => StoredOpeningState(store) == "opened", ct, timeout: TestBudgets.Standard);

        Assert.Equal(SessionOpenReason.Compacted, hook.Openings[^1].Reason);
    }

    /// <summary>A Stop in the Room whose Turn waits in Before cancels the token the hook holds, and the next Turn then runs (O-12, E-7).</summary>
    [Fact]
    public async Task Stop_InThisRoom_EndsAWaitingHook_AndFreesTheSlot()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        HeldBefore held = new();
        FakeAgentSession session = new();
        session.EnqueueReply("next reply");
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = Build(owner, session, held.Hook);

        harness.Room.Enqueue(new QueuedWork(1, MessageItem("first")));
        await held.Entered.Task.WaitAsync(TestBudgets.Standard, ct);
        await harness.Room.StopAsync(RoomId, 1, ct);
        await held.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        harness.Room.Enqueue(new QueuedWork(2, MessageItem("second")));
        await Eventually.UntilAsync(() => session.Prompts.Count == 1, ct, timeout: TestBudgets.Standard);

        Assert.True(held.Cancelled.Task.IsCompletedSuccessfully);
        Assert.Equal(2, held.Hook.Starting.Count);
    }

    /// <summary>The item whose wait a Stop ended is dropped: the Adapter is never prompted with it.</summary>
    [Fact]
    public async Task Stop_InThisRoom_DropsTheItem_NoPromptSent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        HeldBefore held = new();
        FakeAgentSession session = new();
        session.EnqueueReply("next reply");
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = Build(owner, session, held.Hook);

        harness.Room.Enqueue(new QueuedWork(1, MessageItem("first")));
        await held.Entered.Task.WaitAsync(TestBudgets.Standard, ct);
        await harness.Room.StopAsync(RoomId, 1, ct);
        await held.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        harness.Room.Enqueue(new QueuedWork(2, MessageItem("second")));
        await Eventually.UntilAsync(() => session.Prompts.Count >= 1, ct, timeout: TestBudgets.Standard);

        FakeAgentSession control = new();
        control.EnqueueReply("next reply");
        await using RoomSessionHarness controlHarness = Build(new FakeRoomSessionOwner(), control, new RecordingHook());
        controlHarness.Room.Enqueue(new QueuedWork(1, MessageItem("second")));
        await Eventually.UntilAsync(() => control.Prompts.Count == 1, ct, timeout: TestBudgets.Standard);

        Assert.Equal(control.Prompts, session.Prompts);
    }

    /// <summary>While a Message Turn waits in Before, the owner has an open (empty, non-final) Draft for it, so the Room shows the Agent working with Stop (O-12).</summary>
    [Fact]
    public async Task Before_Waiting_OwnerSeesTheTurnAsWorking()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        HeldBefore held = new();
        FakeAgentSession session = new();
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = Build(owner, session, held.Hook);

        harness.Room.Enqueue(new QueuedWork(1, MessageItem("first")));
        await held.Entered.Task.WaitAsync(TestBudgets.Standard, ct);

        MessageDelta working = Assert.Single(owner.Written.OfType<MessageDelta>());
        Assert.False(working.IsFinal);
        Assert.Equal(string.Empty, working.Text);
        Assert.Equal(RoomId, working.RoomId);
        await harness.Room.StopAsync(RoomId, 1, ct);
    }

    /// <summary>A Stop that ends the wait retracts the indicator with a final delta of the same message id, and no prompt is sent.</summary>
    [Fact]
    public async Task Stop_DuringBefore_ClearsTheIndicator()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        HeldBefore held = new();
        FakeAgentSession session = new();
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = Build(owner, session, held.Hook);

        harness.Room.Enqueue(new QueuedWork(1, MessageItem("first")));
        await held.Entered.Task.WaitAsync(TestBudgets.Standard, ct);
        await harness.Room.StopAsync(RoomId, 1, ct);
        await Eventually.UntilAsync(() => owner.Written.OfType<MessageDelta>().Any(delta => delta.IsFinal), ct, timeout: TestBudgets.Standard);

        List<MessageDelta> deltas = [.. owner.Written.OfType<MessageDelta>()];
        Assert.Equal(2, deltas.Count);
        Assert.Equal(deltas[0].MessageId, deltas[1].MessageId);
        Assert.Empty(session.Prompts);
    }

    /// <summary>A Turn that publishes after the wait reuses the indicator's message id: one Draft for the Turn, ended by one final delta.</summary>
    [Fact]
    public async Task Before_Then_Publish_OneDraftOnly()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReply("done");
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = Build(owner, session, new RecordingHook());

        harness.Room.Enqueue(new QueuedWork(1, MessageItem("hello")));
        await Eventually.UntilAsync(() => owner.Written.OfType<MessageDelta>().Any(delta => delta.IsFinal), ct, timeout: TestBudgets.Standard);

        Assert.Single(owner.Written.OfType<MessageDelta>().Select(delta => delta.MessageId).Distinct(StringComparer.Ordinal));
        Assert.Single(owner.Written.OfType<MessageDelta>(), delta => delta.IsFinal);
    }

    /// <summary>A Greeting Turn runs no hook, so it gets no early indicator: its first delta carries text.</summary>
    [Fact]
    public async Task GreetingTurn_NoEarlyIndicator()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReply("hello there");
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = Build(owner, session, new RecordingHook());
        WorkItem greeting = new(RoomId, "Room 1", string.Empty, string.Empty, [], WorkItemKind.Greeting);

        harness.Room.Enqueue(new QueuedWork(1, greeting));
        await Eventually.UntilAsync(() => owner.Written.OfType<MessageDelta>().Any(delta => delta.IsFinal), ct, timeout: TestBudgets.Standard);

        MessageDelta first = owner.Written.OfType<MessageDelta>().First();
        Assert.False(first.IsFinal);
        Assert.NotEqual(string.Empty, first.Text);
    }

    /// <summary>
    /// A Stop returns while a cancel callback the hook registered is still running: the callback runs off the Stop caller
    /// (<c>CancelAsync</c>), so the gate is never held across hook code. With <c>Cancel()</c> the callback runs inline and
    /// the Stop is stuck behind it.
    /// </summary>
    [Fact]
    public async Task Stop_ReturnsWhileTheHooksCancelCallbackIsHeld()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ManualResetEventSlim callbackRelease = new();
        HeldBefore held = new() { CallbackRelease = callbackRelease };
        FakeAgentSession session = new();
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = Build(owner, session, held.Hook);

        try
        {
            harness.Room.Enqueue(new QueuedWork(1, MessageItem("first")));
            await held.Entered.Task.WaitAsync(TestBudgets.Standard, ct);

            Task stop = Task.Run(() => harness.Room.StopAsync(RoomId, 1, ct), ct);
            await stop.WaitAsync(TestBudgets.Short, ct);

            Assert.True(stop.IsCompletedSuccessfully);
        }
        finally
        {
            callbackRelease.Set();
        }
    }

    /// <summary>
    /// A Stop in another Room does not end this Room's wait. A negative needs a window: <c>CancelAsync</c> marks the token
    /// at once, but a wrong cancel could still be queued, so the test waits a short time before it reads the token.
    /// </summary>
    [Fact]
    public async Task Stop_InAnotherRoom_DoesNotEndTheWait()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        HeldBefore held = new();
        FakeAgentSession session = new();
        session.EnqueueReply("reply");
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = Build(owner, session, held.Hook);

        harness.Room.Enqueue(new QueuedWork(1, MessageItem("first")));
        await held.Entered.Task.WaitAsync(TestBudgets.Standard, ct);
        await harness.Room.StopAsync("room-other", 1, ct);
        await Task.Delay(TimeSpan.FromMilliseconds(250), ct);
        bool cancelledAfterWindow = held.Token.IsCancellationRequested;
        held.Release.TrySetResult();
        await Eventually.UntilAsync(() => session.Prompts.Count == 1, ct, timeout: TestBudgets.Standard);

        Assert.False(cancelledAfterWindow);
        Assert.False(held.Cancelled.Task.IsCompleted);
    }

    /// <summary>
    /// A Stop whose mark is below the waiting item's sequence does not end that item's wait: an item admitted after the
    /// mark is not covered by it, so it keeps its hook and is prompted (the publish check's rule).
    /// </summary>
    [Fact]
    public async Task Stop_ForAnEarlierSequence_DoesNotCancelALaterItemsWait()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        HeldBefore held = new();
        FakeAgentSession session = new();
        session.EnqueueReply("reply");
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = Build(owner, session, held.Hook);

        harness.Room.Enqueue(new QueuedWork(2, MessageItem("later")));
        await held.Entered.Task.WaitAsync(TestBudgets.Standard, ct);
        await harness.Room.StopAsync(RoomId, 1, ct);
        await Task.Delay(TimeSpan.FromMilliseconds(250), ct);
        bool cancelledAfterWindow = held.Token.IsCancellationRequested;
        held.Release.TrySetResult();
        await Eventually.UntilAsync(() => session.Prompts.Count == 1, ct, timeout: TestBudgets.Standard);

        Assert.False(cancelledAfterWindow);
        Assert.Contains(Block, session.Prompts[0], StringComparison.Ordinal); // contains-ok: prompt text, not markup.
    }

    /// <summary>
    /// A Stop that lands while the session is still opening (before Before's source is published) reaches Before as a mark:
    /// the hook is not run for that item at all, and the next item then runs its hook and prompts.
    /// </summary>
    [Fact]
    public async Task Stop_MarkedBeforeThePublish_HookTokenIsAlreadyCancelled()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        HeldBefore held = new();
        FakeAgentSession session = new();
        session.EnqueueReply("reply");
        TaskCompletionSource openEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseOpen = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = Build(
            owner,
            session,
            held.Hook,
            open: async token =>
            {
                openEntered.TrySetResult();
                await releaseOpen.Task.WaitAsync(TestBudgets.Standard, token);
                return session;
            });

        try
        {
            harness.Room.Enqueue(new QueuedWork(1, MessageItem("first")));
            await openEntered.Task.WaitAsync(TestBudgets.Standard, ct);
            await harness.Room.StopAsync(RoomId, 1, ct);
        }
        finally
        {
            releaseOpen.TrySetResult();
        }

        harness.Room.Enqueue(new QueuedWork(2, MessageItem("second")));
        await held.Entered.Task.WaitAsync(TestBudgets.Standard, ct);
        held.Release.TrySetResult();
        await Eventually.UntilAsync(() => session.Prompts.Count == 1, ct, timeout: TestBudgets.Standard);

        Assert.Single(held.Hook.Starting);
    }

    /// <summary>A Stop during a first Turn's Before drops that item before its Transcript catch-up, so the next Turn still gets it (E-7).</summary>
    [Fact]
    public async Task Stop_DuringFirstTurnsBefore_NextTurnStillGetsTheCatchUp()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        HeldBefore held = new();
        FakeAgentSession session = new();
        session.EnqueueReply("next reply");
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = Build(owner, session, held.Hook);

        harness.Room.Enqueue(new QueuedWork(1, MessageItem("first")));
        await held.Entered.Task.WaitAsync(TestBudgets.Standard, ct);
        await harness.Room.StopAsync(RoomId, 1, ct);
        await held.Cancelled.Task.WaitAsync(TestBudgets.Standard, ct);
        harness.Room.Enqueue(new QueuedWork(2, MessageItem("second")));
        await Eventually.UntilAsync(() => session.Prompts.Count == 1, ct, timeout: TestBudgets.Standard);

        Assert.Equal("trig-second", Assert.Single(owner.TranscriptReadCalls).BeforeMessageId);
    }

    /// <summary>A session reopened with an Opening that throws starts with no Hook State: the old session's state does not carry over.</summary>
    [Fact]
    public async Task Reopen_OpeningThrows_OldStateDoesNotCarryOver()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        StatefulHook hook = new();
        FakeAgentSession first = new();
        FakeAgentSession second = new();
        Queue<FakeAgentSession> sessions = new([first, second]);
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = Build(
            owner,
            first,
            hook,
            open: _ => Task.FromResult<IAgentSession>(sessions.Dequeue()));

        await RunTurnAsync(harness, first, hook, 1, ct);
        await Eventually.UntilAsync(() => harness.Room.State == RoomSessionState.Idle, ct, timeout: TestBudgets.Standard);
        await harness.Room.CloseAsync();
        hook.ThrowOnOpening = true;
        await RunTurnAsync(harness, second, hook, 2, ct);

        Assert.Equal(2, hook.Starting.Count);
        Assert.Null(hook.Starting[1].State);
    }

    /// <summary>A resume whose Opening is cancelled disposes the session it resumed: nothing else owns it yet.</summary>
    [Fact]
    public async Task Resume_OpeningCancelled_DisposesTheResumedSession()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        RoomSessionStore store = SeededStore(dataDir);
        OpeningHook hook = new() { OpeningGate = static _ => throw new OperationCanceledException() };
        FakeAgentSession fresh = new();
        FakeAgentSession resumed = new();
        FakePersonaHost host = new(fresh, ClaudeProfile()) { CanResume = true };
        host.ResumeHandler = id => string.Equals(id, "stored-id", StringComparison.Ordinal) ? resumed : null;
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = Build(
            owner,
            fresh,
            hook,
            new() { Persona = Nova, RoomSessions = store, Host = host },
            host.OpenAsync);

        harness.Room.Enqueue(new QueuedWork(1, MessageItem("go")));
        await Eventually.UntilAsync(() => resumed.Disposed, ct, timeout: TestBudgets.Standard);

        Assert.True(resumed.Disposed);
    }

    /// <summary>A store that cannot be written after a completed /compact is logged, not a failed Turn: the Room hears no failure and the next Turn runs.</summary>
    [Fact]
    public async Task CompletedCompact_StoreWriteFails_TurnStillSucceeds()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Needs a file the store can read but not replace; only Windows refuses File.Replace on an open file.");
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        RoomSessionStore store = SeededStore(dataDir);
        OpeningHook hook = new() { OpeningState = "opened" };
        FakeAgentSession session = new();
        session.EnqueueReply("compacted");
        session.EnqueueReply("after");
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = BuildStored(owner, session, hook, store);
        string path = Path.Combine(dataDir.Path, "room-sessions", "nova.json");

        using FileStream locked = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        harness.Room.Enqueue(new QueuedWork(1, CommandItem("compact")));
        harness.Room.Enqueue(new QueuedWork(2, MessageItem("next")));
        await Eventually.UntilAsync(() => session.Prompts.Count == 2, ct, timeout: TestBudgets.Standard);

        Assert.Equal(2, hook.Openings.Count);
        Assert.Null(owner.LastFailureReason);
    }

    /// <summary>A shared session has no Room id, so a completed /compact writes no entry: the stored state stays as it was.</summary>
    [Fact]
    public async Task CompletedCompact_NoRoomId_WritesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        RoomSessionStore store = SeededStore(dataDir);
        OpeningHook hook = new() { OpeningState = "opened" };
        FakeAgentSession session = new();
        session.EnqueueReply("compacted");
        session.EnqueueReply("after");
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = Build(
            owner,
            session,
            hook,
            new() { Persona = Nova, RoomSessions = store, RoomId = null });

        harness.Room.Enqueue(new QueuedWork(1, CommandItem("compact")));
        harness.Room.Enqueue(new QueuedWork(2, MessageItem("next")));
        await Eventually.UntilAsync(() => session.Prompts.Count == 2, ct, timeout: TestBudgets.Standard);

        Assert.Equal(2, hook.Openings.Count);
        Assert.Equal("stale", StoredOpeningState(store));
    }

    /// <summary>Cancelling the run token (shutdown) ends the wait, no prompt is sent, and the consumer loop ends so disposal completes.</summary>
    [Fact]
    public async Task RunTokenCancelled_EndsTheWait_AndTheLoopEnds()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        HeldBefore held = new();
        FakeAgentSession session = new();
        FakeRoomSessionOwner owner = new();
        RoomSessionHarness harness = Build(owner, session, held.Hook);

        harness.Room.Enqueue(new QueuedWork(1, MessageItem("first")));
        await held.Entered.Task.WaitAsync(TestBudgets.Standard, ct);
        await harness.DisposeAsync().AsTask().WaitAsync(TestBudgets.Standard, ct);
        await held.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);

        Assert.Empty(session.Prompts);
    }

    /// <summary>
    /// A Turn stopped mid-prompt, once it saw activity, still runs After, and After gets the run's token, which a
    /// Stop does not cancel (E-6).
    /// </summary>
    [Fact]
    public async Task StoppedTurn_StillRunsAfter_WithTheRunToken()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TaskCompletionSource<bool> afterTokenCancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        RecordingHook hook = new() { AfterGate = token => { afterTokenCancelled.TrySetResult(token.IsCancellationRequested); return Task.CompletedTask; } };
        FakeAgentSession session = new();
        session.EnqueueReplyThenHang("partial");
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = Build(owner, session, hook);

        harness.Room.Enqueue(new QueuedWork(1, MessageItem("go")));
        await Eventually.UntilAsync(() => session.Prompts.Count == 1, ct, timeout: TestBudgets.Standard);
        // Stop only once the reply's activity has reached the owner: a Stop before it correctly runs no After (E-5).
        await Eventually.UntilAsync(() => owner.Written.OfType<MessageDelta>().Any(), ct, timeout: TestBudgets.Standard);
        await harness.Room.StopAsync(RoomId, 1, ct);

        // The fake records Ended before it runs AfterGate, so wait on the value itself, not on Ended.
        Assert.False(await afterTokenCancelled.Task.WaitAsync(TestBudgets.Standard, ct));
    }

    /// <summary>
    /// A Before that signals when it is entered and when its token is cancelled, then waits until released, its token
    /// is cancelled, or a bound passes. Only the first call is held; later calls return at once.
    /// </summary>
    private sealed class HeldBefore
    {
        private int calls;

        public HeldBefore()
        {
            this.Hook = new RecordingHook { BeforeGate = this.HoldAsync };
        }

        public RecordingHook Hook { get; }

        /// <summary>The token the held call received; set before <see cref="Entered"/> completes.</summary>
        public CancellationToken Token { get; private set; }

        /// <summary>When set (the test owns it), the held call registers a cancel callback that blocks on it, bounded by <see cref="TestBudgets.Slow"/>.</summary>
        public ManualResetEventSlim? CallbackRelease { get; init; }

        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private async Task HoldAsync(CancellationToken token)
        {
            if (Interlocked.Increment(ref this.calls) != 1)
            {
                return;
            }

            this.Token = token;
            ManualResetEventSlim? callbackRelease = this.CallbackRelease;
            Task wait = this.Release.Task.WaitAsync(TimeSpan.FromSeconds(10), token);

            // Registered after the WaitAsync call on purpose: callbacks run last-in first-out, so this one runs before the
            // wait's own, whose inline continuation would otherwise dispose this registration before it ran.
            using CancellationTokenRegistration registration = callbackRelease is null
                ? default
                : token.Register(() => callbackRelease.Wait(TestBudgets.Slow, CancellationToken.None)); // None on purpose: the token passed here is the cancelled one, which would end the hold at once.
            this.Entered.TrySetResult();
            try
            {
                await wait;
            }
            catch (OperationCanceledException)
            {
                this.Cancelled.TrySetResult();
                throw;
            }
            catch (TimeoutException)
            {
                // The bound passed with no Stop: the test's own assertion reports it.
            }
        }
    }

    /// <summary>A store holding Room 1's entry for Nova, last Message "msg-1", with a stale Hook State under the name "opening".</summary>
    private static RoomSessionStore SeededStore(TempDataDir dataDir)
    {
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
        store.Put(
            "nova",
            RoomId,
            new RoomSessionEntry(
                "stored-id", "claude", "m1", "e1", "msg-1", DateTimeOffset.UtcNow,
                HookState: new Dictionary<string, string?>(StringComparer.Ordinal) { ["opening"] = "stale" }));
        return store;
    }

    private static string? StoredOpeningState(RoomSessionStore store) => store.Get("nova", RoomId)?.HookState?["opening"];

    private static RoomSessionHarness BuildStored(FakeRoomSessionOwner owner, FakeAgentSession session, ITurnHook hook, RoomSessionStore store)
    {
        FakePersonaHost host = new(session, ClaudeProfile());

        // The entry belongs to the session the harness opens, as a stored entry does after a Turn: a /compact writes only to its own session's entry.
        if (store.Get("nova", RoomId) is { } seeded)
        {
            store.Put("nova", RoomId, seeded with { SessionId = session.SessionId });
        }

        return Build(owner, session, hook, new() { Persona = Nova, RoomSessions = store, Host = host }, host.OpenAsync);
    }

    private static WorkItem CommandItem(string name) =>
        new(
            RoomId,
            "Room 1",
            "You",
            "@nova /" + name,
            [],
            WorkItemKind.Command,
            TriggerMessageId: "trig-cmd",
            Command: new AdapterCommandCall(name, string.Empty));

    /// <summary>
    /// Queues a Greeting after the Turn under test and waits until it stores its entry (a new last Message id). The
    /// consumer is a single queue, so by then every earlier Turn, and any Opening it ran, has finished.
    /// </summary>
    private static async Task RunGreetingMarkerAsync(
        RoomSessionHarness harness,
        FakeAgentSession session,
        RoomSessionStore store,
        long sequence,
        CancellationToken ct)
    {
        session.EnqueueReply("hello");
        harness.Room.Enqueue(new QueuedWork(sequence, new WorkItem(RoomId, "Room 1", string.Empty, string.Empty, [], WorkItemKind.Greeting)));
        await Eventually.UntilAsync(() => store.Get("nova", RoomId)?.LastMessageId != "msg-1", ct, timeout: TestBudgets.Standard);
    }

    /// <summary>Enqueues one Message Turn and waits until its After has run.</summary>
    private static async Task RunTurnAsync(
        RoomSessionHarness harness,
        FakeAgentSession session,
        StatefulHook hook,
        long sequence,
        CancellationToken ct)
    {
        session.EnqueueReply("ok");
        int endedBefore = hook.Ended.Count;
        harness.Room.Enqueue(new QueuedWork(sequence, MessageItem("turn-" + sequence)));
        await Eventually.UntilAsync(() => hook.Ended.Count == endedBefore + 1, ct, timeout: TestBudgets.Standard);
    }

    private static RoomSessionHarness BuildOn(TurnHookPipeline pipeline, FakeRoomSessionOwner owner, FakeAgentSession session) =>
        RoomSessionHarness.Create(owner, RoomSessionHarness.FixedOpen(session), new RoomSessionHarnessOptions { Hooks = pipeline });

    private static WorkItem MessageItem(string text) =>
        new(RoomId, "Room 1", "You", text, [], TriggerMessageId: "trig-" + text);

    private static RoomSessionHarness Build(
        FakeRoomSessionOwner owner,
        FakeAgentSession session,
        ITurnHook hook,
        RoomSessionHarnessOptions? options = null,
        Func<CancellationToken, Task<IAgentSession>>? open = null)
    {
        TurnHookPipeline pipeline = new([hook], Scope, NullLogger.Instance);
        RoomSessionHarnessOptions settings = (options ?? new RoomSessionHarnessOptions()) with { Hooks = pipeline };
        return RoomSessionHarness.Create(owner, open ?? RoomSessionHarness.FixedOpen(session), settings);
    }

    /// <summary>
    /// Runs one ordinary Message Turn after the Turn under test and waits for its After. The consumer is a single
    /// queue, so by then every earlier Turn has finished and the hook's lists are complete.
    /// </summary>
    private static async Task RunMarkerTurnAsync(
        RoomSessionHarness harness,
        FakeAgentSession session,
        RecordingHook hook,
        long sequence,
        CancellationToken ct)
    {
        session.EnqueueReply("marker");
        int endedBefore = hook.Ended.Count;
        harness.Room.Enqueue(new QueuedWork(sequence, MessageItem("marker")));
        await Eventually.UntilAsync(() => hook.Ended.Count == endedBefore + 1, ct, timeout: TestBudgets.Standard);
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

    /// <summary>A hook that records every Opening call, can hold it, and answers it with a fixed state; Before and After add nothing.</summary>
    private sealed class OpeningHook : ITurnHook
    {
        private readonly Lock gate = new();
        private readonly List<SessionOpening> openings = [];

        /// <summary>Held at the start of Opening, if set.</summary>
        public Func<CancellationToken, Task>? OpeningGate { get; init; }

        /// <summary>The state Opening returns.</summary>
        public string? OpeningState { get; init; }

        /// <summary>Every Opening call so far.</summary>
        public IReadOnlyList<SessionOpening> Openings
        {
            get
            {
                lock (this.gate)
                {
                    return [.. this.openings];
                }
            }
        }

        /// <inheritdoc />
        public string Name => "opening";

        /// <inheritdoc />
        public bool AppliesTo(TurnHookScope scope) => true;

        /// <inheritdoc />
        public async Task<string?> OnSessionOpeningAsync(SessionOpening opening, CancellationToken cancellationToken)
        {
            lock (this.gate)
            {
                this.openings.Add(opening);
            }

            if (this.OpeningGate is { } held)
            {
                await held(cancellationToken);
            }

            return this.OpeningState;
        }

        /// <inheritdoc />
        public Task<TurnAddition?> BeforeTurnAsync(TurnStarting turn, CancellationToken cancellationToken) =>
            Task.FromResult<TurnAddition?>(null);

        /// <inheritdoc />
        public Task<string?> AfterTurnAsync(TurnEnded turn, TurnAddition? addition, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(this.OpeningState);
    }

    /// <summary>A hook that numbers its Before calls, carries "carry-N" to After, and answers After with "after-of-" plus that carry.</summary>
    private sealed class StatefulHook : ITurnHook
    {
        private readonly Lock gate = new();
        private readonly List<TurnStarting> starting = [];
        private readonly List<TurnEnded> ended = [];
        private readonly List<string?> carries = [];
        private int calls;

        /// <summary>Every Before call so far.</summary>
        public IReadOnlyList<TurnStarting> Starting
        {
            get
            {
                lock (this.gate)
                {
                    return [.. this.starting];
                }
            }
        }

        /// <summary>Every After call so far.</summary>
        public IReadOnlyList<TurnEnded> Ended
        {
            get
            {
                lock (this.gate)
                {
                    return [.. this.ended];
                }
            }
        }

        /// <summary>The carry each After was handed, in order.</summary>
        public IReadOnlyList<string?> Carries
        {
            get
            {
                lock (this.gate)
                {
                    return [.. this.carries];
                }
            }
        }

        /// <inheritdoc />
        public string Name => "stateful";

        /// <inheritdoc />
        public bool AppliesTo(TurnHookScope scope) => true;

        /// <inheritdoc />
        public Task<string?> OnSessionOpeningAsync(SessionOpening opening, CancellationToken cancellationToken) =>
            this.ThrowOnOpening ? throw new InvalidOperationException("Opening failed.") : Task.FromResult<string?>(null);

        /// <summary>When set, Opening throws.</summary>
        public bool ThrowOnOpening { get; set; }

        /// <inheritdoc />
        public Task<TurnAddition?> BeforeTurnAsync(TurnStarting turn, CancellationToken cancellationToken)
        {
            lock (this.gate)
            {
                this.starting.Add(turn);
                this.calls++;
                return Task.FromResult<TurnAddition?>(new TurnAddition(null, "carry-" + this.calls));
            }
        }

        /// <inheritdoc />
        public Task<string?> AfterTurnAsync(TurnEnded turn, TurnAddition? addition, CancellationToken cancellationToken)
        {
            string? carry = addition?.Carry as string;
            lock (this.gate)
            {
                this.ended.Add(turn);
                this.carries.Add(carry);
            }

            return Task.FromResult<string?>("after-of-" + carry);
        }
    }

    /// <summary>A hook that records every call and answers Before with <see cref="Block"/>; a gate can hold either point.</summary>
    private sealed class RecordingHook : ITurnHook
    {
        private readonly Lock gate = new();
        private readonly List<TurnStarting> starting = [];
        private readonly List<TurnEnded> ended = [];

        /// <summary>Held at the start of Before, if set.</summary>
        public Func<CancellationToken, Task>? BeforeGate { get; init; }

        /// <summary>Held at the start of After, if set.</summary>
        public Func<CancellationToken, Task>? AfterGate { get; init; }

        /// <summary>Every Before call so far.</summary>
        public IReadOnlyList<TurnStarting> Starting
        {
            get
            {
                lock (this.gate)
                {
                    return [.. this.starting];
                }
            }
        }

        /// <summary>Every After call so far.</summary>
        public IReadOnlyList<TurnEnded> Ended
        {
            get
            {
                lock (this.gate)
                {
                    return [.. this.ended];
                }
            }
        }

        /// <inheritdoc />
        public string Name => "recording";

        /// <inheritdoc />
        public bool AppliesTo(TurnHookScope scope) => true;

        /// <inheritdoc />
        public Task<string?> OnSessionOpeningAsync(SessionOpening opening, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);

        /// <inheritdoc />
        public async Task<TurnAddition?> BeforeTurnAsync(TurnStarting turn, CancellationToken cancellationToken)
        {
            lock (this.gate)
            {
                this.starting.Add(turn);
            }

            if (this.BeforeGate is { } held)
            {
                await held(cancellationToken);
            }

            return new TurnAddition(Block, null);
        }

        /// <inheritdoc />
        public async Task<string?> AfterTurnAsync(TurnEnded turn, TurnAddition? addition, CancellationToken cancellationToken)
        {
            lock (this.gate)
            {
                this.ended.Add(turn);
            }

            if (this.AfterGate is { } held)
            {
                await held(cancellationToken);
            }

            return "after";
        }
    }
}

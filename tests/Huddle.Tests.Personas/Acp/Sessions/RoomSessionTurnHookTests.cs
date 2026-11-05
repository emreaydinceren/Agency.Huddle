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
            Task.FromResult<string?>(null);

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

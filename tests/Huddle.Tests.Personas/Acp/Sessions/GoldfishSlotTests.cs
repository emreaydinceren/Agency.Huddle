using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Sessions.Fakes;
using Agency.Huddle.Tests.Waiting;

namespace Agency.Huddle.Tests.Acp.Sessions;

/// <summary>
/// Pins the goldfish slot of <see cref="RoomSessionPool"/> (Goldfish corrections slice 2, section B): one clean session opened through
/// <c>OpenCleanAsync</c>, held beside the per-Room sessions but never among them, so the live count, the victim choice and the idle
/// sweep never see it; it shares the pool's Turn gate, is stopped with its Room, and is disposed with the pool.
/// </summary>
public sealed class GoldfishSlotTests
{
    private static readonly Persona Nova = new("nova", "You are Nova.", Model: "m1", Effort: "e1");

    private static readonly CleanSessionSpec Spec = new("p1", "A", "t1");

    /// <summary>The goldfish session is opened through <c>OpenCleanAsync</c> with the spec, and the enqueued item reaches that session, not the shared one.</summary>
    [Fact]
    public async Task OpenGoldfish_OpensThroughOpenCleanAndServesTheItem()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Rig rig = Rig.Create();

        rig.Pool.OpenGoldfish("A", Spec);
        rig.Pool.EnqueueGoldfish(Work(1, "A", "the brief"));
        await Eventually.UntilAsync(() => rig.Host.CleanSessions.Count == 1 && rig.Host.CleanSessions[0].Prompts.Count == 1, ct, timeout: TestBudgets.Standard);

        Assert.Equal(Spec, Assert.Single(rig.Host.CleanSpecs));
        Assert.Empty(rig.Warm.Prompts);
        // contains-ok: prompt text, not markup; the clean prompt's exact shape is pinned in CleanRoomSessionTests.
        Assert.Contains("the brief", rig.Host.CleanSessions[0].Prompts[0], StringComparison.Ordinal);
    }

    /// <summary>A warm session in the same Room and one in another Room are untouched by the goldfish: still the pool's sessions, not disposed, and the slot is not the Room's session.</summary>
    [Fact]
    public async Task OpenGoldfish_LeavesTheWarmSessionsUntouched()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Rig rig = Rig.Create();
        rig.Pool.Enqueue(Work(1, "A", "warm a"));
        rig.Pool.Enqueue(Work(2, "B", "warm b"));
        await Eventually.UntilAsync(() => rig.Host.Sessions.Count == 2 && rig.Host.Sessions.All(static session => session.Prompts.Count == 1), ct, timeout: TestBudgets.Standard);
        RoomSession warmA = Assert.IsType<RoomSession>(rig.Pool.SessionFor("A"));
        RoomSession warmB = Assert.IsType<RoomSession>(rig.Pool.SessionFor("B"));

        RoomSession goldfish = rig.Pool.OpenGoldfish("A", Spec);
        rig.Pool.EnqueueGoldfish(Work(3, "A", "the brief"));
        await Eventually.UntilAsync(() => rig.Host.CleanSessions.Count == 1 && rig.Host.CleanSessions[0].Prompts.Count == 1, ct, timeout: TestBudgets.Standard);

        Assert.NotSame(warmA, goldfish);
        Assert.Same(warmA, rig.Pool.SessionFor("A"));
        Assert.Same(warmB, rig.Pool.SessionFor("B"));
        Assert.All(rig.Host.Sessions.Where(session => !rig.Host.CleanSessions.Contains(session)), static session => Assert.False(session.Disposed));
        Assert.Single(rig.Warm.Prompts);
    }

    /// <summary>A second <c>OpenGoldfish</c> while the slot is taken throws and leaves the first goldfish in place.</summary>
    [Fact]
    public async Task OpenGoldfish_SlotTaken_ThrowsAndKeepsTheFirst()
    {
        await using Rig rig = Rig.Create();
        rig.Pool.OpenGoldfish("A", Spec);

        Assert.Throws<InvalidOperationException>(() => rig.Pool.OpenGoldfish("B", Spec with { RoomId = "B" }));

        Assert.Equal("A", rig.Pool.GoldfishRoomId);
    }

    /// <summary>Shared mode has one session for every Room, so a goldfish cannot be told apart from it: opening one throws.</summary>
    [Fact]
    public async Task OpenGoldfish_SharedMode_Throws()
    {
        await using Rig rig = Rig.Create(sessionPerRoom: false);

        Assert.Throws<InvalidOperationException>(() => rig.Pool.OpenGoldfish("A", Spec));

        Assert.Null(rig.Pool.GoldfishRoomId);
    }

    /// <summary>Queueing to an empty slot throws rather than dropping the item.</summary>
    [Fact]
    public async Task EnqueueGoldfish_EmptySlot_Throws()
    {
        await using Rig rig = Rig.Create();

        Assert.Throws<InvalidOperationException>(() => rig.Pool.EnqueueGoldfish(Work(1, "A", "the brief")));

        Assert.Null(rig.Pool.GoldfishRoomId);
    }

    /// <summary>A real <see cref="RoomSessionStore"/> gets an entry for a warm session's Room and none for the goldfish's: a clean session is never resumable.</summary>
    [Fact]
    public async Task GoldfishTurn_PutsNothingInTheSessionStore()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
        await using Rig rig = Rig.Create(store: store);
        rig.Pool.Enqueue(Work(1, "A", "warm a"));
        await Eventually.UntilAsync(() => store.Get("nova", "A") is not null, ct, timeout: TestBudgets.Standard);

        rig.Pool.OpenGoldfish("B", Spec with { RoomId = "B" });
        rig.Pool.EnqueueGoldfish(Work(2, "B", "the brief"));
        await Eventually.UntilAsync(() => rig.Host.CleanSessions.Count == 1 && rig.Host.CleanSessions[0].Prompts.Count == 1, ct, timeout: TestBudgets.Standard);

        Assert.NotNull(store.Get("nova", "A"));
        Assert.Null(store.Get("nova", "B"));
    }

    /// <summary>At <c>MaxLiveSessions</c> 1 a new Room's session closes the idle warm one, never the goldfish, and the host briefly holds cap plus one sessions.</summary>
    [Fact]
    public async Task OpenGoldfish_AtTheLiveCap_IsNeverTheVictimAndMayRunOneAboveIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Rig rig = Rig.Create(configure: static options => options.MaxLiveSessions = 1);
        rig.Pool.Enqueue(Work(1, "A", "warm a"));
        await Eventually.UntilAsync(() => rig.Warm.Prompts.Count == 1 && rig.Pool.SessionFor("A")?.State == RoomSessionState.Idle, ct, timeout: TestBudgets.Standard);
        RoomSession goldfish = rig.Pool.OpenGoldfish("B", Spec with { RoomId = "B" });
        rig.Pool.EnqueueGoldfish(Work(2, "B", "the brief"));
        await Eventually.UntilAsync(() => rig.Host.CleanSessions.Count == 1 && goldfish.State == RoomSessionState.Idle, ct, timeout: TestBudgets.Standard);

        rig.Pool.Enqueue(Work(3, "C", "warm c"));
        await Eventually.UntilAsync(() => rig.Warm.Disposed && rig.Host.Sessions.Count == 3 && rig.Host.Sessions[2].Prompts.Count == 1, ct, timeout: TestBudgets.Standard);

        Assert.False(rig.Host.CleanSessions[0].Disposed);
        Assert.Equal(2, rig.Host.LiveSessionHighWaterMark);
        Assert.Equal("B", rig.Pool.GoldfishRoomId);
    }

    /// <summary>The goldfish takes its Turn from the pool's one gate: with a single slot held by a warm Turn, it prompts only after that Turn has been released.</summary>
    [Fact]
    public async Task GoldfishTurn_WaitsForTheSharedTurnGate()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        bool warmReleasedWhenGoldfishPrompted = false;
        await using Rig rig = Rig.Create(scriptClean: session => session.OnPrompt = _ => warmReleasedWhenGoldfishPrompted = release.Task.IsCompleted);
        rig.Warm.EnqueueGatedReply(release.Task, "warm done");
        try
        {
            rig.Pool.Enqueue(Work(1, "A", "warm a"));
            await Eventually.UntilAsync(() => rig.Warm.Prompts.Count == 1, ct, timeout: TestBudgets.Standard);

            rig.Pool.OpenGoldfish("B", Spec with { RoomId = "B" });
            rig.Pool.EnqueueGoldfish(Work(2, "B", "the brief"));
            release.SetResult();
            await Eventually.UntilAsync(() => rig.Host.CleanSessions.Count == 1 && rig.Host.CleanSessions[0].Prompts.Count == 1, ct, timeout: TestBudgets.Standard);
        }
        finally
        {
            release.TrySetResult(); // A failed open must not leave the warm Turn gated, or the teardown waits on it.
        }

        Assert.True(warmReleasedWhenGoldfishPrompted);
    }

    /// <summary>A Stop for the goldfish's Room reaches the warm session and the goldfish both; a Stop for another Room reaches neither.</summary>
    [Fact]
    public async Task StopAsync_ForTheGoldfishRoom_ReachesBothSessions()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Rig rig = Rig.Create(
            configure: static options => options.MaxConcurrentTurns = 2,
            scriptClean: static session => session.EnqueueReplyThenHang("working"));
        rig.Warm.EnqueueReplyThenHang("working");
        rig.Pool.Enqueue(Work(1, "A", "warm a"));
        rig.Pool.OpenGoldfish("A", Spec);
        rig.Pool.EnqueueGoldfish(Work(2, "A", "the brief"));
        await Eventually.UntilAsync(() => rig.Warm.Prompts.Count == 1 && rig.Host.CleanSessions.Count == 1 && rig.Host.CleanSessions[0].Prompts.Count == 1, ct, timeout: TestBudgets.Standard);

        await rig.Pool.StopAsync("Z", 9, ct);
        Assert.Equal(0, rig.Warm.CancelCallCount);
        Assert.Equal(0, rig.Host.CleanSessions[0].CancelCallCount);
        await rig.Pool.StopAsync("A", 9, ct);

        Assert.Equal(1, rig.Warm.CancelCallCount);
        Assert.Equal(1, rig.Host.CleanSessions[0].CancelCallCount);
    }

    /// <summary>Closing the goldfish disposes its session and empties the slot, a second close does nothing, and the slot can be opened again.</summary>
    [Fact]
    public async Task CloseGoldfishAsync_DisposesEmptiesTheSlotAndIsIdempotent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Rig rig = Rig.Create();
        rig.Pool.OpenGoldfish("A", Spec);
        rig.Pool.EnqueueGoldfish(Work(1, "A", "the brief"));
        await Eventually.UntilAsync(() => rig.Host.CleanSessions.Count == 1 && rig.Host.CleanSessions[0].Prompts.Count == 1, ct, timeout: TestBudgets.Standard);

        await rig.Pool.CloseGoldfishAsync();
        await rig.Pool.CloseGoldfishAsync();

        Assert.True(rig.Host.CleanSessions[0].Disposed);
        Assert.Null(rig.Pool.GoldfishRoomId);
        rig.Pool.OpenGoldfish("B", Spec with { RoomId = "B" });
        Assert.Equal("B", rig.Pool.GoldfishRoomId);
    }

    /// <summary><c>GoldfishRoomId</c> is null while the slot is empty, the Room's id while it is taken.</summary>
    [Fact]
    public async Task GoldfishRoomId_FollowsTheSlot()
    {
        await using Rig rig = Rig.Create();
        Assert.Null(rig.Pool.GoldfishRoomId);

        rig.Pool.OpenGoldfish("A", Spec);

        Assert.Equal("A", rig.Pool.GoldfishRoomId);
    }

    /// <summary>Disposing the pool disposes a goldfish session that is still open.</summary>
    [Fact]
    public async Task DisposeAsync_DisposesTheGoldfishSession()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Rig rig = Rig.Create();
        rig.Pool.OpenGoldfish("A", Spec);
        rig.Pool.EnqueueGoldfish(Work(1, "A", "the brief"));
        await Eventually.UntilAsync(() => rig.Host.CleanSessions.Count == 1 && rig.Host.CleanSessions[0].Prompts.Count == 1, ct, timeout: TestBudgets.Standard);

        await rig.DisposePoolAsync();

        Assert.True(rig.Host.CleanSessions[0].Disposed);
    }

    private static QueuedWork Work(long sequence, string roomId, string text) =>
        new(sequence, new WorkItem(roomId, roomId, "Human", text, [], TriggerMessageId: "trig-" + sequence));

    /// <summary>A pool over a fake host. The warm (shared) session and every clean one reply "ok" unless a test scripts otherwise; the run token is cancelled before the pool is disposed.</summary>
    private sealed class Rig : IAsyncDisposable
    {
        private readonly CancellationTokenSource runCts;

        private Rig(FakeAgentSession warm, FakePersonaHost host, RoomSessionPool pool, CancellationTokenSource runCts)
        {
            this.Warm = warm;
            this.Host = host;
            this.Pool = pool;
            this.runCts = runCts;
        }

        public FakeAgentSession Warm { get; }

        public FakePersonaHost Host { get; }

        public RoomSessionPool Pool { get; }

        public static Rig Create(
            bool sessionPerRoom = true,
            Action<AcpOptions>? configure = null,
            RoomSessionStore? store = null,
            Action<FakeAgentSession>? scriptClean = null)
        {
            FakeAgentSession warm = new();
            FakePersonaHost host = new(warm, Profile(sessionPerRoom)) { CanResume = true };
            host.OnOpen = session =>
            {
                if (!ReferenceEquals(session, warm))
                {
                    scriptClean?.Invoke(session);
                }
            };
            AcpOptions options = new();
            configure?.Invoke(options);
            CancellationTokenSource runCts = new();
            RoomSessionPool pool = new(
                host,
                new FakeRoomSessionOwner(),
                Nova,
                new FakePromptSource(),
                options,
                TimeProvider.System,
                NullLogger.Instance,
                runCts.Token,
                roomSessions: store,
                agentId: "agent-1");
            return new Rig(warm, host, pool, runCts);
        }

        /// <summary>Cancels the run token, then disposes the pool, so the session consumers end.</summary>
        public async Task DisposePoolAsync()
        {
            await this.runCts.CancelAsync();
            await this.Pool.DisposeAsync();
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            await this.DisposePoolAsync();
            this.runCts.Dispose();
        }
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
}

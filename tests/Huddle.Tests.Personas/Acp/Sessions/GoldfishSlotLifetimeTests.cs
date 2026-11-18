using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Sessions.Fakes;
using Agency.Huddle.Tests.Waiting;

namespace Agency.Huddle.Tests.Acp.Sessions;

/// <summary>
/// Pins the lifetime of the goldfish slot of <see cref="RoomSessionPool"/>: a ticket queued while the slot closes never stays on the shared
/// Turn gate, a disposed pool opens and queues nothing, concurrent closes all wait for the one dispose, and the pool's own disposal and a
/// Stop for another Room leave the slot's session in the state they should.
/// </summary>
public sealed class GoldfishSlotLifetimeTests
{
    private static readonly Persona Nova = new("nova", "You are Nova.", Model: "m1", Effort: "e1");

    private static readonly CleanSessionSpec Spec = new("p1", "A", "t1");

    /// <summary>Queueing to the goldfish and closing it straight away, again and again, leaves no ticket on the shared gate: a warm Turn offered after still runs.</summary>
    [Fact]
    public async Task EnqueueGoldfish_ThenImmediateClose_LeavesNoTicketOnTheGate()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Rig rig = Rig.Create();

        for (int round = 1; round <= 200; round++)
        {
            rig.Pool.OpenGoldfish("A", Spec);
            long sequence = round;
            Task enqueue = Task.Run(() => TryEnqueueGoldfish(rig.Pool, Work(sequence, "A", "the brief")), ct);
            Task close = Task.Run(rig.Pool.CloseGoldfishAsync, ct);
            await Task.WhenAll(enqueue, close);
        }

        rig.Pool.Enqueue(Work(1000, "W", "warm after"));
        await Eventually.UntilAsync(() => rig.Warm.Prompts.Count == 1, ct, timeout: TestBudgets.Standard);

        Assert.Single(rig.Warm.Prompts);
    }

    /// <summary>Opening a goldfish after the pool is disposed throws and leaves the slot empty, so nothing is opened through the disposed host.</summary>
    [Fact]
    public async Task OpenGoldfish_AfterDispose_Throws()
    {
        await using Rig rig = Rig.Create();
        await rig.DisposePoolAsync();

        Assert.Throws<ObjectDisposedException>(() => rig.Pool.OpenGoldfish("A", Spec));

        Assert.Null(rig.Pool.GoldfishRoomId);
    }

    /// <summary>Queueing to the goldfish after the pool is disposed throws <see cref="ObjectDisposedException"/>, not the empty-slot error.</summary>
    [Fact]
    public async Task EnqueueGoldfish_AfterDispose_Throws()
    {
        await using Rig rig = Rig.Create();
        await rig.DisposePoolAsync();

        Assert.Throws<ObjectDisposedException>(() => rig.Pool.EnqueueGoldfish(Work(1, "A", "the brief")));
    }

    /// <summary>A second close, called while the first is still disposing the session, returns only once the session is disposed.</summary>
    [Fact]
    public async Task CloseGoldfishAsync_Concurrent_BothReturnAfterTheDispose()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ManualResetEventSlim entered = new();
        using ManualResetEventSlim release = new();
        await using Rig rig = Rig.Create(scriptClean: session => session.OnDisposed = () =>
        {
            entered.Set();
            release.Wait(TestBudgets.Standard);
        });
        rig.Pool.OpenGoldfish("A", Spec);
        rig.Pool.EnqueueGoldfish(Work(1, "A", "the brief"));
        await Eventually.UntilAsync(() => rig.Host.CleanSessions.Count == 1 && rig.Host.CleanSessions[0].Prompts.Count == 1, ct, timeout: TestBudgets.Standard);
        Task first;
        Task second;
        try
        {
            first = rig.Pool.CloseGoldfishAsync();
            await Eventually.UntilAsync(() => entered.IsSet, ct, timeout: TestBudgets.Standard);

            second = rig.Pool.CloseGoldfishAsync();
            await Task.Delay(TimeSpan.FromMilliseconds(200), ct);
            Assert.False(second.IsCompleted);
        }
        finally
        {
            release.Set();
        }

        await Task.WhenAll(first, second).WaitAsync(TestBudgets.Standard, ct);

        Assert.True(rig.Host.CleanSessions[0].Disposed);
        Assert.Null(rig.Pool.GoldfishRoomId);
    }

    /// <summary>Closing the goldfish while its Turn is in flight cancels the Turn and returns with the session disposed.</summary>
    [Fact]
    public async Task CloseGoldfishAsync_TurnInFlight_CancelsItAndReturns()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Rig rig = Rig.Create(scriptClean: static session => session.EnqueueReplyThenHang("working"));
        rig.Pool.OpenGoldfish("A", Spec);
        rig.Pool.EnqueueGoldfish(Work(1, "A", "the brief"));
        await Eventually.UntilAsync(() => rig.Host.CleanSessions.Count == 1 && rig.Host.CleanSessions[0].Prompts.Count == 1, ct, timeout: TestBudgets.Standard);

        await rig.Pool.CloseGoldfishAsync().WaitAsync(TestBudgets.Standard, ct);

        Assert.True(rig.Host.CleanSessions[0].Disposed);
        Assert.Null(rig.Pool.GoldfishRoomId);
    }

    /// <summary>Disposing the pool while a goldfish Turn is in flight ends the Turn and disposes the session.</summary>
    [Fact]
    public async Task DisposeAsync_GoldfishTurnInFlight_DisposesTheSession()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Rig rig = Rig.Create(scriptClean: static session => session.EnqueueReplyThenHang("working"));
        rig.Pool.OpenGoldfish("A", Spec);
        rig.Pool.EnqueueGoldfish(Work(1, "A", "the brief"));
        await Eventually.UntilAsync(() => rig.Host.CleanSessions.Count == 1 && rig.Host.CleanSessions[0].Prompts.Count == 1, ct, timeout: TestBudgets.Standard);

        await rig.DisposePoolAsync().WaitAsync(TestBudgets.Standard, ct);

        Assert.True(rig.Host.CleanSessions[0].Disposed);
    }

    /// <summary>A Stop for a Room the goldfish does not serve cancels that Room's warm Turn and leaves the goldfish's Turn running.</summary>
    [Fact]
    public async Task StopAsync_ForAnotherRoom_LeavesTheGoldfishAlone()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Rig rig = Rig.Create(
            configure: static options => options.MaxConcurrentTurns = 2,
            scriptClean: static session => session.EnqueueReplyThenHang("working"));
        rig.Warm.EnqueueReplyThenHang("working");
        rig.Pool.Enqueue(Work(1, "B", "warm b"));
        rig.Pool.OpenGoldfish("A", Spec);
        rig.Pool.EnqueueGoldfish(Work(2, "A", "the brief"));
        await Eventually.UntilAsync(() => rig.Warm.Prompts.Count == 1 && rig.Host.CleanSessions.Count == 1 && rig.Host.CleanSessions[0].Prompts.Count == 1, ct, timeout: TestBudgets.Standard);

        await rig.Pool.StopAsync("B", 9, ct);

        Assert.Equal(1, rig.Warm.CancelCallCount);
        Assert.Equal(0, rig.Host.CleanSessions[0].CancelCallCount);
        Assert.False(rig.Host.CleanSessions[0].Disposed);
    }

    /// <summary>Queues to the goldfish; losing the race to a close, which empties the slot, is an expected outcome and not a failure.</summary>
    private static void TryEnqueueGoldfish(RoomSessionPool pool, QueuedWork item)
    {
        try
        {
            pool.EnqueueGoldfish(item);
        }
        catch (InvalidOperationException)
        {
            // The close emptied the slot first: nothing was queued.
        }
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

        public static Rig Create(Action<AcpOptions>? configure = null, Action<FakeAgentSession>? scriptClean = null)
        {
            FakeAgentSession warm = new();
            FakePersonaHost host = new(warm, Profile()) { CanResume = true };
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

    private static AdapterProfile Profile() => new(
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
}

namespace Agency.Huddle.Tests.Acp.Sessions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Sessions.Fakes;
using Agency.Huddle.Tests.Elicitation;
using Agency.Huddle.Tests.Waiting;

/// <summary>
/// Pins Spec §7 for <see cref="RoomSession"/> (corrections D5, 5.3): a fresh start deletes the old Adapter session in the
/// background after closing it, a failure is a Warning that never fails the fresh start, and only a fresh start deletes.
/// </summary>
public sealed class DeleteOnStartAfreshTests
{
    private static readonly Persona Nova = new("nova", "You are Nova.", Model: "m1", Effort: "e1");

    /// <summary>An idle fresh start deletes the old session once, and only after it was disposed.</summary>
    [Fact]
    public async Task FreshStart_Idle_DeletesTheOldSession_AfterClosingIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using DeleteRig rig = DeleteRig.Create();
        await rig.RunFirstTurnAsync(ct);

        await rig.Room.RequestFreshStartAsync(ct);
        await Eventually.UntilAsync(() => rig.First.DeleteCalls == 1, ct, timeout: TestBudgets.Standard);

        Assert.True(rig.First.DisposedAtDelete);
        Assert.True(rig.First.Disposed);
    }

    /// <summary>A fresh start that was pending behind a busy Turn deletes the old session too, after closing it.</summary>
    [Fact]
    public async Task FreshStart_PendingBehindBusyTurn_DeletesTheOldSession_AfterClosingIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using DeleteRig rig = DeleteRig.Create();
        rig.First.EnqueueGatedReply(gate.Task, "one");
        rig.Room.Enqueue(Item(1));
        await Eventually.UntilAsync(() => rig.First.Prompts.Count == 1, ct, timeout: TestBudgets.Standard);
        UserInputLease? lease = rig.Room.TryBeginUserInput();
        Assert.NotNull(lease);
        await rig.Room.RequestFreshStartAsync(ct);
        lease.Dispose();
        rig.Room.Enqueue(Item(2));
        gate.SetResult();

        await Eventually.UntilAsync(() => rig.First.DeleteCalls == 1 && rig.Host.Sessions.Count == 2, ct, timeout: TestBudgets.Standard);

        Assert.True(rig.First.DisposedAtDelete);
        Assert.Equal(2, rig.Host.Sessions.Count);
    }

    /// <summary>A delete that is still held never delays the fresh start: the new session opens and serves the next Turn.</summary>
    [Fact]
    public async Task FreshStart_DeleteHeld_NewSessionStillOpensAndServesTheNextTurn()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TaskCompletionSource deleteGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using DeleteRig rig = DeleteRig.Create();
        rig.First.DeleteGate = deleteGate;
        try
        {
            await rig.RunFirstTurnAsync(ct);

            await rig.Room.RequestFreshStartAsync(ct);
            rig.Room.Enqueue(Item(2));
            await Eventually.UntilAsync(() => rig.PostCount == 2 && rig.Host.Sessions.Count == 2 && rig.First.DeleteCalls == 1, ct, timeout: TestBudgets.Standard);

            Assert.False(deleteGate.Task.IsCompleted);
            Assert.Equal(2, rig.Host.Sessions.Count);
        }
        finally
        {
            deleteGate.TrySetResult();
        }
    }

    /// <summary>Disposing the Room Session waits for a delete that is still held, and finishes once it is released.</summary>
    [Fact]
    public async Task DisposeAsync_DeleteHeld_WaitsForTheDelete()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TaskCompletionSource deleteGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        DeleteRig rig = DeleteRig.Create();
        rig.First.DeleteGate = deleteGate;
        try
        {
            await rig.RunFirstTurnAsync(ct);
            await rig.Room.RequestFreshStartAsync(ct);
            await Eventually.UntilAsync(() => rig.First.DeleteCalls == 1, ct, timeout: TestBudgets.Standard);

            Task disposing = rig.DisposeAsync().AsTask();
            await Assert.ThrowsAsync<TimeoutException>(() => disposing.WaitAsync(TimeSpan.FromMilliseconds(300), ct));
            deleteGate.SetResult();
            await disposing.WaitAsync(TestBudgets.Standard, ct);

            Assert.True(disposing.IsCompletedSuccessfully);
        }
        finally
        {
            deleteGate.TrySetResult();
            await rig.DisposeAsync();
        }
    }

    /// <summary>A dispose racing right behind a fresh start never misses the delete: it is published before the teardown is released.</summary>
    [Fact]
    public async Task DisposeAsync_RightAfterFreshStart_NeverMissesTheDelete()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        for (int round = 0; round < 25; round++)
        {
            TaskCompletionSource deleteGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
            DeleteRig rig = DeleteRig.Create();
            rig.First.DeleteGate = deleteGate;
            try
            {
                await rig.RunFirstTurnAsync(ct);
                await rig.Room.RequestFreshStartAsync(ct);
                Task disposing = rig.DisposeAsync().AsTask();
                await Eventually.UntilAsync(() => rig.First.DeleteCalls == 1, ct, timeout: TestBudgets.Standard);

                Assert.False(disposing.IsCompleted, $"round {round}: dispose finished while the delete was still held");
            }
            finally
            {
                deleteGate.TrySetResult();
                await rig.DisposeAsync();
            }
        }
    }

    /// <summary>Two fresh starts in a row with the first delete held: both sessions are deleted and dispose waits for both.</summary>
    [Fact]
    public async Task FreshStartTwice_FirstDeleteHeld_BothDeleted_AndDisposeWaitsForBoth()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TaskCompletionSource firstGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource secondGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        DeleteRig rig = DeleteRig.Create();
        rig.First.DeleteGate = firstGate;
        try
        {
            await rig.RunFirstTurnAsync(ct);
            await rig.Room.RequestFreshStartAsync(ct);
            rig.Room.Enqueue(Item(2));
            await Eventually.UntilAsync(() => rig.PostCount == 2 && rig.Host.Sessions.Count == 2 && rig.Room.State == RoomSessionState.Idle && rig.Store.Get("nova", "room-1") is not null, ct, timeout: TestBudgets.Standard);
            FakeAgentSession second = rig.Host.Sessions[1];
            second.DeleteGate = secondGate;

            await rig.Room.RequestFreshStartAsync(ct);
            await Eventually.UntilAsync(() => second.DeleteCalls == 1 && rig.First.DeleteCalls == 1, ct, timeout: TestBudgets.Standard);
            Task disposing = rig.DisposeAsync().AsTask();
            secondGate.SetResult();
            await Assert.ThrowsAsync<TimeoutException>(() => disposing.WaitAsync(TimeSpan.FromMilliseconds(300), ct));
            firstGate.SetResult();
            await disposing.WaitAsync(TestBudgets.Standard, ct);

            Assert.True(disposing.IsCompletedSuccessfully);
            Assert.Equal(1, rig.First.DeleteCalls);
            Assert.Equal(1, second.DeleteCalls);
        }
        finally
        {
            firstGate.TrySetResult();
            secondGate.TrySetResult();
            await rig.DisposeAsync();
        }
    }

    /// <summary>A delete that throws is logged at Warning and never fails the fresh start: the next Turn still runs on the new session.</summary>
    [Fact]
    public async Task FreshStart_DeleteThrows_FreshStartStillCompletes_AndLogsWarning()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RecordingLogger logger = new();
        await using DeleteRig rig = DeleteRig.Create(logger);
        rig.First.DeleteFailure = new InvalidOperationException("adapter said no");
        await rig.RunFirstTurnAsync(ct);

        await rig.Room.RequestFreshStartAsync(ct);
        rig.Room.Enqueue(Item(2));
        await Eventually.UntilAsync(() => rig.PostCount == 2 && logger.AtExactly(LogLevel.Warning).Count == 1, ct, timeout: TestBudgets.Standard);

        Assert.Equal(1, rig.First.DeleteCalls);
        Assert.DoesNotContain(nameof(IRoomSessionOwner.ReportTurnFailure), rig.Owner.ReportCalls);
        Assert.Equal("Persona 'nova' could not delete its old session in room room-1.", logger.AtExactly(LogLevel.Warning)[0].Message);
    }

    /// <summary>A second delete that is cancelled still waits for the first delete it was chained behind, so dispose waits for both.</summary>
    [Fact]
    public async Task FreshStartTwice_SecondDeleteCancelled_DisposeStillWaitsForTheFirst()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TaskCompletionSource firstGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        DeleteRig rig = DeleteRig.Create();
        rig.First.DeleteGate = firstGate;
        try
        {
            await rig.RunFirstTurnAsync(ct);
            await rig.Room.RequestFreshStartAsync(ct);
            rig.Room.Enqueue(Item(2));
            await Eventually.UntilAsync(() => rig.PostCount == 2 && rig.Host.Sessions.Count == 2 && rig.Room.State == RoomSessionState.Idle && rig.Store.Get("nova", "room-1") is not null, ct, timeout: TestBudgets.Standard);
            FakeAgentSession second = rig.Host.Sessions[1];
            second.DeleteFailure = new OperationCanceledException();

            await rig.Room.RequestFreshStartAsync(ct);
            await Eventually.UntilAsync(() => second.DeleteCalls == 1 && rig.First.DeleteCalls == 1, ct, timeout: TestBudgets.Standard);
            Task disposing = rig.DisposeAsync().AsTask();
            await Assert.ThrowsAsync<TimeoutException>(() => disposing.WaitAsync(TimeSpan.FromMilliseconds(300), ct));
            firstGate.SetResult();
            await disposing.WaitAsync(TestBudgets.Standard, ct);

            Assert.True(disposing.IsCompletedSuccessfully);
        }
        finally
        {
            firstGate.TrySetResult();
            await rig.DisposeAsync();
        }
    }

    /// <summary>Closing an idle session for eviction is not a fresh start, so it deletes nothing, and a fresh start after it forgets the stored id only.</summary>
    [Fact]
    public async Task Eviction_ThenFreshStart_LeavesDeleteCallsZero()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        DeleteRig rig = DeleteRig.Create();
        await rig.RunFirstTurnAsync(ct);

        await rig.Room.CloseAsync();
        await rig.Room.RequestFreshStartAsync(ct);
        await rig.DisposeAsync();

        Assert.Null(rig.Store.Get("nova", "room-1"));

        Assert.True(rig.First.Disposed);
        Assert.Equal(0, rig.First.DeleteCalls);
    }

    /// <summary>The repeated-failure close forgets the stored entry but deletes nothing.</summary>
    [Fact]
    public async Task RepeatedFailureClose_LeavesDeleteCallsZero()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        DeleteRig rig = DeleteRig.Create();
        rig.First.EnqueueFailure(new InvalidOperationException("boom 1"));
        rig.First.EnqueueFailure(new InvalidOperationException("boom 2"));
        rig.Room.Enqueue(Item(1));
        await Eventually.UntilAsync(() => rig.Owner.ReportCalls.Count(static call => call == nameof(IRoomSessionOwner.ReportTurnFailure)) == 1, ct, timeout: TestBudgets.Standard);
        rig.Room.Enqueue(Item(2));
        await Eventually.UntilAsync(() => rig.Room.State == RoomSessionState.Closed, ct, timeout: TestBudgets.Standard);
        await rig.DisposeAsync();

        Assert.True(rig.First.Disposed);
        Assert.Equal(0, rig.First.DeleteCalls);
    }

    /// <summary>Disposing the Room Session closes the open session but deletes nothing.</summary>
    [Fact]
    public async Task DisposeAsync_LeavesDeleteCallsZero()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        DeleteRig rig = DeleteRig.Create();
        await rig.RunFirstTurnAsync(ct);

        await rig.DisposeAsync();

        Assert.True(rig.First.Disposed);
        Assert.Equal(0, rig.First.DeleteCalls);
    }

    private static QueuedWork Item(long sequence) =>
        new(sequence, new WorkItem("room-1", "Room 1", "Human", "message " + sequence, [], TriggerMessageId: "trig-" + sequence));

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

    /// <summary>One per-Room <see cref="RoomSession"/> over a fake host and a real store, built with a caller-chosen logger and torn down by cancelling its run token first.</summary>
    private sealed class DeleteRig : IAsyncDisposable
    {
        private readonly TempDataDir dataDir;
        private readonly RoomSessionHarness harness;
        private bool disposed;

        private DeleteRig(TempDataDir dataDir, RoomSessionHarness harness, FakePersonaHost host, FakeRoomSessionOwner owner, RoomSessionStore store, FakeAgentSession first)
        {
            this.dataDir = dataDir;
            this.harness = harness;
            this.Host = host;
            this.Owner = owner;
            this.Store = store;
            this.First = first;
        }

        public RoomSession Room => this.harness.Room;

        public FakePersonaHost Host { get; }

        public FakeRoomSessionOwner Owner { get; }

        public RoomSessionStore Store { get; }

        public FakeAgentSession First { get; }

        public int PostCount => this.Owner.Written.OfType<PostMessage>().Count();

        /// <summary>Builds the rig.</summary>
        /// <param name="logger">The logger the Room Session writes to, or <see langword="null"/> for none.</param>
        /// <returns>The rig, to be disposed.</returns>
        public static DeleteRig Create(ILogger? logger = null)
        {
            TempDataDir dataDir = new();
            RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
            FakeAgentSession first = new();
            FakePersonaHost host = new(first, Profile())
            {
                CanResume = true,
                ResumeHandler = _ => first,
                OnOpen = session =>
                {
                    if (!ReferenceEquals(session, first))
                    {
                        session.EnqueueReply("two");
                    }
                },
            };
            FakeRoomSessionOwner owner = new()
            {
                ReadTranscriptHandler = static (_, _, _, _, _) => Task.FromResult<TranscriptTail?>(null),
            };
            CancellationTokenSource runCts = new();
            RoomSession room = new(
                roomId: "room-1",
                open: host.OpenAsync,
                owner: owner,
                scheduler: new ImmediateTurnScheduler(),
                prompts: new FakePromptSource(),
                options: new AcpOptions(),
                logger: logger ?? NullLogger.Instance,
                runToken: runCts.Token,
                persona: Nova,
                agentId: "agent-1",
                contextFill: new ContextFill(),
                roomSessions: store,
                host: host);
            return new DeleteRig(dataDir, RoomSessionHarness.Adopt(room, runCts), host, owner, store, first);
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
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            await this.harness.DisposeAsync();
            this.dataDir.Dispose();
        }
    }
}

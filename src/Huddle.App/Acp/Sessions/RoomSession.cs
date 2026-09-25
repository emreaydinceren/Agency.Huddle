using System.Globalization;
using System.Text;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Acp.Sessions;

/// <summary>Where one Room Session is in its life.</summary>
internal enum RoomSessionState
{
    /// <summary>No <see cref="IAgentSession"/> is open. The next dequeued item opens one.</summary>
    Closed,

    /// <summary>An open is in flight.</summary>
    Opening,

    /// <summary>Open, with no Turn running.</summary>
    Idle,

    /// <summary>Open, running one Turn.</summary>
    Busy,
}

/// <summary>
/// One Agent's working context for one Room: its queue, its session and its Turns (RS §6.1).
/// <c>RoomId == null</c> means a <b>shared</b> session serving every Room the Agent is in - Phase 0/1's
/// one <see cref="RoomSession"/> per Persona. <see cref="ProcessWorkItemAsync"/>,
/// <see cref="WatchForAdapterSilenceAsync"/>, <see cref="RunEventReaderAsync"/> and
/// <see cref="BuildPrompt"/> moved here from <see cref="PersonaRunner"/> with their comments and
/// both traps intact (<c>docs/agencyteam/rules.md</c>, "A Turn ends four ways").
/// </summary>
internal sealed class RoomSession : IAsyncDisposable
{
    private const int MaxDescriptionLength = 200;

    // Bounds how long the idle-timeout watchdog waits for IAgentSession.CancelAsync to reach the far
    // side (TRAP 2) before giving up and cancelling the Turn's own token anyway. See PersonaRunner's
    // former copy of this comment: an adapter that will not even take a cancel is the same adapter
    // that sent nothing in the first place, so this is short.
    private static readonly TimeSpan AdapterCancelGrace = TimeSpan.FromSeconds(5);

    private readonly Func<CancellationToken, Task<IAgentSession>> open;
    private readonly IRoomSessionOwner owner;
    private readonly ITurnScheduler scheduler;
    private readonly IPromptSource prompts;
    private readonly AcpOptions options;
    private readonly FileChangeTracker? fileChanges;
    private readonly IReadOnlyList<string> declaredWatches;
    private readonly ILogger logger;
    private readonly CancellationToken runToken;
    private readonly TimeProvider time;

    // D24: resume (RS §6.1) and the Transcript Catch-up cursor. Only ever consulted for a per-Room
    // session (RoomId not null) with all three present - a shared session or a caller that predates
    // D24 (persona/roomSessions/host all null, the pre-D24 default) simply never resumes and never
    // reads a Transcript, unchanged from before this task.
    private readonly Persona? persona;
    private readonly RoomSessionStore? roomSessionStore;
    private readonly IPersonaHost? host;

    // D27, RS §6.7, finding P-7: marks this session's own Room (or every Room, in shared mode) Busy
    // for the Turn's own duration, so a post OTHER of this Agent's own sessions makes into it while
    // it runs is not recorded - this session already knows what happened here. Both null for a
    // caller that predates D27 (P-13's "absent means off"), or when this session has no known
    // agentId yet (unreachable in practice: PersonaRunner always has one before it builds a pool).
    private readonly OwnPosts? ownPosts;
    private readonly string? agentId;

    // Spec §10.8: records which Room this session's Agent has a Turn running in, for the Tasks UI's
    // "AI reacting" badge. Both null for a caller that predates it, the same "absent means off"
    // reasoning as ownPosts just above.
    private readonly TurnActivity? turnActivity;

    // Guards every field below: the queue, the state machine, the Stop marks, activeTurn and
    // pendingStopCancel - one lock, exactly as turnLock was on PersonaRunner, for the same reason:
    // the Stop path must publish its mark and read activeTurn as one atomic step against the
    // consumer's own re-check (RS §6.1 step 2, D22 correction 3).
    private readonly Lock gate = new();
    private readonly Queue<QueuedWork> queue = new();
    private readonly SemaphoreSlim itemAvailable = new(0);

    // Per-Room Stop marks (finding P-6): the sequence value a Stop in that Room recorded most
    // recently. A missing key means that Room was never stopped. RS Appendix A's RS-I7 describes
    // this as retiring Phase 0's own per-Room mark (P0-I1); it is not retired, it MOVED here and
    // stays in both modes - a shared session (RoomId null) still serves every Room the Agent is in
    // through this one queue, so the mark is what keeps a Stop scoped to the one Room the Human
    // asked to stop rather than dropping every Room's queued work. A per-Room session (D25) needs it
    // too, if only trivially: it happens to hold exactly one Room, but the mark is still what a
    // queued item's own re-check (IsStoppedAtOrBefore) reads.
    private readonly Dictionary<string, long> stopMarks = new(StringComparer.Ordinal);

    private RoomSessionState state = RoomSessionState.Closed;
    private DateTimeOffset lastActivity;
    private IAgentSession? session;
    private Task? openTask;
    private readonly Task consumerTask;
    private Task? eventReaderTask;
    private CancellationTokenSource? eventReaderCts;
    private ActiveTurn? activeTurn;
    private Task? pendingStopCancel;
    private bool running;
    private long lastUsed;

    // D24: consulted and cleared only from ProcessWorkItemAsync, which the single consumer thread
    // always calls after the open that set them (OpenCoreAsync happens-before ProcessWorkItemAsync
    // in RunConsumerAsync's own sequence), so these need no lock of their own - the same reasoning
    // knownRoomNames documents on PersonaRunner.
    private bool firstTurnPending;
    private bool resumedOnLastOpen;
    private string? resumedAfterMessageId;

    // Set alongside firstTurnPending/resumedOnLastOpen; consumed by RunEventReaderAsync's own
    // single-threaded loop, the same unlocked-by-construction reasoning lastUsed itself already
    // relies on (D22 correction 9, built here for D24).
    private bool usageBaselinePending;

    // D26, RS §6.8, finding P-14: this Room Session's OWN consecutive-failure count - separate from
    // the Persona-wide one IRoomSessionOwner.ReportTurnFailure tracks - counted only in per-Room mode
    // (RoomId not null) and only for what actually called ReportTurnFailure: an open/resume failure
    // (RunConsumerAsync's own catch) or a Turn that failed or timed out (ProcessWorkItemAsync's own
    // catches). AgentDisconnectedException does not touch it (RS §9 E-10: "entries are kept"), and
    // neither does a Stop (P-6: "neither counts nor resets"). Touched only by this session's single
    // consumer thread - the same reasoning firstTurnPending relies on - so it needs no lock.
    private int roomConsecutiveFailures;

    /// <summary>Initializes a new instance of the <see cref="RoomSession"/> class.</summary>
    /// <param name="roomId">The Room this session belongs to, or <see langword="null"/> for a shared session serving every Room.</param>
    /// <param name="open">Opens a fresh <see cref="IAgentSession"/>.</param>
    /// <param name="owner">What this session reports Turn outcomes, tokens and health through.</param>
    /// <param name="scheduler">The ticket protocol this session's consumer runs its Turns through.</param>
    /// <param name="prompts">Resolves every <c>turn.*</c> prompt's current text.</param>
    /// <param name="options">The Persona's current ACP options, read live for the idle-timeout bound.</param>
    /// <param name="fileChanges">The Persona's File Changes tracker, or <see langword="null"/> when File Changes is off.</param>
    /// <param name="declaredWatches">The Persona's declared Watched Folders.</param>
    /// <param name="logger">Where this session logs.</param>
    /// <param name="runToken">The runner's own run token; cancelled means shutdown, never a Stop.</param>
    /// <param name="time">
    /// Drives <see cref="LastActivity"/> (D23), so a test can advance it without waiting out real
    /// wall-clock minutes. Defaults to <see cref="TimeProvider.System"/> when omitted, so every
    /// pre-D23 caller keeps compiling and behaving unchanged.
    /// </param>
    /// <param name="persona">The Persona this session belongs to (D24): its Model and Effort gate a resume. <see langword="null"/> disables resume, like every pre-D24 caller.</param>
    /// <param name="roomSessions">Backs resume and the Transcript Catch-up cursor (RS §6.1, §6.6). <see langword="null"/> disables storing and resuming.</param>
    /// <param name="host">Supplies <see cref="IPersonaHost.CanResume"/>, <see cref="AdapterProfile.Id"/> and <see cref="IPersonaHost.ResumeAsync"/> for the resume decision. <see langword="null"/> disables resume.</param>
    /// <param name="ownPosts">Marks this session's Room Busy for a Turn's own duration (D27, RS §6.7). <see langword="null"/> disables it, like every pre-D27 caller.</param>
    /// <param name="agentId">This session's owning Agent's id, passed to <paramref name="ownPosts"/> and <paramref name="turnActivity"/>. <see langword="null"/> disables it, like every pre-D27 caller.</param>
    /// <param name="turnActivity">Records which Room this Agent has a Turn running in (Spec §10.8). <see langword="null"/> disables it, like every caller that predates it.</param>
    public RoomSession(
        string? roomId,
        Func<CancellationToken, Task<IAgentSession>> open,
        IRoomSessionOwner owner,
        ITurnScheduler scheduler,
        IPromptSource prompts,
        AcpOptions options,
        FileChangeTracker? fileChanges,
        IReadOnlyList<string> declaredWatches,
        ILogger logger,
        CancellationToken runToken,
        TimeProvider? time = null,
        Persona? persona = null,
        RoomSessionStore? roomSessions = null,
        IPersonaHost? host = null,
        OwnPosts? ownPosts = null,
        string? agentId = null,
        TurnActivity? turnActivity = null)
    {
        ArgumentNullException.ThrowIfNull(open);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(prompts);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(declaredWatches);
        ArgumentNullException.ThrowIfNull(logger);

        this.RoomId = roomId;
        this.open = open;
        this.owner = owner;
        this.scheduler = scheduler;
        this.prompts = prompts;
        this.options = options;
        this.fileChanges = fileChanges;
        this.declaredWatches = declaredWatches;
        this.logger = logger;
        this.runToken = runToken;
        this.time = time ?? TimeProvider.System;
        this.lastActivity = this.time.GetUtcNow();
        this.persona = persona;
        this.roomSessionStore = roomSessions;
        this.host = host;
        this.ownPosts = ownPosts;
        this.agentId = agentId;
        this.turnActivity = turnActivity;

        this.consumerTask = this.RunConsumerAsync(runToken);
    }

    /// <summary>The Room this session belongs to, or <see langword="null"/> for a shared session.</summary>
    internal string? RoomId { get; }

    /// <summary>Where the session is in its life.</summary>
    internal RoomSessionState State
    {
        get
        {
            lock (this.gate)
            {
                return this.state;
            }
        }
    }

    /// <summary>When the last Turn here ended, or the session opened. Drives eviction (D23).</summary>
    internal DateTimeOffset LastActivity
    {
        get
        {
            lock (this.gate)
            {
                return this.lastActivity;
            }
        }
    }

    /// <summary>How many items are currently queued, not counting one in flight.</summary>
    internal int QueueCount
    {
        get
        {
            lock (this.gate)
            {
                return this.queue.Count;
            }
        }
    }

    /// <summary>The models the open session advertises, or empty while <see cref="RoomSessionState.Closed"/>.</summary>
    internal IReadOnlyList<AgentModelOption> Models => this.session?.Models ?? [];

    /// <summary>Queues a Turn built by the read loop, with its Catch-up already taken.</summary>
    /// <param name="item">The Turn to run, with its sequence number.</param>
    internal void Enqueue(QueuedWork item)
    {
        ArgumentNullException.ThrowIfNull(item);

        lock (this.gate)
        {
            this.queue.Enqueue(item);

            // RS §6.1 ticket protocol rule 1: offered exactly once, by whoever makes it the head. An
            // enqueue offers only when this item became the head with nothing already running - a
            // busy consumer, or a queue that already had a head, offers its next item itself, from
            // its own finally (rule 4).
            if (this.queue.Count == 1 && !this.running)
            {
                this.scheduler.Offer(item.Sequence);
            }
        }

        this.itemAvailable.Release();
    }

    /// <summary>Opens this session's <see cref="IAgentSession"/>, idempotently: a second call while already open or opening awaits the same result.</summary>
    /// <param name="cancellationToken">Cancels the open.</param>
    internal Task OpenAsync(CancellationToken cancellationToken) => this.OpenCoreAsync(cancellationToken);

    /// <summary>
    /// Atomically transitions this session <see cref="RoomSessionState.Closed"/> to
    /// <see cref="RoomSessionState.Opening"/>, one compare-and-set under <see cref="gate"/> (D23
    /// correction 12, P-5 invariant 2). Called by <c>RoomSessionPool.MakeRoomToOpenAsync</c> under
    /// its own pool lock, so the capacity check and this requester's own transition to "live" happen
    /// as one atomic step relative to any other concurrent requester (P-5 invariant 3). The lock
    /// order is always pool → session → this <see cref="gate"/>, never the reverse.
    /// </summary>
    /// <returns><see langword="true"/> if this call performed the transition; <see langword="false"/> if the session was not <see cref="RoomSessionState.Closed"/>.</returns>
    internal bool TryMarkOpening()
    {
        lock (this.gate)
        {
            if (this.state != RoomSessionState.Closed)
            {
                return false;
            }

            this.state = RoomSessionState.Opening;
            return true;
        }
    }

    /// <summary>Ends this Room's live Turn and clears this Room's queue, and nothing else (finding P-6).</summary>
    /// <param name="roomId">The Room the Human asked to stop.</param>
    /// <param name="mark">The Persona-wide sequence value as of this Stop; every queued item at or below it, in this Room, is dropped without spending anything.</param>
    /// <param name="cancellationToken">Passed to the far side's cancel.</param>
    internal Task StopAsync(string roomId, long mark, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);

        ActiveTurn? turn;
        IAgentSession? activeSession;
        lock (this.gate)
        {
            // The mark is taken before anything else, in the same lock that publishes activeTurn in
            // ProcessWorkItemAsync and that the consumer's own re-check reads under: anything already
            // queued for this Room (sequence at or below it) must drain without spending anything.
            this.stopMarks[roomId] = mark;
            turn = this.activeTurn;
            activeSession = this.session;
        }

        // Only this Room's own live Turn is stopped (finding P-6): a Stop in one Room must never
        // touch another Room's Turn, which under a shared session is otherwise indistinguishable from
        // this one except by RoomId.
        if (turn is null || !string.Equals(turn.RoomId, roomId, StringComparison.Ordinal) || activeSession is null)
        {
            return Task.CompletedTask;
        }

        // Set BEFORE the cancellation below, so the consumer can tell this Stop apart from the
        // idle-timeout watchdog firing: both arrive there as the same OperationCanceledException, and
        // only one of them is a failure (TRAP 1).
        turn.MarkStopRequested();

        // Unblocks ProcessWorkItemAsync's await on this Turn immediately, without waiting on the
        // agent process to acknowledge the cancel notification below. A plain Cancel(), not
        // CancelAsync(), so this call - and the read loop awaiting it - returns at once.
        turn.Cancellation.Cancel();

        // Safe from any thread: locks internally and no-ops when no prompt is in flight. NOT awaited
        // here - a queued Turn in a different Room must not reach PromptAsync on the shared session
        // while this cancel is still in flight either, so the task is handed to the consumer, which
        // awaits it (via AwaitPendingStopCancelAsync) before building its next Turn.
        var cancelTask = activeSession.CancelAsync(cancellationToken);
        lock (this.gate)
        {
            this.pendingStopCancel = cancelTask;
        }

        return Task.CompletedTask;
    }

    /// <summary>Sends <c>session/close</c> and keeps the stored id, so the session can be resumed. Only acts while <see cref="RoomSessionState.Idle"/>.</summary>
    internal async Task CloseAsync()
    {
        IAgentSession? closing;
        CancellationTokenSource? readerCts;
        Task? readerTask;
        lock (this.gate)
        {
            if (this.state != RoomSessionState.Idle)
            {
                return;
            }

            this.state = RoomSessionState.Closed;
            closing = this.session;
            readerCts = this.eventReaderCts;
            readerTask = this.eventReaderTask;
            this.session = null;
        }

        if (readerCts is not null)
        {
            // Cancels the event reader's OWN token before the session is disposed below, so its end
            // is not mistaken for an unexpected loop death and reported as Offline (D22 correction 7).
            await readerCts.CancelAsync();
        }

        if (readerTask is not null)
        {
            await SafeAwaitAsync(readerTask);
        }

        if (closing is not null)
        {
            await closing.DisposeAsync();
        }

        readerCts?.Dispose();
    }

    /// <summary>
    /// D26, RS §6.8, finding P-14: the same teardown <see cref="CloseAsync"/> does - cancel the event
    /// reader's own token, await it, dispose the agent session - but callable from
    /// <see cref="RoomSessionState.Busy"/> rather than only <see cref="RoomSessionState.Idle"/>: the
    /// consumer calling this still holds the failed Turn's own admitted ticket, and moving straight
    /// to <see cref="RoomSessionState.Closed"/> here, before that ticket completes, is what P-5
    /// invariant 1 requires. Safe to call when nothing is open (an open/resume failure already left
    /// this <see cref="RoomSessionState.Closed"/> with no session): the guard below simply no-ops the
    /// teardown while still stamping <see cref="RoomSessionState.Closed"/>.
    /// </summary>
    private async Task CloseAfterRepeatedFailureAsync()
    {
        IAgentSession? closing;
        CancellationTokenSource? readerCts;
        Task? readerTask;
        lock (this.gate)
        {
            closing = this.session;
            readerCts = this.eventReaderCts;
            readerTask = this.eventReaderTask;
            this.session = null;
            this.state = RoomSessionState.Closed;
        }

        if (closing is null)
        {
            return;
        }

        if (readerCts is not null)
        {
            await readerCts.CancelAsync();
        }

        if (readerTask is not null)
        {
            await SafeAwaitAsync(readerTask);
        }

        await closing.DisposeAsync();
        readerCts?.Dispose();
    }

    /// <summary>Ends this session's consumer, its event reader, and disposes any open <see cref="IAgentSession"/>, in that order.</summary>
    public async ValueTask DisposeAsync()
    {
        await SafeAwaitAsync(this.consumerTask);
        await SafeAwaitAsync(this.eventReaderTask);

        if (this.session is not null)
        {
            await this.session.DisposeAsync();
        }

        this.eventReaderCts?.Dispose();
        this.itemAvailable.Dispose();
    }

    /// <summary>The unlocked core behind <see cref="OpenAsync"/>, reused by the consumer's own "open if Closed" step (RS §6.1 step 2).</summary>
    private async Task OpenCoreAsync(CancellationToken cancellationToken)
    {
        Task open;
        lock (this.gate)
        {
            if (this.state is RoomSessionState.Idle or RoomSessionState.Busy)
            {
                return;
            }

            if (this.openTask is { } inFlight)
            {
                open = inFlight;
            }
            else
            {
                this.state = RoomSessionState.Opening;
                open = this.OpenAndStartReaderAsync(cancellationToken);
                this.openTask = open;
            }
        }

        try
        {
            await open;
        }
        finally
        {
            lock (this.gate)
            {
                this.openTask = null;
            }
        }
    }

    /// <summary>Calls the injected opener (or resumes, RS §6.1), starts this session's event reader, and publishes the resulting state.</summary>
    private async Task OpenAndStartReaderAsync(CancellationToken cancellationToken)
    {
        try
        {
            var (opened, resumed, resumedAfterId) = await this.OpenOrResumeAsync(cancellationToken);
            var readerCts = CancellationTokenSource.CreateLinkedTokenSource(this.runToken);
            lock (this.gate)
            {
                this.session = opened;
                this.eventReaderCts = readerCts;

                // A lazy open runs inside an admitted Turn (running is already set), and the
                // consumer's admission step only promotes Idle to Busy - it left this session
                // Closed. Publishing Idle here would run that whole first Turn looking evictable,
                // and RoomSessionPool.PickVictim would close it mid-Turn for another Room's open.
                this.state = this.running ? RoomSessionState.Busy : RoomSessionState.Idle;
                this.lastActivity = this.time.GetUtcNow();
                this.lastUsed = 0;
            }

            // D24: this open's first Turn (a Message carrying a TriggerMessageId, per-Room only)
            // reads the Transcript - resumedAfterId only when this open actually resumed, so a
            // fresh open (including a "not found" fallback, RS §9 E-1) reads the latest Messages
            // instead. usageBaselinePending mirrors resumed: a resumed session's first
            // UsageUpdated reports its whole restored context, not tokens this Turn spent (D22
            // correction 9).
            this.firstTurnPending = true;
            this.resumedOnLastOpen = resumed;
            this.resumedAfterMessageId = resumedAfterId;
            this.usageBaselinePending = resumed;

            // D23 correction 17 / D24 correction 23: reported on every open, not only the one
            // StartAsync makes at start-up, so a per-Room Persona with no Human Room still gets
            // the warning the first time any of its Rooms opens.
            this.owner.ReportModels(opened.Models);

            this.eventReaderTask = this.RunEventReaderAsync(opened, readerCts.Token);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // A start-up open failure escapes to the caller and leaves the session Closed, not
            // Opening (D22 correction 6). Covers both a fresh open and a resume (RS §9 E-2: "a
            // Turn failure. The entry is kept").
            lock (this.gate)
            {
                this.state = RoomSessionState.Closed;
            }

            throw;
        }
    }

    /// <summary>
    /// RS §6.1 "Opening": resumes by the stored id when this is a per-Room session with a stored
    /// entry whose Adapter, Model and Effort all still match the Persona's current ones and the host
    /// advertises <see cref="IPersonaHost.CanResume"/>; opens fresh otherwise, including when
    /// <see cref="IPersonaHost.ResumeAsync"/> itself answers "not found" (<see langword="null"/>, RS
    /// §9 E-1). A resume that throws is left to propagate - the caller's own catch reports it as an
    /// open failure and keeps the entry (RS §9 E-2), never falling back to a fresh open.
    /// </summary>
    /// <param name="cancellationToken">Cancels the open or resume.</param>
    /// <returns>The opened session, whether it was resumed, and - only when resumed - the stored <see cref="RoomSessionEntry.LastMessageId"/> the Transcript Catch-up range starts after.</returns>
    private async Task<(IAgentSession Session, bool Resumed, string? ResumedAfterMessageId)> OpenOrResumeAsync(CancellationToken cancellationToken)
    {
        if (this.RoomId is { } roomId && this.roomSessionStore is not null && this.host is not null && this.persona is not null)
        {
            var entry = this.roomSessionStore.Get(this.owner.PersonaName, roomId);
            if (entry is not null
                && this.host.CanResume
                && string.Equals(entry.AdapterId, this.host.Profile.Id, StringComparison.OrdinalIgnoreCase)
                && string.Equals(entry.Model, this.persona.Model, StringComparison.Ordinal)
                && string.Equals(entry.Effort, this.persona.Effort, StringComparison.Ordinal))
            {
                var resumedSession = await this.host.ResumeAsync(entry.SessionId, cancellationToken);
                if (resumedSession is not null)
                {
                    return (resumedSession, true, entry.LastMessageId);
                }
            }
        }

        var opened = await this.open(cancellationToken);
        return (opened, false, null);
    }

    private async Task RunConsumerAsync(CancellationToken ct)
    {
        Exception? terminatingException = null;
        try
        {
            while (true)
            {
                await this.itemAvailable.WaitAsync(ct);

                QueuedWork head;
                lock (this.gate)
                {
                    head = this.queue.Peek();
                }

                try
                {
                    await this.scheduler.WaitAsync(head.Sequence, ct);
                }
                catch (OperationCanceledException)
                {
                    // Shutdown: the head was offered but will never run (RS §6.1 step 5).
                    this.scheduler.Withdraw(head.Sequence);
                    throw;
                }

                lock (this.gate)
                {
                    this.queue.Dequeue();
                    this.running = true;
                    this.state = this.state == RoomSessionState.Idle ? RoomSessionState.Busy : this.state;
                }

                try
                {
                    if (this.IsStoppedAtOrBefore(head.Item.RoomId, head.Sequence))
                    {
                        // Queued in this Room at or before its last Stop: drain it without spending
                        // anything, rather than working through a backlog the Human already asked to
                        // clear for that Room. A different Room's queue is unaffected (finding P-6).
                        continue;
                    }

                    // D22 correction 11: checked here, straight after admission and BEFORE opening -
                    // an item drained for a spent Budget must never pay for opening a session it will
                    // not prompt.
                    if (this.owner.TokenBudgetSpent)
                    {
                        this.owner.ReportTokenBudgetSpent();
                        continue;
                    }

                    // Before building the next Turn, let a still-settling Stop cancel from a
                    // different Room finish reaching the Adapter first - see pendingStopCancel's
                    // comment. This must run even when nothing above was dropped: the very next item
                    // after a Stop is exactly the case that races it.
                    await this.AwaitPendingStopCancelAsync(ct);

                    if (this.State == RoomSessionState.Closed)
                    {
                        // Opening happens inside this admitted slot (finding P-5).
                        await this.scheduler.MakeRoomToOpenAsync(this, ct);

                        try
                        {
                            await this.OpenCoreAsync(ct);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            // D24 correction 20: an open or resume failure on this lazy path is a
                            // Turn failure, not a fatal loop error - OpenAndStartReaderAsync's own
                            // catch already left the state Closed (D22 correction 6). No Turn ever
                            // started for this item, so nothing is minted and no final MessageDelta
                            // is written; the item is simply dropped, like a Stop-marked one.
                            if (ex is AgentDisconnectedException)
                            {
                                // RS §9 E-10: the Adapter process itself is gone, which the streak
                                // already exists to distinguish from a repeating model/quota/network
                                // failure - this does not count toward it, and nothing is forgotten.
                                this.owner.ReportOffline("The Adapter process disconnected.");
                            }
                            else
                            {
                                this.owner.ReportTurnFailure(head.Item.RoomName, ex.Message);

                                // D26, finding P-14: an open/resume failure counts toward THIS
                                // session's own streak; no store.Put ever ran for this item (no Turn
                                // started), so there is no ordering concern - just forget and close.
                                if (this.RoomId is { } openFailedRoomId && ++this.roomConsecutiveFailures >= 2)
                                {
                                    this.roomSessionStore?.Forget(this.owner.PersonaName, openFailedRoomId);
                                    await this.CloseAfterRepeatedFailureAsync();
                                }
                            }

                            this.logger.LogWarning(
                                ex,
                                "Persona '{PersonaName}' failed to open a session for room {RoomId}.",
                                this.owner.PersonaName,
                                head.Item.RoomId);
                            continue;
                        }
                    }

                    await this.ProcessWorkItemAsync(head.Item, head.Sequence, ct);
                }
                finally
                {
                    lock (this.gate)
                    {
                        this.running = false;
                        this.state = this.state == RoomSessionState.Busy ? RoomSessionState.Idle : this.state;
                        this.lastActivity = this.time.GetUtcNow();

                        // RS §6.1 ticket protocol rule 4: if a next item exists, offer it BEFORE
                        // completing the current ticket - finding P-4's cross-Room arrival order
                        // depends on this exact sequence.
                        if (this.queue.Count > 0)
                        {
                            this.scheduler.Offer(this.queue.Peek().Sequence);
                        }
                    }

                    // Complete is safe for a dropped ticket that was admitted.
                    this.scheduler.Complete(head.Sequence);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            // An exception escaping ProcessWorkItemAsync's own finally must not leave this loop
            // silently dead: nothing else drains this session's queue, so a deaf Agent would
            // otherwise sit behind a pipe AgentGateway.IsOnline still reports as up.
            terminatingException = ex;
            this.logger.LogWarning(ex, "Persona '{PersonaName}' consumer loop stopped unexpectedly.", this.owner.PersonaName);
        }
        finally
        {
            if (!ct.IsCancellationRequested)
            {
                this.owner.ReportLoopEnded("consumer loop", terminatingException);
            }
        }
    }

    /// <summary>Whether a Room's Stop mark covers <paramref name="sequence"/>, under <see cref="gate"/>.</summary>
    private bool IsStoppedAtOrBefore(string roomId, long sequence)
    {
        lock (this.gate)
        {
            return this.stopMarks.TryGetValue(roomId, out var mark) && sequence <= mark;
        }
    }

    /// <summary>
    /// Awaits whatever <see cref="pendingStopCancel"/> currently holds, if anything - see
    /// <see cref="StopAsync"/>'s comment for why this must run before the next Turn is built. Any
    /// exception the far side's cancel throws is swallowed here, except cancellation of the run
    /// itself, which must still end this loop the normal way.
    /// </summary>
    /// <param name="ct">The run's own cancellation token, never the stopped Turn's.</param>
    private async Task AwaitPendingStopCancelAsync(CancellationToken ct)
    {
        Task? pending;
        lock (this.gate)
        {
            pending = this.pendingStopCancel;
            this.pendingStopCancel = null;
        }

        if (pending is null)
        {
            return;
        }

        try
        {
            await pending.WaitAsync(ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // The settling cancel may itself fault, or land late; either way it already did its job
            // of latching the Stop and cancelling the Turn token, and this consumer's next Turn - in
            // whatever Room - must proceed regardless of how that cancel ended.
        }
    }

    private async Task ProcessWorkItemAsync(WorkItem item, long sequence, CancellationToken ct)
    {
        // The token-Budget check (D22 correction 11) runs in the consumer, before this method is
        // ever called - and before this session opens - so an item drained for a spent Budget never
        // pays for opening a session it will not prompt.
        //
        // Declared before the try below so the finally block can see them (FC §6.8, finding P-17).
        // collected stays null for a Greeting (FC §6.8: a Greeting neither collects nor commits, the
        // trigger is the prompt itself, not a delivered Message) and for a null tracker (§6.11: the
        // Adapter cannot read files).
        CollectedChanges? collected = null;
        var promptReturned = false;
        var replyPosted = false;

        // D26, finding P-14: latched by this Turn's own failure/timeout catch clauses below, read
        // only in the finally block, after the store.Put segment (correction 8's ordering) - so a
        // second consecutive failure in per-Room mode can close and forget this session without
        // undoing the entry this same Turn just wrote.
        var roomFailureThisTurn = false;

        if (item.Kind == WorkItemKind.Message && this.fileChanges is not null)
        {
            // Collected at Turn start, before the Turn/cancellation/watchdog exists, so a slow disk
            // scan is never counted as Adapter silence (rules.md TRAP 1/TRAP 2 ordering) and so a
            // Turn that waited in the queue lists changes made while it waited. A scan or directory
            // failure must not kill the consumer loop, which nothing else drains.
            try
            {
                collected = await this.fileChanges.CollectAsync(this.owner.PersonaName, item.RoomId, this.declaredWatches, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                this.logger.LogWarning(ex, "Persona '{PersonaName}' failed to collect File Changes for room {RoomId}.", this.owner.PersonaName, item.RoomId);
            }

            item = item with { FileChanges = collected?.Report };
        }

        // D24, RS §6.5: the first Turn after any open (per-Room only) reads the Room's Transcript,
        // after the File Changes collect and before the locked check-and-publish below (D22
        // correction 3) - open and this read must both finish before the watchdog is armed, so
        // neither counts as Adapter silence. Cleared here regardless of outcome: RS §9 E-3 (refused
        // or timed out) and a Greeting or a Turn with no TriggerMessageId all still consume this
        // Turn's "first after open" opportunity (D24 correction 19) without ever reading anything.
        if (this.firstTurnPending)
        {
            this.firstTurnPending = false;
            if (this.RoomId is not null && item.Kind == WorkItemKind.Message && item.TriggerMessageId is { } triggerId)
            {
                TranscriptTail? tail = null;
                try
                {
                    tail = await this.owner.ReadTranscriptAsync(
                        item.RoomId,
                        this.resumedOnLastOpen ? this.resumedAfterMessageId : null,
                        triggerId,
                        this.options.TranscriptCatchUpMessages,
                        ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    this.logger.LogWarning(
                        ex, "Persona '{PersonaName}' Transcript read for room {RoomId} failed.", this.owner.PersonaName, item.RoomId);
                }

                if (tail is not null)
                {
                    // D27 correction 11: own-post lines are dropped only when the Transcript is
                    // actually set on this Turn's WorkItem - that range already holds those posts,
                    // whether or not it turned out to hold any Messages. A refused or timed-out read
                    // (RS §9 E-3, tail null) leaves OwnPostLines untouched, so they still render
                    // through the ordinary catch-up path below.
                    item = item with
                    {
                        Transcript = new TranscriptCatchUp(this.resumedOnLastOpen, tail.Messages, tail.Omitted),
                        OwnPostLines = null,
                    };
                }
            }
        }

        // Exactly one session serves every Room the Agent is in (there is one session per Persona in
        // shared mode), so a failed turn must log and continue rather than end the loop: a dying
        // agent process must not silently deafen the Agent for every other Room.
        //
        // The Message id is minted here, before the prompt is sent, in the same form
        // ChatService.PostAsync uses when its own messageId argument is null: a 32-character
        // lowercase-hex Guid, which NameRules.IsValidId accepts. Carrying it on PostMessage is what
        // lets a ProtocolError's RelatedMessageId, and later a MessageDelta, correlate back to this
        // Turn.
        var messageId = Guid.CreateVersion7().ToString("N");
        var completion = new TaskCompletionSource<TurnOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        var turnCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var turn = new ActiveTurn(item.RoomId, messageId, new StringBuilder(), completion, turnCancellation);
        IAgentSession activeSession;
        lock (this.gate)
        {
            // Re-checked here, not only by the consumer, because a Stop can arrive while this item
            // sat inside the CollectAsync scan above, after the consumer's own check ran and before
            // any Turn existed for it to cancel. Compared against this Room's own mark in stopMarks,
            // exactly as the consumer's own check does (finding P-6). Inside this lock because
            // StopAsync writes the mark before taking this same lock to read activeTurn, so either
            // this check sees the mark or the Stop sees the Turn once published below. Nothing has
            // been committed on this path.
            if (this.stopMarks.TryGetValue(item.RoomId, out var mark) && sequence <= mark)
            {
                turnCancellation.Dispose();
                return;
            }

            if (this.session is not { } opened)
            {
                // The open-if-Closed step in the consumer must have succeeded before this method is
                // ever called; a null session here means it did not, so there is nothing to prompt.
                turnCancellation.Dispose();
                return;
            }

            activeSession = opened;
            this.activeTurn = turn;
        }

        // The idle bound this Turn is watched against, and the watchdog that enforces it. Armed
        // here, right after the Turn is published, so it covers the whole Turn including the initial
        // PromptAsync call below - a hang before the first event is exactly what motivates this.
        TimeSpan idleBound = IdleTimeoutFrom(this.options.TurnIdleTimeoutSeconds);
        CancellationTokenSource watchdogCancellation = new();
        Task? watchdog = idleBound > TimeSpan.Zero
            ? this.WatchForAdapterSilenceAsync(activeSession, turn, item, idleBound, watchdogCancellation.Token, ct)
            : null;

        if (this.agentId is { } beginTurnAgentId)
        {
            this.ownPosts?.BeginTurn(beginTurnAgentId, this.RoomId);

            // D8 correction 1: this.RoomId is null for a shared session, but the Turn itself always
            // belongs to a real Room - turn.RoomId, captured from item.RoomId above - so TurnActivity
            // is always told the Turn's own Room, never this session's (possibly null) one.
            this.turnActivity?.Begin(beginTurnAgentId, turn.RoomId);
        }

        try
        {
            var prompt = RoomSession.BuildPrompt(item, this.prompts);
            await activeSession.PromptAsync(prompt, turnCancellation.Token);
            promptReturned = true;
            var outcome = await completion.Task.WaitAsync(turnCancellation.Token);

            // A refusal or a cancelled Turn is not a successful reply, even if some text arrived
            // before the stop: neither is fit to post into the Room.
            var isPostable = outcome.Reason is not (StopReason.Refusal or StopReason.Cancelled)
                && !string.IsNullOrWhiteSpace(outcome.Text);
            if (isPostable)
            {
                await this.owner.WriteAsync(new PostMessage(item.RoomId, messageId, outcome.Text), ct);
                replyPosted = true;
            }

            // Health reporting (T4.3): every StopReason but Cancelled is a Turn that completed, so
            // it resets the escalating-failure counter - Cancelled is the Human stopping it, reported
            // as nothing, and the one outcome that must leave the counter untouched.
            switch (outcome.Reason)
            {
                case StopReason.EndTurn:
                    this.owner.ReportTurnCompleted();

                    // D26, RS §6.8: "a completed Turn resets" this session's own streak too, the one
                    // outcome that does.
                    this.roomConsecutiveFailures = 0;
                    break;
                case StopReason.MaxTokens:
                case StopReason.MaxTurnRequests:
                case StopReason.Refusal:
                    this.owner.ReportIncompleteStop(outcome.Reason);
                    break;
                case StopReason.Cancelled:
                    break;
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && !turn.StopRequested && turn.TimedOut)
        {
            // The idle-timeout watchdog fired, not a Human Stop (TRAP 1): both cancel the same Turn
            // token and land here as the identical OperationCanceledException, so the filter checks
            // the latches each producer writes BEFORE it cancels, not the exception. A genuine Stop
            // wins any tie - only StopAsync sets StopRequested, and this filter demands it be absent -
            // because a Human's own Stop must never be reported as a failure.
            this.owner.ReportTurnFailure(
                item.RoomName,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the Adapter sent nothing for {idleBound.TotalSeconds} seconds, so the Turn was abandoned and cancelled"));
            roomFailureThisTurn = true;
            this.logger.LogWarning(
                "Persona '{PersonaName}' turn in room {RoomId} was abandoned after {IdleBoundSeconds} seconds of silence.",
                this.owner.PersonaName,
                item.RoomId,
                idleBound.TotalSeconds);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // The Human stopped this Turn; the run itself is not shutting down. A Turn ends in one
            // of four ways - completed, stopped, timed out, or failed - and a stopped one is a
            // normal outcome, never a failure: logged at Information, its partial text discarded
            // rather than saved, and no Message posted. The exception type alone cannot tell a Stop
            // apart from shutdown - both surface as OperationCanceledException - so the run token,
            // not the exception, is what is checked above.
            if (this.logger.IsEnabled(LogLevel.Information))
            {
                this.logger.LogInformation(
                    "Persona '{PersonaName}' turn in room {RoomId} was stopped.",
                    this.owner.PersonaName,
                    item.RoomId);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (AgentDisconnectedException ex)
        {
            this.owner.ReportOffline("The Adapter process disconnected.");
            this.logger.LogWarning(ex, "Persona '{PersonaName}' failed to process a turn in room {RoomId}.", this.owner.PersonaName, item.RoomId);
        }
        catch (Exception ex)
        {
            // Quota, a network failure and expiring credentials all arrive as the same AgentException
            // ("session/prompt failed: ..."), and its wording belongs to the Adapter and will change -
            // so this escalates on repetition alone, never on message content. Stays Degraded rather
            // than Offline: the session and the pipe may both be healthy while the model provider is
            // refusing, and Offline would be a claim this runner cannot support.
            this.owner.ReportTurnFailure(item.RoomName, ex.Message);
            roomFailureThisTurn = true;
            this.logger.LogWarning(ex, "Persona '{PersonaName}' failed to process a turn in room {RoomId}.", this.owner.PersonaName, item.RoomId);
        }
        finally
        {
            // First, and before the Turn's own CancellationTokenSource is disposed below: a watchdog
            // still running past that point would call Cancel() on a disposed source. Cancelling it
            // stops WatchForAdapterSilenceAsync's Task.Delay immediately when the Turn ended on its
            // own, without waiting out whatever of the idle bound remained.
            await watchdogCancellation.CancelAsync();
            await SafeAwaitAsync(watchdog);
            watchdogCancellation.Dispose();

            if (this.agentId is { } endTurnAgentId)
            {
                this.ownPosts?.EndTurn(endTurnAgentId, this.RoomId);
                this.turnActivity?.End(endTurnAgentId, turn.RoomId);
            }

            lock (this.gate)
            {
                if (ReferenceEquals(this.activeTurn, turn))
                {
                    this.activeTurn = null;
                }
            }

            // Written on every path a Turn can end while this runner is alive - success, refusal,
            // exception, and Stop - so a partial reply can never outlive its Turn on screen.
            //
            // Shutdown is the one path that deliberately skips it, and skipping is not a gap: the
            // pipe is closing, and AgentGateway.Unregister clears every Draft this Agent owns as the
            // connection drops, so the Room is left clean either way. Writing it anyway needed an
            // uncancellable token - the run token is already cancelled by then - and an unbounded
            // write into a pipe that is being torn down simply blocks, hanging teardown until
            // something else happens to cancel it.
            if (!ct.IsCancellationRequested)
            {
                try
                {
                    await this.owner.WriteAsync(new MessageDelta(item.RoomId, messageId, string.Empty, IsFinal: true), ct);
                }
                catch (Exception ex) when (ex is IOException or ObjectDisposedException)
                {
                    // The pipe died before the terminator could be written; the Turn already ended
                    // one way or another, so there is nothing left to signal and nothing to retry.
                    this.logger.LogWarning(ex, "Persona '{PersonaName}' failed to write the final delta for a turn in room {RoomId}.", this.owner.PersonaName, item.RoomId);
                }
            }

            // After the Draft terminator above, so a slow or failing commit never delays the Turn's
            // own MessageDelta reaching the Room. Uses the local turn, never this.activeTurn -
            // already nulled above - and copies Touched under gate since the event reader can still
            // be writing to it concurrently with this method's own thread. Gated on
            // !ct.IsCancellationRequested (shutdown) and, per finding P-17, on the Turn having either
            // returned from PromptAsync or shown activity: a prompt refused outright before any event
            // (E-5) saw neither and must not commit, so the Room's list repeats next Turn; a Stop
            // after activity (E-6) still commits. An exception other than IOException/
            // ObjectDisposedException from the terminator write above escapes this finally block
            // entirely, so this commit - and the Dispose below - are both skipped in that case;
            // acceptable, since the consumer's own catch already logs and keeps the loop alive, and
            // nothing was lost that a later commit could still repair.
            if (this.fileChanges is not null && collected is not null && !ct.IsCancellationRequested && (promptReturned || turn.SawActivity))
            {
                HashSet<string> touched;
                lock (this.gate)
                {
                    touched = [.. turn.Touched];
                }

                try
                {
                    await this.fileChanges.CommitAsync(this.owner.PersonaName, item.RoomId, collected, touched, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    this.logger.LogWarning(ex, "Persona '{PersonaName}' failed to commit File Changes for room {RoomId}.", this.owner.PersonaName, item.RoomId);
                }
            }

            // RS §6.6 "Written at Turn end": per-Room mode only, whenever the prompt was sent
            // (finding P-17, same gate as the File Changes commit above) and the run is not
            // shutting down. LastMessageId is the reply's own minted id when one was actually
            // posted, else the triggering Message's - a Greeting with neither leaves it null,
            // legal per RoomSessionEntry's own doc comment.
            if (this.RoomId is { } storeRoomId && this.roomSessionStore is not null && this.host is not null && this.persona is not null
                && !ct.IsCancellationRequested && (promptReturned || turn.SawActivity))
            {
                var lastMessageId = replyPosted ? messageId : item.TriggerMessageId;
                this.roomSessionStore.Put(
                    this.owner.PersonaName,
                    storeRoomId,
                    new RoomSessionEntry(activeSession.SessionId, this.host.Profile.Id, this.persona.Model, this.persona.Effort, lastMessageId, this.time.GetUtcNow()));
            }

            turnCancellation.Dispose();
        }

        // D26, RS §6.8, finding P-14: a second consecutive failure closes and forgets THIS per-Room
        // session, so its next Turn opens fresh - checked here, after the block above, so a store
        // entry this same Turn just wrote (P-17) is not left behind for a session about to be torn
        // down (correction 8's ordering: Put, then Forget). A no-op in shared mode (RoomId null) and
        // for any Turn that did not itself report a failure.
        if (this.RoomId is { } failedRoomId && roomFailureThisTurn && ++this.roomConsecutiveFailures >= 2)
        {
            this.roomSessionStore?.Forget(this.owner.PersonaName, failedRoomId);
            await this.CloseAfterRepeatedFailureAsync();
        }
    }

    /// <summary>
    /// Bounds silence, not duration: <see cref="ActiveTurn.IdleFor"/> restarts on every event
    /// <see cref="RunEventReaderAsync"/> observes, so a long streaming Turn that keeps reporting
    /// activity never trips this however long it runs; only a Turn that goes quiet for
    /// <paramref name="idleBound"/> does.
    /// </summary>
    /// <remarks>
    /// TRAP 2 (docs/agencyteam/rules.md): the obvious implementation - cancel <paramref name="turn"/>'s
    /// token, then call <see cref="IAgentSession.CancelAsync"/> - never reaches the far side.
    /// <c>DotAcpAgentSession.PromptAsync</c>'s <c>finally</c> nulls its <c>promptCts</c> the instant the
    /// prompt call throws locally, and <c>DotAcpAgentSession.CancelAsync</c> silently no-ops when
    /// <c>promptCts</c> is null. Cancelling the Turn's token first makes the local await throw before
    /// <c>CancelAsync</c> ever runs, so the Adapter that is actually hung is never told to stop - it
    /// keeps working, unaware anything ended. This method therefore does the OPPOSITE of
    /// <see cref="StopAsync"/>'s ordering on purpose: it tells the far side FIRST, with
    /// <see cref="IAgentSession.CancelAsync"/>, and only cancels <paramref name="turn"/>'s own token
    /// once that call has returned or been abandoned after <see cref="AdapterCancelGrace"/>. The Stop
    /// path gets away with the opposite order only because it runs on the read loop while the
    /// consumer resumes elsewhere - a deliberate responsiveness trade for a Human-initiated Stop that
    /// this watchdog, reporting a failure, does not get to make.
    /// </remarks>
    /// <param name="activeSession">The Turn's own session, cancelled first (TRAP 2).</param>
    /// <param name="turn">The Turn being watched.</param>
    /// <param name="item">The work item the Turn is processing, named in the warning this logs.</param>
    /// <param name="idleBound">How long a silence is tolerated before this fires.</param>
    /// <param name="watchdogToken">
    /// Cancelled from <see cref="ProcessWorkItemAsync"/>'s <c>finally</c> once the Turn ends on its
    /// own, so this loop stops without ever firing.
    /// </param>
    /// <param name="ct">The run token, passed through to <see cref="IAgentSession.CancelAsync"/>.</param>
    private async Task WatchForAdapterSilenceAsync(
        IAgentSession activeSession, ActiveTurn turn, WorkItem item, TimeSpan idleBound, CancellationToken watchdogToken, CancellationToken ct)
    {
        try
        {
            TimeSpan remaining = idleBound;
            while (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining, watchdogToken);
                remaining = idleBound - turn.IdleFor;
            }

            // Latched BEFORE the cancellation below, so ProcessWorkItemAsync's catch can tell this
            // firing apart from a genuine Stop (TRAP 1) - both surface as the same
            // OperationCanceledException there.
            turn.MarkTimedOut();

            this.logger.LogWarning(
                "Persona '{PersonaName}' turn in room {RoomId} sent nothing for {IdleBoundSeconds} seconds; cancelling it.",
                this.owner.PersonaName,
                item.RoomId,
                idleBound.TotalSeconds);

            try
            {
                // TRAP 2: tell the far side first, before this Turn's own token is cancelled below.
                await activeSession.CancelAsync(ct).WaitAsync(AdapterCancelGrace, ct);
            }
            catch (Exception ex) when (ex is AgentException or IOException or ObjectDisposedException or TimeoutException)
            {
                // An adapter that will not even take a cancel is the same adapter that sent nothing
                // in the first place: expected here, not exceptional, and does not stop the local
                // abort below.
                this.logger.LogWarning(
                    ex,
                    "Persona '{PersonaName}' failed to notify the Adapter of an idle-timeout cancel in room {RoomId}.",
                    this.owner.PersonaName,
                    item.RoomId);
            }

            await turn.Cancellation.CancelAsync();
        }
        catch (OperationCanceledException)
        {
            // The Turn ended before the bound was reached, or the run itself is shutting down.
        }
    }

    /// <summary>Converts a configured idle-timeout bound to a <see cref="TimeSpan"/>, disabling it for zero or less.</summary>
    /// <param name="seconds"><see cref="AcpOptions.TurnIdleTimeoutSeconds"/>'s current value.</param>
    /// <returns>The bound as a <see cref="TimeSpan"/>, or <see cref="TimeSpan.Zero"/> when disabled.</returns>
    private static TimeSpan IdleTimeoutFrom(int seconds) => seconds > 0 ? TimeSpan.FromSeconds(seconds) : TimeSpan.Zero;

    private async Task RunEventReaderAsync(IAgentSession activeSession, CancellationToken ct)
    {
        Exception? terminatingException = null;
        try
        {
            await foreach (var agentEvent in activeSession.Events.ReadAllAsync(ct))
            {
                // Any sign of life restarts the idle-timeout bound, including event types this loop
                // otherwise ignores below: what is bounded is silence, not progress. Taking gate is
                // what makes this safe against ProcessWorkItemAsync's finally, which clears
                // activeTurn under the same lock.
                lock (this.gate)
                {
                    this.activeTurn?.MarkActivity();
                }

                if (agentEvent is MessageChunk chunk)
                {
                    await this.AppendAndPublishDeltaAsync(chunk.Text, ct);
                }
                else if (agentEvent is ToolCallStarted started)
                {
                    this.RecordTouchedPaths(started.Kind, started.RawInputJson);
                    await this.WriteToolActivityAsync(started.ToolCallId, started.Title, MapToolCallStatus(started.Status), ct);
                }
                else if (agentEvent is ToolCallUpdated updated)
                {
                    this.RecordTouchedPaths(updated.Kind, updated.RawInputJson);
                    await this.WriteToolActivityAsync(updated.ToolCallId, updated.Title, MapToolCallStatus(updated.Status), ct);
                }
                else if (agentEvent is TurnCompleted completed)
                {
                    ActiveTurn? completedTurn;
                    string text;
                    lock (this.gate)
                    {
                        completedTurn = this.activeTurn;
                        text = completedTurn?.Text.ToString() ?? string.Empty;
                        this.activeTurn = null;
                    }

                    completedTurn?.Completion.TrySetResult(new TurnOutcome(text, completed.StopReason));
                }
                else if (agentEvent is UsageUpdated usage)
                {
                    // Used is how full the context window is, not a running bill - Huddle.Console
                    // renders it as "{Used}/{Size}" - so it FALLS when the session compacts. Summing
                    // Used itself would re-count the whole window on every update; summing only the
                    // rises counts each token once, which is the closest honest proxy for what the
                    // session actually cost. lastUsed lives here, not on the owner (RS §6.8): it is
                    // one session's context fill, and a shared lastUsed would read one session's fill
                    // against another's and count rises that never happened.
                    //
                    // D22 correction 9: on a resume, this session's FIRST UsageUpdated reports its
                    // whole restored context, not tokens this Turn spent, so it is taken as the
                    // baseline and never added - only rises after it count.
                    if (this.usageBaselinePending)
                    {
                        this.usageBaselinePending = false;
                        this.lastUsed = usage.Used;
                    }
                    else
                    {
                        var previous = this.lastUsed;
                        this.lastUsed = usage.Used;
                        if (usage.Used > previous)
                        {
                            this.owner.AddTokens(usage.Used - previous);
                        }
                    }
                }

                // ThoughtChunk, PlanUpdated, ModeChanged, UserMessageChunk, UnsupportedContent and
                // UnknownUpdate are ignored.
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown, or this session was closed (its own linked token was cancelled).
        }
        catch (Exception ex)
        {
            terminatingException = ex;
            this.logger.LogWarning(ex, "Persona '{PersonaName}' agent event stream ended unexpectedly.", this.owner.PersonaName);
        }
        finally
        {
            // Whatever ended this loop - a fault, or ordinary shutdown mid-turn - the single consumer
            // must not be left awaiting a TurnCompleted that can now never arrive: this channel is
            // done, for good, in every case. Without this, a crashed adapter process left
            // ProcessWorkItemAsync blocked on completion.Task forever, deafening the Agent in every
            // Room with no signal of any kind.
            ActiveTurn? strandedTurn;
            lock (this.gate)
            {
                strandedTurn = this.activeTurn;
                this.activeTurn = null;
            }

            strandedTurn?.Completion.TrySetException(
                new InvalidOperationException(
                    $"Persona '{this.owner.PersonaName}': the agent event stream ended before its in-flight turn completed."));

            // A deliberate CloseAsync cancels this reader's OWN token before disposing the session
            // (D22 correction 7): that end must never be reported as Offline, and since ct here is
            // that same per-open token - linked to the run token - checking it covers both a close
            // and ordinary shutdown in one place.
            if (!ct.IsCancellationRequested)
            {
                this.owner.ReportLoopEnded("event reader", terminatingException);
            }
        }
    }

    /// <summary>
    /// Appends one increment of Message text to the active Turn's running total, then publishes it
    /// as a <see cref="MessageDelta"/> so the Room can show the reply as it arrives. No throttling
    /// here - the browser is where that cost belongs.
    /// </summary>
    /// <param name="text">The increment of text this event carries.</param>
    /// <param name="ct">Cancels the delta write.</param>
    private async Task AppendAndPublishDeltaAsync(string text, CancellationToken ct)
    {
        ActiveTurn? turn;
        lock (this.gate)
        {
            turn = this.activeTurn;
            if (turn is null)
            {
                return;
            }

            turn.Text.Append(text);
        }

        // DeltaWriteFailed and the write below are touched only from this single-threaded event
        // reader loop, so no lock is needed to read or set it here.
        if (turn.DeltaWriteFailed)
        {
            return;
        }

        try
        {
            await this.owner.WriteAsync(new MessageDelta(turn.RoomId, turn.MessageId, text, IsFinal: false), ct);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            // A dead pipe must not also kill the event reader: the turn keeps running, and this is
            // logged once per turn rather than once per chunk, since a stream of identical warnings
            // is worse than one.
            turn.DeltaWriteFailed = true;
            this.logger.LogWarning(
                ex,
                "Persona '{PersonaName}' failed to write a message delta for a turn in room {RoomId}.",
                this.owner.PersonaName,
                turn.RoomId);
        }
    }

    /// <summary>
    /// Publishes one tool call's current lifecycle state as a <see cref="ToolActivity"/>, attributed
    /// to the active Turn's Room and Message id. A no-op when there is no active Turn.
    /// </summary>
    /// <param name="toolCallId">The id of the tool call this activity reports on.</param>
    /// <param name="title">A human-readable label for the call, if the agent supplied one.</param>
    /// <param name="status">The call's current lifecycle state, already mapped to the wire enum.</param>
    /// <param name="ct">Cancels the write.</param>
    private async Task WriteToolActivityAsync(string toolCallId, string? title, ToolActivityStatus status, CancellationToken ct)
    {
        ActiveTurn? turn;
        lock (this.gate)
        {
            turn = this.activeTurn;
        }

        if (turn is null)
        {
            return;
        }

        try
        {
            await this.owner.WriteAsync(new ToolActivity(turn.RoomId, turn.MessageId, toolCallId, title, status), ct);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            this.logger.LogWarning(
                ex,
                "Persona '{PersonaName}' failed to write a tool activity update for a turn in room {RoomId}.",
                this.owner.PersonaName,
                turn.RoomId);
        }
    }

    /// <summary>
    /// Adds every rooted path found in <paramref name="rawInputJson"/> to the active Turn's
    /// <see cref="ActiveTurn.Touched"/> set, per FC §6.8 item 2 and FC D-3b - but only for
    /// <see cref="ToolKind.Edit"/>, <see cref="ToolKind.Delete"/> or <see cref="ToolKind.Move"/>: an
    /// <see cref="ToolKind.Execute"/> call is not parsed, because a shell command is not a path and
    /// guessing at one could claim another Teammate's change as this Agent's own. A no-op when there
    /// is no active Turn.
    /// </summary>
    /// <param name="kind">The tool call's category, as ACP reported it.</param>
    /// <param name="rawInputJson">The tool call's raw input, if any.</param>
    private void RecordTouchedPaths(ToolKind kind, string? rawInputJson)
    {
        if (kind is not (ToolKind.Edit or ToolKind.Delete or ToolKind.Move))
        {
            return;
        }

        lock (this.gate)
        {
            if (this.activeTurn is not { } turn)
            {
                return;
            }

            foreach (var path in TouchedPaths.From(rawInputJson))
            {
                turn.Touched.Add(path);
            }
        }
    }

    /// <summary>Builds the prompt text delivered to the model for one Turn.</summary>
    /// <param name="item">The Turn's Room, sender, text, any catch-up context, and its <see cref="WorkItemKind"/>.</param>
    /// <param name="prompts">Resolves each <c>turn.*</c> prompt's current text — a configured override, or the <see cref="PromptCatalog"/> default.</param>
    /// <returns>
    /// For a <see cref="WorkItemKind.Greeting"/>, the <c>turn.greeting</c> prompt alone. Otherwise: with
    /// no catch-up context, the <c>turn.message</c> line alone; with catch-up context, a
    /// <c>turn.catchUpHeader</c> line, one <c>turn.catchUpLine</c> per missed message, a blank line,
    /// then the <c>turn.message</c> line.
    /// </returns>
    internal static string BuildPrompt(WorkItem item, IPromptSource prompts)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(prompts);

        // The Room's id rides along with its name because it is the only way an Agent can learn one.
        // mcp__team__post_message and mcp__team__invite_agent both take a room id, and nothing else
        // in a turn carries it: without this they reach only Rooms the Agent created itself.
        var room = RoomSession.RoomLabel(item, prompts);

        if (item.Kind == WorkItemKind.Greeting)
        {
            // No triggering Message and no catch-up (Spec §6.14): the trigger is this Prompt, not
            // Transcript text, and a Greeting is by definition the first Turn in an empty Room. FC
            // §6.8: the File Changes block never applies to a Greeting either.
            return prompts.Render("turn.greeting", new Dictionary<string, string> { ["{{roomLabel}}"] = room });
        }

        var builder = new StringBuilder();
        RoomSession.AppendFileChangesBlock(builder, item.FileChanges, prompts);

        // RS §6.5: the Transcript block replaces the catch-up buffer on this Turn only, and only
        // when it actually holds Messages — an empty Transcript (the read was refused, timed out, or
        // this Room's first Turn ever has nothing before the trigger) falls through to today's
        // buffer-based catch-up untouched, keeping every pre-D24 prompt byte-identical.
        if (item.Transcript is { Messages.Count: > 0 } transcript)
        {
            RoomSession.AppendTranscriptBlock(builder, room, transcript, prompts);
            builder.Append(RoomSession.RenderMessage(prompts, room, item.SenderName, item.Text));
            return builder.ToString();
        }

        var ownPostLines = item.OwnPostLines ?? [];
        if (item.MissedMessages.Count == 0 && ownPostLines.Count == 0)
        {
            builder.Append(RoomSession.RenderMessage(prompts, room, item.SenderName, item.Text));
            return builder.ToString();
        }

        // RS §6.7: a block of only own posts still gets turn.catchUpHeader, exactly like a block of
        // only missed Messages - the header introduces the whole Catch-up block, not just one kind
        // of line inside it.
        builder.Append(prompts.Render("turn.catchUpHeader", new Dictionary<string, string> { ["{{roomLabel}}"] = room }));
        builder.Append('\n');
        foreach (var missed in item.MissedMessages)
        {
            builder.Append(prompts.Render(
                "turn.catchUpLine",
                new Dictionary<string, string> { ["{{sender}}"] = missed.SenderName, ["{{text}}"] = missed.Text }));
            builder.Append('\n');
        }

        foreach (var line in ownPostLines)
        {
            builder.Append(prompts.Render("turn.ownPostLine", new Dictionary<string, string> { ["{{text}}"] = line }));
            builder.Append('\n');
        }

        builder.Append('\n');
        builder.Append(RoomSession.RenderMessage(prompts, room, item.SenderName, item.Text));

        return builder.ToString();
    }

    /// <summary>
    /// Writes RS §6.5's Transcript block: the fresh-or-resumed header, an omitted-count line when
    /// <see cref="TranscriptCatchUp.Omitted"/> is positive, one <c>turn.catchUpLine</c> per Message
    /// (the Agent's own Messages appear under its own Name, per RS §6.9), then a blank line.
    /// </summary>
    private static void AppendTranscriptBlock(StringBuilder builder, string room, TranscriptCatchUp transcript, IPromptSource prompts)
    {
        var headerKey = transcript.Resumed ? "turn.transcriptResumedHeader" : "turn.transcriptHeader";
        builder.Append(prompts.Render(headerKey, new Dictionary<string, string> { ["{{roomLabel}}"] = room }));
        builder.Append('\n');

        if (transcript.Omitted > 0)
        {
            builder.Append(prompts.Render(
                "turn.transcriptOmitted",
                new Dictionary<string, string> { ["{{count}}"] = transcript.Omitted.ToString(CultureInfo.InvariantCulture) }));
            builder.Append('\n');
        }

        foreach (var message in transcript.Messages)
        {
            builder.Append(prompts.Render(
                "turn.catchUpLine",
                new Dictionary<string, string> { ["{{sender}}"] = message.SenderName, ["{{text}}"] = message.Text }));
            builder.Append('\n');
        }

        builder.Append('\n');
    }

    /// <summary>
    /// FC §6.8: with a non-empty <paramref name="report"/>, writes the File Changes block ahead of
    /// everything else — the header, one line per change, one line per unchecked folder, an
    /// "…and N more." line when the cap left some out, then a blank line. Writes nothing for a
    /// <see langword="null"/> or empty report: "absent means unchanged".
    /// </summary>
    private static void AppendFileChangesBlock(StringBuilder builder, FileChangesReport? report, IPromptSource prompts)
    {
        if (report is null || report.IsEmpty)
        {
            return;
        }

        builder.Append(prompts.Render("turn.fileChangesHeader", new Dictionary<string, string>()));
        builder.Append('\n');

        foreach (var change in report.Changes)
        {
            var key = change.Kind switch
            {
                FileChangeKind.Added => "turn.fileAdded",
                FileChangeKind.Changed => "turn.fileChanged",
                FileChangeKind.Deleted => "turn.fileDeleted",
                _ => throw new InvalidOperationException($"Unknown File Change kind '{change.Kind}'."),
            };

            builder.Append(prompts.Render(key, new Dictionary<string, string> { ["{{path}}"] = change.FullPath }));

            if (change.ByYouRoomName is not null)
            {
                builder.Append(prompts.Render("turn.fileByYouSuffix", new Dictionary<string, string> { ["{{roomName}}"] = change.ByYouRoomName }));
            }

            builder.Append('\n');
        }

        foreach (var uncheckedFolder in report.Unchecked)
        {
            builder.Append(prompts.Render(
                "turn.folderUnchecked",
                new Dictionary<string, string>
                {
                    ["{{path}}"] = uncheckedFolder,
                    ["{{max}}"] = report.MaxFilesPerFolder.ToString(CultureInfo.InvariantCulture),
                }));
            builder.Append('\n');
        }

        if (report.NotListed > 0)
        {
            builder.Append(prompts.Render(
                "turn.fileChangesMore",
                new Dictionary<string, string> { ["{{count}}"] = report.NotListed.ToString(CultureInfo.InvariantCulture) }));
            builder.Append('\n');
        }

        builder.Append('\n');
    }

    /// <summary>Builds the bracketed Room label that opens every line of a prompt.</summary>
    /// <param name="item">The work item whose Room is being labelled.</param>
    /// <param name="prompts">Resolves the <c>turn.roomLabel</c> prompt's current text.</param>
    /// <returns>The label, carrying both the Room's name and its id.</returns>
    private static string RoomLabel(WorkItem item, IPromptSource prompts) =>
        prompts.Render(
            "turn.roomLabel",
            new Dictionary<string, string> { ["{{roomName}}"] = item.RoomName, ["{{roomId}}"] = item.RoomId });

    /// <summary>Renders one <c>turn.message</c> line: a Room label, its sender, and its text.</summary>
    /// <param name="prompts">Resolves the <c>turn.message</c> prompt's current text.</param>
    /// <param name="roomLabel">The already-rendered Room label to open the line with.</param>
    /// <param name="sender">The message's sender name.</param>
    /// <param name="text">The message's text.</param>
    /// <returns>The rendered <c>turn.message</c> line.</returns>
    private static string RenderMessage(IPromptSource prompts, string roomLabel, string sender, string text) =>
        prompts.Render(
            "turn.message",
            new Dictionary<string, string> { ["{{roomLabel}}"] = roomLabel, ["{{sender}}"] = sender, ["{{text}}"] = text });

    /// <summary>Maps an ACP tool call's lifecycle state to the wire enum shown to the Human.</summary>
    /// <param name="status">The ACP status to map.</param>
    /// <returns>The equivalent <see cref="ToolActivityStatus"/>.</returns>
    /// <remarks>
    /// One arm per declared member and deliberately <em>no</em> discard arm, so that adding a member
    /// to ACP's <see cref="ToolCallStatus"/> fails this build (<c>CS8509</c>) instead of degrading
    /// into a run-time throw nobody sees until a model happens to use it - the two enums are a
    /// hand-maintained pair, and the compiler is the only thing that will notice them drifting apart.
    /// <c>CS8524</c> is a different complaint: it is about a value cast from an integer that names no
    /// member at all, which cannot arrive here, because the only values passed in are ones
    /// <c>SessionUpdateMapper</c> itself produced from a declared member. Suppressing that one is
    /// what keeps <c>CS8509</c> live.
    /// </remarks>
#pragma warning disable CS8524 // Only SessionUpdateMapper's own declared members reach this; see remarks.
    private static ToolActivityStatus MapToolCallStatus(ToolCallStatus status) => status switch
    {
        ToolCallStatus.Pending => ToolActivityStatus.Pending,
        ToolCallStatus.InProgress => ToolActivityStatus.InProgress,
        ToolCallStatus.Completed => ToolActivityStatus.Completed,
        ToolCallStatus.Failed => ToolActivityStatus.Failed,
    };
#pragma warning restore CS8524

    private static async Task SafeAwaitAsync(Task? task)
    {
        if (task is null)
        {
            return;
        }

        try
        {
            await task;
        }
        catch (Exception)
        {
            // Teardown must not throw regardless of how the task ended.
        }
    }

    /// <summary>The Turn currently in flight: its Room, its minted Message id, the text so far, and how it ends.</summary>
    private sealed record ActiveTurn(
        string RoomId,
        string MessageId,
        StringBuilder Text,
        TaskCompletionSource<TurnOutcome> Completion,
        CancellationTokenSource Cancellation)
    {
        // lastActivityTicks, stopRequested and timedOut are each written by one thread and read by
        // another - lastActivityTicks by the event-reader loop, read by the idle-timeout watchdog;
        // stopRequested by StopAsync, timedOut by the watchdog, both read by the consumer's catch
        // clauses - so every access goes through Interlocked or Volatile, never a plain read or write.
        // Environment.TickCount64 (monotonic, unaffected by a wall-clock change) is used rather than an
        // injected TimeProvider: threading one through here would churn every test's construction, and
        // this file already reads DateTimeOffset.UtcNow directly elsewhere, so this is consistent with
        // the existing style rather than a new one.
        private long lastActivityTicks = Environment.TickCount64;
        private bool stopRequested;
        private bool timedOut;
        private bool sawActivity;

        /// <summary>
        /// Whether a <see cref="MessageDelta"/> write has already failed for this Turn. Set once a
        /// write throws, so a dead pipe is logged at most once per Turn rather than once per chunk.
        /// Touched only from the single-threaded event-reader loop, so it needs no lock of its own.
        /// </summary>
        public bool DeltaWriteFailed { get; set; }

        /// <summary>
        /// Full paths this Agent's own tool calls touched this Turn (FC §6.8 item 2), read and
        /// written only under <see cref="RoomSession.gate"/>: <see cref="RecordTouchedPaths"/> adds
        /// to it from the event-reader loop, and <see cref="ProcessWorkItemAsync"/>'s <c>finally</c>
        /// copies it once the Turn has ended.
        /// </summary>
        public HashSet<string> Touched { get; } = new(FolderSnapshot.PathComparer);

        /// <summary>
        /// Whether a Stop has already been latched for this Turn. Set by
        /// <see cref="MarkStopRequested"/>, always BEFORE the cancellation it causes, so the consumer
        /// awaiting this Turn can tell a genuine Stop apart from the idle-timeout watchdog firing
        /// (<see cref="TimedOut"/>) - both surface as the same <see cref="OperationCanceledException"/>.
        /// </summary>
        public bool StopRequested => Volatile.Read(ref this.stopRequested);

        /// <summary>
        /// Whether the idle-timeout watchdog has already latched a firing for this Turn. Set by
        /// <see cref="MarkTimedOut"/>, always BEFORE the cancellation it causes, for the same reason
        /// <see cref="StopRequested"/> exists.
        /// </summary>
        public bool TimedOut => Volatile.Read(ref this.timedOut);

        /// <summary>How long it has been, as of now, since the last sign of life on this Turn.</summary>
        public TimeSpan IdleFor =>
            TimeSpan.FromMilliseconds(Environment.TickCount64 - Interlocked.Read(ref this.lastActivityTicks));

        /// <summary>
        /// Whether the event-reader loop has observed any <see cref="AgentEvent"/> at all for this
        /// Turn, set by <see cref="MarkActivity"/> - finding P-17's latch for a commit rule
        /// <see cref="ProcessWorkItemAsync"/> applies: a Turn commits its File Changes when
        /// <c>PromptAsync</c> returned, or when this is <see langword="true"/>, because a Stop can
        /// make <c>PromptAsync</c> throw after real work already happened (FC §9 E-6). A late
        /// <see cref="UsageUpdated"/> from a prior Turn setting this on a Turn that never itself saw
        /// activity is harmless: it only ever widens when a commit is allowed, never causes one to be
        /// skipped.
        /// </summary>
        public bool SawActivity => Volatile.Read(ref this.sawActivity);

        /// <summary>
        /// Restarts the idle clock and latches <see cref="SawActivity"/>. Called by the event-reader
        /// loop on any <see cref="AgentEvent"/> it observes, including ones it otherwise ignores -
        /// what the idle-timeout bound measures is silence, not progress, so any activity at all
        /// counts.
        /// </summary>
        public void MarkActivity()
        {
            Interlocked.Exchange(ref this.lastActivityTicks, Environment.TickCount64);
            Volatile.Write(ref this.sawActivity, true);
        }

        /// <summary>
        /// Latches that this Turn is ending because a Human pressed Stop. MUST be called before
        /// <see cref="Cancellation"/> is cancelled, so <see cref="StopRequested"/> is already true by
        /// the time the consumer observes the resulting <see cref="OperationCanceledException"/>.
        /// </summary>
        public void MarkStopRequested() => Volatile.Write(ref this.stopRequested, true);

        /// <summary>
        /// Latches that this Turn is ending because the idle-timeout watchdog fired. MUST be called
        /// before <see cref="Cancellation"/> is cancelled, for the same reason
        /// <see cref="MarkStopRequested"/> is.
        /// </summary>
        public void MarkTimedOut() => Volatile.Write(ref this.timedOut, true);
    }

    /// <summary>How a Turn ended: the text it produced, and why it stopped.</summary>
    private sealed record TurnOutcome(string Text, StopReason Reason);
}

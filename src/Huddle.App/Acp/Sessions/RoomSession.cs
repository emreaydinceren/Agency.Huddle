using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Memory;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.References;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using System.Globalization;
using System.Text;

namespace Agency.Huddle.App.Acp.Sessions;

/// <summary>
/// One Agent's working context for one Room: its queue, its session and its Turns (RS §6.1).
/// <c>RoomId == null</c> means a <b>shared</b> session serving every Room the Agent is in - Phase 0/1's
/// one <see cref="RoomSession"/> per Persona. <see cref="ProcessWorkItemAsync"/>,
/// <see cref="WatchForAdapterSilenceAsync"/>, <see cref="RunEventReaderAsync"/> and
/// <see cref="BuildPrompt"/> moved here from <see cref="PersonaRunner"/> with their comments and
/// both traps intact (<c>docs/engineering/rules.md</c>, "A Turn ends four ways").
/// </summary>
internal sealed class RoomSession : IAsyncDisposable, IElicitationScope
{
    // The bounds on Acp:UserInputTimeoutSeconds. The floor keeps a question from expiring before a
    // Human could read it and, since zero and below are raised to it, means a question is never
    // unbounded. The ceiling keeps CancellationTokenSource from throwing on a typo.
    private const int MinUserInputTimeoutSeconds = 30;
    private const int MaxUserInputTimeoutSeconds = 86_400;

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

    // Context meter: the latest context-window fill of this session, shown in the Members panel.
    // Null for a caller that predates it, the same "absent means off" reasoning as turnActivity.
    private readonly ContextFill? contextFill;
    private readonly ISessionSettings? sessionSettings;
    private readonly LastCatchUpStore? lastCatchUp;

    // The note the current Turn owes after a fresh start; set by AddTranscriptCatchUpAsync and
    // cleared by SendFreshStartNoteAsync. Touched only from the single consumer thread.
    private FreshStartNote? noteOwed;

    // Spec §6.14: collects the Library documents mentioned in a Turn's Message and Catch-up. Null
    // means the Library is off (PersonaSupervisor is the only place that gate is applied) or this
    // caller predates it.
    private readonly LibraryDocumentCollector? libraryDocs;
    private readonly ReferenceExpander? references;
    private readonly bool readsFiles;

    // Shows the Human an agent's elicitation request (E-3). Null means there is no UI to show it in:
    // every request is then answered as cancelled, which is what an agent with no scope bound gets.
    private readonly IElicitationBridge? elicitationBridge;

    // Guards every field below: the queue, the state machine, the Stop marks, activeTurn and
    // pendingStopCancel - one lock, exactly as turnLock was on PersonaRunner, for the same reason:
    // the Stop path must publish its mark and read activeTurn as one atomic step against the
    // consumer's own re-check (RS §6.1 step 2, D22 correction 3).
    private readonly Lock gate = new();
    private readonly Queue<QueuedWork> queue = new();
    private readonly SemaphoreSlim itemAvailable = new(0);
    private bool freshStartPending;
    private bool freshStartNotePending;
    private bool skipResumeOnNextOpen;
    private Task? pendingFreshTeardown;
    private Task? pendingDelete;

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

    // Numbers each Adapter notice so its Turn-activity row has an id no tool call can carry. Touched
    // only from the single-threaded event reader.
    private long noticeSequence;
    private Task? pendingStopCancel;
    private bool running;
    private long lastUsed;

    /// <summary>The latest non-null model a usage update of this session named; kept across updates that name none.</summary>
    private string? lastModel;

    // D24: consulted and cleared only from ProcessWorkItemAsync, which the single consumer thread
    // always calls after the open that set them (OpenCoreAsync happens-before ProcessWorkItemAsync
    // in RunConsumerAsync's own sequence), so these need no lock of their own - the same reasoning
    // knownRoomNames documents on PersonaRunner.
    private bool firstTurnPending;
    private bool resumedOnLastOpen;
    private string? resumedAfterMessageId;

    // Set alongside firstTurnPending/resumedOnLastOpen and consumed by RunEventReaderAsync's own
    // single-threaded loop, the same reasoning lastUsed itself relies on (D22 correction 9, built
    // here for D24). It is ALSO set by the consumer when a Command Turn completes (Commands spec,
    // section 6.7), so the two threads share it: volatile, so the reader sees the consumer's write
    // on the very next update rather than after some later fence.
    private volatile bool usageBaselinePending;

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
    /// <param name="persona">The Persona this session belongs to (D24): its Model, Effort and Work Mode gate a resume. <see langword="null"/> disables resume, like every pre-D24 caller.</param>
    /// <param name="roomSessions">Backs resume and the Transcript Catch-up cursor (RS §6.1, §6.6). <see langword="null"/> disables storing and resuming.</param>
    /// <param name="host">Supplies <see cref="IPersonaHost.CanResume"/>, <see cref="AdapterProfile.Id"/> and <see cref="IPersonaHost.ResumeAsync"/> for the resume decision. <see langword="null"/> disables resume.</param>
    /// <param name="ownPosts">Marks this session's Room Busy for a Turn's own duration (D27, RS §6.7). <see langword="null"/> disables it, like every pre-D27 caller.</param>
    /// <param name="agentId">This session's owning Agent's id, passed to <paramref name="ownPosts"/> and <paramref name="turnActivity"/>. <see langword="null"/> disables it, like every pre-D27 caller.</param>
    /// <param name="turnActivity">Records which Room this Agent has a Turn running in (Spec §10.8). <see langword="null"/> disables it, like every caller that predates it.</param>
    /// <param name="libraryDocs">Collects the Library documents mentioned in a Turn (Spec §6.14). <see langword="null"/> when the Library is off, like every caller that predates it.</param>
    /// <param name="readsFiles">Whether the resolved Adapter Profile can read files (FC §6.11's Library counterpart): <see langword="false"/> inlines a kept document's own text instead of just its path.</param>
    /// <param name="elicitationBridge">Shows the Human the questions the agent asks through this session (E-3). <see langword="null"/> answers every one as cancelled, like every caller that predates it.</param>
    /// <param name="references">Builds the Referenced items header for a Message Turn (spec section 9.1). <see langword="null"/> adds no header, like every caller that predates it.</param>
    /// <param name="contextFill">Records how full this session's context window is, for the Members panel's context meter. <see langword="null"/> disables it, like every caller that predates it.</param>
    /// <param name="sessionSettings">The resolved Session settings the first Turn's Transcript catch-up reads (the Message cap and the token budget). <see langword="null"/> uses the <see cref="AcpOptions"/> values, like every caller that predates it.</param>
    /// <param name="lastCatchUp">Receives the whole prompt text of the first ordinary Turn after a fresh start. <see langword="null"/> writes no file, like every caller that predates it.</param>
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
        TurnActivity? turnActivity = null,
        LibraryDocumentCollector? libraryDocs = null,
        bool readsFiles = true,
        IElicitationBridge? elicitationBridge = null,
        ReferenceExpander? references = null,
        ContextFill? contextFill = null,
        ISessionSettings? sessionSettings = null,
        LastCatchUpStore? lastCatchUp = null)
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
        this.contextFill = contextFill;
        this.sessionSettings = sessionSettings;
        this.lastCatchUp = lastCatchUp;
        this.libraryDocs = libraryDocs;
        this.references = references;
        this.readsFiles = readsFiles;
        this.elicitationBridge = elicitationBridge;

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

    /// <summary>
    /// Opens a question on the Turn in flight, if there is one: pauses that Turn's idle watchdog until the
    /// returned lease is disposed, and hands back the token that is cancelled when the Turn ends. Taken
    /// under <see cref="gate"/>, the same lock that publishes and clears <see cref="activeTurn"/>, so a
    /// lease is only ever opened on a Turn that is still live.
    /// </summary>
    /// <returns>The lease, or <see langword="null"/> when no Turn is in flight in this session.</returns>
    internal UserInputLease? TryBeginUserInput()
    {
        lock (this.gate)
        {
            return this.activeTurn?.BeginUserInput();
        }
    }

    /// <summary>
    /// Answers an agent's question by handing it to the <see cref="IElicitationBridge"/> for the Turn's
    /// own Room, with the Turn's idle watchdog paused while it waits (E-3). The answer is
    /// <see cref="ElicitationCancelled"/> whenever there is nobody to ask or nothing left to answer: no
    /// bridge, no known Agent, no Turn in flight, the Turn stopped or ended, the session shut down, the
    /// caller gave up, or <see cref="AcpOptions.UserInputTimeoutSeconds"/> ran out. None of those is a
    /// Turn failure: the question's own end never sets the Turn's timed-out latch and never reports one.
    /// </summary>
    /// <param name="request">The form the agent asked for.</param>
    /// <param name="cancellationToken">Cancelled when the agent's own request can no longer be answered.</param>
    /// <returns>How the question ended.</returns>
    public async Task<ElicitationResult> ElicitAsync(ElicitationRequest request, CancellationToken cancellationToken)
    {
        if (this.elicitationBridge is not { } bridge || this.agentId is not { } askerAgentId)
        {
            return new ElicitationCancelled();
        }

        using UserInputLease? lease = this.TryBeginUserInput();
        if (lease is null)
        {
            return new ElicitationCancelled();
        }

        // Read live, like the idle bound, and on this session's own clock so a test drives it.
        TimeSpan bound = UserInputTimeoutFrom(this.options.UserInputTimeoutSeconds);
        using CancellationTokenSource timeout = new(bound, this.time);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lease.Token, timeout.Token);
        try
        {
            return await bridge.RequestAsync(new ElicitationContext(lease.RoomId, askerAgentId), request, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // A Stop, the Turn ending, a shutdown and the caller giving up are all expected and silent;
            // only the bound running out is worth a line, because the Human sees a question vanish.
            if (timeout.IsCancellationRequested)
            {
#pragma warning disable HAA0601 // Boxing a value-type log argument is the cost of ILogger params object[]; accepted together with HAA0101.
                this.logger.LogWarning(
                    "Persona '{PersonaName}' dropped a question in room {RoomId}: no answer after {TimeoutSeconds} seconds.",
                    this.owner.PersonaName,
                    lease.RoomId,
                    bound.TotalSeconds);
#pragma warning restore HAA0601
            }

            return new ElicitationCancelled();
        }
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

    /// <summary>Whether a start afresh has been requested and not yet acted on (LR-R15).</summary>
    internal bool FreshStartPending
    {
        get
        {
            lock (this.gate)
            {
                return this.freshStartPending;
            }
        }
    }

    /// <summary>Whether this session's first ordinary Turn owes a fresh-start note, because a start afresh arrived before it existed (LR-R19).</summary>
    internal bool FreshStartNotePending
    {
        get
        {
            lock (this.gate)
            {
                return this.freshStartNotePending;
            }
        }
    }

    /// <summary>
    /// Records that the Human asked this Room to start afresh (Spec 6.3). An idle session is torn down at once, in
    /// the background, and forgotten; an evicted one only forgets what is stored; a busy or opening one
    /// is marked and starts afresh before the next item is served. A shared session ignores it.
    /// </summary>
    /// <param name="cancellationToken">Checked on entry.</param>
    /// <returns>A completed task; any teardown is handed to the consumer, never awaited here.</returns>
    internal Task RequestFreshStartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (this.RoomId is null)
        {
            return Task.CompletedTask;
        }

        bool forgetNow = false;
        TaskCompletionSource? teardownDone = null;
        (IAgentSession? Session, CancellationTokenSource? ReaderCts, Task? ReaderTask) taken = default;
        lock (this.gate)
        {
            if (this.state == RoomSessionState.Idle && !this.running && this.queue.Count == 0)
            {
                taken = this.TakeOpenLocked();
                teardownDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                this.pendingFreshTeardown = teardownDone.Task;
                this.skipResumeOnNextOpen = true;
                this.freshStartNotePending = true;
            }
            else if (this.state == RoomSessionState.Closed && !this.running)
            {
                // Evicted: nothing is open, so only the stored state is forgotten.
                this.skipResumeOnNextOpen = true;
                this.freshStartNotePending = true;
                forgetNow = true;
            }
            else
            {
                this.freshStartPending = true;
            }
        }

        if (forgetNow)
        {
            this.ForgetStoredState();
        }

        if (teardownDone is not null)
        {
            // Off the caller's and the read loop's thread: disposal never runs under the gate or on the reader.
            _ = Task.Run(() => this.TeardownThenForgetAsync(taken, teardownDone), CancellationToken.None);
        }

        return Task.CompletedTask;
    }

    /// <summary>Disposes the taken session, then forgets the stored state (the consumer path's order), then completes the pending teardown.</summary>
    /// <param name="taken">The session, reader token source and reader loop taken under the gate.</param>
    /// <param name="done">Completed when the teardown and the forget are over.</param>
    private async Task TeardownThenForgetAsync((IAgentSession? Session, CancellationTokenSource? ReaderCts, Task? ReaderTask) taken, TaskCompletionSource done)
    {
        try
        {
            await DisposeOpenAsync(taken.Session, taken.ReaderCts, taken.ReaderTask).ConfigureAwait(false);
            this.ForgetStoredState();

            // Published before the teardown is released, so a DisposeAsync that wakes on it always sees the delete.
            // StartDelete only starts the task: the delete never delays the new session's open.
            this.StartDelete(taken.Session);
        }
        finally
        {
            done.TrySetResult();
        }
    }

    /// <summary>
    /// Starts deleting a closed session's Adapter-side copy in the background, so a fresh start never waits on it.
    /// A second fresh start while an earlier delete is pending chains onto it: neither task is lost, and
    /// <see cref="DisposeAsync"/> awaits the one tracked task exactly once.
    /// </summary>
    /// <param name="old">The session that was closed, or <see langword="null"/> when nothing was open.</param>
    private void StartDelete(IAgentSession? old)
    {
        if (old is null)
        {
            return;
        }

        lock (this.gate)
        {
            Task? previous = this.pendingDelete;
            this.pendingDelete = Task.Run(() => this.DeleteOldAsync(old, previous), CancellationToken.None);
        }
    }

    /// <summary>Deletes one closed session, logging a failure at Warning, then waits for the delete it was chained behind.</summary>
    /// <param name="old">The closed session.</param>
    /// <param name="previous">The earlier pending delete, or <see langword="null"/>.</param>
    private async Task DeleteOldAsync(IAgentSession old, Task? previous)
    {
        try
        {
            await old.DeleteAsync(this.runToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            this.logger.LogWarning(ex, "Persona '{PersonaName}' could not delete its old session in room {RoomId}.", this.owner.PersonaName, this.RoomId);
        }
        finally
        {
            await SafeAwaitAsync(previous).ConfigureAwait(false);
        }
    }

    /// <summary>Forgets this Room's stored resume entry and context-fill reading.</summary>
    private void ForgetStoredState()
    {
        if (this.RoomId is not { } roomId)
        {
            return;
        }

        this.roomSessionStore?.Forget(this.owner.PersonaName, roomId);
        if (this.agentId is { } id)
        {
            this.contextFill?.Forget(id, roomId);
        }
    }

    /// <summary>Takes the open session and its reader under <see cref="gate"/> (held by the caller) and marks the session Closed. Disposal is the caller's, outside the lock.</summary>
    /// <returns>The taken session, reader token source and reader loop.</returns>
    private (IAgentSession? Session, CancellationTokenSource? ReaderCts, Task? ReaderTask) TakeOpenLocked()
    {
        (IAgentSession? Session, CancellationTokenSource? ReaderCts, Task? ReaderTask) taken = (this.session, this.eventReaderCts, this.eventReaderTask);
        this.session = null;
        this.state = RoomSessionState.Closed;
        return taken;
    }

    /// <summary>
    /// Runs before an admitted item opens or prompts: waits for a teardown started while idle, then
    /// applies a pending start afresh (close, forget, mark note-pending) so the lazy open that follows is fresh.
    /// </summary>
    /// <returns>A task that completes when the start afresh has been applied.</returns>
    private async Task ApplyFreshStartAsync()
    {
        Task? teardown;
        bool apply;
        lock (this.gate)
        {
            teardown = this.pendingFreshTeardown;
            apply = this.freshStartPending;
            this.freshStartPending = false;
        }

        if (teardown is not null)
        {
            // Not bounded by the run token: the teardown does not depend on it, and DisposeAsync must still see it if this is abandoned.
            await SafeAwaitAsync(teardown).ConfigureAwait(false);
            lock (this.gate)
            {
                if (ReferenceEquals(this.pendingFreshTeardown, teardown))
                {
                    this.pendingFreshTeardown = null;
                }
            }
        }

        if (!apply)
        {
            return;
        }

        (IAgentSession? Session, CancellationTokenSource? ReaderCts, Task? ReaderTask) taken;
        lock (this.gate)
        {
            taken = this.TakeOpenLocked();
        }

        await DisposeOpenAsync(taken.Session, taken.ReaderCts, taken.ReaderTask).ConfigureAwait(false);
        this.ForgetStoredState();
        lock (this.gate)
        {
            this.skipResumeOnNextOpen = true;
            this.freshStartNotePending = true;
        }

        this.StartDelete(taken.Session);
    }

    /// <summary>Tells a newly created session that its first ordinary Turn owes a fresh-start note. Called by the pool before the session serves anything.</summary>
    internal void MarkFreshStartNotePending()
    {
        lock (this.gate)
        {
            this.freshStartNotePending = true;
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
#pragma warning disable CA1849, MA0042 // Deliberately synchronous: CancelAsync would let the callbacks finish after this returns, and the consumer's await on this Turn must be released before the cancel notification below is sent.
        turn.Cancellation.Cancel();
#pragma warning restore MA0042
#pragma warning restore CA1849

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

            // The context reading is deliberately kept: the session is still resumable and its context still exists.
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
            await readerCts.CancelAsync().ConfigureAwait(false);
        }

        if (readerTask is not null)
        {
            await SafeAwaitAsync(readerTask).ConfigureAwait(false);
        }

        if (closing is not null)
        {
            await closing.DisposeAsync().ConfigureAwait(false);
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
    private Task CloseAfterRepeatedFailureAsync() => this.TearDownAsync();

    /// <summary>
    /// The teardown of the repeated-failure close: moves the session to
    /// <see cref="RoomSessionState.Closed"/> from any state and disposes the open agent session and its reader.
    /// </summary>
    private Task TearDownAsync()
    {
        (IAgentSession? Session, CancellationTokenSource? ReaderCts, Task? ReaderTask) taken;
        lock (this.gate)
        {
            taken = this.TakeOpenLocked();
        }

        return DisposeOpenAsync(taken.Session, taken.ReaderCts, taken.ReaderTask);
    }

    /// <summary>Cancels the reader's own token, awaits it, then disposes the agent session. A no-op when nothing was open.</summary>
    /// <param name="closing">The session to dispose, or <see langword="null"/>.</param>
    /// <param name="readerCts">The reader's token source.</param>
    /// <param name="readerTask">The reader loop.</param>
    private static async Task DisposeOpenAsync(IAgentSession? closing, CancellationTokenSource? readerCts, Task? readerTask)
    {
        if (closing is null)
        {
            return;
        }

        if (readerCts is not null)
        {
            await readerCts.CancelAsync().ConfigureAwait(false);
        }

        if (readerTask is not null)
        {
            await SafeAwaitAsync(readerTask).ConfigureAwait(false);
        }

        await closing.DisposeAsync().ConfigureAwait(false);
        readerCts?.Dispose();
    }

    /// <summary>Ends this session's consumer, its event reader, and disposes any open <see cref="IAgentSession"/>, in that order.</summary>
    public async ValueTask DisposeAsync()
    {
        await SafeAwaitAsync(this.consumerTask).ConfigureAwait(false);
        await SafeAwaitAsync(this.eventReaderTask).ConfigureAwait(false);
        await SafeAwaitAsync(this.pendingFreshTeardown).ConfigureAwait(false);
        Task? deleting;
        lock (this.gate)
        {
            deleting = this.pendingDelete;
        }

        await SafeAwaitAsync(deleting).ConfigureAwait(false);


        if (this.session is not null)
        {
            await this.session.DisposeAsync().ConfigureAwait(false);
        }

        this.eventReaderCts?.Dispose();
        this.itemAvailable.Dispose();
    }

    /// <summary>The unlocked core behind <see cref="OpenAsync"/>, reused by the consumer's own "open if Closed" step (RS §6.1 step 2).</summary>
    private async Task OpenCoreAsync(CancellationToken cancellationToken)
    {
        Task opening;
        lock (this.gate)
        {
            if (this.state is RoomSessionState.Idle or RoomSessionState.Busy)
            {
                return;
            }

            if (this.openTask is { } inFlight)
            {
                opening = inFlight;
            }
            else
            {
                this.state = RoomSessionState.Opening;
                opening = this.OpenAndStartReaderAsync(cancellationToken);
                this.openTask = opening;
            }
        }

        try
        {
            await opening.ConfigureAwait(false);
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
            var (opened, resumed, resumedAfterId) = await this.OpenOrResumeAsync(cancellationToken).ConfigureAwait(false);

            // A freshly opened and a resumed session alike: either can be asked a question on its first
            // Turn, and one with no scope bound is answered as cancelled (E-3).
            opened.BindElicitationScope(this);
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

            // A fresh open starts an empty context, so a reading left by an earlier session must
            // not show; a resume keeps it, because that context still exists.
            if (!resumed && this.agentId is { } freshAgentId)
            {
                this.contextFill?.Forget(freshAgentId, this.RoomId);
            }

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
    /// entry whose Adapter, Model, Effort and Work Mode all still match the Persona's current ones and the host
    /// advertises <see cref="IPersonaHost.CanResume"/>; opens fresh otherwise, including when
    /// <see cref="IPersonaHost.ResumeAsync"/> itself answers "not found" (<see langword="null"/>, RS
    /// §9 E-1). A resume that throws is left to propagate - the caller's own catch reports it as an
    /// open failure and keeps the entry (RS §9 E-2), never falling back to a fresh open.
    /// </summary>
    /// <param name="cancellationToken">Cancels the open or resume.</param>
    /// <returns>The opened session, whether it was resumed, and - only when resumed - the stored <see cref="RoomSessionEntry.LastMessageId"/> the Transcript Catch-up range starts after.</returns>
    private async Task<(IAgentSession Session, bool Resumed, string? ResumedAfterMessageId)> OpenOrResumeAsync(CancellationToken cancellationToken)
    {
        bool skipResume;
        lock (this.gate)
        {
            skipResume = this.skipResumeOnNextOpen;
            this.skipResumeOnNextOpen = false;
        }

        if (!skipResume && this.RoomId is { } roomId && this.roomSessionStore is not null && this.host is not null && this.persona is not null)
        {
            var entry = this.roomSessionStore.Get(this.owner.PersonaName, roomId);
            if (entry is not null
                && this.host.CanResume
                && entry.Matches(this.host.Profile.Id, this.persona))
            {
                var resumedSession = await this.host.ResumeAsync(entry.SessionId, cancellationToken).ConfigureAwait(false);
                if (resumedSession is not null)
                {
                    return (resumedSession, true, entry.LastMessageId);
                }
            }
        }

        var opened = await this.open(cancellationToken).ConfigureAwait(false);
        return (opened, false, null);
    }

    private async Task RunConsumerAsync(CancellationToken ct)
    {
        Exception? terminatingException = null;
        try
        {
            while (true)
            {
                await this.ServeNextAsync(ct).ConfigureAwait(false);
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

    /// <summary>
    /// One iteration of the consumer loop: waits for an item, waits for the scheduler to admit it,
    /// serves it and releases the slot.
    /// </summary>
    /// <param name="ct">The run's own cancellation token.</param>
    private async Task ServeNextAsync(CancellationToken ct)
    {
        await this.itemAvailable.WaitAsync(ct).ConfigureAwait(false);

        QueuedWork head;
        lock (this.gate)
        {
            head = this.queue.Peek();
        }

        await this.WaitForAdmissionAsync(head, ct).ConfigureAwait(false);

        this.MarkHeadAdmitted();

        try
        {
            await this.ServeAdmittedHeadAsync(head, ct).ConfigureAwait(false);
        }
        finally
        {
            this.ReleaseAdmittedHead(head);
        }
    }

    /// <summary>
    /// Serves an admitted head: drops it when a Stop or a spent Budget covers it, otherwise opens the
    /// session if it is Closed and processes the item.
    /// </summary>
    /// <param name="head">The admitted work.</param>
    /// <param name="ct">The run's own cancellation token.</param>
    private async Task ServeAdmittedHeadAsync(QueuedWork head, CancellationToken ct)
    {
        if (this.IsStoppedAtOrBefore(head.Item.RoomId, head.Sequence))
        {
            // Queued in this Room at or before its last Stop: drain it without spending
            // anything, rather than working through a backlog the Human already asked to
            // clear for that Room. A different Room's queue is unaffected (finding P-6).
            return;
        }

        // D22 correction 11: checked here, straight after admission and BEFORE opening -
        // an item drained for a spent Budget must never pay for opening a session it will
        // not prompt.
        if (this.owner.TokenBudgetSpent)
        {
            this.owner.ReportTokenBudgetSpent();
            return;
        }

        // Before building the next Turn, let a still-settling Stop cancel from a
        // different Room finish reaching the Adapter first - see pendingStopCancel's
        // comment. This must run even when nothing above was dropped: the very next item
        // after a Stop is exactly the case that races it.
        await this.AwaitPendingStopCancelAsync(ct).ConfigureAwait(false);
        await this.ApplyFreshStartAsync().ConfigureAwait(false);

        if (this.State == RoomSessionState.Closed
            && !await this.TryLazyOpenAsync(head, ct).ConfigureAwait(false))
        {
            return;
        }

        await this.ProcessWorkItemAsync(head.Item, head.Sequence, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Waits for the scheduler to admit <paramref name="head"/>; on shutdown withdraws it before
    /// the cancellation propagates.
    /// </summary>
    /// <param name="head">The queue head that was offered to the scheduler.</param>
    /// <param name="ct">The run's own cancellation token.</param>
    private async Task WaitForAdmissionAsync(QueuedWork head, CancellationToken ct)
    {
        try
        {
            await this.scheduler.WaitAsync(head.Sequence, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Shutdown: the head was offered but will never run (RS §6.1 step 5).
            this.scheduler.Withdraw(head.Sequence);
            throw;
        }
    }

    /// <summary>Dequeues the admitted head and marks the session running, under <see cref="gate"/>.</summary>
    private void MarkHeadAdmitted()
    {
        lock (this.gate)
        {
            this.queue.Dequeue();
            this.running = true;
            this.state = this.state == RoomSessionState.Idle ? RoomSessionState.Busy : this.state;
        }
    }

    /// <summary>
    /// Opens this Closed session inside the admitted slot (finding P-5).
    /// </summary>
    /// <param name="head">The admitted work, used only to report a failure.</param>
    /// <param name="ct">The run's own cancellation token.</param>
    /// <returns><see langword="true"/> when the session is open and the item should be processed; <see langword="false"/> when the open failed and the item is dropped.</returns>
    private async Task<bool> TryLazyOpenAsync(QueuedWork head, CancellationToken ct)
    {
        // Opening happens inside this admitted slot (finding P-5).
        await this.scheduler.MakeRoomToOpenAsync(this, ct).ConfigureAwait(false);

        try
        {
            await this.OpenCoreAsync(ct).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await this.HandleLazyOpenFailureAsync(head, ex).ConfigureAwait(false);
            return false;
        }
    }

    /// <summary>
    /// D24 correction 20: an open or resume failure on the lazy path is a Turn failure, not a fatal
    /// loop error - OpenAndStartReaderAsync's own catch already left the state Closed (D22 correction
    /// 6). No Turn ever started for this item, so nothing is minted and no final MessageDelta is
    /// written; the item is simply dropped, like a Stop-marked one.
    /// </summary>
    /// <param name="head">The dropped work.</param>
    /// <param name="ex">The open or resume failure.</param>
    private async Task HandleLazyOpenFailureAsync(QueuedWork head, Exception ex)
    {
        if (ex is AgentDisconnectedException)
        {
            // RS §9 E-10: the Adapter process itself is gone, which the streak
            // already exists to distinguish from a repeating model/quota/network
            // failure - this does not count toward it, and nothing is forgotten.
            this.owner.ReportOffline("The Adapter process disconnected.");
        }
        else
        {
            this.owner.ReportTurnFailure(head.Item.RoomName, RoomSession.DescribeTurnFailure(ex));

            // D26, finding P-14: an open/resume failure counts toward THIS
            // session's own streak; no store.Put ever ran for this item (no Turn
            // started), so there is no ordering concern - just forget and close.
            if (this.RoomId is { } openFailedRoomId && ++this.roomConsecutiveFailures >= 2)
            {
                this.roomSessionStore?.Forget(this.owner.PersonaName, openFailedRoomId);
                await this.CloseAfterRepeatedFailureAsync().ConfigureAwait(false);
            }
        }

        this.logger.LogWarning(
            ex,
            "Persona '{PersonaName}' failed to open a session for room {RoomId}.",
            this.owner.PersonaName,
            head.Item.RoomId);
    }

    /// <summary>Leaves the admitted slot: clears running, offers the next head, then completes the ticket.</summary>
    /// <param name="head">The work whose ticket is completed.</param>
    private void ReleaseAdmittedHead(QueuedWork head)
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
            await pending.WaitAsync(ct).ConfigureAwait(false);
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
        // collected stays null for a Greeting (FC §6.8: a Greeting neither collects nor commits, the
        // trigger is the prompt itself, not a delivered Message) and for a null tracker (§6.11: the
        // Adapter cannot read files). It is carried on the TurnRun so the finally block can see it
        // (FC §6.8, finding P-17).
        (item, CollectedChanges? collected) = await this.CollectFileChangesAsync(item, ct).ConfigureAwait(false);
        item = await this.AddTranscriptCatchUpAsync(item, ct).ConfigureAwait(false);
        item = await this.AddLibraryDocumentsAsync(item, ct).ConfigureAwait(false);
        item = await this.AddReferencedItemsAsync(item, ct).ConfigureAwait(false);

        // The file is written here, before the watchdog is armed (traps.md TRAP 1): BuildPrompt is pure.
        this.WriteLastCatchUp(item);
        TurnRun? run = this.TryPublishTurn(item, collected, sequence, ct);
        if (run is null)
        {
            return;
        }

        this.ArmWatchdog(run, ct);
        this.BeginTurnActivity(run);

        try
        {
            await this.RunTurnGuardedAsync(run, ct).ConfigureAwait(false);
        }
        finally
        {
            await this.FinishTurnAsync(run, ct).ConfigureAwait(false);
        }

        await this.SendFreshStartNoteAsync(ct).ConfigureAwait(false);

        // D26, RS §6.8, finding P-14: a second consecutive failure closes and forgets THIS per-Room
        // session, so its next Turn opens fresh - checked here, after the block above, so a store
        // entry this same Turn just wrote (P-17) is not left behind for a session about to be torn
        // down (correction 8's ordering: Put, then Forget). A no-op in shared mode (RoomId null) and
        // for any Turn that did not itself report a failure.
        if (this.RoomId is { } failedRoomId && run.RoomFailureThisTurn && ++this.roomConsecutiveFailures >= 2)
        {
            this.roomSessionStore?.Forget(this.owner.PersonaName, failedRoomId);
            await this.CloseAfterRepeatedFailureAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Collects the Room's File Changes at Turn start and attaches the report to the item.
    /// </summary>
    /// <remarks>
    /// Collected before the Turn/cancellation/watchdog exists, so a slow disk scan is never counted
    /// as Adapter silence (rules.md TRAP 1/TRAP 2 ordering) and so a Turn that waited in the queue
    /// lists changes made while it waited. A scan or directory failure must not kill the consumer
    /// loop, which nothing else drains.
    /// </remarks>
    /// <param name="item">The item about to be prompted.</param>
    /// <param name="ct">The run's own cancellation token.</param>
    /// <returns>The item (with <see cref="WorkItem.FileChanges"/> set for a Message when a tracker exists) and what was collected, or <see langword="null"/> when nothing was.</returns>
    private async Task<(WorkItem Item, CollectedChanges? Collected)> CollectFileChangesAsync(WorkItem item, CancellationToken ct)
    {
        CollectedChanges? collected = null;

        if (item.Kind == WorkItemKind.Message && this.fileChanges is not null)
        {
            try
            {
                collected = await this.fileChanges.CollectAsync(this.owner.PersonaName, item.RoomId, this.declaredWatches, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                this.logger.LogWarning(ex, "Persona '{PersonaName}' failed to collect File Changes for room {RoomId}.", this.owner.PersonaName, item.RoomId);
            }

            item = item with { FileChanges = collected?.Report };
        }

        return (item, collected);
    }

    /// <summary>
    /// D24, RS §6.5: the first Turn after any open (per-Room only) reads the Room's Transcript,
    /// after the File Changes collect and before the locked check-and-publish (D22 correction 3).
    /// </summary>
    /// <remarks>
    /// Open and this read must both finish before the watchdog is armed, so neither counts as
    /// Adapter silence. The first-Turn flag is cleared here regardless of outcome: RS §9 E-3
    /// (refused or timed out) and a Greeting or a Turn with no TriggerMessageId all still consume
    /// this Turn's "first after open" opportunity (D24 correction 19) without ever reading anything.
    /// A Command Turn sends only the bare command, so it reads no Transcript and must not use the
    /// opportunity up: the Messages this session has not seen are still unseen, and the next
    /// ordinary Turn is the one that needs them (Commands spec, section 6.6).
    /// </remarks>
    /// <param name="item">The item about to be prompted.</param>
    /// <param name="ct">The run's own cancellation token.</param>
    /// <returns>The item, with its Transcript Catch-up set when one was read.</returns>
    private async Task<WorkItem> AddTranscriptCatchUpAsync(WorkItem item, CancellationToken ct)
    {
        if (this.firstTurnPending && item.Kind != WorkItemKind.Command)
        {
            this.firstTurnPending = false;
            if (this.FreshStartNotePending)
            {
                this.noteOwed = new FreshStartNote(item.RoomId, "asked", 0, 0, false);
            }

            if (this.RoomId is not null && item.Kind == WorkItemKind.Message && item.TriggerMessageId is { } triggerId)
            {
                SessionSettings settings = this.sessionSettings?.Current ?? new SessionSettings(this.options.TranscriptCatchUpMessages, this.options.TranscriptCatchUpTokens);
                TranscriptTail? tail = null;
                try
                {
                    tail = await this.owner.ReadTranscriptAsync(
                        item.RoomId,
                        this.resumedOnLastOpen ? this.resumedAfterMessageId : null,
                        triggerId,
                        settings.CatchUpMessages,
                        ct).ConfigureAwait(false);
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
                        Transcript = this.BuildCatchUp(tail, settings),
                        OwnPostLines = null,
                    };
                }
                else if (this.noteOwed is not null)
                {
                    this.noteOwed = this.noteOwed with { ReadFailed = true };
                }
            }
        }

        return item;
    }

    /// <summary>Trims a Transcript tail to the token budget (LR-R11) and counts what the trim dropped on top of the tail's own omitted count.</summary>
    /// <param name="tail">The tail the owner returned.</param>
    /// <param name="settings">The settings snapshot that set the Message cap for this read.</param>
    /// <returns>The catch-up for the Turn.</returns>
    private TranscriptCatchUp BuildCatchUp(TranscriptTail tail, SessionSettings settings)
    {
        CatchUpTrimResult result = CatchUpTrim.Apply(tail.Messages, this.IsOwn, settings.CatchUpTokens, this.options.CatchUpKeepOwnMessages);
        if (this.noteOwed is not null)
        {
            this.noteOwed = this.noteOwed with { MessageCount = result.Kept.Count, EstimatedTokens = result.EstimatedTokens };
        }

        return new TranscriptCatchUp(this.resumedOnLastOpen, result.Kept, tail.Omitted + result.Dropped, result.DroppedOwn);
    }

    /// <summary>Writes the whole prompt text of the first ordinary Turn after a fresh start to the last-catch-up file (C3). A failure is logged and never fails the Turn.</summary>
    /// <param name="item">The item about to be prompted, fully enriched.</param>
    private void WriteLastCatchUp(WorkItem item)
    {
        if (this.noteOwed is null || this.lastCatchUp is null)
        {
            return;
        }

        try
        {
            this.lastCatchUp.Write(this.owner.PersonaName, item.RoomId, RoomSession.BuildPrompt(item, this.prompts));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            this.logger.LogWarning(
                ex, "Persona '{PersonaName}' could not write the last catch-up for room {RoomId}.", this.owner.PersonaName, item.RoomId);
        }
    }

    /// <summary>Sends the fresh-start note the finished Turn owed, if any, and clears the owed flag. A failure is logged and never fails the Turn.</summary>
    /// <param name="ct">The run's own cancellation token.</param>
    /// <returns>A task that completes when the note has been handed to the owner.</returns>
    private async Task SendFreshStartNoteAsync(CancellationToken ct)
    {
        FreshStartNote? note = this.noteOwed;
        if (note is null)
        {
            return;
        }

        this.noteOwed = null;
        lock (this.gate)
        {
            this.freshStartNotePending = false;
        }

        try
        {
            await this.owner.WriteAsync(note, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            this.logger.LogWarning(
                ex, "Persona '{PersonaName}' could not send the fresh-start note for room {RoomId}.", this.owner.PersonaName, note.RoomId);
        }
    }

    /// <summary>Whether <paramref name="message"/> is this Agent's own (C7): by sender id when the session has an agent id, otherwise by Persona name.</summary>
    /// <param name="message">The Message to test.</param>
    /// <returns><see langword="true"/> when the Agent posted it.</returns>
    private bool IsOwn(ChatMessage message) =>
        this.agentId is { } id
            ? string.Equals(message.SenderId, id, StringComparison.Ordinal)
            : string.Equals(message.SenderName, this.owner.PersonaName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Spec §6.14, corrections-B6 item 7 / B5 item 28: collects the Library documents the Turn's
    /// texts mention, after the Transcript Catch-up read and before the locked check-and-publish.
    /// </summary>
    /// <remarks>
    /// Collected before the publish so a slow scan is never counted as Adapter silence, the same
    /// reasoning the File Changes collect already relies on. Scans the triggering Message's own
    /// text, the catch-up buffer and, when this Turn set one, the Transcript Catch-up - never
    /// OwnPostLines (this Agent's own earlier posts, not new documents to fetch) and never a
    /// Greeting's empty text (the Kind check). A scan or file-read failure must not kill the
    /// consumer loop, which nothing else drains.
    /// </remarks>
    /// <param name="item">The item about to be prompted.</param>
    /// <param name="ct">The run's own cancellation token.</param>
    /// <returns>The item, with <see cref="WorkItem.LibraryDocuments"/> set when the collect succeeded.</returns>
    private async Task<WorkItem> AddLibraryDocumentsAsync(WorkItem item, CancellationToken ct)
    {
        if (this.libraryDocs is not null && item.Kind == WorkItemKind.Message)
        {
            try
            {
                IReadOnlyList<string> texts =
                [
                    item.Text,
                    .. item.MissedMessages.Select(missed => missed.Text),
                    .. item.Transcript?.Messages.Select(message => message.Text) ?? [],
                ];
                var report = await this.libraryDocs.CollectAsync(texts, this.PromptDeliveryFor(this.libraryDocs), ct).ConfigureAwait(false);
                item = item with { LibraryDocuments = report };
                this.LogWithheldImages(report, item.RoomId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                this.logger.LogWarning(
                    ex, "Persona '{PersonaName}' failed to collect Library documents for room {RoomId}.", this.owner.PersonaName, item.RoomId);
            }
        }

        return item;
    }

    /// <summary>
    /// Spec section 9.1: the Referenced items header describes only the references in the triggering
    /// Message's own text, never the catch-up or Transcript, looked up now.
    /// </summary>
    /// <remarks>
    /// Only an ordinary Message Turn gets one: a Command is the bare command and a Greeting has no
    /// Message (traps.md, a Command Turn must not drain anything it does not send). Expanded before
    /// the locked check-and-publish, like the Library collect, so a slow lookup is not counted as
    /// Adapter silence. A failed lookup must not kill the consumer loop, which nothing else drains.
    /// </remarks>
    /// <param name="item">The item about to be prompted.</param>
    /// <param name="ct">The run's own cancellation token.</param>
    /// <returns>The item, with <see cref="WorkItem.ReferencedItems"/> set when the lookup succeeded.</returns>
    private async Task<WorkItem> AddReferencedItemsAsync(WorkItem item, CancellationToken ct)
    {
        if (this.references is not null && item.Kind == WorkItemKind.Message)
        {
            try
            {
                string? header = await this.references.ExpandAsync(item.Text, this.host?.Profile.UsesToolNamePrefix ?? true, ct).ConfigureAwait(false);
                item = item with { ReferencedItems = header };
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                this.logger.LogWarning(
                    ex, "Persona '{PersonaName}' failed to expand references for room {RoomId}.", this.owner.PersonaName, item.RoomId);
            }
        }

        return item;
    }

    /// <summary>
    /// Mints the Turn's Message id and publishes the Turn as <see cref="activeTurn"/>, unless a Stop
    /// covers it or the session is gone.
    /// </summary>
    /// <remarks>
    /// Exactly one session serves every Room the Agent is in (there is one session per Persona in
    /// shared mode), so a failed turn must log and continue rather than end the loop: a dying
    /// agent process must not silently deafen the Agent for every other Room.
    /// The Message id is minted before the prompt is sent, in the same form ChatService.PostAsync
    /// uses when its own messageId argument is null: a 32-character lowercase-hex Guid, which
    /// NameRules.IsValidId accepts. Carrying it on PostMessage is what lets a ProtocolError's
    /// RelatedMessageId, and later a MessageDelta, correlate back to this Turn.
    /// </remarks>
    /// <param name="item">The fully prepared item.</param>
    /// <param name="collected">What the File Changes collect returned, if anything.</param>
    /// <param name="sequence">The item's scheduler sequence, compared against the Room's Stop mark.</param>
    /// <param name="ct">The run's own cancellation token, which the Turn's token is linked to.</param>
    /// <returns>The published Turn's state, or <see langword="null"/> when nothing was published and nothing was committed.</returns>
    private TurnRun? TryPublishTurn(WorkItem item, CollectedChanges? collected, long sequence, CancellationToken ct)
    {
        var messageId = Guid.CreateVersion7().ToString("N");
        var completion = new TaskCompletionSource<TurnOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        var turnCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
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
                return null;
            }

            if (this.session is not { } opened)
            {
                // The open-if-Closed step in the consumer must have succeeded before this method is
                // ever called; a null session here means it did not, so there is nothing to prompt.
                turnCancellation.Dispose();
                return null;
            }

            // Built here, after both early returns above, so a Turn that never publishes never creates
            // the question source linked to turnCancellation that EndUserInput must dispose.
            ActiveTurn turn = new(item.RoomId, messageId, new StringBuilder(), completion, turnCancellation, this.time);
            this.activeTurn = turn;
            return new TurnRun(item, collected, messageId, completion, turnCancellation, turn, opened);
        }
    }

    /// <summary>
    /// Arms the idle-timeout watchdog for the published Turn.
    /// </summary>
    /// <remarks>
    /// Armed right after the Turn is published, so it covers the whole Turn including the initial
    /// PromptAsync call - a hang before the first event is exactly what motivates this.
    /// </remarks>
    /// <param name="run">The published Turn.</param>
    /// <param name="ct">The run's own cancellation token.</param>
    private void ArmWatchdog(TurnRun run, CancellationToken ct)
    {
        TimeSpan idleBound = IdleTimeoutFrom(this.options.TurnIdleTimeoutSeconds);
        run.IdleBound = idleBound;
        run.Watchdog = idleBound > TimeSpan.Zero
            ? this.WatchForAdapterSilenceAsync(run.ActiveSession, run.Turn, run.Item, idleBound, run.WatchdogCancellation.Token, ct)
            : null;
    }

    /// <summary>Tells the own-post and activity trackers a Turn has begun.</summary>
    /// <param name="run">The published Turn.</param>
    private void BeginTurnActivity(TurnRun run)
    {
        if (this.agentId is { } beginTurnAgentId)
        {
            this.ownPosts?.BeginTurn(beginTurnAgentId, this.RoomId);

            // D8 correction 1: this.RoomId is null for a shared session, but the Turn itself always
            // belongs to a real Room - turn.RoomId, captured from item.RoomId above - so TurnActivity
            // is always told the Turn's own Room, never this session's (possibly null) one.
            this.turnActivity?.Begin(beginTurnAgentId, run.Turn.RoomId);
        }
    }

    /// <summary>
    /// Runs the Turn and classifies how it ended: a completed Turn is reported by
    /// <see cref="RunTurnAsync"/>; the catch clauses here handle a watchdog timeout, a Human Stop,
    /// shutdown, a disconnect and any other failure.
    /// </summary>
    /// <param name="run">The published Turn.</param>
    /// <param name="ct">The run's own cancellation token.</param>
    private async Task RunTurnGuardedAsync(TurnRun run, CancellationToken ct)
    {
        try
        {
            await this.RunTurnAsync(run, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && !run.Turn.StopRequested && run.Turn.TimedOut)
        {
            // The idle-timeout watchdog fired, not a Human Stop (TRAP 1): both cancel the same Turn
            // token and land here as the identical OperationCanceledException, so the filter checks
            // the latches each producer writes BEFORE it cancels, not the exception. A genuine Stop
            // wins any tie - only StopAsync sets StopRequested, and this filter demands it be absent -
            // because a Human's own Stop must never be reported as a failure.
            this.ReportIdleTimeout(run);
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
                    run.Item.RoomId);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (AgentDisconnectedException ex)
        {
            this.owner.ReportOffline("The Adapter process disconnected.");
            this.logger.LogWarning(ex, "Persona '{PersonaName}' failed to process a turn in room {RoomId}.", this.owner.PersonaName, run.Item.RoomId);
        }
        catch (Exception ex)
        {
            // Quota, a network failure and expiring credentials all arrive as the same AgentException
            // ("session/prompt failed: ..."), and its wording belongs to the Adapter and will change -
            // so this escalates on repetition alone, never on message content. Stays Degraded rather
            // than Offline: the session and the pipe may both be healthy while the model provider is
            // refusing, and Offline would be a claim this runner cannot support.
            this.owner.ReportTurnFailure(run.Item.RoomName, RoomSession.DescribeTurnFailure(ex));
            run.RoomFailureThisTurn = true;
            this.logger.LogWarning(ex, "Persona '{PersonaName}' failed to process a turn in room {RoomId}.", this.owner.PersonaName, run.Item.RoomId);
        }
    }

    /// <summary>The reason a failed Turn reports: the Human's sign-in instruction for an authentication failure, the exception text otherwise.</summary>
    /// <param name="ex">The exception the Turn failed with.</param>
    /// <returns>The reason passed to <see cref="IRoomSessionOwner.ReportTurnFailure"/>.</returns>
    private static string DescribeTurnFailure(Exception ex) =>
        ex is AgentAuthenticationRequiredException ? PersonaSupervisor.SignInAgainReason : ex.Message;

    /// <summary>Reports a Turn the idle-timeout watchdog abandoned: tells the Room and logs the silence that triggered it.</summary>
    /// <param name="run">The abandoned Turn.</param>
    private void ReportIdleTimeout(TurnRun run)
    {
        this.owner.ReportTurnFailure(
            run.Item.RoomName,
            string.Create(
                CultureInfo.InvariantCulture,
                $"the Adapter sent nothing for {run.IdleBound.TotalSeconds} seconds, so the Turn was abandoned and cancelled"));
        run.RoomFailureThisTurn = true;
#pragma warning disable HAA0601 // Boxing a value-type log argument is the cost of ILogger params object[]; accepted together with HAA0101.
        this.logger.LogWarning(
            "Persona '{PersonaName}' turn in room {RoomId} was abandoned after {IdleBoundSeconds} seconds of silence.",
            this.owner.PersonaName,
            run.Item.RoomId,
            run.IdleBound.TotalSeconds);
#pragma warning restore HAA0601
    }

    /// <summary>Sends the prompt, waits for the Turn's outcome, posts the reply and reports the outcome's health.</summary>
    /// <param name="run">The published Turn.</param>
    /// <param name="ct">The run's own cancellation token.</param>
    private async Task RunTurnAsync(TurnRun run, CancellationToken ct)
    {
        WorkItem item = run.Item;
        var prompt = RoomSession.BuildPrompt(item, this.prompts);
        await run.ActiveSession.PromptAsync(new AgentPrompt(prompt, RoomSession.BuildPromptBlocks(item)), run.TurnCancellation.Token).ConfigureAwait(false);
        run.PromptReturned = true;
        var outcome = await run.Completion.Task.WaitAsync(run.TurnCancellation.Token).ConfigureAwait(false);

        string? postText = RoomSession.ResolvePostText(run, outcome);

        if (postText is not null)
        {
            await this.ReleaseHeldTailAsync(run, SilentReply.Strip(outcome.Text), ct).ConfigureAwait(false);
            await this.owner.WriteAsync(new PostMessage(item.RoomId, run.MessageId, postText), ct).ConfigureAwait(false);
            run.ReplyPosted = true;
        }

        // A compaction drops the context's fill and the next ordinary Turn rebuilds the session's
        // fixed overhead, a rise that is not a cost of that Turn. Taking the next update as the
        // baseline, as a resumed session does, keeps it from being counted as spend (Commands
        // spec, section 6.7). Only a completed command: a Stop changed nothing.
        if (item.Kind == WorkItemKind.Command && outcome.Reason == StopReason.EndTurn)
        {
            this.usageBaselinePending = true;
        }

        this.ReportTurnHealth(outcome.Reason);
    }

    /// <summary>Streams what was held back at the end of a Turn that turned out not to end with the silent token.</summary>
    /// <param name="run">The Turn that finished.</param>
    /// <param name="postText">The text being posted, which is what the page should end up showing.</param>
    /// <param name="ct">The run's own cancellation token.</param>
    private async Task ReleaseHeldTailAsync(TurnRun run, string postText, CancellationToken ct)
    {
        int streamed;
        lock (this.gate)
        {
            streamed = run.Turn.StreamedLength;
        }

        if (run.Turn.DeltaWriteFailed || streamed >= postText.Length)
        {
            return;
        }

        try
        {
            await this.owner.WriteAsync(new MessageDelta(run.Item.RoomId, run.MessageId, postText[streamed..], IsFinal: false), ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            // The pipe died; the posted Message below carries the text, so nothing is lost.
            run.Turn.DeltaWriteFailed = true;
            this.logger.LogWarning(ex, "Persona '{PersonaName}' failed to write a message delta for a turn in room {RoomId}.", this.owner.PersonaName, run.Item.RoomId);
        }
    }

    /// <summary>The text to post for a finished Turn, or <see langword="null"/> when there is nothing fit to post.</summary>
    /// <param name="run">The Turn that finished.</param>
    /// <param name="outcome">How it ended.</param>
    /// <returns>The reply text, or the fixed line describing a command that said nothing.</returns>
    private static string? ResolvePostText(TurnRun run, TurnOutcome outcome)
    {
        // A refusal or a cancelled Turn is not a successful reply, even if some text arrived
        // before the stop: neither is fit to post into the Room.
        // A reply that is the silent token, or ends with it, loses the token first; what is left decides
        // whether there is anything to post.
        string replyText = SilentReply.Strip(outcome.Text);
        var isPostable = outcome.Reason is not (StopReason.Refusal or StopReason.Cancelled)
            && !string.IsNullOrWhiteSpace(replyText);
        string? postText = isPostable ? replyText : null;

        // An Adapter's /compact replies with no text at all, so a command that said nothing is
        // reported by a fixed line built from its tool output. Without it the Room could not tell a
        // finished compaction from a failed one. A command that did reply keeps its own words.
        if (postText is null && run.Item.Kind == WorkItemKind.Command && run.Item.Command is { } ranCommand)
        {
            postText = AdapterCommandOutcome.Describe(ranCommand.Name, outcome.Reason, run.Turn.LastOutputJson, run.Turn.ToolCallFailed);
        }

        return postText;
    }

    /// <summary>
    /// Health reporting (T4.3): every StopReason but Cancelled is a Turn that completed, so it resets
    /// the escalating-failure counter - Cancelled is the Human stopping it, reported as nothing, and
    /// the one outcome that must leave the counter untouched.
    /// </summary>
    /// <param name="reason">How the Turn stopped.</param>
    private void ReportTurnHealth(StopReason reason)
    {
        switch (reason)
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
                this.owner.ReportIncompleteStop(reason);
                break;
            case StopReason.Cancelled:
                break;
        }
    }

    /// <summary>
    /// Tears the Turn down on every path it can end, in a fixed order: watchdog, trackers, user input,
    /// <see cref="activeTurn"/>, the Draft terminator, the File Changes commit, then the stored
    /// session entry, and finally the Turn's cancellation source.
    /// </summary>
    /// <remarks>
    /// An exception other than IOException/ObjectDisposedException from the terminator write escapes
    /// this method entirely, so the commit, the store write and the Dispose are all skipped in that
    /// case; acceptable, since the consumer's own catch already logs and keeps the loop alive, and
    /// nothing was lost that a later commit could still repair.
    /// </remarks>
    /// <param name="run">The Turn that ended.</param>
    /// <param name="ct">The run's own cancellation token.</param>
    private async Task FinishTurnAsync(TurnRun run, CancellationToken ct)
    {
        // First, and before the Turn's own CancellationTokenSource is disposed below: a watchdog
        // still running past that point would call Cancel() on a disposed source. Cancelling it
        // stops WatchForAdapterSilenceAsync's Task.Delay immediately when the Turn ended on its
        // own, without waiting out whatever of the idle bound remained.
        await run.WatchdogCancellation.CancelAsync().ConfigureAwait(false);
        await SafeAwaitAsync(run.Watchdog).ConfigureAwait(false);
        run.WatchdogCancellation.Dispose();

        if (this.agentId is { } endTurnAgentId)
        {
            this.ownPosts?.EndTurn(endTurnAgentId, this.RoomId);
            this.turnActivity?.End(endTurnAgentId, run.Turn.RoomId);
        }

        // However this Turn ended - the Adapter finished, a Stop, the watchdog, a shutdown, a
        // failure - a question still waiting on it can no longer be answered, so its token is
        // cancelled here (E-3). Before activeTurn is cleared below, and unconditionally: the
        // Adapter's own prompt call ends without cancelling anything it handed out.
        run.Turn.EndUserInput();

        lock (this.gate)
        {
            if (ReferenceEquals(this.activeTurn, run.Turn))
            {
                this.activeTurn = null;
            }
        }

        await this.WriteFinalDeltaAsync(run, ct).ConfigureAwait(false);
        await this.CommitFileChangesAsync(run, ct).ConfigureAwait(false);
        this.StoreSessionEntry(run, ct);

        run.TurnCancellation.Dispose();
    }

    /// <summary>
    /// Writes the Draft terminator on every path a Turn can end while this runner is alive -
    /// success, refusal, exception, and Stop - so a partial reply can never outlive its Turn on screen.
    /// </summary>
    /// <remarks>
    /// Shutdown is the one path that deliberately skips it, and skipping is not a gap: the
    /// pipe is closing, and AgentGateway.Unregister clears every Draft this Agent owns as the
    /// connection drops, so the Room is left clean either way. Writing it anyway needed an
    /// uncancellable token - the run token is already cancelled by then - and an unbounded
    /// write into a pipe that is being torn down simply blocks, hanging teardown until
    /// something else happens to cancel it.
    /// </remarks>
    /// <param name="run">The Turn that ended.</param>
    /// <param name="ct">The run's own cancellation token.</param>
    private async Task WriteFinalDeltaAsync(TurnRun run, CancellationToken ct)
    {
        if (!ct.IsCancellationRequested)
        {
            try
            {
                await this.owner.WriteAsync(new MessageDelta(run.Item.RoomId, run.MessageId, string.Empty, IsFinal: true), ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                // The pipe died before the terminator could be written; the Turn already ended
                // one way or another, so there is nothing left to signal and nothing to retry.
                this.logger.LogWarning(ex, "Persona '{PersonaName}' failed to write the final delta for a turn in room {RoomId}.", this.owner.PersonaName, run.Item.RoomId);
            }
        }
    }

    /// <summary>
    /// Commits the File Changes the Turn collected, after the Draft terminator so a slow or failing
    /// commit never delays the Turn's own MessageDelta reaching the Room.
    /// </summary>
    /// <remarks>
    /// Uses the Turn's own state, never this.activeTurn - already nulled - and copies Touched under
    /// gate since the event reader can still be writing to it concurrently with this method's own
    /// thread. Gated on !ct.IsCancellationRequested (shutdown) and, per finding P-17, on the Turn
    /// having either returned from PromptAsync or shown activity: a prompt refused outright before
    /// any event (E-5) saw neither and must not commit, so the Room's list repeats next Turn; a Stop
    /// after activity (E-6) still commits.
    /// </remarks>
    /// <param name="run">The Turn that ended.</param>
    /// <param name="ct">The run's own cancellation token.</param>
    private async Task CommitFileChangesAsync(TurnRun run, CancellationToken ct)
    {
        if (this.fileChanges is not null && run.Collected is not null && !ct.IsCancellationRequested && (run.PromptReturned || run.Turn.SawActivity))
        {
            HashSet<string> touched;
            lock (this.gate)
            {
                touched = [.. run.Turn.Touched];
            }

            try
            {
                await this.fileChanges.CommitAsync(this.owner.PersonaName, run.Item.RoomId, run.Collected, touched, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                this.logger.LogWarning(ex, "Persona '{PersonaName}' failed to commit File Changes for room {RoomId}.", this.owner.PersonaName, run.Item.RoomId);
            }
        }
    }

    /// <summary>
    /// RS §6.6 "Written at Turn end": per-Room mode only, whenever the prompt was sent (finding P-17,
    /// same gate as the File Changes commit) and the run is not shutting down.
    /// </summary>
    /// <remarks>
    /// LastMessageId is the reply's own minted id when one was actually posted, else the triggering
    /// Message's - a Greeting with neither leaves it null, legal per RoomSessionEntry's own doc
    /// comment. A Command Turn writes nothing: it left the Catch-up buffer undrained, so this session
    /// has NOT seen the Messages before it, and storing the outcome Message's id as LastMessageId
    /// would make a resume skip them for good. The entry an earlier Turn stored is still right,
    /// because a compaction does not change the session id, Model or Effort.
    /// </remarks>
    /// <param name="run">The Turn that ended.</param>
    /// <param name="ct">The run's own cancellation token.</param>
    private void StoreSessionEntry(TurnRun run, CancellationToken ct)
    {
        if (this.RoomId is { } storeRoomId && this.roomSessionStore is not null && this.host is not null && this.persona is not null
            && run.Item.Kind != WorkItemKind.Command
            && !ct.IsCancellationRequested && (run.PromptReturned || run.Turn.SawActivity))
        {
            var lastMessageId = run.ReplyPosted ? run.MessageId : run.Item.TriggerMessageId;

            // The meter's latest reading rides along, so a restart can show it again before this
            // Room's session reopens. Read after the Turn's usage updates, which precede its outcome.
            ContextFillReading? context = this.agentId is { } id ? this.contextFill?.Get(id, storeRoomId) : null;
            this.roomSessionStore.Put(
                this.owner.PersonaName,
                storeRoomId,
                new RoomSessionEntry(
                    run.ActiveSession.SessionId, this.host.Profile.Id, this.persona.Model, this.persona.Effort, lastMessageId, this.time.GetUtcNow(), this.persona.WorkMode,
                    context?.Used, context?.Size ?? 0));
        }
    }

    /// <summary>
    /// The state of one published Turn that <see cref="ProcessWorkItemAsync"/> threads through its
    /// phases: what the Turn is, and the latches its phases write for the later ones to read.
    /// </summary>
    /// <param name="item">The fully prepared item.</param>
    /// <param name="collected">What the File Changes collect returned, null for a Greeting or without a tracker.</param>
    /// <param name="messageId">The Message id minted for this Turn.</param>
    /// <param name="completion">Completed by the event reader when the Turn's outcome arrives.</param>
    /// <param name="turnCancellation">The Turn's own cancellation source, disposed last.</param>
    /// <param name="turn">The Turn published as the active one.</param>
    /// <param name="activeSession">The session the Turn was published against.</param>
    private sealed class TurnRun(
        WorkItem item,
        CollectedChanges? collected,
        string messageId,
        TaskCompletionSource<TurnOutcome> completion,
        CancellationTokenSource turnCancellation,
        ActiveTurn turn,
        IAgentSession activeSession)
    {
        /// <summary>Gets the fully prepared item.</summary>
        public WorkItem Item { get; } = item;

        /// <summary>Gets what the File Changes collect returned.</summary>
        public CollectedChanges? Collected { get; } = collected;

        /// <summary>Gets the Message id minted for this Turn.</summary>
        public string MessageId { get; } = messageId;

        /// <summary>Gets the completion the event reader completes with the Turn's outcome.</summary>
        public TaskCompletionSource<TurnOutcome> Completion { get; } = completion;

        /// <summary>Gets the Turn's own cancellation source.</summary>
        public CancellationTokenSource TurnCancellation { get; } = turnCancellation;

        /// <summary>Gets the Turn published as the active one.</summary>
        public ActiveTurn Turn { get; } = turn;

        /// <summary>Gets the session the Turn was published against.</summary>
        public IAgentSession ActiveSession { get; } = activeSession;

        /// <summary>Gets the cancellation source of the idle-timeout watchdog.</summary>
        public CancellationTokenSource WatchdogCancellation { get; } = new();

        /// <summary>Gets or sets the idle bound this Turn is watched against.</summary>
        public TimeSpan IdleBound { get; set; }

        /// <summary>Gets or sets the running watchdog, or <see langword="null"/> when the bound is disabled.</summary>
        public Task? Watchdog { get; set; }

        /// <summary>Gets or sets whether PromptAsync returned (finding P-17).</summary>
        public bool PromptReturned { get; set; }

        /// <summary>Gets or sets whether a reply was posted for this Turn.</summary>
        public bool ReplyPosted { get; set; }

        /// <summary>
        /// Gets or sets whether this Turn's own failure/timeout catch clauses latched a failure (D26,
        /// finding P-14): read only after the store write (correction 8's ordering), so a second
        /// consecutive failure in per-Room mode can close and forget this session without undoing
        /// the entry this same Turn just wrote.
        /// </summary>
        public bool RoomFailureThisTurn { get; set; }
    }

    /// <summary>
    /// Bounds silence, not duration: <see cref="ActiveTurn.IdleFor"/> restarts on every event
    /// <see cref="RunEventReaderAsync"/> observes, so a long streaming Turn that keeps reporting
    /// activity never trips this however long it runs; only a Turn that goes quiet for
    /// <paramref name="idleBound"/> does.
    /// </summary>
    /// <remarks>
    /// TRAP 2 (docs/engineering/rules.md): the obvious implementation - cancel <paramref name="turn"/>'s
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
                await Task.Delay(remaining, this.time, watchdogToken).ConfigureAwait(false);
                remaining = idleBound - turn.IdleFor;
            }

            // Latched BEFORE the cancellation below, so ProcessWorkItemAsync's catch can tell this
            // firing apart from a genuine Stop (TRAP 1) - both surface as the same
            // OperationCanceledException there.
            turn.MarkTimedOut();

#pragma warning disable HAA0601 // Boxing a value-type log argument is the cost of ILogger params object[]; accepted together with HAA0101.
            this.logger.LogWarning(
                "Persona '{PersonaName}' turn in room {RoomId} sent nothing for {IdleBoundSeconds} seconds; cancelling it.",
                this.owner.PersonaName,
                item.RoomId,
                idleBound.TotalSeconds);
#pragma warning restore HAA0601

            try
            {
                // TRAP 2: tell the far side first, before this Turn's own token is cancelled below.
                await activeSession.CancelAsync(ct).WaitAsync(AdapterCancelGrace, this.time, ct).ConfigureAwait(false);
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

            await turn.Cancellation.CancelAsync().ConfigureAwait(false);
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

    /// <summary>Converts the configured question bound to a <see cref="TimeSpan"/>, clamped so it is never disabled and never too large to arm.</summary>
    /// <param name="seconds"><see cref="AcpOptions.UserInputTimeoutSeconds"/>'s current value.</param>
    /// <returns>The bound, between <see cref="MinUserInputTimeoutSeconds"/> and <see cref="MaxUserInputTimeoutSeconds"/> seconds.</returns>
    private static TimeSpan UserInputTimeoutFrom(int seconds) =>
        TimeSpan.FromSeconds(Math.Clamp(seconds, MinUserInputTimeoutSeconds, MaxUserInputTimeoutSeconds));

    private async Task RunEventReaderAsync(IAgentSession activeSession, CancellationToken ct)
    {
        Exception? terminatingException = null;
        try
        {
            await foreach (var agentEvent in activeSession.Events.ReadAllAsync(ct).ConfigureAwait(false))
            {
                await this.HandleAgentEventAsync(agentEvent, ct).ConfigureAwait(false);
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
            this.FailStrandedTurn();

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
    /// Dispatches one event from the Adapter's stream: streamed text, tool calls, Turn completion,
    /// usage and the advertised command list. Every event first counts as a sign of life.
    /// </summary>
    /// <param name="agentEvent">The event just read.</param>
    /// <param name="ct">The reader's own cancellation token.</param>
    private async Task HandleAgentEventAsync(AgentEvent agentEvent, CancellationToken ct)
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
            await this.AppendAndPublishDeltaAsync(chunk.Text, ct).ConfigureAwait(false);
        }
        else if (agentEvent is ToolCallStarted started)
        {
            this.RecordToolCallResult(started.ToolCallId, isStart: true, started.Status, null);
            this.RecordTouchedPaths(started.Kind, started.RawInputJson);
            string? memoryFile = this.RecordMemoryToolPath(started.Kind, started.Title, started.ToolName, started.RawInputJson);
            await this.WriteToolActivityAsync(started.ToolCallId, started.Title, MapToolCallStatus(started.Status), started.Location, started.Diff, memoryFile, ct).ConfigureAwait(false);
        }
        else if (agentEvent is ToolCallUpdated updated)
        {
            this.RecordToolCallResult(updated.ToolCallId, isStart: false, updated.Status, updated.RawOutputJson);
            this.RecordTouchedPaths(updated.Kind, updated.RawInputJson);
            string? memoryFile = this.RecordMemoryToolPath(updated.Kind, updated.Title, updated.ToolName, updated.RawInputJson);
            await this.WriteToolActivityAsync(updated.ToolCallId, updated.Title, MapToolCallStatus(updated.Status), updated.Location, updated.Diff, memoryFile, ct).ConfigureAwait(false);
        }
        else if (agentEvent is TurnCompleted completed)
        {
            this.CompleteActiveTurn(completed);
        }
        else if (agentEvent is UsageUpdated usage)
        {
            this.CountUsage(usage);
        }
        else if (agentEvent is AvailableCommandsUpdated advertised)
        {
            // The Adapter's complete list, replacing the last. The owner filters it by the
            // Adapter Profile's allowlist and keeps only what a Human may run.
            this.owner.SetCommands(advertised.SessionId, advertised.Commands);
        }
        else if (agentEvent is AgentNotice notice)
        {
            await this.ShowNoticeAsync(notice, ct).ConfigureAwait(false);
        }

        // ThoughtChunk, PlanUpdated, ModeChanged, UserMessageChunk, UnsupportedContent and
        // UnknownUpdate are ignored.
    }

    /// <summary>
    /// Logs an Adapter notice and shows it as a <see cref="ToolActivityStatus.Notice"/> row in the active
    /// Turn's activity, so it is never part of the reply text. A notice outside a Turn is logged only:
    /// a notice is live, not history, so there is nowhere later to show it.
    /// </summary>
    /// <param name="notice">The notice the Adapter sent.</param>
    /// <param name="ct">The reader's own cancellation token.</param>
    private async Task ShowNoticeAsync(AgentNotice notice, CancellationToken ct)
    {
        string text = notice.Description is null ? notice.Title : $"{notice.Title}: {notice.Description}";
        LogLevel level = notice.Severity == NoticeSeverity.Info ? LogLevel.Information : LogLevel.Warning;
        if (this.logger.IsEnabled(level))
        {
            this.logger.Log(level, "Persona '{PersonaName}' Adapter notice: {Notice}", this.owner.PersonaName, text);
        }

        this.noticeSequence++;
        string rowId = string.Create(CultureInfo.InvariantCulture, $"huddle-notice-{this.noticeSequence}");
        await this.WriteToolActivityAsync(rowId, text, ToolActivityStatus.Notice, null, null, null, ct).ConfigureAwait(false);
    }

    /// <summary>Takes the active Turn off <see cref="activeTurn"/> and completes it with the text gathered so far.</summary>
    /// <param name="completed">The completion event carrying the stop reason.</param>
    private void CompleteActiveTurn(TurnCompleted completed)
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

    /// <summary>Counts the rise in context fill a usage update reports as spent tokens, and records its cost.</summary>
    /// <param name="usage">The usage update.</param>
    private void CountUsage(UsageUpdated usage)
    {
        // The context meter shows the latest gauge reading, every update, the resume baseline
        // included: that one is the restored context, which is what the meter should show.
        this.lastModel = usage.Model ?? this.lastModel;
        if (this.agentId is { } id)
        {
            this.contextFill?.Report(id, this.RoomId, usage.Used, usage.Size, this.lastModel);
        }

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

        // Display-only, and deliberately after and apart from the token logic: a cost
        // never changes what the token Budget counts. The Adapter sends it on only the
        // last update of a Turn, as a running total for this session.
        if (usage.Cost is { } cost)
        {
            this.owner.AddSpend(usage.SessionId, cost.Amount, cost.Currency);
        }
    }

    /// <summary>
    /// Fails the in-flight Turn, if any, because the event stream ended for good.
    /// </summary>
    /// <remarks>
    /// Whatever ended the reader loop - a fault, or ordinary shutdown mid-turn - the single consumer
    /// must not be left awaiting a TurnCompleted that can now never arrive: this channel is
    /// done, for good, in every case. Without this, a crashed adapter process left
    /// ProcessWorkItemAsync blocked on completion.Task forever, deafening the Agent in every
    /// Room with no signal of any kind.
    /// </remarks>
    private void FailStrandedTurn()
    {
        ActiveTurn? strandedTurn;
        lock (this.gate)
        {
            strandedTurn = this.activeTurn;
            this.activeTurn = null;
        }

        strandedTurn?.Completion.TrySetException(
            new InvalidOperationException(
                $"Persona '{this.owner.PersonaName}': the agent event stream ended before its in-flight turn completed."));
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
        string streamable;
        lock (this.gate)
        {
            turn = this.activeTurn;
            if (turn is null)
            {
                return;
            }

            turn.Text.Append(text);

            // The tail that could still become the silent token is held back; the rest streams as written.
            // Only the part not yet streamed is looked at, so a long reply is not copied again for every chunk.
            int streamed = turn.StreamedLength;
            string pending = turn.Text.ToString(streamed, turn.Text.Length - streamed);
            int safe = SilentReply.SafeLength(pending);
            streamable = pending[..safe];
            turn.StreamedLength = streamed + safe;
        }

        // DeltaWriteFailed is volatile, because ReleaseHeldTailAsync sets it from the run task as well;
        // the write below needs no lock, as only this reader loop streams while the Turn runs.
        if (turn.DeltaWriteFailed || streamable.Length == 0)
        {
            return;
        }

        try
        {
            await this.owner.WriteAsync(new MessageDelta(turn.RoomId, turn.MessageId, streamable, IsFinal: false), ct).ConfigureAwait(false);
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
    /// <param name="location">The call's first location, or <see langword="null"/> when this notification has none.</param>
    /// <param name="diff">The call's first change, or <see langword="null"/> when this notification has none.</param>
    /// <param name="memoryFile">The file a <c>memory</c> App Tool call names, used when the call has no location or change: the Adapter reports neither for an App Tool.</param>
    /// <param name="ct">Cancels the write.</param>
    private async Task WriteToolActivityAsync(
        string toolCallId,
        string? title,
        ToolActivityStatus status,
        ToolCallLocationInfo? location,
        ToolCallDiffInfo? diff,
        string? memoryFile,
        CancellationToken ct)
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

        string? path = location?.Path ?? diff?.Path ?? memoryFile;
        EditChange? edit = BuildEditChange(diff);
        if (edit is not null)
        {
            // The Adapter repeats the same diff on a later update of the same call (the second one
            // adds the line), so a preview already sent is not sent again: it is the largest thing on
            // this wire. The map is touched only from the single-threaded event reader.
            if (turn.LastSentEdits.TryGetValue(toolCallId, out EditChange? lastSent) && edit == lastSent)
            {
                edit = null;
            }
            else
            {
                turn.LastSentEdits[toolCallId] = edit;
            }
        }

        try
        {
            await this.owner.WriteAsync(new ToolActivity(turn.RoomId, turn.MessageId, toolCallId, title, status, path, location?.Line, edit), ct).ConfigureAwait(false);
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
    /// Builds the wire <see cref="EditChange"/> for a tool call's first change, each side clipped
    /// rune-safely to <see cref="ToolActivityLimits.MaxEditSideLength"/>, or <see langword="null"/>
    /// when the call has no change.
    /// </summary>
    /// <param name="diff">The call's first change, or <see langword="null"/>.</param>
    private static EditChange? BuildEditChange(ToolCallDiffInfo? diff)
    {
        if (diff is null)
        {
            return null;
        }

        string? oldText = ToolActivityLimits.Clip(diff.OldText, ToolActivityLimits.MaxEditSideLength, out bool oldClipped);
        string? newText = ToolActivityLimits.Clip(diff.NewText, ToolActivityLimits.MaxEditSideLength, out bool newClipped);
        return new EditChange(oldText, newText, oldClipped || newClipped, diff.OmittedChanges);
    }

    /// <summary>
    /// Keeps, for the Turn in flight, the last tool output that is a JSON object and whether any tool
    /// call failed, which is all an Adapter command's outcome line is built from. Only calls this Turn
    /// itself started count: a Stop or an idle timeout lets the consumer move on without waiting for
    /// the aborted Turn's tail, so a late update for an earlier Turn's call must not be read as this
    /// Turn's failure.
    /// </summary>
    /// <param name="toolCallId">The tool call the update belongs to.</param>
    /// <param name="isStart">Whether this is the call's first event, which enrols it in the Turn.</param>
    /// <param name="status">The tool call's status on this update.</param>
    /// <param name="rawOutputJson">The tool call's output, if this update carries one.</param>
    private void RecordToolCallResult(string toolCallId, bool isStart, ToolCallStatus status, string? rawOutputJson)
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

        if (isStart)
        {
            turn.ToolCallIds.Add(toolCallId);
        }
        else if (!turn.ToolCallIds.Contains(toolCallId))
        {
            return;
        }

        if (status == ToolCallStatus.Failed)
        {
            turn.ToolCallFailed = true;
        }

        if (rawOutputJson is not null && rawOutputJson.AsSpan().TrimStart().StartsWith('{'))
        {
            turn.LastOutputJson = rawOutputJson;
        }
    }

    /// <summary>
    /// Adds every rooted path found in <paramref name="rawInputJson"/> to the active Turn's
    /// <see cref="ActiveTurn.Touched"/> set, per FC §6.8 item 2 and FC D-3b - but only for
    /// <see cref="ToolKind.Edit"/>, <see cref="ToolKind.Delete"/> or <see cref="ToolKind.Move"/>: an
    /// <see cref="ToolKind.Execute"/> call is not parsed, because a shell command is not a path and
    /// guessing at one could claim another Agent's change as this Agent's own. A no-op when there
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

    /// <summary>
    /// Adds the file a <c>memory</c> App Tool call changes to the active Turn's <see cref="ActiveTurn.Touched"/> set, so File
    /// Changes counts a save through the tool as the Agent's own, like an <see cref="ToolKind.Edit"/> (Instructions spec §6.1), and
    /// returns the file any <c>memory</c> call names, which is the only argument the tool-call log would otherwise lose.
    /// The tool cannot record it itself: one tool server serves every Room Session of the Agent and never learns the Room.
    /// <c>claude-agent-acp</c> 0.75.1 has no case for an MCP tool in <c>toolInfoFromToolUse</c> (<c>dist/tools.js</c>), so it
    /// reports the call with <c>kind: "other"</c> and the tool's prefixed name, <c>mcp__team__memory</c>, as its <c>title</c>.
    /// The name is read from <c>_meta.claudeCode.toolName</c> (<see cref="ToolCallStarted.ToolName"/>) when the Adapter sends it,
    /// with the title as the fallback for an Adapter that sends no <c>_meta</c>. A live run saw it in the title:
    /// the tool-activity line, which shows the title, read <c>mcp__team__list_agents</c> (manual-tests run log, 2026-09-15). Only <c>write</c>,
    /// <c>append</c>, <c>replace</c> and <c>delete</c> count. A call the tool then refuses is recorded too, harmlessly:
    /// <c>FileChangeTracker.CommitAsync</c> stores a touched file's state as it is on disk, which a refusal left unchanged.
    /// Input that does not parse, or names no file the Agent may use, records nothing; the scan still lists the change.
    /// </summary>
    /// <param name="kind">The tool call's category, as ACP reported it.</param>
    /// <param name="title">The tool call's title, which carries the tool's name for an App Tool.</param>
    /// <param name="toolName">The Adapter's own tool name, when it sends one; it wins over <paramref name="title"/> (Spec §5).</param>
    /// <param name="rawInputJson">The tool call's raw input, if any.</param>
    /// <returns>
    /// The file the call names, for every command that names one (a <c>read</c> as well as a <c>write</c>), so the tool-call log
    /// and the tool-activity line say which memory file was used; <see langword="null"/> for any other call.
    /// </returns>
    private string? RecordMemoryToolPath(ToolKind kind, string? title, string? toolName, string? rawInputJson)
    {
        string? name = toolName ?? title;
        if (kind != ToolKind.Other
            || name is null
            || !MemoryTool.IsToolName(name)
            || this.fileChanges?.MemoryPaths is not { } memoryPaths
            || MemoryRequest.FromJson(rawInputJson) is not { } request
            || !memoryPaths.TryResolve(this.owner.PersonaName, request.Folder, request.File, out string? path))
        {
            return null;
        }

        if (request.Mutates)
        {
            lock (this.gate)
            {
                this.activeTurn?.Touched.Add(path);
            }
        }

        return path;
    }

    /// <summary>
    /// What precedes a prompt that would otherwise open with a slash. An Adapter reads a command only
    /// at the very start of a prompt, so this makes the text plain. A Prompt template can be edited to
    /// open with another Agent's text, and <c>PromptValidator</c> reports but never refuses, so the
    /// guard is applied to every prompt rather than trusted to the templates (Commands spec, D-9).
    /// </summary>
    internal const string CommandGuardMarker = "Message: ";

    /// <summary>Builds the prompt text delivered to the model for one Turn.</summary>
    /// <param name="item">The Turn's Room, sender, text, any catch-up context, and its <see cref="WorkItemKind"/>.</param>
    /// <param name="prompts">Resolves each <c>turn.*</c> prompt's current text — a configured override, or the <see cref="PromptCatalog"/> default.</param>
    /// <returns>
    /// For a <see cref="WorkItemKind.Command"/>, the bare command: <c>/compact</c>, or
    /// <c>/compact arguments</c>, and nothing else. For a <see cref="WorkItemKind.Greeting"/>, the
    /// <c>turn.greeting</c> prompt alone. Otherwise: with
    /// no catch-up context, the <c>turn.message</c> line alone; with catch-up context, a
    /// <c>turn.catchUpHeader</c> line, one <c>turn.catchUpLine</c> per missed message, a blank line,
    /// then the <c>turn.message</c> line.
    /// </returns>
    internal static string BuildPrompt(WorkItem item, IPromptSource prompts)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(prompts);

        if (item.Kind == WorkItemKind.Command)
        {
            // Exactly the command, one text block, no Room label: the Adapter recognises it only at
            // the start of the prompt, and its input is the rest of this line.
            AdapterCommandCall command = item.Command
                ?? throw new InvalidOperationException("A Command work item carries no command.");
            return command.Arguments.Length == 0 ? $"/{command.Name}" : $"/{command.Name} {command.Arguments}";
        }

        string prompt = RoomSession.BuildFramedPrompt(item, prompts);
        return prompt.AsSpan().TrimStart().StartsWith('/') ? CommandGuardMarker + prompt : prompt;
    }

    /// <summary>
    /// The Prompt blocks that follow the prompt's text on the wire (design §6.6): the block of each of
    /// the Turn's Library documents, in order. Only an ordinary Message Turn can carry one: a
    /// <see cref="WorkItemKind.Command"/> is the bare command and a <see cref="WorkItemKind.Greeting"/>
    /// has no triggering Message, and neither collects Library documents in the first place.
    /// </summary>
    /// <param name="item">The Turn's work item.</param>
    /// <returns>The blocks, possibly none.</returns>
    internal static IReadOnlyList<AgentPromptBlock> BuildPromptBlocks(WorkItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.Kind != WorkItemKind.Message || item.LibraryDocuments is not { } report)
        {
            return [];
        }

        return [.. report.Items.Select(document => document.Block).OfType<AgentPromptBlock>()];
    }

    /// <summary>
    /// What this Turn's prompt may carry beyond text, built from what the Adapter advertised at
    /// <c>initialize</c> and the profile's <c>PromptBlocks</c> kill switch. No host (every caller that
    /// predates resume) means nothing was advertised, so nothing is sent.
    /// </summary>
    /// <param name="collector">Supplies the per-Turn caps.</param>
    private PromptDelivery PromptDeliveryFor(LibraryDocumentCollector collector)
    {
        AgentPromptCapabilities capabilities = this.host?.PromptCapabilities ?? AgentPromptCapabilities.None;
        bool allowed = this.host?.Profile.PromptBlocks ?? false;
        return collector.DeliveryFor(
            this.readsFiles,
            images: capabilities.Image && allowed,
            embeddedText: capabilities.EmbeddedContext && allowed && !this.readsFiles);
    }

    /// <summary>Logs one Information line when an image that could have gone as a block did not: counts by reason, never the path's contents.</summary>
    private void LogWithheldImages(LibraryDocumentsReport report, string roomId)
    {
        List<string> reasons =
        [
            .. report.Items
                .Where(document => document.Withheld != PromptBlockWithheld.None)
                .GroupBy(document => document.Withheld)
                .OrderBy(group => group.Key)
                .Select(group => string.Create(CultureInfo.InvariantCulture, $"{group.Key}={group.Count()}")),
        ];

        if (reasons.Count > 0 && this.logger.IsEnabled(LogLevel.Information))
        {
            string summary = string.Join(", ", reasons);
            this.logger.LogInformation(
                "Persona '{PersonaName}' sent library file paths in room {RoomId} as path lines instead of blocks: {Reasons}.",
                this.owner.PersonaName,
                roomId,
                summary);
        }
    }

    private static string BuildFramedPrompt(WorkItem item, IPromptSource prompts)
    {
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
        RoomSession.AppendLibraryDocumentsBlock(builder, item.LibraryDocuments, prompts);

        // Spec section 9.3: after the Library documents block and before any catch-up or the Message.
        // Absent means unchanged, so every prompt without a header stays byte-identical.
        if (item.ReferencedItems is { } referencedItems)
        {
            builder.Append(referencedItems);
            builder.Append("\n\n");
        }

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

        RoomSession.AppendCatchUpAndMessage(builder, room, item, ownPostLines, prompts);

        return builder.ToString();
    }

    /// <summary>Writes the Catch-up block (missed Messages and own posts) and then the triggering Message.</summary>
    /// <param name="builder">The prompt being built.</param>
    /// <param name="room">The Room label.</param>
    /// <param name="item">The item being prompted.</param>
    /// <param name="ownPostLines">This Agent's own earlier posts, rendered as lines.</param>
    /// <param name="prompts">Where the prompt templates come from.</param>
    private static void AppendCatchUpAndMessage(StringBuilder builder, string room, WorkItem item, IReadOnlyList<string> ownPostLines, IPromptSource prompts)
    {
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

        if (transcript.OmittedOwn > 0)
        {
            builder.Append(prompts.Render(
                "turn.transcriptOmittedOwn",
                new Dictionary<string, string> { ["{{count}}"] = transcript.OmittedOwn.ToString(CultureInfo.InvariantCulture) }));
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

    /// <summary>
    /// Spec §6.14: with a non-empty <paramref name="report"/>, writes the Library documents block
    /// directly after File Changes — the header, one line per document (a too-large document's line
    /// replaces the ordinary one), the document's text fenced and truncation-noted when inlined, an
    /// "…and N more documents." line when the cap left some out, then a blank line. Writes nothing for
    /// a <see langword="null"/> or empty report: "absent means unchanged", as for File Changes.
    /// </summary>
    private static void AppendLibraryDocumentsBlock(StringBuilder builder, LibraryDocumentsReport? report, IPromptSource prompts)
    {
        if (report is null || report.IsEmpty)
        {
            return;
        }

        builder.Append(prompts.Render("turn.libraryDocsHeader", new Dictionary<string, string>()));
        builder.Append('\n');

        foreach (var item in report.Items)
        {
            RoomSession.AppendLibraryDocument(builder, report, item, prompts);
        }

        if (report.NotListed > 0)
        {
            builder.Append(prompts.Render(
                "turn.libraryDocsMore",
                new Dictionary<string, string> { ["{{count}}"] = report.NotListed.ToString(CultureInfo.InvariantCulture) }));
            builder.Append('\n');
        }

        builder.Append('\n');
    }

    /// <summary>Writes the line for a Library document that travels as a block, or an image the model cannot be given, plus its truncation note.</summary>
    /// <param name="builder">The prompt being built.</param>
    /// <param name="report">The report the document belongs to, for its inline-size cap.</param>
    /// <param name="item">The document to write.</param>
    /// <param name="prompts">Where the prompt templates come from.</param>
    private static void AppendNonPathLibraryDocument(StringBuilder builder, LibraryDocumentsReport report, LibraryDocumentItem item, IPromptSource prompts)
    {
        var deliveryKey = item.Delivery == LibraryDocumentDelivery.Block ? "turn.libraryDocIncluded" : "turn.libraryImageUnavailable";
        builder.Append(prompts.Render(
            deliveryKey,
            new Dictionary<string, string>
            {
                ["{{path}}"] = item.FullPath,
                ["{{location}}"] = item.Location,
                ["{{size}}"] = item.Size,
            }));
        builder.Append('\n');

        if (item.Block is AgentTextResourceBlock && item.Truncated)
        {
            builder.Append(prompts.Render(
                "turn.libraryDocTruncated",
                new Dictionary<string, string>
                {
                    ["{{max}}"] = report.MaxInlineBytes.ToString(CultureInfo.InvariantCulture),
                    ["{{size}}"] = item.Size,
                }));
            builder.Append('\n');
        }
    }

    /// <summary>Writes one Library document's line, and its inlined text and truncation note when it has them.</summary>
    /// <param name="builder">The prompt being built.</param>
    /// <param name="report">The report the document belongs to, for its inline-size cap.</param>
    /// <param name="item">The document to write.</param>
    /// <param name="prompts">Where the prompt templates come from.</param>
    private static void AppendLibraryDocument(StringBuilder builder, LibraryDocumentsReport report, LibraryDocumentItem item, IPromptSource prompts)
    {
        // A document that travels with the message, or an image the model cannot be given, is worded
        // by that fact before anything else: an image over the editable limit is "too large to read"
        // as a path, and still arrives as a block (design §6.5).
        if (item.Delivery != LibraryDocumentDelivery.Path)
        {
            RoomSession.AppendNonPathLibraryDocument(builder, report, item, prompts);
            return;
        }

        if (item.TooLarge)
        {
            var sizeKb = (item.Length + 1023) / 1024;
            builder.Append(prompts.Render(
                "turn.libraryDocTooLarge",
                new Dictionary<string, string>
                {
                    ["{{label}}"] = item.Location,
                    ["{{path}}"] = item.FullPath,
                    ["{{sizeKb}}"] = sizeKb.ToString(CultureInfo.InvariantCulture),
                }));
            builder.Append('\n');
            return;
        }

        builder.Append(prompts.Render(
            "turn.libraryDoc",
            new Dictionary<string, string>
            {
                ["{{path}}"] = item.FullPath,
                ["{{location}}"] = item.Location,
                ["{{size}}"] = item.Size,
            }));
        builder.Append('\n');

        if (item.Text is not null)
        {
            var fence = RoomSession.ChooseFence(item.Text);
            builder.Append(prompts.Render(
                "turn.libraryDocInline",
                new Dictionary<string, string> { ["{{fence}}"] = fence, ["{{text}}"] = item.Text }));
            builder.Append('\n');

            if (item.Truncated)
            {
                builder.Append(prompts.Render(
                    "turn.libraryDocTruncated",
                    new Dictionary<string, string>
                    {
                        ["{{max}}"] = report.MaxInlineBytes.ToString(CultureInfo.InvariantCulture),
                        ["{{size}}"] = item.Size,
                    }));
                builder.Append('\n');
            }
        }
    }

    /// <summary>
    /// Corrections-B5 item 32: the fence for an inlined document's text is the longest run of
    /// backticks already in that text, plus one, never shorter than three — so the fence can never be
    /// closed early by a backtick run the text itself contains.
    /// </summary>
    /// <param name="text">The document's text about to be fenced.</param>
    /// <returns>A run of backticks safe to wrap <paramref name="text"/> in.</returns>
    private static string ChooseFence(string text)
    {
        var longestRun = 0;
        var currentRun = 0;
        foreach (var c in text)
        {
            if (c == '`')
            {
                currentRun++;
                longestRun = Math.Max(longestRun, currentRun);
            }
            else
            {
                currentRun = 0;
            }
        }

        return new string('`', Math.Max(3, longestRun + 1));
    }

    /// <summary>Builds the bracketed Room label that opens every line of a prompt.</summary>
    /// <param name="item">The work item whose Room is being labelled.</param>
    /// <param name="prompts">Resolves the <c>turn.roomLabel</c> prompt's current text.</param>
    /// <returns>The label, carrying both the Room's name and its id.</returns>
    private static string RoomLabel(WorkItem item, IPromptSource prompts) =>
        prompts.Render(
            "turn.roomLabel",
            new Dictionary<string, string>
            {
                ["{{roomName}}"] = item.RoomName,
                ["{{roomId}}"] = item.RoomId,
                ["{{teamSuffix}}"] = item.Team is null ? string.Empty : $", Team: {item.Team}",
            });

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
            await task.ConfigureAwait(false);
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
        CancellationTokenSource Cancellation,
        TimeProvider Time)
    {
        // lastActivityTimestamp, stopRequested and timedOut are each written by one thread and read by
        // another - lastActivityTimestamp by the event-reader loop, read by the idle-timeout watchdog;
        // stopRequested by StopAsync, timedOut by the watchdog, both read by the consumer's catch
        // clauses - so every access goes through Interlocked or Volatile, never a plain read or write.
        // The stamp is TimeProvider.GetTimestamp (monotonic, unaffected by a wall-clock change) from the
        // session's own injected TimeProvider, so a test drives the idle clock and the watchdog's delay
        // from one FakeTimeProvider rather than waiting out real seconds.
        private long lastActivityTimestamp = Time.GetTimestamp();
        private bool stopRequested;
        private bool timedOut;
        private bool sawActivity;

        // The questions the agent has open on this Turn (E-3). A COUNT, not a flag: two overlapping
        // questions must keep the watchdog paused until both are answered. Per Turn, never per session,
        // so one that is somehow never released ends with its Turn instead of pausing the next one.
        // Written with Interlocked by whoever opens or answers a question, read by the watchdog.
        private int openRequests;

        // Cancelled and disposed exactly once, by EndUserInput. Linked to Cancellation, so a Stop, the
        // watchdog's timeout and a shutdown all cancel every open question without a line of their own.
        // BeginUserInput reads the token under userInputGate while the source is still live, so a lease
        // opened a moment before the Turn ends can read its token afterwards without touching a
        // disposed source.
        private readonly CancellationTokenSource userInputCts = CancellationTokenSource.CreateLinkedTokenSource(Cancellation.Token);
        private readonly Lock userInputGate = new();
        private bool userInputEnded;

        /// <summary>
        /// Whether a <see cref="MessageDelta"/> write has already failed for this Turn. Set once a
        /// write throws, so a dead pipe is logged at most once per Turn rather than once per chunk.
        /// Written by the event-reader loop and, at the Turn's end, by the run task that releases the held tail,
        /// so every access goes through <see cref="Volatile"/> and one thread's failure is seen by the other.
        /// </summary>
        public bool DeltaWriteFailed
        {
            get => Volatile.Read(ref this.deltaWriteFailed);
            set => Volatile.Write(ref this.deltaWriteFailed, value);
        }

        private bool deltaWriteFailed;

        /// <summary>
        /// How many characters of <see cref="Text"/> have been sent to the page. The tail that could still be the
        /// silent token is not, so this runs behind the text until the Turn ends. Read and written under the session's gate.
        /// </summary>
        public int StreamedLength { get; set; }

        /// <summary>
        /// The last tool output of this Turn that was a JSON object, which is where an Adapter command
        /// reports its figures. Written by the single-threaded event-reader loop and read by the
        /// consumer only after <see cref="Completion"/> has been awaited, so it needs no lock.
        /// </summary>
        public string? LastOutputJson { get; set; }

        /// <summary>Whether any tool call of this Turn reached <see cref="ToolCallStatus.Failed"/>. Same threading as <see cref="LastOutputJson"/>.</summary>
        public bool ToolCallFailed { get; set; }

        /// <summary>The ids of the tool calls this Turn started, so an update belonging to an earlier Turn is ignored. Same threading as <see cref="LastOutputJson"/>.</summary>
        public HashSet<string> ToolCallIds { get; } = new(StringComparer.Ordinal);

        /// <summary>
        /// The last <see cref="EditChange"/> written for each tool call of this Turn, keyed by tool
        /// call id, so an unchanged preview is not sent twice. Touched only from the single-threaded
        /// event-reader loop, so it needs no lock, and it dies with the Turn.
        /// </summary>
        public Dictionary<string, EditChange> LastSentEdits { get; } = new(StringComparer.Ordinal);

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

        /// <summary>
        /// How long it has been, as of now, since the last sign of life on this Turn - and zero while the
        /// agent has a question open, because a Turn waiting on the Human is not an Adapter gone quiet.
        /// The watchdog's loop then computes <c>remaining = idleBound - IdleFor</c> as a full bound and
        /// sleeps again rather than firing; answering restarts the clock from the answer, see
        /// <see cref="BeginUserInput"/>.
        /// </summary>
        public TimeSpan IdleFor =>
            Volatile.Read(ref this.openRequests) > 0
                ? TimeSpan.Zero
                : this.Time.GetElapsedTime(Interlocked.Read(ref this.lastActivityTimestamp));

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
            Interlocked.Exchange(ref this.lastActivityTimestamp, this.Time.GetTimestamp());
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

        /// <summary>
        /// Opens one question on this Turn: counts it, which pauses the idle watchdog through
        /// <see cref="IdleFor"/>, and counts as activity. Nothing is opened once <see cref="EndUserInput"/>
        /// has run.
        /// </summary>
        /// <returns>The lease whose disposal ends this question's pause, or <see langword="null"/> when this Turn has already ended.</returns>
        public UserInputLease? BeginUserInput()
        {
            CancellationToken token;
            lock (this.userInputGate)
            {
                if (this.userInputEnded)
                {
                    return null;
                }

                token = this.userInputCts.Token;
                Interlocked.Increment(ref this.openRequests);
            }

            this.MarkActivity();
            return new UserInputLease(this.RoomId, this.ReleaseUserInput, token);
        }

        /// <summary>
        /// Cancels every question still open on this Turn and disposes their shared source. The Turn is
        /// over, so nothing can be answered any more. Safe to call more than once.
        /// </summary>
        public void EndUserInput()
        {
            lock (this.userInputGate)
            {
                if (this.userInputEnded)
                {
                    return;
                }

                this.userInputEnded = true;
            }

            this.userInputCts.Cancel();
            this.userInputCts.Dispose();
        }

        /// <summary>
        /// Ends one question's pause. The clock is restarted BEFORE the count drops, so the watchdog can
        /// never see an unpaused Turn with the stale timestamp the question's whole wait left behind, and
        /// fire the instant the Human answers.
        /// </summary>
        private void ReleaseUserInput()
        {
            this.MarkActivity();
            Interlocked.Decrement(ref this.openRequests);
        }
    }

    /// <summary>How a Turn ended: the text it produced, and why it stopped.</summary>
    private sealed record TurnOutcome(string Text, StopReason Reason);
}

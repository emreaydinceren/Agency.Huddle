using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp.Sessions.TurnHooks;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Memory;
using Agency.Huddle.App.Prompts;
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
/// <see cref="TurnPromptFramer.BuildPrompt"/> moved here from <see cref="PersonaRunner"/> with their comments and
/// both traps intact (<c>docs/engineering/rules.md</c>, "A Turn ends four ways").
/// </summary>
internal sealed class RoomSession : IAsyncDisposable, IElicitationScope, IConnectorTurnLatch
{
    // The bounds on Acp:UserInputTimeoutSeconds. The floor keeps a question from expiring before a
    // Human could read it and, since zero and below are raised to it, means a question is never
    // unbounded. The ceiling keeps CancellationTokenSource from throwing on a typo.
    private const int MinUserInputTimeoutSeconds = 30;
    private const int MaxUserInputTimeoutSeconds = 86_400;

    /// <summary>The prefix of every Huddle App Tool's name as <c>claude-agent-acp</c> reports it.</summary>
    private const string TeamToolPrefix = "mcp__team__";

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
    private readonly MemoryPaths? memoryPaths;
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

    // D24: the first-Turn Transcript catch-up and the fresh-start note it owes; consumer-only state.
    private readonly TranscriptCatchUpStep transcriptCatchUp;

    // Spec §6.14, §9.1: adds the Library documents and the Referenced items to a Message Turn. Null
    // means neither is on (PersonaSupervisor is the only place those gates are applied) or this
    // caller predates it.
    private readonly TurnEnricherPipeline? enrichers;

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

    // The triggering Message id of the item admitted and not yet released, so a re-sent delivery of it is ignored; guarded by gate.
    private string? runningTriggerId;

    // The triggering Message ids of the last Turns that ended, oldest first, so the queue's re-send of a Message that already ran
    // (it arrives beside the live re-delivery of the same Message) is not run twice; guarded by gate.
    private readonly Queue<string> endedTriggerIds = new();
    private readonly HashSet<string> endedTriggerSet = new(StringComparer.Ordinal);

    // Numbers each Adapter notice so its Turn-activity row has an id no tool call can carry. Touched
    // only from the single-threaded event reader.
    private long noticeSequence;
    private Task? pendingStopCancel;
    private bool running;
    private long lastUsed;

    private readonly TurnHookPipeline? hooks;

    /// <summary>
    /// The per-hook state the Turn hooks keep, keyed by hook name. Consumer-only: both uses
    /// (the Before point in <c>ProcessWorkItemAsync</c> and the After point in <c>FinishTurnAsync</c>)
    /// run on the consumer.
    /// The opening of a session also writes it, under the gate (<c>ReplaceHookState</c>), and the Opening hooks read it
    /// without it: the eager open at start-up is awaited to its end before the read loop starts
    /// (<c>PersonaRunner</c> awaits <c>OpenAtStartAsync</c> first), so no item exists to run a Turn beside it, and a lazy open
    /// runs on the consumer inside the admitted slot.
    /// </summary>
    private readonly Dictionary<string, string?> hookState = new(StringComparer.Ordinal);

    /// <summary>
    /// The Stop token of the Turn hooks' Before points while they run, or <see langword="null"/> otherwise. Published and
    /// cleared under <c>gate</c>, so the Stop path never cancels a disposed source (O-12); the consumer disposes it.
    /// </summary>
    private CancellationTokenSource? beforeTurnStop;

    /// <summary>The scheduler sequence of the item the Stop source above belongs to; read only with it, under <c>gate</c>.</summary>
    private long beforeTurnStopSequence;

    /// <summary>The Room whose item the Stop source above belongs to; read only with it, under <c>gate</c>.</summary>
    private string? beforeTurnStopRoomId;

    /// <summary>The latest non-null model a usage update of this session named; kept across updates that name none.</summary>
    private string? lastModel;

    // Set alongside the first-Turn flag TranscriptCatchUpStep.MarkOpened sets and consumed by RunEventReaderAsync's own
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
    /// <param name="enrichers">Adds the Library documents (Spec §6.14) and the Referenced items header (spec section 9.1) to a Message Turn. <see langword="null"/> adds neither, like every caller that predates it.</param>
    /// <param name="elicitationBridge">Shows the Human the questions the agent asks through this session (E-3). <see langword="null"/> answers every one as cancelled, like every caller that predates it.</param>
    /// <param name="contextFill">Records how full this session's context window is, for the Members panel's context meter. <see langword="null"/> disables it, like every caller that predates it.</param>
    /// <param name="sessionSettings">The resolved Session settings the first Turn's Transcript catch-up reads (the Message cap and the token budget). <see langword="null"/> uses the <see cref="AcpOptions"/> values, like every caller that predates it.</param>
    /// <param name="lastCatchUp">Receives the whole prompt text of the first ordinary Turn after a fresh start. <see langword="null"/> writes no file, like every caller that predates it.</param>
    /// <param name="hooks">The Turn hooks run around each Message Turn. <see langword="null"/> runs none, like every caller that predates it.</param>
    /// <param name="memoryPaths">Resolves an Agent's own <c>memory</c> writes to paths for <c>Touched</c>. <see langword="null"/> records none, like every caller that predates it.</param>
    public RoomSession(
        string? roomId,
        Func<CancellationToken, Task<IAgentSession>> open,
        IRoomSessionOwner owner,
        ITurnScheduler scheduler,
        IPromptSource prompts,
        AcpOptions options,
        ILogger logger,
        CancellationToken runToken,
        TimeProvider? time = null,
        Persona? persona = null,
        RoomSessionStore? roomSessions = null,
        IPersonaHost? host = null,
        OwnPosts? ownPosts = null,
        string? agentId = null,
        TurnActivity? turnActivity = null,
        TurnEnricherPipeline? enrichers = null,
        IElicitationBridge? elicitationBridge = null,
        ContextFill? contextFill = null,
        ISessionSettings? sessionSettings = null,
        LastCatchUpStore? lastCatchUp = null,
        TurnHookPipeline? hooks = null,
        MemoryPaths? memoryPaths = null)
    {
        ArgumentNullException.ThrowIfNull(open);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(prompts);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        this.RoomId = roomId;
        this.open = open;
        this.owner = owner;
        this.scheduler = scheduler;
        this.prompts = prompts;
        this.options = options;
        this.memoryPaths = memoryPaths;
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
        this.transcriptCatchUp = new TranscriptCatchUpStep(owner, options, sessionSettings, lastCatchUp, prompts, logger, roomId, this.IsOwn);
        this.enrichers = enrichers;
        this.hooks = hooks;
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

    /// <summary>Whether a Turn for this Message is queued or running here, so a re-sent delivery of it can be ignored before anything is drained for it.</summary>
    /// <param name="messageId">The Message id.</param>
    /// <param name="resent"><see langword="true"/> for a delivery the server sends again: a Message whose Turn already ended counts as present too.</param>
    /// <returns><see langword="true"/> when it is already queued or running.</returns>
    internal bool HasMessage(string messageId, bool resent = false)
    {
        lock (this.gate)
        {
            return this.HasMessageLocked(messageId, resent);
        }
    }

    // Caller holds gate.
    private bool HasMessageLocked(string messageId, bool resent) =>
        string.Equals(messageId, this.runningTriggerId, StringComparison.Ordinal)
        || (resent && this.endedTriggerSet.Contains(messageId))
        || this.queue.Any(queued => string.Equals(queued.Item.TriggerMessageId, messageId, StringComparison.Ordinal));

    // Caller holds gate.
    private void RememberEndedTrigger(string? triggerId)
    {
        if (triggerId is null || !this.endedTriggerSet.Add(triggerId))
        {
            return;
        }

        this.endedTriggerIds.Enqueue(triggerId);
        if (this.endedTriggerIds.Count > 64)
        {
            _ = this.endedTriggerSet.Remove(this.endedTriggerIds.Dequeue());
        }
    }

    /// <summary>The trigger ids of the Turns queued behind the running one in a Room, so a late Turn's later block can leave them out (Turn Queue spec 5.8).</summary>
    /// <param name="roomId">The Room.</param>
    /// <returns>The ids, possibly none.</returns>
    private List<string> QueuedTriggerIds(string roomId)
    {
        lock (this.gate)
        {
            return [.. this.queue
                .Where(queued => string.Equals(queued.Item.RoomId, roomId, StringComparison.Ordinal))
                .Select(queued => queued.Item.TriggerMessageId)
                .OfType<string>()];
        }
    }

    /// <summary>Queues a Turn built by the read loop, with its Catch-up already taken.</summary>
    /// <param name="item">The Turn to run, with its sequence number.</param>
    internal void Enqueue(QueuedWork item)
    {
        ArgumentNullException.ThrowIfNull(item);

        lock (this.gate)
        {
            // A Message already queued or running here is not run twice: a re-sent delivery of it is ignored, with no ack, because
            // the Turn that is queued or running will ack it (Turn Queue spec, section 5.8).
            if (item.Item.TriggerMessageId is { } trigger && this.HasMessageLocked(trigger, item.Item.Redelivery is not null))
            {
                // Backstop: EnqueueReply checks first, so nothing was drained for this one; a race between check and here is not lost context in practice.
                return;
            }

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
        ElicitationResult result;
        try
        {
            result = await bridge.RequestAsync(new ElicitationContext(lease.RoomId, askerAgentId), request, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            result = new ElicitationCancelled();
        }

        // The real bridge resolves a cancelled request as ElicitationCancelled and does not throw, so the bound running out is read
        // here, after the await, not in a catch. A Stop, the Turn ending, a shutdown or the caller giving up cancelled the question
        // some other way and stays silent; only the bound running out is worth a line, because the Human sees a question vanish.
        if (result is ElicitationCancelled && timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested && !lease.Token.IsCancellationRequested)
        {
            this.MarkApprovalTimedOut();
#pragma warning disable HAA0601 // Boxing a value-type log argument is the cost of ILogger params object[]; accepted together with HAA0101.
            this.logger.LogWarning(
                "Persona '{PersonaName}' dropped a question in room {RoomId}: no answer after {TimeoutSeconds} seconds.",
                this.owner.PersonaName,
                lease.RoomId,
                bound.TotalSeconds);
#pragma warning restore HAA0601
        }

        return result;
    }

    /// <summary>
    /// Whether a question on the Turn now in flight ran out of time with no answer. The connector approval prompt reads it and
    /// refuses every further connector call on that Turn without asking; the next Turn starts clear because the mark lives on the
    /// Turn, not on this session.
    /// </summary>
    public bool ApprovalTimedOut
    {
        get
        {
            lock (this.gate)
            {
                return this.activeTurn?.ApprovalTimedOut ?? false;
            }
        }
    }

    /// <summary>Gets the Room of the Turn now in flight, or <see langword="null"/> when there is none.</summary>
    public string? ActiveRoomId
    {
        get
        {
            lock (this.gate)
            {
                return this.activeTurn?.RoomId;
            }
        }
    }

    /// <summary>Marks the Turn now in flight as having had a question go unanswered.</summary>
    private void MarkApprovalTimedOut()
    {
        lock (this.gate)
        {
            this.activeTurn?.MarkApprovalTimedOut();
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

            this.CancelBeforeWait(roomId);

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
            // Called under the gate, synchronous to its first await: yield so Opening never runs under it.
            await Task.Yield();
            var (opened, resumed, resumedAfterId) = await this.OpenOrResumeAsync(cancellationToken).ConfigureAwait(false);

            // Fresh or resumed alike: an unbound session's question is answered as cancelled (E-3).
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
            this.transcriptCatchUp.MarkOpened(resumed, resumedAfterId);
            this.usageBaselinePending = resumed;

            // A fresh open starts an empty context, so an earlier reading must not show; a resume keeps it.
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
                    this.ReplaceHookState(entry.HookState);
                    try
                    {
                        await this.RunOpeningHooksAsync(SessionOpenReason.Resumed, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // Nothing owns the resumed session yet: this.session is set only after this method returns.
                        await resumedSession.DisposeAsync().ConfigureAwait(false);
                        throw;
                    }

                    return (resumedSession, true, entry.LastMessageId);
                }
            }
        }

        // Opening runs before the open: AgentsMdTurnHook captures its hashes then (Spec 8.5).
        this.ReplaceHookState(null);
        await this.RunOpeningHooksAsync(SessionOpenReason.Fresh, cancellationToken).ConfigureAwait(false);
        var opened = await this.open(cancellationToken).ConfigureAwait(false);
        return (opened, false, null);
    }

    /// <summary>
    /// Swaps the hook state for a new session's: cleared, then filled from <paramref name="stored"/>. The swap is under the gate and
    /// never awaits. The consumer touches <c>hookState</c> only inside a Turn. No open can run beside a Turn's Before or After:
    /// the eager open at start-up (<c>RoomSessionPool.OpenAtStartAsync</c>) is awaited to its end by <c>PersonaRunner</c>
    /// before the read loop starts, so nothing is queued until it is done, and every later open is the lazy one, which the
    /// consumer itself runs (<c>TryLazyOpenAsync</c>) before the Turn.
    /// </summary>
    /// <param name="stored">The entry's stored state, or <see langword="null"/> for a fresh session.</param>
    private void ReplaceHookState(IReadOnlyDictionary<string, string?>? stored)
    {
        lock (this.gate)
        {
            this.hookState.Clear();
            if (stored is not null)
            {
                foreach (var pair in stored)
                {
                    this.hookState[pair.Key] = pair.Value;
                }
            }
        }
    }

    private async Task RunOpeningHooksAsync(SessionOpenReason reason, CancellationToken cancellationToken)
    {
        if (this.hooks is not null)
        {
            await this.hooks.OnSessionOpeningAsync(reason, this.hookState, cancellationToken).ConfigureAwait(false);
        }
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
            await this.SendAckAsync(head.Item, QueueAckKind.Finished, null, QueueOutcome.Dropped, ct).ConfigureAwait(false);
            return;
        }

        // D22 correction 11: checked here, straight after admission and BEFORE opening -
        // an item drained for a spent Budget must never pay for opening a session it will
        // not prompt.
        if (this.owner.TokenBudgetSpent)
        {
            this.owner.ReportTokenBudgetSpent();
            await this.SendAckAsync(head.Item, QueueAckKind.Finished, null, QueueOutcome.Dropped, ct).ConfigureAwait(false);
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

        await this.ProcessWorkItemAsync(this.FoldBehind(head), head.Sequence, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Folded Turns (Turn Queue spec, section 6): when the admitted head is Foldable, takes every item directly behind it in the same Room
    /// that is also an ordinary Foldable Message above this Room's Stop mark, up to the first that is not, and returns one item that answers
    /// them all. Runs after the lazy open, so a failed open drops the head alone.
    /// </summary>
    /// <param name="head">The admitted head, already dequeued.</param>
    /// <returns>The head's own item when nothing folds in, otherwise an item that lists every folded Message, oldest first.</returns>
    private WorkItem FoldBehind(QueuedWork head)
    {
        WorkItem first = head.Item;
        if (!first.Foldable || first.Kind != WorkItemKind.Message || first.TriggerMessageId is null)
        {
            return first;
        }

        List<WorkItem> taken = [first];
        lock (this.gate)
        {
            while (this.queue.Count > 0 && this.CanFoldIn(this.queue.Peek(), first.RoomId) && this.itemAvailable.Wait(0, this.runToken))
            {
                taken.Add(this.queue.Dequeue().Item);
            }

            foreach (WorkItem extra in taken.Skip(1))
            {
                this.RememberEndedTrigger(extra.TriggerMessageId);
            }
        }

        if (taken.Count == 1)
        {
            return first;
        }

        List<CaughtUpMessage> missed = [.. taken.SelectMany(item => item.MissedMessages)];
        List<string> postLines = [.. taken.SelectMany(item => item.OwnPostLines ?? [])];
        return first with
        {
            MissedMessages = missed,
            OwnPostLines = postLines.Count > 0 ? postLines : null,
            Folded = [.. taken.Select(item => new FoldedMessage(item.TriggerMessageId ?? string.Empty, item.SenderName, item.Text, null))],
        };
    }

    // Caller holds gate. Wait(0) is taken last: it consumes the count that Enqueue released for the item, and only an item that does fold may consume it.
    private bool CanFoldIn(QueuedWork next, string roomId) =>
        next.Item is { Foldable: true, Kind: WorkItemKind.Message, TriggerMessageId: not null }
        && string.Equals(next.Item.RoomId, roomId, StringComparison.Ordinal)
        && !this.IsStoppedAtOrBefore(roomId, next.Sequence);

    /// <summary>The ids the server's rows for a Turn carry: every folded Message, oldest first, or the trigger alone.</summary>
    /// <param name="item">The Turn's item.</param>
    /// <param name="triggerId">The item's trigger id.</param>
    /// <returns>The ids an ack or a reply names.</returns>
    private static string[] TriggerIds(WorkItem item, string triggerId) =>
        item.Folded is { Count: > 0 } folded ? [.. folded.Select(message => message.Id)] : [triggerId];

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
            this.runningTriggerId = this.queue.Dequeue().Item.TriggerMessageId;
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
            await this.SendAckAsync(head.Item, QueueAckKind.Finished, null, QueueOutcome.Failed, ct).ConfigureAwait(false);
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
            this.RememberEndedTrigger(this.runningTriggerId);
            this.runningTriggerId = null;
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

    /// <summary>
    /// Ends the wait of the Turn hooks' Before points when the item waiting in them is in <paramref name="roomId"/> and the Room's mark
    /// covers its sequence: an item admitted after the mark (a later sequence) keeps its wait, as the publish check keeps its Turn.
    /// Called under <see cref="gate"/>, so the source cannot be disposed meanwhile (the consumer clears the field under this same gate
    /// first). <c>CancelAsync</c>, not <c>Cancel</c>: Cancel runs the hook's continuations inline, on this caller's thread with the
    /// gate held. CancelAsync marks the token cancelled now, keeping the order with the mark, and runs the callbacks elsewhere. Not
    /// awaited: nothing may be awaited under the lock.
    /// </summary>
    /// <param name="roomId">The Room the Human stopped.</param>
    private void CancelBeforeWait(string roomId)
    {
        if (this.beforeTurnStop is { } waiting
            && string.Equals(this.beforeTurnStopRoomId, roomId, StringComparison.Ordinal)
            && this.IsStoppedAtOrBefore(roomId, this.beforeTurnStopSequence))
        {
            _ = waiting.CancelAsync();
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
        TurnRun? run;
        try
        {
            run = await this.PrepareAndPublishAsync(item, sequence, ct).ConfigureAwait(false);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // Nothing was prompted: the server's row closes as failed, then the exception goes on to the consumer's own handling.
            await this.SendAckAsync(item, QueueAckKind.Finished, null, QueueOutcome.Failed, ct).ConfigureAwait(false);
            throw;
        }

        if (run is null)
        {
            await this.SendAckAsync(item, QueueAckKind.Finished, null, QueueOutcome.Dropped, ct).ConfigureAwait(false);
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
    /// Runs the Before hooks and publishes the Turn. While a Message Turn waits in the hooks, an empty Draft under the
    /// Turn's own message id shows the Agent working with the Draft's Stop control (O-12); the published Turn reuses
    /// that id, so the Room never shows two Drafts, and a Turn that is dropped instead retracts it.
    /// </summary>
    /// <param name="item">The item being prepared.</param>
    /// <param name="sequence">The item's sequence, compared with the Room's Stop mark.</param>
    /// <param name="ct">The run's own cancellation token.</param>
    /// <returns>The published Turn, or <see langword="null"/> when the item was dropped.</returns>
    private async Task<TurnRun?> PrepareAndPublishAsync(WorkItem item, long sequence, CancellationToken ct)
    {
        string messageId = Guid.CreateVersion7().ToString("N");
        bool announced = this.hooks is { HookNames.Count: > 0 } && item.Kind == WorkItemKind.Message;
        if (announced)
        {
            await this.WriteDeltaAsync(item.RoomId, messageId, isFinal: false, ct).ConfigureAwait(false);
        }

        TurnRun? run = null;
        try
        {
            (item, TurnHookBeforeResult? hookResult) = await this.RunBeforeHooksAsync(item, sequence, ct).ConfigureAwait(false);

            // A Stop that ended the Before wait (or landed in it) drops the item now, as TryPublishTurn would later: the
            // catch-up below clears firstTurnPending, and the next Turn still needs it (E-7).
            if (this.IsStoppedAtOrBefore(item.RoomId, sequence))
            {
                return null;
            }

            item = await this.transcriptCatchUp.AddAsync(item, this.FreshStartNotePending, messageId, this.QueuedTriggerIds(item.RoomId), ct).ConfigureAwait(false);
            if (this.enrichers is not null)
            {
                item = await this.enrichers.EnrichAsync(item, this.owner.PersonaName, this.host, ct).ConfigureAwait(false);
            }

            // The file is written here, before the watchdog is armed (traps.md TRAP 1): BuildPrompt is pure.
            this.transcriptCatchUp.WriteLastCatchUp(item);
            run = this.TryPublishTurn(item, hookResult, sequence, messageId, ct);
            return run;
        }
        finally
        {
            if (announced && run is null)
            {
                await this.WriteDeltaAsync(item.RoomId, messageId, isFinal: true, ct).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Runs the Turn hooks' Before points for a Message Turn and attaches their blocks to the item.
    /// Runs before the Turn, the cancellation source and the watchdog exist, so a slow hook is never
    /// counted as Adapter silence.
    /// </summary>
    /// <param name="item">The item being prepared.</param>
    /// <param name="sequence">The item's sequence, compared with the Room's Stop mark.</param>
    /// <param name="ct">The run's own cancellation token.</param>
    /// <returns>The item with its hook blocks set, and the result, or <see langword="null"/> when no hook ran.</returns>
    private async Task<(WorkItem Item, TurnHookBeforeResult? Result)> RunBeforeHooksAsync(WorkItem item, long sequence, CancellationToken ct)
    {
        if (this.hooks is null || item.Kind != WorkItemKind.Message)
        {
            return (item, null);
        }

        using CancellationTokenSource stop = new();
        lock (this.gate)
        {
            this.beforeTurnStop = stop;
            this.beforeTurnStopRoomId = item.RoomId;
            this.beforeTurnStopSequence = sequence;
            if (this.IsStoppedAtOrBefore(item.RoomId, sequence))
            {
                _ = stop.CancelAsync(); // Not awaited under the gate; the token is cancelled at once and the hooks have not started.
            }
        }

        try
        {
            TurnHookBeforeResult result = await this.hooks
                .BeforeTurnAsync(item.RoomId, this.hookState, ct, stop.Token)
                .ConfigureAwait(false);
            return (result.Blocks.Count > 0 ? item with { HookBlocks = result.Blocks } : item, result);
        }
        finally
        {
            lock (this.gate)
            {
                this.beforeTurnStop = null;
                this.beforeTurnStopRoomId = null;
            }
        }
    }

    /// <summary>Sends the fresh-start note the finished Turn owed, if any, and clears the owed flag. A failure is logged and never fails the Turn.</summary>
    /// <param name="ct">The run's own cancellation token.</param>
    /// <returns>A task that completes when the note has been handed to the owner.</returns>
    private async Task SendFreshStartNoteAsync(CancellationToken ct)
    {
        FreshStartNote? note = this.transcriptCatchUp.TakeNote();
        if (note is null)
        {
            return;
        }

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
    /// <param name="hookResult">What the Turn hooks' Before points produced, or <see langword="null"/> when none ran.</param>
    /// <param name="sequence">The item's scheduler sequence, compared against the Room's Stop mark.</param>
    /// <param name="messageId">The Turn's message id, minted before the hooks ran so an early Draft shares it.</param>
    /// <param name="ct">The run's own cancellation token, which the Turn's token is linked to.</param>
    /// <returns>The published Turn's state, or <see langword="null"/> when nothing was published and nothing was committed.</returns>
    private TurnRun? TryPublishTurn(WorkItem item, TurnHookBeforeResult? hookResult, long sequence, string messageId, CancellationToken ct)
    {
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
            if (this.IsStoppedAtOrBefore(item.RoomId, sequence))
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
            return new TurnRun(item, messageId, completion, turnCancellation, turn, opened, hookResult);
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
            run.Failed = true;
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
            run.Failed = true;
            this.logger.LogWarning(ex, "Persona '{PersonaName}' failed to process a turn in room {RoomId}.", this.owner.PersonaName, run.Item.RoomId);
        }
    }

    /// <summary>
    /// Sends one <see cref="QueueAck"/> for the item's trigger Message (Turn Queue spec, section 5.5). Nothing is sent for an
    /// item with no trigger Message (the Greeting) or once the run token is cancelled: the server decides what becomes of a
    /// row then. A write that fails is logged at Debug and dropped, so an ack never breaks a Turn or the consumer loop.
    /// </summary>
    /// <param name="item">The item whose Turn the ack is about.</param>
    /// <param name="kind">Whether the Turn started or finished.</param>
    /// <param name="replyMessageId">The reply's id for a <see cref="QueueAckKind.Started"/> ack, otherwise <see langword="null"/>.</param>
    /// <param name="outcome">How the Turn ended, for a <see cref="QueueAckKind.Finished"/> ack, otherwise <see langword="null"/>.</param>
    /// <param name="ct">The run's own cancellation token.</param>
    private async Task SendAckAsync(WorkItem item, QueueAckKind kind, string? replyMessageId, QueueOutcome? outcome, CancellationToken ct)
    {
        if (item.TriggerMessageId is not { } triggerId || ct.IsCancellationRequested)
        {
            return;
        }

        try
        {
            await this.owner.WriteAsync(new QueueAck(item.RoomId, TriggerIds(item, triggerId), kind, replyMessageId, outcome), ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
            if (this.logger.IsEnabled(LogLevel.Debug))
            {
                this.logger.LogDebug(ex, "Persona '{PersonaName}' could not send a queue ack for room {RoomId}.", this.owner.PersonaName, item.RoomId);
            }
        }
    }

    /// <summary>
    /// Reports a Turn that ended with nothing posted: Failed for a failure or a timeout, Stopped for a Stop, otherwise Silent
    /// (<c>NO_REPLY</c>, a refusal, an empty reply). A Turn that posted its reply sends nothing: the reply's
    /// <c>InReplyTo</c> closes the row.
    /// </summary>
    /// <param name="run">The Turn that ended.</param>
    /// <param name="ct">The run's own cancellation token.</param>
    private async Task ReportUnpostedTurnAsync(TurnRun run, CancellationToken ct)
    {
        if (run.ReplyPosted)
        {
            return;
        }

        QueueOutcome outcome;
        if (run.Failed)
        {
            outcome = QueueOutcome.Failed;
        }
        else if (run.Turn.StopRequested || run.EndReason == StopReason.Cancelled)
        {
            outcome = QueueOutcome.Stopped;
        }
        else
        {
            outcome = QueueOutcome.Silent;
        }

        await this.SendAckAsync(run.Item, QueueAckKind.Finished, null, outcome, ct).ConfigureAwait(false);
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
        run.Failed = true;
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
        var prompt = TurnPromptFramer.BuildPrompt(item, this.prompts);
        await this.SendAckAsync(item, QueueAckKind.Started, run.MessageId, null, ct).ConfigureAwait(false);
        await run.ActiveSession.PromptAsync(new AgentPrompt(prompt, TurnPromptFramer.BuildPromptBlocks(item)), run.TurnCancellation.Token).ConfigureAwait(false);
        run.PromptReturned = true;
        var outcome = await run.Completion.Task.WaitAsync(run.TurnCancellation.Token).ConfigureAwait(false);
        run.EndReason = outcome.Reason;

        string? postText = RoomSession.ResolvePostText(run, outcome);

        if (postText is not null)
        {
            await this.ReleaseHeldTailAsync(run, SilentReply.Strip(outcome.Text), ct).ConfigureAwait(false);
            await this.owner.WriteAsync(new PostMessage(item.RoomId, run.MessageId, postText, InReplyTo: item.TriggerMessageId is { } triggerId ? TriggerIds(item, triggerId) : null), ct).ConfigureAwait(false);
            run.ReplyPosted = true;
        }

        // A compaction drops the context's fill and the next ordinary Turn rebuilds the session's
        // fixed overhead, a rise that is not a cost of that Turn. Taking the next update as the
        // baseline, as a resumed session does, keeps it from being counted as spend (Commands
        // spec, section 6.7). Only a completed command: a Stop changed nothing.
        if (item.Kind == WorkItemKind.Command && outcome.Reason == StopReason.EndTurn)
        {
            this.usageBaselinePending = true;
            if (string.Equals(item.Command?.Name, "compact", StringComparison.OrdinalIgnoreCase))
            {
                await this.RunCompactedOpeningAsync(run.ActiveSession.SessionId, ct).ConfigureAwait(false);
            }
        }

        this.ReportTurnHealth(outcome.Reason);
    }

    /// <summary>
    /// Runs the hooks' Opening with the reason Compacted after a completed /compact and writes the resulting Hook State
    /// to the stored entry at once: a Command Turn stores no entry of its own, so this is the only write. Does not hold
    /// the gate across the await; a throwing Opening is already logged by the pipeline.
    /// </summary>
    /// <param name="sessionId">The id of the session that compacted; only an entry holding this id is written.</param>
    /// <param name="ct">The run's own cancellation token.</param>
    private async Task RunCompactedOpeningAsync(string sessionId, CancellationToken ct)
    {
        if (this.hooks is null)
        {
            return;
        }

        await this.hooks.OnSessionOpeningAsync(SessionOpenReason.Compacted, this.hookState, ct).ConfigureAwait(false);
        if (this.RoomId is { } compactRoomId && this.roomSessionStore is not null)
        {
            try
            {
                this.roomSessionStore.UpdateHookState(
                    this.owner.PersonaName,
                    compactRoomId,
                    sessionId,
                    new Dictionary<string, string?>(this.hookState, StringComparer.Ordinal));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The compaction is done and its reply posted: a store that cannot be written must not fail the Turn.
                this.logger.LogWarning(
                    ex, "Persona '{PersonaName}' could not store the hook state after /compact in room {RoomId}.", this.owner.PersonaName, compactRoomId);
            }
        }
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
            postText = AdapterCommandOutcome.Describe(ranCommand.Name, outcome.Reason, run.Turn.ToolCalls.LastOutputJson, run.Turn.ToolCalls.ToolCallFailed);
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
        await this.ReportUnpostedTurnAsync(run, ct).ConfigureAwait(false);
        await this.RunAfterHooksAsync(run, ct).ConfigureAwait(false);
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
    private Task WriteFinalDeltaAsync(TurnRun run, CancellationToken ct) =>
        this.WriteDeltaAsync(run.Item.RoomId, run.MessageId, isFinal: true, ct);

    /// <summary>
    /// Writes an empty <see cref="MessageDelta"/>: non-final opens (or keeps) the Draft, final removes it. Skipped on
    /// shutdown, and a dead pipe is logged rather than thrown, as for the terminator.
    /// </summary>
    /// <param name="roomId">The Room of the Turn.</param>
    /// <param name="messageId">The Turn's message id.</param>
    /// <param name="isFinal">Whether this is the terminator.</param>
    /// <param name="ct">The run's own cancellation token.</param>
    private async Task WriteDeltaAsync(string roomId, string messageId, bool isFinal, CancellationToken ct)
    {
        if (!ct.IsCancellationRequested)
        {
            try
            {
                await this.owner.WriteAsync(new MessageDelta(roomId, messageId, string.Empty, isFinal), ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                // The pipe died before the delta could be written; the Turn already ended
                // one way or another, so there is nothing left to signal and nothing to retry.
                this.logger.LogWarning(ex, "Persona '{PersonaName}' failed to write a delta for a turn in room {RoomId}.", this.owner.PersonaName, roomId);
            }
        }
    }

    /// <summary>
    /// Runs the Turn hooks' After points, after the Draft terminator so a slow or failing hook never delays
    /// the Turn's own MessageDelta reaching the Room. Gated on shutdown and, per finding P-17, on the Turn
    /// having either returned from PromptAsync or shown activity. The token is
    /// <paramref name="ct"/>, never the Turn's: a stopped Turn has already cancelled its own and must still commit.
    /// </summary>
    /// <param name="run">The Turn that ended.</param>
    /// <param name="ct">The run's own cancellation token.</param>
    private async Task RunAfterHooksAsync(TurnRun run, CancellationToken ct)
    {
        if (this.hooks is null || run.HookResult is null || ct.IsCancellationRequested || !(run.PromptReturned || run.Turn.SawActivity))
        {
            return;
        }

        HashSet<string> touched;
        lock (this.gate)
        {
            touched = [.. run.Turn.ToolCalls.Touched];
        }

        await this.hooks.AfterTurnAsync(run.Item.RoomId, this.hookState, run.HookResult, touched, ct).ConfigureAwait(false);
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
            var lastMessageId = run.ReplyPosted ? run.MessageId : run.Item.Folded?[^1].Id ?? run.Item.TriggerMessageId;

            // The meter's latest reading rides along, so a restart can show it again before this
            // Room's session reopens. Read after the Turn's usage updates, which precede its outcome.
            ContextFillReading? context = this.agentId is { } id ? this.contextFill?.Get(id, storeRoomId) : null;
            try
            {
                this.roomSessionStore.Put(
                    this.owner.PersonaName,
                    storeRoomId,
                    new RoomSessionEntry(
                        run.ActiveSession.SessionId, this.host.Profile.Id, this.persona.Model, this.persona.Effort, lastMessageId, this.time.GetUtcNow(), this.persona.WorkMode,
                        context?.Used, context?.Size ?? 0, new Dictionary<string, string?>(this.hookState, StringComparer.Ordinal)));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The Turn is finished and its reply posted: a store that cannot be written must not fail it. The stored entry
                // stays as it was, so a resumed session hears again what this one has already heard.
                this.logger.LogWarning(
                    ex, "Persona '{PersonaName}' could not store the session entry after a turn in room {RoomId}.", this.owner.PersonaName, storeRoomId);
            }
        }
    }

    /// <summary>
    /// The state of one published Turn that <see cref="ProcessWorkItemAsync"/> threads through its
    /// phases: what the Turn is, and the latches its phases write for the later ones to read.
    /// </summary>
    /// <param name="item">The fully prepared item.</param>
    /// <param name="messageId">The Message id minted for this Turn.</param>
    /// <param name="completion">Completed by the event reader when the Turn's outcome arrives.</param>
    /// <param name="turnCancellation">The Turn's own cancellation source, disposed last.</param>
    /// <param name="turn">The Turn published as the active one.</param>
    /// <param name="activeSession">The session the Turn was published against.</param>
    /// <param name="hookResult">What the Turn hooks' Before points produced, or <see langword="null"/> when none ran.</param>
    private sealed class TurnRun(
        WorkItem item,
        string messageId,
        TaskCompletionSource<TurnOutcome> completion,
        CancellationTokenSource turnCancellation,
        ActiveTurn turn,
        IAgentSession activeSession,
        TurnHookBeforeResult? hookResult)
    {
        /// <summary>Gets the fully prepared item.</summary>
        public WorkItem Item { get; } = item;

        /// <summary>Gets what the Turn hooks' Before points produced, or <see langword="null"/> when none ran (a Greeting, a Command, or no pipeline).</summary>
        public TurnHookBeforeResult? HookResult { get; } = hookResult;

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

        /// <summary>Gets or sets whether the Turn failed, timed out or lost its Adapter, so its queue row closes as failed.</summary>
        public bool Failed { get; set; }

        /// <summary>Gets or sets how the Adapter ended the Turn, or <see langword="null"/> when no outcome arrived.</summary>
        public StopReason? EndReason { get; set; }

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
            await this.HandleToolCallAsync(started.ToolCallId, isStart: true, started.Kind, started.Title, started.ToolName, started.Status, started.Location, started.Diff, started.RawInputJson, null, ct).ConfigureAwait(false);
        }
        else if (agentEvent is ToolCallUpdated updated)
        {
            await this.HandleToolCallAsync(updated.ToolCallId, isStart: false, updated.Kind, updated.Title, updated.ToolName, updated.Status, updated.Location, updated.Diff, updated.RawInputJson, updated.RawOutputJson, ct).ConfigureAwait(false);
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
        await this.WriteToolActivityAsync(rowId, text, ToolActivityStatus.Notice, null, null, null, null, null, ct).ConfigureAwait(false);
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
            this.owner.AddSpend(usage.SessionId, cost.Amount, cost.Currency, this.RoomId);
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
    /// <param name="arguments">A compact summary of the call's input, or <see langword="null"/> when this event carried none.</param>
    /// <param name="result">The text an App Tool returned, or <see langword="null"/> for any other call or an event with no output.</param>
    /// <param name="ct">Cancels the write.</param>
    private async Task WriteToolActivityAsync(
        string toolCallId,
        string? title,
        ToolActivityStatus status,
        ToolCallLocationInfo? location,
        ToolCallDiffInfo? diff,
        string? memoryFile,
        string? arguments,
        string? result,
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
            await this.owner.WriteAsync(new ToolActivity(turn.RoomId, turn.MessageId, toolCallId, title, status, path, location?.Line, edit, arguments, result), ct).ConfigureAwait(false);
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
    /// Records one tool call on the Turn in flight, then publishes its activity. The steps run in this
    /// order: the call's result, the paths it touched, the memory file it names, the activity row. Each
    /// recording step finds the active Turn afresh, and the <see cref="TurnToolCalls.Touched"/> changes
    /// happen under the gate in that same step, so a late event after the Turn ended records nothing.
    /// </summary>
    /// <param name="toolCallId">The id of the tool call.</param>
    /// <param name="isStart">Whether this is the call's first event.</param>
    /// <param name="kind">The tool call's category, as ACP reported it.</param>
    /// <param name="title">A human-readable label for the call, if the agent supplied one.</param>
    /// <param name="toolName">The Adapter's own tool name, when it sends one.</param>
    /// <param name="status">The call's lifecycle state on this event.</param>
    /// <param name="location">The call's first location, or <see langword="null"/> when this event has none.</param>
    /// <param name="diff">The call's first change, or <see langword="null"/> when this event has none.</param>
    /// <param name="rawInputJson">The tool call's raw input, if any.</param>
    /// <param name="rawOutputJson">The tool call's output, if this event carries one.</param>
    /// <param name="ct">The reader's own cancellation token.</param>
    private async Task HandleToolCallAsync(
        string toolCallId,
        bool isStart,
        ToolKind kind,
        string? title,
        string? toolName,
        ToolCallStatus status,
        ToolCallLocationInfo? location,
        ToolCallDiffInfo? diff,
        string? rawInputJson,
        string? rawOutputJson,
        CancellationToken ct)
    {
        ActiveTurn? turn;
        lock (this.gate)
        {
            turn = this.activeTurn;
        }

        turn?.ToolCalls.RecordResult(toolCallId, isStart, status, rawOutputJson);

        lock (this.gate)
        {
            this.activeTurn?.ToolCalls.RecordTouched(kind, rawInputJson);
        }

        string? memoryFile = this.RecordMemoryToolPath(kind, title, toolName, rawInputJson);

        // Only Huddle's own App Tools have their answer recorded: another tool's output may be a file's
        // contents or a connector's mail, and the lasting log must not hold that.
        string? callName = toolName ?? title;
        string? result = callName is not null && callName.StartsWith(TeamToolPrefix, StringComparison.Ordinal)
            ? ToolCallSummary.Result(rawOutputJson)
            : null;
        await this.WriteToolActivityAsync(
            toolCallId, title, MapToolCallStatus(status), location, diff, memoryFile, ToolCallSummary.Arguments(rawInputJson), result, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Adds the file a <c>memory</c> App Tool call changes to the active Turn's <see cref="TurnToolCalls.Touched"/> set, so File
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
            || this.memoryPaths is not { } resolver
            || MemoryRequest.FromJson(rawInputJson) is not { } request
            || !resolver.TryResolve(this.owner.PersonaName, request.Folder, request.File, out string? path))
        {
            return null;
        }

        if (request.Mutates)
        {
            lock (this.gate)
            {
                this.activeTurn?.ToolCalls.AddTouched(path);
            }
        }

        return path;
    }

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
        private bool approvalTimedOut;
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
        /// What this Turn keeps of its tool calls. Threading as <see cref="TurnToolCalls"/> describes:
        /// the event reader is the only writer, except <see cref="TurnToolCalls.Touched"/>, which
        /// goes through <see cref="RoomSession.gate"/>.
        /// </summary>
        public TurnToolCalls ToolCalls { get; } = new();

        /// <summary>
        /// The last <see cref="EditChange"/> written for each tool call of this Turn, keyed by tool
        /// call id, so an unchanged preview is not sent twice. Touched only from the single-threaded
        /// event-reader loop, so it needs no lock, and it dies with the Turn.
        /// </summary>
        public Dictionary<string, EditChange> LastSentEdits { get; } = new(StringComparer.Ordinal);

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

        /// <summary>Gets a value indicating whether a question on this Turn went unanswered until its bound; not the Turn's own timeout.</summary>
        public bool ApprovalTimedOut => Volatile.Read(ref this.approvalTimedOut);

        /// <summary>Records that a question on this Turn went unanswered until its bound.</summary>
        public void MarkApprovalTimedOut() => Volatile.Write(ref this.approvalTimedOut, true);

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

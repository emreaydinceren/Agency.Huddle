using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Memory;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Services;

namespace Agency.Huddle.App.Acp.Sessions;

/// <summary>
/// Owns every <see cref="RoomSession"/> a Persona has work in (RS §6.2): lazy per-Room opens, idle
/// eviction, the live-session cap, Stop routing, and the ticket gate every Room Session's Turns run
/// through. In shared mode (<c>SessionPerRoom: false</c>, finding P-9) it holds exactly one
/// <see cref="RoomSession"/> serving every Room, opened once at start and never evicted or swept
/// (finding P-20).
/// </summary>
internal sealed class RoomSessionPool : ITurnScheduler, IAsyncDisposable
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(30);

    private readonly IPersonaHost host;
    private readonly IRoomSessionOwner owner;
    private readonly Persona persona;
    private readonly IPromptSource prompts;
    private readonly AcpOptions options;
    private readonly TimeProvider time;
    private readonly MemoryPaths? memoryPaths;
    private readonly ILogger logger;
    private readonly CancellationToken runToken;
    private readonly RoomSessionStore? roomSessions;
    private readonly OwnPosts? ownPosts;
    private readonly string? agentId;
    private readonly TurnActivity? turnActivity;
    private readonly TurnHooks.TurnEnricherPipeline? enrichers;
    private readonly ContextFill? contextFill;
    private readonly ISessionSettings? sessionSettings;
    private readonly LastCatchUpStore? lastCatchUp;
    private readonly TurnHooks.TurnHookPipeline? hooks;
    private readonly IElicitationBridge? elicitationBridge;
    private readonly bool sessionPerRoom;
    private readonly TurnGate gate;
    private readonly int effectiveMaxLiveSessions;

    // Guards sessionsByRoom and startupSession. Never held across an await, and never acquired from
    // inside a RoomSession's own gate (lock order: pool -> session -> gate; D23 correction 12).
    private readonly Lock poolLock = new();
    private readonly Dictionary<string, RoomSession> sessionsByRoom = new(StringComparer.Ordinal);
    private readonly HashSet<string> notePendingRooms = new(StringComparer.Ordinal);

    // The goldfish slot: one clean session beside sessionsByRoom, never in it, so the live count, PickVictim and the idle
    // sweep never see it. Guarded by poolLock.
    private RoomSession? goldfish;
    private string? goldfishRoomId;

    // The goldfish session's own run token, linked to runToken: a session ends only when its run token is cancelled, so
    // closing the slot while the app runs cancels this one.
    private CancellationTokenSource? goldfishCts;

    // The close that is disposing the last emptied slot, so a later close awaits it rather than returning early. Guarded by poolLock.
    private Task? goldfishClosing;

    private readonly RoomSession? sharedSession;
    private readonly ITimer? sweepTimer;
    private RoomSession? startupSession;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="RoomSessionPool"/> class.</summary>
    /// <param name="host">The Persona's running Adapter host, which opens every Room Session's <see cref="IAgentSession"/>.</param>
    /// <param name="owner">What every Room Session reports Turn outcomes, tokens and health through.</param>
    /// <param name="persona">The Persona this pool serves (RS §6.1 resume: its Model, Effort and Work Mode must still match a stored entry).</param>
    /// <param name="prompts">Resolves every <c>turn.*</c> prompt's current text.</param>
    /// <param name="options">The Persona's current ACP options.</param>
    /// <param name="time">Drives idle eviction and the sweep timer.</param>
    /// <param name="logger">Where this pool logs.</param>
    /// <param name="runToken">The runner's own run token; cancelled means shutdown.</param>
    /// <param name="roomSessions">
    /// Backs resume and the Transcript Catch-up cursor (RS §6.1, §6.6), per-Room mode only (finding
    /// P-15). <see langword="null"/> disables storing and resuming - a caller that predates D24.
    /// </param>
    /// <param name="ownPosts">Marks each Room Session's Room Busy for a Turn's own duration (D27, RS §6.7). <see langword="null"/> disables it, like every pre-D27 caller.</param>
    /// <param name="agentId">This Persona's Agent id, passed to <paramref name="ownPosts"/> and <paramref name="turnActivity"/>. <see langword="null"/> disables it, like every pre-D27 caller.</param>
    /// <param name="turnActivity">Records which Room each Room Session's Agent has a Turn running in (Spec §10.8). <see langword="null"/> disables it, like every caller that predates it.</param>
    /// <param name="enrichers">Adds the Library documents (Spec §6.14) and the Referenced items header to a Message Turn, passed to every Room Session this pool creates. <see langword="null"/> adds neither, like every caller that predates it.</param>
    /// <param name="elicitationBridge">Shows the Human the questions the agent asks (E-3), passed to every Room Session this pool creates. <see langword="null"/> answers every one as cancelled, like every caller that predates it.</param>
    /// <param name="contextFill">Records each Room Session's context-window fill, passed to every Room Session this pool creates. <see langword="null"/> disables it, like every caller that predates it.</param>
    /// <param name="sessionSettings">The resolved Session settings passed to every Room Session, for the first Turn's Transcript catch-up. <see langword="null"/> uses the <see cref="AcpOptions"/> values, like every caller that predates it.</param>
    /// <param name="lastCatchUp">The store every Room Session writes its first post-fresh-start prompt to. <see langword="null"/> writes no file.</param>
    /// <param name="hooks">The Turn hook pipeline every Room Session runs. <see langword="null"/> runs none.</param>
    /// <param name="memoryPaths">Resolves an Agent's own <c>memory</c> writes, passed to every Room Session this pool creates. <see langword="null"/> records none.</param>
    public RoomSessionPool(
        IPersonaHost host,
        IRoomSessionOwner owner,
        Persona persona,
        IPromptSource prompts,
        AcpOptions options,
        TimeProvider time,
        ILogger logger,
        CancellationToken runToken,
        RoomSessionStore? roomSessions = null,
        OwnPosts? ownPosts = null,
        string? agentId = null,
        TurnActivity? turnActivity = null,
        TurnHooks.TurnEnricherPipeline? enrichers = null,
        IElicitationBridge? elicitationBridge = null,
        ContextFill? contextFill = null,
        ISessionSettings? sessionSettings = null,
        LastCatchUpStore? lastCatchUp = null,
        TurnHooks.TurnHookPipeline? hooks = null,
        MemoryPaths? memoryPaths = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(persona);
        ArgumentNullException.ThrowIfNull(prompts);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);

        this.host = host;
        this.owner = owner;
        this.persona = persona;
        this.prompts = prompts;
        this.options = options;
        this.time = time;
        this.memoryPaths = memoryPaths;
        this.logger = logger;
        this.runToken = runToken;
        this.roomSessions = roomSessions;
        this.ownPosts = ownPosts;
        this.agentId = agentId;
        this.turnActivity = turnActivity;
        this.enrichers = enrichers;
        this.contextFill = contextFill;
        this.sessionSettings = sessionSettings;
        this.lastCatchUp = lastCatchUp;
        this.hooks = hooks;
        this.elicitationBridge = elicitationBridge;
        this.sessionPerRoom = host.Profile.SessionPerRoom;

        var configuredConcurrency = Math.Max(1, options.MaxConcurrentTurns);
        this.gate = new TurnGate(configuredConcurrency);

        this.effectiveMaxLiveSessions = RoomSessionPool.ResolveLiveSessionCap(logger, options.MaxLiveSessions, configuredConcurrency);


        if (!this.sessionPerRoom)
        {
            // Shared mode's one session is created eagerly: every Room ever routes to it, so there is
            // never a "which Room" question the way there is per-Room.
            this.sharedSession = this.CreateSession(roomId: null);
        }

        if (this.sessionPerRoom && this.options.SessionIdleMinutes > 0)
        {
            // Finding P-20: the sweep exists only in per-Room mode. A no-op ManualTimeProvider timer
            // (D23 correction 15) never actually fires in a test; the real 30-second cadence here is
            // what production relies on.
            this.sweepTimer = this.time.CreateTimer(
                this.OnSweepTimerFired, null, RoomSessionPool.SweepInterval, RoomSessionPool.SweepInterval);
        }
    }

    /// <summary>The live-session cap actually used: the configured cap, raised to the concurrency limit when it is lower (with a warning).</summary>
    /// <param name="logger">Receives the warning when the cap is raised.</param>
    /// <param name="configuredLiveCap">The configured <c>MaxLiveSessions</c>.</param>
    /// <param name="configuredConcurrency">The effective concurrent-Turn limit.</param>
    private static int ResolveLiveSessionCap(ILogger logger, int configuredLiveCap, int configuredConcurrency)
    {
        int effective = Math.Max(configuredLiveCap, configuredConcurrency);
        if (effective != configuredLiveCap)
        {
            logger.LogWarning(
                "MaxLiveSessions {Configured} is below MaxConcurrentTurns {Concurrent}; raised to {Effective}.",
#pragma warning disable HAA0601 // Boxing a value-type log argument is the cost of ILogger params object[]; accepted together with HAA0101.
                configuredLiveCap,
                configuredConcurrency,
                effective);
#pragma warning restore HAA0601
        }

        return effective;
    }
    /// <summary>The models the session opened at start advertises, or empty when nothing opened yet.</summary>
    internal IReadOnlyList<AgentModelOption> Models => this.startupSession?.Models ?? [];

    /// <summary>Queues a Turn: routed to the shared session, or lazily created/looked-up per <see cref="WorkItem.RoomId"/>.</summary>
    /// <param name="item">The Turn to run, with its sequence number.</param>
    internal void Enqueue(QueuedWork item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var session = this.sessionPerRoom ? this.GetOrCreateSession(item.Item.RoomId) : this.sharedSession!;
        session.Enqueue(item);
    }

    /// <summary>Opens the shared session (shared mode) or the Room with the Human's session (per-Room mode), as <see cref="PersonaRunner.StartAsync"/> did before D23.</summary>
    /// <param name="humanRoomId">The Room with exactly the Human and this Agent, or <see langword="null"/> when none exists.</param>
    /// <param name="cancellationToken">Cancels the open.</param>
    internal async Task OpenAtStartAsync(string? humanRoomId, CancellationToken cancellationToken)
    {
        if (!this.sessionPerRoom)
        {
            await this.sharedSession!.OpenAsync(cancellationToken).ConfigureAwait(false);
            this.startupSession = this.sharedSession;
            return;
        }

        if (humanRoomId is null)
        {
            // No Room with the Human: nothing opens at start (RS §6.2's exception is the Human Room
            // only). The Persona's first Turn, whenever it arrives, opens its own Room lazily.
            return;
        }

        var session = this.GetOrCreateSession(humanRoomId);

        // D22 correction 6 / P-5 invariant 1's exception: this open runs before any ticket exists for
        // it and before the read loop's consumers can offer one, so it bypasses the gate entirely -
        // safe only because it completes before anything else could contend for a slot.
        await session.OpenAsync(cancellationToken).ConfigureAwait(false);
        this.startupSession = session;
    }

    /// <summary>Routes a Stop to that Room's session (per-Room), or the shared one; a Room with no session yet is a no-op.</summary>
    /// <param name="roomId">The Room the Human asked to stop.</param>
    /// <param name="mark">The Persona-wide sequence value as of this Stop.</param>
    /// <param name="cancellationToken">Passed to the far side's cancel.</param>
    internal Task StopAsync(string roomId, long mark, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);

        if (!this.sessionPerRoom)
        {
            return this.sharedSession!.StopAsync(roomId, mark, cancellationToken);
        }

        RoomSession? session;
        RoomSession? clean = null;
        lock (this.poolLock)
        {
            this.sessionsByRoom.TryGetValue(roomId, out session);
            if (string.Equals(this.goldfishRoomId, roomId, StringComparison.Ordinal))
            {
                clean = this.goldfish;
            }
        }

        return StopBothAsync(session, clean, roomId, mark, cancellationToken);
    }

    private static async Task StopBothAsync(RoomSession? session, RoomSession? clean, string roomId, long mark, CancellationToken cancellationToken)
    {
        Task warm = session is null ? Task.CompletedTask : session.StopAsync(roomId, mark, cancellationToken);
        Task cold = clean is null ? Task.CompletedTask : clean.StopAsync(roomId, mark, cancellationToken);
        await Task.WhenAll(warm, cold).ConfigureAwait(false);
    }

    /// <summary>The Room the goldfish session serves, or <see langword="null"/> when the slot is empty.</summary>
    internal string? GoldfishRoomId
    {
        get
        {
            lock (this.poolLock)
            {
                return this.goldfishRoomId;
            }
        }
    }

    /// <summary>Opens the goldfish slot: a clean session that opens through <see cref="IPersonaHost.OpenCleanAsync"/> and shares this pool's Turn gate.</summary>
    /// <param name="roomId">The Room the possession lives in.</param>
    /// <param name="spec">What the clean session is opened for.</param>
    /// <returns>The new session.</returns>
    /// <exception cref="InvalidOperationException">The slot is taken, or the host is not per-Room.</exception>
    internal RoomSession OpenGoldfish(string roomId, CleanSessionSpec spec)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        ArgumentNullException.ThrowIfNull(spec);

        if (!this.sessionPerRoom)
        {
            throw new InvalidOperationException("A goldfish session needs a per-Room Adapter host.");
        }

        lock (this.poolLock)
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);
            if (this.goldfish is not null)
            {
                throw new InvalidOperationException("The goldfish slot is already taken.");
            }

            CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(this.runToken);
            RoomSession session = this.NewGoldfishSession(roomId, spec, cts.Token);
            this.goldfishCts = cts;
            this.goldfish = session;
            this.goldfishRoomId = roomId;
            return session;
        }
    }

    /// <summary>Queues a Turn to the goldfish session.</summary>
    /// <param name="item">The Turn to run.</param>
    /// <exception cref="InvalidOperationException">The slot is empty.</exception>
    /// <exception cref="ObjectDisposedException">The pool is disposed.</exception>
    internal void EnqueueGoldfish(QueuedWork item)
    {
        // Queued under poolLock, the lock a close empties the slot under: the item is queued before the close takes the session,
        // so the close sees its ticket, or the slot is already empty and this throws.
        lock (this.poolLock)
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);
            if (this.goldfish is null)
            {
                throw new InvalidOperationException("The goldfish slot is empty.");
            }

            this.goldfish.Enqueue(item);
        }
    }

    /// <summary>
    /// Disposes the goldfish session and empties the slot. Does nothing when it is empty, except that a close already disposing the
    /// last session is awaited, so every caller returns after that session is disposed.
    /// </summary>
    /// <returns>A task that completes when the session is disposed.</returns>
    internal Task CloseGoldfishAsync()
    {
        RoomSession session;
        CancellationTokenSource cts;
        Task prior;
        TaskCompletionSource done = new(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (this.poolLock)
        {
            if (this.goldfish is null || this.goldfishCts is null)
            {
                return this.goldfishClosing ?? Task.CompletedTask;
            }

            session = this.goldfish;
            cts = this.goldfishCts;
            prior = this.goldfishClosing ?? Task.CompletedTask;
            this.goldfish = null;
            this.goldfishCts = null;
            this.goldfishRoomId = null;
            this.goldfishClosing = done.Task;
        }

        return RoomSessionPool.CloseSlotAsync(session, cts, prior, done);
    }

    private static async Task CloseSlotAsync(RoomSession session, CancellationTokenSource cts, Task prior, TaskCompletionSource done)
    {
        try
        {
            await prior.ConfigureAwait(false);
            await cts.CancelAsync().ConfigureAwait(false);
            await session.DisposeAsync().ConfigureAwait(false);

            // The consumer may have ended before it took a ticket an enqueue had offered: withdraw it, so the shared gate never holds it.
            session.WithdrawQueuedTickets();
            done.SetResult();
        }
        catch (Exception ex)
        {
            done.SetException(ex);
            throw;
        }
        finally
        {
            cts.Dispose();
        }
    }

    private RoomSession NewGoldfishSession(string roomId, CleanSessionSpec spec, CancellationToken sessionToken) =>
        new(
            roomId,
            open: ct => this.host.OpenCleanAsync(spec, ct),
            owner: this.owner,
            scheduler: this,
            prompts: this.prompts,
            options: this.options,
            logger: this.logger,
            runToken: sessionToken,
            time: this.time,
            persona: this.persona,
            host: this.host,
            agentId: this.agentId,
            turnActivity: this.turnActivity,
            enrichers: this.enrichers,
            elicitationBridge: this.elicitationBridge,
            sessionSettings: this.sessionSettings,
            hooks: this.hooks?.ForClean(),
            clean: true);

    /// <summary>Closes every <c>Idle</c>, empty-queue Room Session whose <see cref="RoomSession.LastActivity"/> is older than <see cref="AcpOptions.SessionIdleMinutes"/>. A no-op in shared mode (finding P-20) or when the threshold is zero or less.</summary>
    internal Task SweepIdleAsync()
    {
        if (!this.sessionPerRoom || this.options.SessionIdleMinutes <= 0)
        {
            return Task.CompletedTask;
        }

        var threshold = this.time.GetUtcNow() - TimeSpan.FromMinutes(this.options.SessionIdleMinutes);
        List<RoomSession> stale;
        lock (this.poolLock)
        {
            stale = [.. this.sessionsByRoom.Values.Where(s => s.State == RoomSessionState.Idle && s.QueueCount == 0 && s.LastActivity < threshold)];
        }

        return this.CloseAllAsync(stale);
    }

    /// <inheritdoc/>
    public void Offer(long ticket) => this.gate.Offer(ticket);

    /// <inheritdoc/>
    public Task WaitAsync(long ticket, CancellationToken cancellationToken) => this.gate.WaitAsync(ticket, cancellationToken);

    /// <inheritdoc/>
    public void Withdraw(long ticket) => this.gate.Withdraw(ticket);

    /// <inheritdoc/>
    public void Complete(long ticket) => this.gate.Complete(ticket);

    /// <inheritdoc/>
    /// <remarks>
    /// P-5's three invariants, enforced here and in <see cref="RoomSession.TryMarkOpening"/>:
    /// (1) a session is <c>Busy</c> or <c>Opening</c> only while holding an admitted ticket - true
    /// because this method runs only from a Room Session's own consumer, after admission, right
    /// before it opens; (2) the requester's <c>Closed</c>→<c>Opening</c> transition is one
    /// compare-and-set under the session's own lock; (3) the live-count check and that same CAS run
    /// under this <see cref="poolLock"/> as one atomic step, so two concurrent requesters can never
    /// both believe they took the last place. A no-op in shared mode: one session needs no eviction.
    /// The victim's actual close (I/O) runs after <see cref="poolLock"/> is released - never an await
    /// while holding it.
    /// </remarks>
    public async Task MakeRoomToOpenAsync(RoomSession requester, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requester);

        if (!this.sessionPerRoom)
        {
            return;
        }

        RoomSession? victim = null;
        lock (this.poolLock)
        {
            if (!requester.TryMarkOpening())
            {
                // Another path already opened or is opening this session; nothing left to do here.
                return;
            }

            var live = this.sessionsByRoom.Values.Count(s => s.State != RoomSessionState.Closed);
            if (live <= this.effectiveMaxLiveSessions)
            {
                return;
            }

            victim = RoomSessionPool.PickVictim(this.sessionsByRoom.Values, requester);
        }

        if (victim is null)
        {
            this.logger.LogWarning(
#pragma warning disable HAA0601 // Boxing a value-type log argument is the cost of ILogger params object[]; accepted together with HAA0101.
                "No Idle Room Session was evictable at the live cap of {Cap}; opening anyway.", this.effectiveMaxLiveSessions);
#pragma warning restore HAA0601
            return;
        }

        await victim.CloseAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;

        if (this.sweepTimer is not null)
        {
            await this.sweepTimer.DisposeAsync().ConfigureAwait(false);
        }

        // try/finally: the host owns the Adapter process, so it is disposed even when a session throws
        // while closing - a skipped host is a `node` process that outlives the app.
        try
        {
            await this.CloseGoldfishAsync().ConfigureAwait(false);

            if (this.sessionPerRoom)
            {
                List<RoomSession> sessions;
                lock (this.poolLock)
                {
                    sessions = [.. this.sessionsByRoom.Values];
                }

                foreach (var session in sessions)
                {
                    await session.DisposeAsync().ConfigureAwait(false);
                }
            }
            else if (this.sharedSession is not null)
            {
                await this.sharedSession.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            await this.host.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>The <see cref="TimerCallback"/> the sweep timer invokes every <see cref="SweepInterval"/>.</summary>
    /// <param name="state">Unused; the timer carries no state.</param>
    private void OnSweepTimerFired(object? state) => _ = this.SweepIdleAsync();

    /// <summary>Routes a start afresh to that Room's session; a Room with no session forgets what is stored and is remembered as note-pending. A no-op in shared mode (LR-E5).</summary>
    /// <param name="roomId">The Room the Human asked to start afresh.</param>
    /// <param name="cancellationToken">Passed to the session.</param>
    internal Task StartAfreshAsync(string roomId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);

        if (!this.sessionPerRoom)
        {
            return Task.CompletedTask;
        }

        RoomSession? session;
        lock (this.poolLock)
        {
            if (!this.sessionsByRoom.TryGetValue(roomId, out session))
            {
                this.roomSessions?.Forget(this.persona.Name, roomId);
                if (this.agentId is not null)
                {
                    this.contextFill?.Forget(this.agentId, roomId);
                }

                this.notePendingRooms.Add(roomId);
            }
        }

        return session is null ? Task.CompletedTask : session.RequestFreshStartAsync(cancellationToken);
    }

    /// <summary>The Room's session, or the shared one in shared mode; <see langword="null"/> when a per-Room Room has none yet.</summary>
    /// <param name="roomId">The Room.</param>
    internal RoomSession? SessionFor(string roomId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);

        if (!this.sessionPerRoom)
        {
            return this.sharedSession;
        }

        lock (this.poolLock)
        {
            return this.sessionsByRoom.GetValueOrDefault(roomId);
        }
    }

    private RoomSession CreateSession(string? roomId)
    {
        RoomSession session = this.NewSession(roomId);
        if (roomId is not null)
        {
            lock (this.poolLock)
            {
                if (this.notePendingRooms.Remove(roomId))
                {
                    session.MarkFreshStartNotePending();
                }
            }
        }

        return session;
    }

    private RoomSession NewSession(string? roomId) =>
        new(
            roomId,
            open: this.host.OpenAsync,
            owner: this.owner,
            scheduler: this,
            prompts: this.prompts,
            options: this.options,
            logger: this.logger,
            runToken: this.runToken,
            time: this.time,
            persona: this.persona,
            roomSessions: this.roomSessions,
            host: this.host,
            ownPosts: this.ownPosts,
            agentId: this.agentId,
            turnActivity: this.turnActivity,
            enrichers: this.enrichers,
            elicitationBridge: this.elicitationBridge,
            contextFill: this.contextFill,
            sessionSettings: this.sessionSettings,
            lastCatchUp: this.lastCatchUp,
            hooks: this.hooks,
            memoryPaths: this.memoryPaths);

    private RoomSession GetOrCreateSession(string roomId)
    {
        lock (this.poolLock)
        {
            if (!this.sessionsByRoom.TryGetValue(roomId, out var session))
            {
                session = this.CreateSession(roomId);
                this.sessionsByRoom[roomId] = session;
            }

            return session;
        }
    }

    private async Task CloseAllAsync(List<RoomSession> sessions)
    {
        foreach (var session in sessions)
        {
            try
            {
                await session.CloseAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // The sweep must never throw (RS §6.2): nothing else drives it but the timer, and an
                // unhandled exception there would silently stop future sweeps for good.
                this.logger.LogWarning(ex, "The idle Room Session sweep failed to close one session.");
            }
        }
    }

    /// <summary>Picks the eviction victim: an <c>Idle</c> session with an empty queue, preferred over one with queued work, oldest <see cref="RoomSession.LastActivity"/> first within each group.</summary>
    /// <param name="candidates">Every currently known Room Session.</param>
    /// <param name="requester">Excluded: the session that is itself about to open.</param>
    private static RoomSession? PickVictim(IEnumerable<RoomSession> candidates, RoomSession requester)
    {
        RoomSession? victim = null;
        foreach (var candidate in candidates)
        {
            if (ReferenceEquals(candidate, requester) || candidate.State != RoomSessionState.Idle)
            {
                continue;
            }

            if (victim is null)
            {
                victim = candidate;
                continue;
            }

            var candidateEmpty = candidate.QueueCount == 0;
            var victimEmpty = victim.QueueCount == 0;
            bool emptierThanVictim = candidateEmpty && !victimEmpty;
            bool sameEmptinessButIdler = candidateEmpty == victimEmpty && candidate.LastActivity < victim.LastActivity;
            if (emptierThanVictim || sameEmptinessButIdler)
            {
                victim = candidate;
            }
        }

        return victim;
    }
}

using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Prompts;

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
    private readonly FileChangeTracker? fileChanges;
    private readonly IReadOnlyList<string> declaredWatches;
    private readonly ILogger logger;
    private readonly CancellationToken runToken;
    private readonly RoomSessionStore? roomSessions;
    private readonly OwnPosts? ownPosts;
    private readonly string? agentId;
    private readonly bool sessionPerRoom;
    private readonly TurnGate gate;
    private readonly int effectiveMaxLiveSessions;

    // Guards sessionsByRoom and startupSession. Never held across an await, and never acquired from
    // inside a RoomSession's own gate (lock order: pool -> session -> gate; D23 correction 12).
    private readonly Lock poolLock = new();
    private readonly Dictionary<string, RoomSession> sessionsByRoom = new(StringComparer.Ordinal);

    private readonly RoomSession? sharedSession;
    private readonly ITimer? sweepTimer;
    private RoomSession? startupSession;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="RoomSessionPool"/> class.</summary>
    /// <param name="host">The Persona's running Adapter host, which opens every Room Session's <see cref="IAgentSession"/>.</param>
    /// <param name="owner">What every Room Session reports Turn outcomes, tokens and health through.</param>
    /// <param name="persona">The Persona this pool serves (RS §6.1 resume: its Model and Effort must still match a stored entry).</param>
    /// <param name="prompts">Resolves every <c>turn.*</c> prompt's current text.</param>
    /// <param name="options">The Persona's current ACP options.</param>
    /// <param name="time">Drives idle eviction and the sweep timer.</param>
    /// <param name="fileChanges">The Persona's File Changes tracker, or <see langword="null"/> when File Changes is off.</param>
    /// <param name="declaredWatches">The Persona's declared Watched Folders.</param>
    /// <param name="logger">Where this pool logs.</param>
    /// <param name="runToken">The runner's own run token; cancelled means shutdown.</param>
    /// <param name="roomSessions">
    /// Backs resume and the Transcript Catch-up cursor (RS §6.1, §6.6), per-Room mode only (finding
    /// P-15). <see langword="null"/> disables storing and resuming - a caller that predates D24.
    /// </param>
    /// <param name="ownPosts">Marks each Room Session's Room Busy for a Turn's own duration (D27, RS §6.7). <see langword="null"/> disables it, like every pre-D27 caller.</param>
    /// <param name="agentId">This Persona's Agent id, passed to <paramref name="ownPosts"/>. <see langword="null"/> disables it, like every pre-D27 caller.</param>
    public RoomSessionPool(
        IPersonaHost host,
        IRoomSessionOwner owner,
        Persona persona,
        IPromptSource prompts,
        AcpOptions options,
        TimeProvider time,
        FileChangeTracker? fileChanges,
        IReadOnlyList<string> declaredWatches,
        ILogger logger,
        CancellationToken runToken,
        RoomSessionStore? roomSessions = null,
        OwnPosts? ownPosts = null,
        string? agentId = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(persona);
        ArgumentNullException.ThrowIfNull(prompts);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(declaredWatches);
        ArgumentNullException.ThrowIfNull(logger);

        this.host = host;
        this.owner = owner;
        this.persona = persona;
        this.prompts = prompts;
        this.options = options;
        this.time = time;
        this.fileChanges = fileChanges;
        this.declaredWatches = declaredWatches;
        this.logger = logger;
        this.runToken = runToken;
        this.roomSessions = roomSessions;
        this.ownPosts = ownPosts;
        this.agentId = agentId;
        this.sessionPerRoom = host.Profile.SessionPerRoom;

        var configuredConcurrency = Math.Max(1, options.MaxConcurrentTurns);
        this.gate = new TurnGate(configuredConcurrency);

        var configuredLiveCap = options.MaxLiveSessions;
        this.effectiveMaxLiveSessions = Math.Max(configuredLiveCap, configuredConcurrency);
        if (this.effectiveMaxLiveSessions != configuredLiveCap)
        {
            this.logger.LogWarning(
                "MaxLiveSessions {Configured} is below MaxConcurrentTurns {Concurrent}; raised to {Effective}.",
                configuredLiveCap,
                configuredConcurrency,
                this.effectiveMaxLiveSessions);
        }

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
            await this.sharedSession!.OpenAsync(cancellationToken);
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
        await session.OpenAsync(cancellationToken);
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
        lock (this.poolLock)
        {
            this.sessionsByRoom.TryGetValue(roomId, out session);
        }

        return session is null ? Task.CompletedTask : session.StopAsync(roomId, mark, cancellationToken);
    }

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
                "No Idle Room Session was evictable at the live cap of {Cap}; opening anyway.", this.effectiveMaxLiveSessions);
            return;
        }

        await victim.CloseAsync();
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
            await this.sweepTimer.DisposeAsync();
        }

        if (this.sessionPerRoom)
        {
            List<RoomSession> sessions;
            lock (this.poolLock)
            {
                sessions = [.. this.sessionsByRoom.Values];
            }

            foreach (var session in sessions)
            {
                await session.DisposeAsync();
            }
        }
        else if (this.sharedSession is not null)
        {
            await this.sharedSession.DisposeAsync();
        }

        await this.host.DisposeAsync();
    }

    /// <summary>The <see cref="TimerCallback"/> the sweep timer invokes every <see cref="SweepInterval"/>.</summary>
    /// <param name="state">Unused; the timer carries no state.</param>
    private void OnSweepTimerFired(object? state) => _ = this.SweepIdleAsync();

    private RoomSession CreateSession(string? roomId) =>
        new(
            roomId,
            open: this.host.OpenAsync,
            owner: this.owner,
            scheduler: this,
            prompts: this.prompts,
            options: this.options,
            fileChanges: this.fileChanges,
            declaredWatches: this.declaredWatches,
            logger: this.logger,
            runToken: this.runToken,
            time: this.time,
            persona: this.persona,
            roomSessions: this.roomSessions,
            host: this.host,
            ownPosts: this.ownPosts,
            agentId: this.agentId);

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
                await session.CloseAsync();
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
            if (candidateEmpty && !victimEmpty)
            {
                victim = candidate;
            }
            else if (candidateEmpty == victimEmpty && candidate.LastActivity < victim.LastActivity)
            {
                victim = candidate;
            }
        }

        return victim;
    }
}

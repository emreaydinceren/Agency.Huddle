using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Pipes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Teammates;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Runs one Persona: it brings the Persona online as an Agent and keeps it there. A
/// <see cref="PersonaRunner"/> is simultaneously a pipe client — it dials the application's own named
/// pipe as an ordinary protocol client and registers an Agent, exactly as
/// <see cref="Agency.Huddle.App.Demo.DemoAgentHost"/> does, deliberately with no privileged access to the Team
/// Directory — and an ACP client, owning one <see cref="IPersonaHost"/> and, in shared mode (RS §6.1),
/// one <see cref="RoomSession"/> serving every Room the Agent is in. This runner keeps the pipe, the
/// handshake, the read loop (Reply Gate, Catch-up buffers, known Room names, Stop routing), the
/// Persona-wide token counter and failure streak, and the Greeting; the Turn machinery itself —
/// queue, consumer, session, event reader and Turns — lives in <see cref="RoomSession"/>
/// (<see cref="IRoomSessionOwner"/> is what a Room Session calls back into this runner through).
/// </summary>
/// <remarks>
/// Deliberately not a <see cref="Microsoft.Extensions.Hosting.BackgroundService"/>: hosted services are
/// fixed at build time, and instances of this class are created per Persona at runtime.
/// <see cref="PersonaSupervisor"/> owns the lifetime of each instance, one per Persona.
/// </remarks>
internal sealed class PersonaRunner : IAsyncDisposable, IRoomSessionOwner
{
    private const int MaxConnectAttempts = 30;
    private static readonly TimeSpan ConnectRetryDelay = TimeSpan.FromMilliseconds(200);
    private const int MaxDescriptionLength = 200;

    private readonly Persona persona;
    private readonly TeamOptions options;
    private readonly IAgentHostFactory factory;
    private readonly IPromptSource prompts;
    private readonly RoomFollows roomFollows;
    private readonly ILogger<PersonaRunner> logger;
    private readonly FileChangeTracker? fileChanges;
    private readonly IReadOnlyList<string> declaredWatches;
    private readonly CancellationTokenSource runCts = new();

    // Per-Room catch-up buffers for Messages the Agent received but was not Mentioned in (ADR-0004).
    // In memory only, per instance, keyed by Room id: a restart loses them, and an Agent that was
    // offline when a Message was delivered never had it to buffer in the first place. Guarded by
    // catchUpLock because, although only the read loop touches these buffers today, nothing about
    // this field's contract promises that will always stay true.
    private readonly Dictionary<string, List<CaughtUpMessage>> catchUpBuffers = [];
    private readonly Lock catchUpLock = new();

    // Pending Transcript reads (RS §6.5, D24): keyed by the ReadTranscript.RequestId this runner
    // minted, completed either by the matching TranscriptTail or by a ProtocolError naming that
    // RequestId as its RelatedMessageId - both arrive on the read loop, the same thread that started
    // the wait never blocks it, so a ConcurrentDictionary (rather than catchUpLock) is what lets
    // several Room Sessions' own consumer threads each await their own pending read concurrently.
    private readonly ConcurrentDictionary<string, TaskCompletionSource<TranscriptTail?>> pendingTranscriptReads = new(StringComparer.Ordinal);

    // D23 correction 17 / D24 correction 23: the Model-not-in-catalog warning fires at most once per
    // runner, from whichever Room Session opens first - 0 means not yet reported, 1 means reported;
    // Interlocked because more than one Room Session can open concurrently in per-Room mode.
    private int modelWarningReported;

    // The third layer of the cap (roadmap item 2). tokensConsumed is written by RoomSession's event
    // reader (through AddTokens below) and read on both the read loop and RoomSession's own consumer,
    // so every access goes through Interlocked: a plain long is not guaranteed to be read whole
    // across threads, and warnings-as-errors will not catch that.
    private long tokensConsumed;

    // The Stop mechanism (roadmap item 5). sequenceCounter is assigned to every enqueued WorkItem,
    // written on the read loop only but read by both the read loop and RoomSession's consumer, so it
    // goes through Interlocked for the same cross-thread reason tokensConsumed does. The per-Room
    // Stop marks that used to live beside it moved into RoomSession with D22 (finding P-6): this
    // runner still mints the one Persona-wide sequence a Stop is marked against, but no longer knows
    // which items that mark drops.
    private long sequenceCounter;

    // Health reporting (T4.3). D26: this streak is Persona-wide (RS §6.8: "failures in any Room
    // Session feed one consecutive-failure streak"), and at MaxConcurrentTurns >= 2 more than one
    // RoomSession's own consumer can call IRoomSessionOwner.ReportTurnFailure at once, so - unlike
    // when this was a single Room's own business - it now needs Interlocked, the same cross-thread
    // reason tokensConsumed above already has.
    private int consecutiveTurnFailures;

    private readonly TimeProvider timeProvider;

    private readonly RoomSessionStore? roomSessions;
    private readonly OwnPosts? ownPosts;

    private RoomSessionPool? pool;
    private JsonLineStream? stream;
    private IPersonaHost? host;
    private Task? readLoopTask;
    private string? agentId;
    private bool disposed;

    // Guards the Greeting (Spec §6.14) against being queued twice on this instance - at most once
    // per runner lifetime, even if a later re-Welcome ever reaches StartAsync's caller again. Touched
    // only from StartAsync, which runs once to completion before the read loop that could otherwise
    // race it starts, so this needs no Interlocked.
    private bool greetingQueued;

    // D16 P0-3: every Room id this runner currently knows of, mapped to that Room's current name -
    // RoomLabels.Distinguish's input. Filled from Welcome.Rooms in StartAsync, before the read loop
    // (and the Greeting, which is queued from StartAsync too) can touch it, and kept current by every
    // MessagePosted the read loop sees afterward. Only ever touched by StartAsync, before its own
    // loops start, and by the single-threaded read loop after that - never concurrently with itself -
    // so, unlike catchUpBuffers, this needs no lock of its own.
    private readonly Dictionary<string, string> knownRoomNames = new(StringComparer.Ordinal);

    public PersonaRunner(
        Persona persona,
        IOptions<TeamOptions> options,
        IAgentHostFactory factory,
        IPromptSource prompts,
        RoomFollows roomFollows,
        ILogger<PersonaRunner> logger,
        FileChangeTracker? fileChanges = null,
        TimeProvider? timeProvider = null,
        RoomSessionStore? roomSessions = null,
        OwnPosts? ownPosts = null)
    {
        ArgumentNullException.ThrowIfNull(persona);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(prompts);
        ArgumentNullException.ThrowIfNull(roomFollows);
        ArgumentNullException.ThrowIfNull(logger);

        this.persona = persona;
        this.options = options.Value;
        this.factory = factory;
        this.prompts = prompts;
        this.roomFollows = roomFollows;
        this.logger = logger;
        this.fileChanges = fileChanges;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.roomSessions = roomSessions;
        this.ownPosts = ownPosts;
        this.declaredWatches = PersonaFrontmatter.TryReadIdentity(persona.Text, out var identity, out _)
            ? identity.Watches ?? []
            : [];
    }

    /// <summary>
    /// Raised whenever this runner observes something worth reporting about its own Agent's session
    /// or Turns — everything it knows arrives in an Envelope or an <see cref="AgentEvent"/>, so this is
    /// the only channel it has. Deliberately not backed by a <see cref="PersonaHealth"/> reference:
    /// this class has no privileged in-process access and stays testable with no DI container.
    /// <see cref="PersonaSupervisor"/> subscribes when it constructs a runner and forwards every raised
    /// <see cref="PersonaStatus"/> into <see cref="PersonaHealth.Report"/>, which alone decides whether
    /// anything actually changed.
    /// </summary>
    public event Action<PersonaStatus>? StatusChanged;

    /// <inheritdoc />
    string IRoomSessionOwner.PersonaName => this.persona.Name;

    /// <inheritdoc />
    Task IRoomSessionOwner.WriteAsync(ProtocolMessage message, CancellationToken cancellationToken)
    {
        if (this.stream is not { } activeStream)
        {
            throw new InvalidOperationException($"Persona '{this.persona.Name}' has no pipe connection to write on.");
        }

        return activeStream.WriteAsync(message, cancellationToken);
    }

    /// <inheritdoc />
    bool IRoomSessionOwner.TokenBudgetSpent
    {
        get
        {
            var tokenBudget = this.options.Acp.TokenBudget;
            return tokenBudget > 0 && Interlocked.Read(ref this.tokensConsumed) >= tokenBudget;
        }
    }

    /// <inheritdoc />
    void IRoomSessionOwner.AddTokens(long delta) => Interlocked.Add(ref this.tokensConsumed, delta);

    /// <inheritdoc />
    void IRoomSessionOwner.ReportTokenBudgetSpent()
    {
        var tokenBudget = this.options.Acp.TokenBudget;
        this.logger.LogWarning(
            "Persona '{PersonaName}' has spent its token budget of {TokenBudget} and is taking no more turns until a human speaks to it.",
            this.persona.Name,
            tokenBudget);
        this.RaiseStatusChanged(
            PersonaState.Degraded,
            string.Create(
                CultureInfo.InvariantCulture,
                $"The per-Persona token Budget of {tokenBudget} is spent; no more Turns until a Human speaks."));
    }

    /// <inheritdoc />
    void IRoomSessionOwner.ReportTurnCompleted()
    {
        Interlocked.Exchange(ref this.consecutiveTurnFailures, 0);
        this.RaiseStatusChanged(PersonaState.Online, null);
    }

    /// <inheritdoc />
    void IRoomSessionOwner.ReportIncompleteStop(StopReason reason)
    {
        Interlocked.Exchange(ref this.consecutiveTurnFailures, 0);
        this.RaiseStatusChanged(
            PersonaState.Degraded,
            $"The last Turn ended without a reply — {DescribeIncompleteStop(reason)}.");
    }

    /// <inheritdoc />
    void IRoomSessionOwner.ReportTurnFailure(string roomName, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomName);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        // D26, RS §6.8: the reason always names the Room the failed Turn belonged to - a same-named
        // Room already carries D16's " #xxxxxx" disambiguating suffix in roomName, which is what
        // actually tells the Human which one failed.
        var count = Interlocked.Increment(ref this.consecutiveTurnFailures);
        var message = count >= 3
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{count} consecutive Turns have failed; this is unlikely to be transient — the last, in Room '{roomName}': {reason}")
            : $"A Turn in Room '{roomName}' failed — {reason}";
        this.RaiseStatusChanged(PersonaState.Degraded, message);
    }

    /// <inheritdoc />
    void IRoomSessionOwner.ReportOffline(string reason) => this.RaiseStatusChanged(PersonaState.Offline, reason);

    /// <inheritdoc />
    void IRoomSessionOwner.ReportLoopEnded(string loopName, Exception? exception) =>
        this.ReportLoopEndedUnlessShuttingDown(loopName, exception, CancellationToken.None);

    /// <inheritdoc />
    async Task<TranscriptTail?> IRoomSessionOwner.ReadTranscriptAsync(
        string roomId, string? afterMessageId, string beforeMessageId, int max, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        ArgumentException.ThrowIfNullOrWhiteSpace(beforeMessageId);

        // Guid.CreateVersion7, exactly as ProcessWorkItemAsync mints a Message id: a 32-character
        // lowercase-hex string NameRules.IsValidId accepts, unique enough that the read loop's
        // pendingTranscriptReads lookup below never collides with another Turn's in-flight read.
        var requestId = Guid.CreateVersion7().ToString("N");
        var pending = new TaskCompletionSource<TranscriptTail?>(TaskCreationOptions.RunContinuationsAsynchronously);
        this.pendingTranscriptReads[requestId] = pending;

        try
        {
            await ((IRoomSessionOwner)this).WriteAsync(new ReadTranscript(requestId, roomId, afterMessageId, beforeMessageId, max), cancellationToken);
            return await pending.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or ObjectDisposedException)
        {
            // RS §9 E-3: a refused or timed-out Transcript read is a Warning, never a Turn failure -
            // the Turn goes ahead without the block, exactly as if this Room had never seen one.
            this.logger.LogWarning(
                ex, "Persona '{PersonaName}' Transcript read for room {RoomId} was not answered in time.", this.persona.Name, roomId);
            return null;
        }
        finally
        {
            this.pendingTranscriptReads.TryRemove(requestId, out _);
        }
    }

    /// <inheritdoc />
    void IRoomSessionOwner.ReportModels(IReadOnlyList<AgentModelOption> models)
    {
        // IAgentSession.Models' own doc comment: an EMPTY list means unknown, never "no models are
        // available", so it must never produce a Degraded report. Only a genuinely non-empty catalog
        // that omits the stored Model is worth a warning - and only a warning: the session still
        // started on the Adapter's own default, and the Persona still answers (rules.md: "A Model the
        // agent does not advertise is a warning, never a failure"). Reported at most once per runner
        // (D23 correction 17 / D24 correction 23), from whichever Room Session opens first.
        if (this.persona.Model is not { Length: > 0 } storedModel || models.Count == 0
            || models.Any(model => string.Equals(model.Id, storedModel, StringComparison.Ordinal)))
        {
            return;
        }

        if (Interlocked.CompareExchange(ref this.modelWarningReported, 1, 0) != 0)
        {
            return;
        }

        this.RaiseStatusChanged(
            PersonaState.Degraded,
            $"The Model '{storedModel}' is not in the Adapter's catalog; running on its default.");
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var client = await this.ConnectAsync(cancellationToken);
        var jsonLineStream = new JsonLineStream(client);
        this.stream = jsonLineStream;

        var description = BuildDescription(this.persona.Text);
        await jsonLineStream.WriteAsync(new Hello(this.persona.Name, description), cancellationToken);

        var welcomeMessage = await jsonLineStream.ReadAsync(cancellationToken);
        if (welcomeMessage is not Welcome welcome)
        {
            // A ProtocolError carries the one useful fact in this failure - which of invalidName or
            // nameReserved the server refused the handshake for (AgentConnection lines 62-73) - so it
            // is spelled out rather than discarded behind its type name.
            var received = welcomeMessage switch
            {
                null => "end of stream.",
                ProtocolError error => $"a ProtocolError ({error.Code}: {error.Message}).",
                _ => $"{welcomeMessage.GetType().Name}.",
            };

            throw new InvalidOperationException(
                $"Expected a Welcome envelope for Persona '{this.persona.Name}' but received {received}");
        }

        this.agentId = welcome.AgentId;

        // Self-heal for roadmap item 8: ADR-0005 says a forgotten mcp__team__unfollow_room self-heals
        // on restart. With the follow set living in the RoomFollows singleton rather than a field on
        // this instance, that no longer comes free from object lifetime - a singleton outlives any one
        // runner - so it must be done explicitly, here, before the read loop below ever calls
        // IsFollowing for this Agent id. Doing it up front also covers an Agent that reconnects on the
        // same pipe without this PersonaRunner ever being recreated.
        this.roomFollows.ClearAgent(this.agentId);
        this.ownPosts?.ClearAgent(this.agentId);

        // D16 P0-3: seeded before the Greeting block below (which also labels its Room) and before
        // the read loop starts, so RoomLabels.Distinguish always has this runner's full set of known
        // Rooms to compare against, from the very first Turn.
        foreach (var room in welcome.Rooms)
        {
            this.knownRoomNames[room.Id] = room.Name;
        }

        // Only now, with Registration complete, does the Agent id exist, so only now can the host
        // be started with the tools bound to it by construction (docs/acp/agent-guide.md §3.6).
        // this.host is assigned BEFORE the session opens, so a failed first open is still disposed
        // with the runner - StopAsync's this.host-is-not-null disposal covers it.
        this.host = await this.factory.StartAsync(this.persona, welcome.AgentId, cancellationToken);

        // RS §6.6: pruned at start against Welcome.Rooms - nothing else tells this runner a Room was
        // deleted while it was offline. Per-Room mode only (finding P-15: shared mode never touches
        // the store); a null roomSessions (P-13's "absent means off" for a caller that predates D24)
        // is likewise a no-op.
        if (this.roomSessions is not null && this.host.Profile.SessionPerRoom)
        {
            this.roomSessions.Prune(this.persona.Name, welcome.Rooms.Select(room => room.Id).ToList());
        }
        else if (this.roomSessions is not null)
        {
            // E-12: shared mode never resumes (finding P-15), so any stored entries here are stale -
            // most likely left behind by a switch away from per-Room. Forgetting them on every shared
            // start is what makes a later switch back to per-Room start fresh rather than resuming
            // against a session, Model or Effort that no longer matches.
            this.roomSessions.ForgetAll(this.persona.Name);
        }

        // RS §6.2: the pool owns lazy per-Room opens, eviction and Stop routing; in shared mode (RS
        // principle 6: "SessionPerRoom: false is today plus Phase 0") it holds one RoomSession,
        // RoomId null, serving every Room this Agent is in. Persona and roomSessions (D24) let every
        // Room Session it creates resume by id when the Adapter, Model and Effort all still match.
        this.pool = new RoomSessionPool(
            this.host,
            this,
            this.persona,
            this.prompts,
            this.options.Acp,
            this.timeProvider,
            this.fileChanges,
            this.declaredWatches,
            this.logger,
            this.runCts.Token,
            this.roomSessions,
            this.ownPosts,
            this.agentId);

        // Per-Room mode opens the Room with exactly two Members, one of them the Human - the same
        // predicate as the Greeting's below, without IsEmpty: unlike the Greeting, this open must
        // happen whether or not that Room already has Messages. Shared mode ignores this id and opens
        // its one shared session instead. Opened here, exactly where this runner opened its one
        // session before, so the start-up failure modes are unchanged; it goes through the same
        // resume path as any later lazy open (D24 correction 18), and the Model-not-in-catalog
        // warning this open may trigger now arrives through IRoomSessionOwner.ReportModels (D24
        // correction 23) rather than a one-time check here.
        var humanRoom = welcome.Rooms.FirstOrDefault(room =>
            room.Members.Count == 2 && room.Members.Any(member => member.Kind == UserKind.Human));
        await this.pool.OpenAtStartAsync(humanRoom?.Id, cancellationToken);

        // The Greeting (Spec §6.14): queued here, only now that the session opened successfully, so a
        // failed start never leaves a queued Turn with no session. At most once per runner lifetime
        // (greetingQueued), only for the built-in Chief of Staff (_builtin: chief-of-staff in the
        // Persona's own text - the runner never reads the database, only what arrived in this
        // Welcome), and only for a Room with exactly two Members, one of them the Human, that has
        // taken no Messages yet.
        if (!this.greetingQueued &&
            PersonaFrontmatter.TryReadIdentity(this.persona.Text, out var identity, out _) &&
            string.Equals(identity.Builtin, BuiltinTeammate.ChiefOfStaffMarker, StringComparison.Ordinal))
        {
            var emptyHumanRoom = welcome.Rooms.FirstOrDefault(room =>
                room.Members.Count == 2 && room.Members.Any(member => member.Kind == UserKind.Human) && room.IsEmpty);
            if (emptyHumanRoom is not null)
            {
                this.greetingQueued = true;
                var greetingSequence = Interlocked.Increment(ref this.sequenceCounter);
                var greetingRoomName = RoomLabels.Distinguish(emptyHumanRoom.Id, emptyHumanRoom.Name, this.knownRoomNames);
                this.pool.Enqueue(new QueuedWork(
                    greetingSequence,
                    new WorkItem(emptyHumanRoom.Id, greetingRoomName, string.Empty, string.Empty, [], WorkItemKind.Greeting)));
            }
        }

        this.readLoopTask = this.RunReadLoopAsync(this.runCts.Token);
    }

    /// <summary>
    /// Delegates to the pool's idle sweep (RS §6.2). The production caller is the pool's own 30-second
    /// timer; this is the test seam that lets a test drive the sweep directly against a settable
    /// clock rather than waiting out real minutes.
    /// </summary>
    internal Task SweepIdleSessionsAsync() => this.pool?.SweepIdleAsync() ?? Task.CompletedTask;

    public async Task StopAsync()
    {
        if (this.runCts.IsCancellationRequested)
        {
            return;
        }

        await this.runCts.CancelAsync();

        await SafeAwaitAsync(this.readLoopTask);

        // RoomSessionPool.DisposeAsync disposes every Room Session it holds, then the host (RS §6.2
        // "Disposal"). this.host is assigned BEFORE the pool is constructed, so a host that started
        // but never got as far as the pool (unreachable in practice, but not provably impossible) is
        // still disposed here rather than leaked.
        if (this.pool is not null)
        {
            await this.pool.DisposeAsync();
        }
        else if (this.host is not null)
        {
            await this.host.DisposeAsync();
        }

        if (this.stream is not null)
        {
            await this.stream.DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;
        await this.StopAsync();
        this.runCts.Dispose();
    }

    private async Task RunReadLoopAsync(CancellationToken ct)
    {
        Exception? terminatingException = null;
        try
        {
            while (true)
            {
                var message = await this.stream!.ReadAsync(ct);
                if (message is null)
                {
                    return;
                }

                if (message is MessagePosted posted)
                {
                    // D16 P0-3: kept current before anything below reads it, including the
                    // catch-up-only branch, so a rename is picked up from the very next delivery in
                    // that Room regardless of which ReplyDecision it gets (RS §9 E-7).
                    this.knownRoomNames[posted.RoomId] = posted.RoomName;

                    // A Human Message ends the unattended run, so the token Budget starts over with
                    // it. The per-Room Budget resets server-side on the very same Message; this one
                    // is per Persona, because one session spans every Room the Agent is in.
                    if (SenderIsHuman(posted))
                    {
                        Interlocked.Exchange(ref this.tokensConsumed, 0);

                        // Clears whatever Degraded state the spent token Budget reported - the only
                        // thing this specific signal is documented to clear (T4.3).
                        this.RaiseStatusChanged(PersonaState.Online, null);
                    }

                    var following = this.agentId is not null && this.roomFollows.IsFollowing(this.agentId, posted.RoomId);
                    switch (ReplyGate.Decide(
                        posted.Mentioned, posted.Members.Count, posted.AgentMessagesSinceHuman, posted.Budget, following))
                    {
                        case ReplyDecision.Reply:
                            var missed = this.TakeCatchUp(posted.RoomId);

                            // D27, RS §6.7, finding P-22: drained here, in the read loop, beside
                            // TakeCatchUp - not by the pool at Turn start - so a post made into this
                            // Room after this item was already queued is not missed (RS principle 4).
                            IReadOnlyList<string> ownPostLines = this.agentId is { } takeAgentId && this.ownPosts is { } ownPostsTake
                                ? ownPostsTake.Take(takeAgentId, posted.RoomId)
                                : [];
                            var labelledRoomName = RoomLabels.Distinguish(posted.RoomId, posted.RoomName, this.knownRoomNames);
                            var item = new WorkItem(
                                posted.RoomId, labelledRoomName, posted.Message.SenderName, posted.Message.Text, missed,
                                TriggerMessageId: posted.Message.Id,
                                OwnPostLines: ownPostLines.Count > 0 ? ownPostLines : null);
                            var sequence = Interlocked.Increment(ref this.sequenceCounter);

                            // Never call the agent from the read loop: hand the item to the pool,
                            // which routes it to the shared session or that Room's own one.
                            this.pool?.Enqueue(new QueuedWork(sequence, item));
                            break;

                        case ReplyDecision.CatchUp:
                            // Nothing is submitted to the agent until it is Mentioned (the repo owner's
                            // absolute rule), but the Message is not thrown away: it rides along, as
                            // context only, the next time this Agent is Mentioned in that Room.
                            if (this.logger.IsEnabled(LogLevel.Information))
                            {
                                this.logger.LogInformation(
                                    "Persona '{PersonaName}' read a message in room {RoomId} as context only: it was not mentioned and the room has {MemberCount} members.",
                                    this.persona.Name,
                                    posted.RoomId,
                                    posted.Members.Count);
                            }

                            this.AppendCatchUp(posted.RoomId, posted.Message.SenderName, posted.Message.Text);
                            break;

                        case ReplyDecision.BudgetExhausted:
                            // Deliberately NOT buffered as Catch-up. A Catch-up Message was missed; this
                            // one is being held, and if the Human extends the Room's Budget this exact
                            // Message is delivered again - at which point a buffered copy would reach the
                            // model twice in one prompt.
                            this.logger.LogWarning(
                                "Persona '{PersonaName}' declined a turn in room {RoomId}: the room has spent its budget of {Budget} agent messages.",
                                this.persona.Name,
                                posted.RoomId,
                                posted.Budget);
                            break;
                    }
                }
                else if (message is TranscriptTail tail)
                {
                    // Completes the matching ReadTranscriptAsync wait, if one is still pending; an
                    // answer to a read this runner already gave up on (its own 10-second wait
                    // elapsed) finds nothing to complete and is simply dropped.
                    if (this.pendingTranscriptReads.TryRemove(tail.RequestId, out var pendingTranscript))
                    {
                        pendingTranscript.TrySetResult(tail);
                    }
                }
                else if (message is ProtocolError error)
                {
                    // D24 correction 21: a refused ReadTranscript answers with a ProtocolError whose
                    // RelatedMessageId is the pending RequestId, and that match is checked BEFORE the
                    // NotMember/UnknownRoom/BadMessage arm below - a Transcript refusal is a Warning
                    // only (RS §9 E-3), never the Degraded "a post was refused" report that arm makes.
                    if (error.RelatedMessageId is not null
                        && this.pendingTranscriptReads.TryRemove(error.RelatedMessageId, out var pendingTranscript))
                    {
                        pendingTranscript.TrySetResult(null);
                        this.logger.LogWarning(
                            "Persona '{PersonaName}' had a Transcript read refused: {Code} - {Reason}",
                            this.persona.Name,
                            error.Code,
                            error.Message);
                    }
                    else
                    {
                        // Dropped silently until now, which made a refused post - a spent Budget, a Room
                        // this Agent is not a Member of - invisible outside the server's own log. The
                        // Room can now be named: ProcessWorkItemAsync mints a messageId for every post,
                        // so error.RelatedMessageId correlates back to the Turn that sent it. Wiring
                        // that correlation into this log line is a later task.
                        this.logger.LogWarning(
                            "Persona '{PersonaName}' had a message refused: {Code} - {Reason}",
                            this.persona.Name,
                            error.Code,
                            error.Message);

                        // notMember/unknownRoom/badMessage are a real misconfiguration the Human should
                        // see. budgetExhausted is deliberately excluded: ADR-0006 already owns that
                        // surface (the Room view asks the Human, with Continue), and a Degraded badge
                        // there would mark an Agent that is working exactly as designed.
                        if (error.Code is ErrorCodes.NotMember or ErrorCodes.UnknownRoom or ErrorCodes.BadMessage)
                        {
                            this.RaiseStatusChanged(PersonaState.Degraded, $"A post was refused — {error.Message}");
                        }
                    }
                }
                else if (message is StopTurn stop)
                {
                    // The mark is the runner's own sequence counter as of right now (D22 correction
                    // 2): the counter itself stays on this runner - it also numbers the Greeting - but
                    // what it drops is entirely RoomSession's own business now (finding P-6).
                    var mark = Interlocked.Read(ref this.sequenceCounter);
                    if (this.pool is not null)
                    {
                        await this.pool.StopAsync(stop.RoomId, mark, ct);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            terminatingException = ex;
            this.logger.LogWarning(ex, "Persona '{PersonaName}' read loop stopped unexpectedly.", this.persona.Name);
        }
        finally
        {
            this.ReportLoopEndedUnlessShuttingDown("read loop", terminatingException, ct);
        }
    }

    private async Task<NamedPipeClientStream> ConnectAsync(CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MaxConnectAttempts; attempt++)
        {
            var client = new NamedPipeClientStream(".", this.options.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            try
            {
                await client.ConnectAsync(100, ct);
                return client;
            }
            catch (TimeoutException)
            {
                client.Dispose();
            }
            catch (IOException)
            {
                client.Dispose();
            }

            if (attempt < MaxConnectAttempts)
            {
                await Task.Delay(ConnectRetryDelay, ct);
            }
        }

        throw new InvalidOperationException(
            $"Persona '{this.persona.Name}' failed to connect to pipe '{this.options.PipeName}' after {MaxConnectAttempts} attempts.");
    }

    /// <summary>
    /// Raises <see cref="StatusChanged"/>. This runner never decides whether anything actually
    /// changed - <see cref="PersonaHealth.Report"/>, reached through <see cref="PersonaSupervisor"/>'s
    /// subscription, is the sole judge of that, so every call here is unconditional.
    /// </summary>
    /// <param name="state">What is now known about this Persona's Agent.</param>
    /// <param name="reason">Why, when known; otherwise <see langword="null"/>.</param>
    private void RaiseStatusChanged(PersonaState state, string? reason)
    {
        if (this.StatusChanged is not { } handlers)
        {
            return;
        }

        var status = new PersonaStatus(state, reason, DateTimeOffset.UtcNow);
        foreach (var handler in handlers.GetInvocationList())
        {
            try
            {
                ((Action<PersonaStatus>)handler).Invoke(status);
            }
            catch (Exception ex)
            {
                // Reporting health must never be able to kill the Agent it is reporting on. Every
                // one of this runner's loops raises this, some of them from a finally, so an
                // unguarded subscriber could both end the loop and mask the exception that ended it
                // - turning a diagnostic into the outage it exists to describe. Same reasoning, and
                // the same shape, as RoomEvents.Publish.
                this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.StatusChanged));
            }
        }
    }

    /// <summary>
    /// Reports <see cref="PersonaState.Offline"/> when one of this runner's long-lived loops has
    /// ended for any reason other than the run itself being cancelled. A dead loop otherwise leaves
    /// the pipe open with nothing left reading or writing it, and <c>AgentGateway.IsOnline</c> alone
    /// cannot tell a deaf Agent from a healthy one - see <see cref="PersonaStatusResolver"/>.
    /// </summary>
    /// <param name="loopName">Which loop ended, named in the reported reason.</param>
    /// <param name="exception">The exception that ended the loop, if any reached its catch clause.</param>
    /// <param name="ct">The run token; cancelled means this is ordinary shutdown, not a failure.</param>
    private void ReportLoopEndedUnlessShuttingDown(string loopName, Exception? exception, CancellationToken ct)
    {
        if (ct.IsCancellationRequested || this.runCts.IsCancellationRequested)
        {
            // Normal shutdown: nothing to report.
            return;
        }

        var reason = exception switch
        {
            null => $"Its {loopName} ended unexpectedly.",
            IOException => $"The pipe '{this.options.PipeName}' broke: {exception.Message}",
            _ => $"Its {loopName} ended unexpectedly: {exception.Message}",
        };

        this.RaiseStatusChanged(PersonaState.Offline, reason);
    }

    /// <summary>Names, in words fit for the Human, why a Turn ended without producing a reply.</summary>
    /// <param name="reason">One of <see cref="StopReason.MaxTokens"/>, <see cref="StopReason.MaxTurnRequests"/> or <see cref="StopReason.Refusal"/>.</param>
    /// <returns>A short clause completing "The last Turn ended without a reply — ...".</returns>
    private static string DescribeIncompleteStop(StopReason reason) => reason switch
    {
        StopReason.MaxTokens => "it ran out of tokens",
        StopReason.MaxTurnRequests => "it made too many requests",
        StopReason.Refusal => "the Model refused to answer",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Not an incomplete-stop reason."),
    };

    /// <summary>
    /// Whether a delivery was authored by the Human. A sender who has left the Room since posting
    /// resolves to no Member at all, and that counts as not-Human on purpose: an unattributable
    /// Message must fail toward the cap rather than reset it.
    /// </summary>
    /// <param name="posted">The delivery to attribute.</param>
    /// <returns><see langword="true"/> only if the sender is still a Member and is the Human.</returns>
    private static bool SenderIsHuman(MessagePosted posted) =>
        posted.Members.FirstOrDefault(m => m.Id == posted.Message.SenderId)?.Kind == UserKind.Human;

    private void AppendCatchUp(string roomId, string senderName, string text)
    {
        lock (this.catchUpLock)
        {
            if (!this.catchUpBuffers.TryGetValue(roomId, out var buffer))
            {
                buffer = [];
                this.catchUpBuffers[roomId] = buffer;
            }

            buffer.Add(new CaughtUpMessage(senderName, text));

            var max = this.options.Acp.CatchUpMessages;
            while (buffer.Count > max)
            {
                buffer.RemoveAt(0);
            }
        }
    }

    private CaughtUpMessage[] TakeCatchUp(string roomId)
    {
        lock (this.catchUpLock)
        {
            if (!this.catchUpBuffers.TryGetValue(roomId, out var buffer) || buffer.Count == 0)
            {
                return [];
            }

            var taken = buffer.ToArray();
            buffer.Clear();
            return taken;
        }
    }

    private static string BuildDescription(string personaText)
    {
        // The body, not the raw text: personaText may open with a YAML frontmatter block, whose
        // first line is always "---", not something fit to use as a description.
        var (_, body) = PersonaFrontmatter.Parse(personaText);

        foreach (var rawLine in body.Split('\n'))
        {
            var line = rawLine.Trim().TrimStart('#').Trim();
            if (line.Length == 0)
            {
                continue;
            }

            return line.Length > MaxDescriptionLength ? line[..MaxDescriptionLength] : line;
        }

        return string.Empty;
    }

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
}

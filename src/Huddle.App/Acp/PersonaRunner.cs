using Microsoft.Extensions.Options;
using System.Globalization;
using System.IO.Pipes;
using System.Text;
using System.Threading.Channels;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Runs one Persona: it brings the Persona online as an Agent and keeps it there. A
/// <see cref="PersonaRunner"/> is simultaneously a pipe client — it dials the application's own named
/// pipe as an ordinary protocol client and registers an Agent, exactly as
/// <see cref="Agency.Huddle.App.Demo.DemoAgentHost"/> does, deliberately with no privileged access to the Team
/// Directory — and an ACP client, owning one <see cref="IAgentHost"/> and one
/// <see cref="IAgentSession"/> with the Persona as its system prompt.
/// </summary>
/// <remarks>
/// Deliberately not a <see cref="Microsoft.Extensions.Hosting.BackgroundService"/>: hosted services are
/// fixed at build time, and instances of this class are created per Persona at runtime.
/// <see cref="PersonaSupervisor"/> owns the lifetime of each instance, one per Persona.
/// </remarks>
internal sealed class PersonaRunner : IAsyncDisposable
{
    private const int MaxConnectAttempts = 30;
    private static readonly TimeSpan ConnectRetryDelay = TimeSpan.FromMilliseconds(200);
    private const int MaxDescriptionLength = 200;

    // Bounds how long the idle-timeout watchdog waits for IAgentSession.CancelAsync to reach the far
    // side (TRAP 2) before giving up and cancelling the Turn's own token anyway. An adapter that will
    // not even take a cancel is the same adapter that sent nothing in the first place, so this is
    // short: there is nothing to gain from waiting longer on a session already proven unresponsive.
    private static readonly TimeSpan AdapterCancelGrace = TimeSpan.FromSeconds(5);

    private readonly Persona persona;
    private readonly TeamOptions options;
    private readonly IAgentHostFactory factory;
    private readonly IPromptSource prompts;
    private readonly RoomFollows roomFollows;
    private readonly ILogger<PersonaRunner> logger;
    private readonly CancellationTokenSource runCts = new();
    private readonly Channel<QueuedWork> workItems = Channel.CreateUnbounded<QueuedWork>();
    private readonly Lock turnLock = new();

    // Per-Room catch-up buffers for Messages the Agent received but was not Mentioned in (ADR-0004).
    // In memory only, per instance, keyed by Room id: a restart loses them, and an Agent that was
    // offline when a Message was delivered never had it to buffer in the first place. Guarded by
    // catchUpLock because, although only the read loop touches these buffers today, nothing about
    // this field's contract promises that will always stay true.
    private readonly Dictionary<string, List<CaughtUpMessage>> catchUpBuffers = [];
    private readonly Lock catchUpLock = new();

    // The third layer of the cap (roadmap item 2). tokensConsumed is written on the event-reader loop
    // and read on both the read loop and the consumer, so every access goes through Interlocked: a
    // plain long is not guaranteed to be read whole across threads, and warnings-as-errors will not
    // catch that. lastUsed is touched only by the event reader and needs no such care.
    private long tokensConsumed;
    private long lastUsed;

    // The Stop mechanism (roadmap item 5). sequenceCounter is assigned to every enqueued WorkItem;
    // stopHighWaterMark is the sequence value a Stop recorded most recently. sequenceCounter is
    // written on the read loop only but read by both the read loop and the consumer, and
    // stopHighWaterMark is written on the read loop and read on the consumer - the same
    // cross-thread reason tokensConsumed goes through Interlocked applies to both.
    private long sequenceCounter;
    private long stopHighWaterMark;

    // Health reporting (T4.3). Touched only from RunConsumerAsync's single-threaded call into
    // ProcessWorkItemAsync, so - unlike tokensConsumed above - this needs no Interlocked: there is
    // never a second thread that could observe it mid-update.
    private int consecutiveTurnFailures;

    private ActiveTurn? activeTurn;
    private JsonLineStream? stream;
    private IAgentHost? host;
    private IAgentSession? session;
    private Task? readLoopTask;
    private Task? consumerTask;
    private Task? eventReaderTask;
    private string? agentId;
    private bool disposed;

    public PersonaRunner(
        Persona persona,
        IOptions<TeamOptions> options,
        IAgentHostFactory factory,
        IPromptSource prompts,
        RoomFollows roomFollows,
        ILogger<PersonaRunner> logger)
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

        // Only now, with Registration complete, does the Agent id exist, so only now can the session
        // be created with the tools bound to it by construction (docs/acp/agent-guide.md §3.6).
        var created = await this.factory.CreateAsync(this.persona, welcome.AgentId, cancellationToken);
        this.host = created.Host;
        this.session = created.Session;

        // IAgentSession.Models' own doc comment: an EMPTY list means unknown, never "no models are
        // available", so it must never produce a Degraded report. Only a genuinely non-empty catalog
        // that omits the stored Model is worth a warning - and only a warning: the session still
        // started on the Adapter's own default, and the Persona still answers (rules.md: "A Model the
        // agent does not advertise is a warning, never a failure").
        if (this.persona.Model is { Length: > 0 } storedModel &&
            this.session.Models.Count > 0 &&
            !this.session.Models.Any(model => string.Equals(model.Id, storedModel, StringComparison.Ordinal)))
        {
            this.RaiseStatusChanged(
                PersonaState.Degraded,
                $"The Model '{storedModel}' is not in the Adapter's catalog; running on its default.");
        }

        this.eventReaderTask = this.RunEventReaderAsync(this.runCts.Token);
        this.consumerTask = this.RunConsumerAsync(this.runCts.Token);
        this.readLoopTask = this.RunReadLoopAsync(this.runCts.Token);
    }

    public async Task StopAsync()
    {
        if (this.runCts.IsCancellationRequested)
        {
            return;
        }

        await this.runCts.CancelAsync();
        this.workItems.Writer.TryComplete();

        await SafeAwaitAsync(this.readLoopTask);
        await SafeAwaitAsync(this.consumerTask);
        await SafeAwaitAsync(this.eventReaderTask);

        if (this.session is not null)
        {
            await this.session.DisposeAsync();
        }

        if (this.host is not null)
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
                            var item = new WorkItem(posted.RoomId, posted.RoomName, posted.Message.SenderName, posted.Message.Text, missed);
                            var sequence = Interlocked.Increment(ref this.sequenceCounter);

                            // Never call the agent from the read loop: hand the item to the single consumer.
                            this.workItems.Writer.TryWrite(new QueuedWork(sequence, item));
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
                else if (message is ProtocolError error)
                {
                    // Dropped silently until now, which made a refused post - a spent Budget, a Room
                    // this Agent is not a Member of - invisible outside the server's own log. The Room
                    // can now be named: ProcessWorkItemAsync mints a messageId for every post, so
                    // error.RelatedMessageId correlates back to the Turn that sent it. Wiring that
                    // correlation into this log line is a later task.
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
                else if (message is StopTurn)
                {
                    // The mark is taken before anything else: anything already queued (sequence at
                    // or below it) must drain without spending anything, the same stance the
                    // token-budget guard in ProcessWorkItemAsync takes for a spent Budget.
                    Interlocked.Exchange(ref this.stopHighWaterMark, Interlocked.Read(ref this.sequenceCounter));

                    ActiveTurn? turn;
                    lock (this.turnLock)
                    {
                        turn = this.activeTurn;
                    }

                    // Set BEFORE the cancellation below, so the consumer can tell this Stop apart
                    // from the idle-timeout watchdog firing: both arrive there as the same
                    // OperationCanceledException, and only one of them is a failure (TRAP 1).
                    turn?.MarkStopRequested();

                    // Unblocks ProcessWorkItemAsync's await on this Turn immediately, without
                    // waiting on the agent process to acknowledge the cancel notification below.
                    turn?.Cancellation.Cancel();

                    // Safe from any thread: locks internally and no-ops when no prompt is in
                    // flight (src/Huddle.Acp/DotAcp/DotAcpAgentSession.cs:144-171).
                    await this.session!.CancelAsync(ct);
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

    private async Task RunConsumerAsync(CancellationToken ct)
    {
        Exception? terminatingException = null;
        try
        {
            await foreach (var queued in this.workItems.Reader.ReadAllAsync(ct))
            {
                if (queued.Sequence <= Interlocked.Read(ref this.stopHighWaterMark))
                {
                    // Queued at or before the last Stop: drain it without spending anything,
                    // rather than working through a backlog the Human already asked to clear.
                    continue;
                }

                await this.ProcessWorkItemAsync(queued.Item, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            // An exception escaping ProcessWorkItemAsync's own finally - or the channel read itself
            // faulting - must not leave this loop silently dead: nothing else drains workItems, so a
            // deaf Agent would otherwise sit behind a pipe AgentGateway.IsOnline still reports as up.
            terminatingException = ex;
            this.logger.LogWarning(ex, "Persona '{PersonaName}' consumer loop stopped unexpectedly.", this.persona.Name);
        }
        finally
        {
            this.ReportLoopEndedUnlessShuttingDown("consumer loop", terminatingException, ct);
        }
    }

    private async Task ProcessWorkItemAsync(WorkItem item, CancellationToken ct)
    {
        // Checked here rather than in the read loop so that items already queued behind the cap drain
        // without spending anything - the same surprise roadmap item 4 records for cancelling a Turn,
        // where the queue behind it keeps going.
        var tokenBudget = this.options.Acp.TokenBudget;
        if (tokenBudget > 0 && Interlocked.Read(ref this.tokensConsumed) >= tokenBudget)
        {
            this.logger.LogWarning(
                "Persona '{PersonaName}' has spent its token budget of {TokenBudget} and is taking no more turns until a human speaks to it.",
                this.persona.Name,
                tokenBudget);
            this.RaiseStatusChanged(
                PersonaState.Degraded,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The per-Persona token Budget of {tokenBudget} is spent; no more Turns until a Human speaks."));
            return;
        }

        // Exactly one session serves every Room the Agent is in (there is one session per Persona),
        // so a failed turn must log and continue rather than end the loop: a dying agent process must
        // not silently deafen the Agent for every other Room.
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
        lock (this.turnLock)
        {
            this.activeTurn = turn;
        }

        // The idle bound this Turn is watched against, and the watchdog that enforces it. Armed here,
        // right after the Turn is published, so it covers the whole Turn including the initial
        // PromptAsync call below - a hang before the first event is exactly what motivates this.
        TimeSpan idleBound = IdleTimeoutFrom(this.options.Acp.TurnIdleTimeoutSeconds);
        CancellationTokenSource watchdogCancellation = new();
        Task? watchdog = idleBound > TimeSpan.Zero
            ? this.WatchForAdapterSilenceAsync(turn, item, idleBound, watchdogCancellation.Token, ct)
            : null;

        try
        {
            var prompt = BuildPrompt(item, this.prompts);
            await this.session!.PromptAsync(prompt, turnCancellation.Token);
            var outcome = await completion.Task.WaitAsync(turnCancellation.Token);

            // A refusal or a cancelled Turn is not a successful reply, even if some text arrived
            // before the stop: neither is fit to post into the Room.
            var isPostable = outcome.Reason is not (StopReason.Refusal or StopReason.Cancelled)
                && !string.IsNullOrWhiteSpace(outcome.Text);
            if (isPostable)
            {
                await this.stream!.WriteAsync(new PostMessage(item.RoomId, messageId, outcome.Text), ct);
            }

            // Health reporting (T4.3): every StopReason but Cancelled is a Turn that completed, so
            // it resets the escalating-failure counter below - Cancelled is the Human stopping it,
            // reported as nothing, and the one outcome that must leave the counter untouched.
            switch (outcome.Reason)
            {
                case StopReason.EndTurn:
                    this.consecutiveTurnFailures = 0;
                    this.RaiseStatusChanged(PersonaState.Online, null);
                    break;
                case StopReason.MaxTokens:
                case StopReason.MaxTurnRequests:
                case StopReason.Refusal:
                    this.consecutiveTurnFailures = 0;
                    this.RaiseStatusChanged(
                        PersonaState.Degraded,
                        $"The last Turn ended without a reply — {DescribeIncompleteStop(outcome.Reason)}.");
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
            // wins any tie - only StopTurn sets StopRequested, and this filter demands it be absent -
            // because a Human's own Stop must never be reported as a failure.
            this.ReportTurnFailure(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the Adapter sent nothing for {idleBound.TotalSeconds} seconds, so the Turn was abandoned and cancelled"));
            this.logger.LogWarning(
                "Persona '{PersonaName}' turn in room {RoomId} was abandoned after {IdleBoundSeconds} seconds of silence.",
                this.persona.Name,
                item.RoomId,
                idleBound.TotalSeconds);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // The Human stopped this Turn; the run itself is not shutting down. A Turn ends in one
            // of three ways - completed, stopped, or failed - and a stopped one is a normal
            // outcome, never a failure: logged at Information, its partial text discarded rather
            // than saved, and no Message posted. The exception type alone cannot tell a Stop apart
            // from shutdown - both surface as OperationCanceledException - so the run token, not
            // the exception, is what is checked above.
            if (this.logger.IsEnabled(LogLevel.Information))
            {
                this.logger.LogInformation(
                    "Persona '{PersonaName}' turn in room {RoomId} was stopped.",
                    this.persona.Name,
                    item.RoomId);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (AgentDisconnectedException ex)
        {
            this.RaiseStatusChanged(PersonaState.Offline, "The Adapter process disconnected.");
            this.logger.LogWarning(ex, "Persona '{PersonaName}' failed to process a turn in room {RoomId}.", this.persona.Name, item.RoomId);
        }
        catch (Exception ex)
        {
            // Quota, a network failure and expiring credentials all arrive as the same AgentException
            // ("session/prompt failed: ..."), and its wording belongs to the Adapter and will change -
            // so this escalates on repetition alone, never on message content. Stays Degraded rather
            // than Offline: the session and the pipe may both be healthy while the model provider is
            // refusing, and Offline would be a claim this runner cannot support.
            this.ReportTurnFailure(ex.Message);
            this.logger.LogWarning(ex, "Persona '{PersonaName}' failed to process a turn in room {RoomId}.", this.persona.Name, item.RoomId);
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

            lock (this.turnLock)
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
            if (this.stream is not null && !ct.IsCancellationRequested)
            {
                try
                {
                    await this.stream.WriteAsync(new MessageDelta(item.RoomId, messageId, string.Empty, IsFinal: true), ct);
                }
                catch (Exception ex) when (ex is IOException or ObjectDisposedException)
                {
                    // The pipe died before the terminator could be written; the Turn already ended
                    // one way or another, so there is nothing left to signal and nothing to retry.
                    this.logger.LogWarning(ex, "Persona '{PersonaName}' failed to write the final delta for a turn in room {RoomId}.", this.persona.Name, item.RoomId);
                }
            }

            turnCancellation.Dispose();
        }
    }

    /// <summary>
    /// Records one failed Turn against <see cref="consecutiveTurnFailures"/> and reports
    /// <see cref="PersonaState.Degraded"/>, escalating the wording on the third consecutive failure.
    /// Shared by every failure path a Turn can take — an exception from the Adapter, and (below) the
    /// idle-timeout watchdog firing — so both build the same streak and the same escalating reason.
    /// </summary>
    /// <param name="reason">What went wrong, in words fit to follow "A Turn failed — ".</param>
    private void ReportTurnFailure(string reason)
    {
        this.consecutiveTurnFailures++;
        var message = this.consecutiveTurnFailures >= 3
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{this.consecutiveTurnFailures} consecutive Turns have failed; this is unlikely to be transient — {reason}")
            : $"A Turn failed — {reason}";
        this.RaiseStatusChanged(PersonaState.Degraded, message);
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
    /// <c>promptCts</c> is null (src/Huddle.Acp/DotAcp/DotAcpAgentSession.cs:144-171). Cancelling the
    /// Turn's token first makes the local await throw before <c>CancelAsync</c> ever runs, so the
    /// Adapter that is actually hung is never told to stop - it keeps working, unaware anything ended.
    /// This method therefore does the OPPOSITE of the read loop's Stop handling (line ~322) on
    /// purpose: it tells the far side FIRST, with <see cref="IAgentSession.CancelAsync"/>, and only
    /// cancels <paramref name="turn"/>'s own token once that call has returned or been abandoned after
    /// <see cref="AdapterCancelGrace"/>. The Stop path gets away with the opposite order only because
    /// it runs on the read loop while the consumer resumes elsewhere - a deliberate responsiveness
    /// trade for a Human-initiated Stop that this watchdog, reporting a failure, does not get to make.
    /// </remarks>
    /// <param name="turn">The Turn being watched.</param>
    /// <param name="item">The work item the Turn is processing, named in the warning this logs.</param>
    /// <param name="idleBound">How long a silence is tolerated before this fires.</param>
    /// <param name="watchdogToken">
    /// Cancelled from <see cref="ProcessWorkItemAsync"/>'s <c>finally</c> once the Turn ends on its
    /// own, so this loop stops without ever firing.
    /// </param>
    /// <param name="ct">The run token, passed through to <see cref="IAgentSession.CancelAsync"/>.</param>
    private async Task WatchForAdapterSilenceAsync(
        ActiveTurn turn, WorkItem item, TimeSpan idleBound, CancellationToken watchdogToken, CancellationToken ct)
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
                this.persona.Name,
                item.RoomId,
                idleBound.TotalSeconds);

            try
            {
                // TRAP 2: tell the far side first, before this Turn's own token is cancelled below.
                await this.session!.CancelAsync(ct).WaitAsync(AdapterCancelGrace, ct);
            }
            catch (Exception ex) when (ex is AgentException or IOException or ObjectDisposedException or TimeoutException)
            {
                // An adapter that will not even take a cancel is the same adapter that sent nothing
                // in the first place: expected here, not exceptional, and does not stop the local
                // abort below.
                this.logger.LogWarning(
                    ex,
                    "Persona '{PersonaName}' failed to notify the Adapter of an idle-timeout cancel in room {RoomId}.",
                    this.persona.Name,
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

    private async Task RunEventReaderAsync(CancellationToken ct)
    {
        Exception? terminatingException = null;
        try
        {
            await foreach (var agentEvent in this.session!.Events.ReadAllAsync(ct))
            {
                // Any sign of life restarts the idle-timeout bound, including event types this loop
                // otherwise ignores below: what is bounded is silence, not progress. Taking turnLock
                // is what makes this safe against ProcessWorkItemAsync's finally, which clears
                // activeTurn under the same lock.
                lock (this.turnLock)
                {
                    this.activeTurn?.MarkActivity();
                }

                if (agentEvent is MessageChunk chunk)
                {
                    await this.AppendAndPublishDeltaAsync(chunk.Text, ct);
                }
                else if (agentEvent is ToolCallStarted started)
                {
                    await this.WriteToolActivityAsync(started.ToolCallId, started.Title, MapToolCallStatus(started.Status), ct);
                }
                else if (agentEvent is ToolCallUpdated updated)
                {
                    await this.WriteToolActivityAsync(updated.ToolCallId, updated.Title, MapToolCallStatus(updated.Status), ct);
                }
                else if (agentEvent is TurnCompleted completed)
                {
                    ActiveTurn? completedTurn;
                    string text;
                    lock (this.turnLock)
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
                    // session actually cost.
                    var previous = this.lastUsed;
                    this.lastUsed = usage.Used;
                    if (usage.Used > previous)
                    {
                        Interlocked.Add(ref this.tokensConsumed, usage.Used - previous);
                    }
                }

                // ThoughtChunk, PlanUpdated, ModeChanged, UserMessageChunk, UnsupportedContent and
                // UnknownUpdate are ignored.
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            terminatingException = ex;
            this.logger.LogWarning(ex, "Persona '{PersonaName}' agent event stream ended unexpectedly.", this.persona.Name);
        }
        finally
        {
            // Whatever ended this loop - a fault, or ordinary shutdown mid-turn - the single consumer
            // must not be left awaiting a TurnCompleted that can now never arrive: this channel is
            // done, for good, in every case. Without this, a crashed adapter process left
            // ProcessWorkItemAsync blocked on completion.Task forever, deafening the Agent in every
            // Room with no signal of any kind.
            ActiveTurn? strandedTurn;
            lock (this.turnLock)
            {
                strandedTurn = this.activeTurn;
                this.activeTurn = null;
            }

            strandedTurn?.Completion.TrySetException(
                new InvalidOperationException(
                    $"Persona '{this.persona.Name}': the agent event stream ended before its in-flight turn completed."));

            this.ReportLoopEndedUnlessShuttingDown("event reader", terminatingException, ct);
        }
    }

    /// <summary>
    /// Appends one increment of Message text to the active Turn's running total, then publishes it
    /// to <see cref="stream"/> as a <see cref="MessageDelta"/> so the Room can show the reply as it
    /// arrives. No throttling here - the browser is where that cost belongs.
    /// </summary>
    /// <param name="text">The increment of text this event carries.</param>
    /// <param name="ct">Cancels the delta write.</param>
    private async Task AppendAndPublishDeltaAsync(string text, CancellationToken ct)
    {
        ActiveTurn? turn;
        lock (this.turnLock)
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
        if (turn.DeltaWriteFailed || this.stream is null)
        {
            return;
        }

        try
        {
            await this.stream.WriteAsync(new MessageDelta(turn.RoomId, turn.MessageId, text, IsFinal: false), ct);
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
                this.persona.Name,
                turn.RoomId);
        }
    }

    /// <summary>
    /// Publishes one tool call's current lifecycle state to <see cref="stream"/> as a
    /// <see cref="ToolActivity"/>, attributed to the active Turn's Room and Message id. A no-op
    /// when there is no active Turn, or the pipe is not connected.
    /// </summary>
    /// <param name="toolCallId">The id of the tool call this activity reports on.</param>
    /// <param name="title">A human-readable label for the call, if the agent supplied one.</param>
    /// <param name="status">The call's current lifecycle state, already mapped to the wire enum.</param>
    /// <param name="ct">Cancels the write.</param>
    private async Task WriteToolActivityAsync(string toolCallId, string? title, ToolActivityStatus status, CancellationToken ct)
    {
        ActiveTurn? turn;
        lock (this.turnLock)
        {
            turn = this.activeTurn;
        }

        if (turn is null || this.stream is null)
        {
            return;
        }

        try
        {
            await this.stream.WriteAsync(new ToolActivity(turn.RoomId, turn.MessageId, toolCallId, title, status), ct);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            this.logger.LogWarning(
                ex,
                "Persona '{PersonaName}' failed to write a tool activity update for a turn in room {RoomId}.",
                this.persona.Name,
                turn.RoomId);
        }
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
                // one of this runner's three loops raises this, two of them from a finally, so an
                // unguarded subscriber could both end the loop and mask the exception that ended it
                // - turning a diagnostic into the outage it exists to describe. Same reasoning, and
                // the same shape, as RoomEvents.Publish.
                this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.StatusChanged));
            }
        }
    }

    /// <summary>
    /// Reports <see cref="PersonaState.Offline"/> when one of this runner's three long-lived loops has
    /// ended for any reason other than the run itself being cancelled. A dead loop otherwise leaves
    /// the pipe open with nothing left reading or writing it, and <c>AgentGateway.IsOnline</c> alone
    /// cannot tell a deaf Agent from a healthy one - see <see cref="PersonaStatusResolver"/>.
    /// </summary>
    /// <param name="loopName">Which loop ended, named in the reported reason.</param>
    /// <param name="exception">The exception that ended the loop, if any reached its catch clause.</param>
    /// <param name="ct">The run token; cancelled means this is ordinary shutdown, not a failure.</param>
    private void ReportLoopEndedUnlessShuttingDown(string loopName, Exception? exception, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
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

    /// <summary>Builds the prompt text delivered to the model for one Turn.</summary>
    /// <param name="item">The Turn's Room, sender, text and any catch-up context.</param>
    /// <param name="prompts">Resolves each <c>turn.*</c> prompt's current text — a configured override, or the <see cref="PromptCatalog"/> default.</param>
    /// <returns>
    /// The full prompt: with no catch-up context, the <c>turn.message</c> line alone; with catch-up
    /// context, a <c>turn.catchUpHeader</c> line, one <c>turn.catchUpLine</c> per missed message, a
    /// blank line, then the <c>turn.message</c> line.
    /// </returns>
    internal static string BuildPrompt(WorkItem item, IPromptSource prompts)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(prompts);

        // The Room's id rides along with its name because it is the only way an Agent can learn one.
        // mcp__team__post_message and mcp__team__invite_agent both take a room id, and nothing else
        // in a turn carries it: without this they reach only Rooms the Agent created itself.
        var room = RoomLabel(item, prompts);

        if (item.MissedMessages.Count == 0)
        {
            return RenderMessage(prompts, room, item.SenderName, item.Text);
        }

        var builder = new StringBuilder();
        builder.Append(prompts.Render("turn.catchUpHeader", new Dictionary<string, string> { ["{{roomLabel}}"] = room }));
        builder.Append('\n');
        foreach (var missed in item.MissedMessages)
        {
            builder.Append(prompts.Render(
                "turn.catchUpLine",
                new Dictionary<string, string> { ["{{sender}}"] = missed.SenderName, ["{{text}}"] = missed.Text }));
            builder.Append('\n');
        }

        builder.Append('\n');
        builder.Append(RenderMessage(prompts, room, item.SenderName, item.Text));

        return builder.ToString();
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

    internal sealed record WorkItem(
        string RoomId,
        string RoomName,
        string SenderName,
        string Text,
        IReadOnlyList<CaughtUpMessage> MissedMessages);

    internal sealed record CaughtUpMessage(string SenderName, string Text);

    /// <summary>One queued Turn, with the sequence number a Stop compares against.</summary>
    private sealed record QueuedWork(long Sequence, WorkItem Item);

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
        // stopRequested by the read loop, timedOut by the watchdog, both read by the consumer's catch
        // clauses - so every access goes through Interlocked or Volatile, never a plain read or write.
        // Environment.TickCount64 (monotonic, unaffected by a wall-clock change) is used rather than an
        // injected TimeProvider: threading one through here would churn PersonaSupervisor's
        // construction and every test's `new PersonaRunner(...)`, and this file already reads
        // DateTimeOffset.UtcNow directly elsewhere (RaiseStatusChanged), so this is consistent with
        // the existing style rather than a new one.
        private long lastActivityTicks = Environment.TickCount64;
        private bool stopRequested;
        private bool timedOut;

        /// <summary>
        /// Whether a <see cref="MessageDelta"/> write has already failed for this Turn. Set once a
        /// write throws, so a dead pipe is logged at most once per Turn rather than once per chunk.
        /// Touched only from the single-threaded event-reader loop, so it needs no lock of its own.
        /// </summary>
        public bool DeltaWriteFailed { get; set; }

        /// <summary>
        /// Whether the read loop has already latched a Stop for this Turn. Set by
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
        /// Restarts the idle clock. Called by the event-reader loop on any <see cref="AgentEvent"/> it
        /// observes, including ones it otherwise ignores - what the idle-timeout bound measures is
        /// silence, not progress, so any activity at all counts.
        /// </summary>
        public void MarkActivity() => Interlocked.Exchange(ref this.lastActivityTicks, Environment.TickCount64);

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
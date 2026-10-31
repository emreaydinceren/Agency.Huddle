using Microsoft.Extensions.Options;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.References;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Skills;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Starts and owns one <see cref="PersonaRunner"/> per Persona. This is the only hosted service in the
/// ACP bridge because <see cref="PersonaRunner"/> instances are created per Persona at runtime, while
/// hosted services themselves are fixed at build time.
/// </summary>
internal sealed class PersonaSupervisor : BackgroundService, Agency.Huddle.App.Teams.IPersonaStartGate
{
    private readonly TeamOptions options;
    private readonly PersonaStore personaStore;
    private readonly IAgentHostFactory factory;
    private readonly AdapterProfileResolver resolver;
    private readonly PersonaHealth health;
    private readonly IPromptSource prompts;
    private readonly RoomFollows roomFollows;
    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger<PersonaSupervisor> logger;

    // Resolves a Persona's assigned Skill names a second time, purely to report the Degraded
    // warning for one that does not exist (Spec §6.4, §12 F-1) — DotAcpAgentHostFactory resolves
    // the same names again itself, for the tool grants and the Skill Index; see the comment beside
    // the AdapterProfileResolver.Resolve call in StartHostIfMissingAsync for why this duplication
    // is the accepted cost rather than a reason to widen IAgentHostFactory.StartAsync.
    private readonly SkillStore skills;

    // Optional (finding P-13): null means File Changes is off for every Persona this supervisor
    // starts, the same as an explicit Team:FileChanges:Enabled=false.
    private readonly FileChangeTracker? fileChanges;
    private readonly RoomSessionStore? roomSessions;
    private readonly OwnPosts? ownPosts;
    private readonly TurnActivity? turnActivity;
    private readonly ContextFill? contextFill;
    private readonly ISessionSettings? sessionSettings;
    private readonly LibraryDocumentCollector? libraryDocs;
    private readonly ReferenceExpander? references;

    // Every runner this supervisor builds writes the cost its Adapter reports into this table, which
    // the Agent card reads. Null where a supervisor is built without a container.
    private readonly PersonaSpend? spend;
    private readonly PersonaCommands? commands;

    // Shows the Human the forms every runner this supervisor starts is asked to fill in (elicitation
    // bridge). Null answers every form as cancelled, like a supervisor built without a container.
    private readonly IElicitationBridge? elicitationBridge;

    private readonly Lock gate = new();
    private readonly Dictionary<string, PersonaRunner> hosts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Action<PersonaStatus>> statusHandlers = new(StringComparer.Ordinal);
    private readonly HashSet<string> starting = new(StringComparer.Ordinal);
    private readonly HashSet<string> restarting = new(StringComparer.Ordinal);

    // Personas whose change arrived while they were starting or restarting, when it could not be acted
    // on: a start reads the Persona before it awaits the Adapter, so an edit landing during that await
    // is not in the host being started. Marked under `gate` in the same block that sees the Persona
    // busy, and settled by SettlePendingChange once the start or restart that made it busy ends, so an
    // edit is never lost to the window between the two.
    private readonly HashSet<string> pendingChange = new(StringComparer.Ordinal);

    // The Persona a running host was last started with — its text AND its Model — so a save that
    // changes neither (or a debounced watcher event that fires after a no-op edit) does not restart
    // an agent: a restart is expensive and destroys the session's conversation memory.
    private readonly Dictionary<string, Persona> startedByName = new(StringComparer.Ordinal);

    private CancellationToken runningToken;
    private bool subscribed;

    // Set under `gate` as the very first action of StopAsync, before the unsubscribe. A plain
    // event gives no synchronisation against an Invoke() already in flight: PersonasChanged reads
    // the delegate field once, so a debounce-timer callback that captured it a moment before the
    // `-=` still runs OnPersonasChanged afterwards. Without this flag, that in-flight call can see
    // `hosts` already cleared, conclude the Persona is not running, and start a second
    // PersonaRunner - spawning a second `node` adapter process - with nothing left to dispose it.
    // Checked (not just set) under `gate` in OnPersonasChanged, StartHostIfMissingAsync and
    // RestartHostAsync: an event that arrives during or after shutdown must never start new work.
    private bool stopping;

    public PersonaSupervisor(
        IOptions<TeamOptions> options,
        PersonaStore personaStore,
        IAgentHostFactory factory,
        AdapterProfileResolver resolver,
        PersonaHealth health,
        IPromptSource prompts,
        RoomFollows roomFollows,
        ILoggerFactory loggerFactory,
        ILogger<PersonaSupervisor> logger,
        SkillStore skills,
        FileChangeTracker? fileChanges = null,
        RoomSessionStore? roomSessions = null,
        OwnPosts? ownPosts = null,
        TurnActivity? turnActivity = null,
        LibraryDocumentCollector? libraryDocs = null,
        PersonaSpend? spend = null,
        PersonaCommands? commands = null,
        IElicitationBridge? elicitationBridge = null,
        ReferenceExpander? references = null,
        ContextFill? contextFill = null,
        ISessionSettings? sessionSettings = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(personaStore);
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(health);
        ArgumentNullException.ThrowIfNull(prompts);
        ArgumentNullException.ThrowIfNull(roomFollows);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(skills);

        this.options = options.Value;
        this.personaStore = personaStore;
        this.factory = factory;
        this.resolver = resolver;
        this.health = health;
        this.prompts = prompts;
        this.roomFollows = roomFollows;
        this.loggerFactory = loggerFactory;
        this.logger = logger;
        this.skills = skills;
        this.fileChanges = fileChanges;
        this.roomSessions = roomSessions;
        this.ownPosts = ownPosts;
        this.turnActivity = turnActivity;
        this.contextFill = contextFill;
        this.sessionSettings = sessionSettings;
        this.libraryDocs = libraryDocs;
        this.references = references;
        this.spend = spend;
        this.commands = commands;
        this.elicitationBridge = elicitationBridge;
    }

    /// <inheritdoc />
    public async Task<bool> WaitUntilRunningAsync(string name, TimeSpan timeout, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        using CancellationTokenSource limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(timeout);
        try
        {
            while (true)
            {
                lock (this.gate)
                {
                    if (this.hosts.ContainsKey(name))
                    {
                        return true;
                    }
                }

                // A host that already failed to start will not appear; do not wait out the timeout for it.
                if (this.health.Get(name) is { State: PersonaState.Offline })
                {
                    return false;
                }

                await Task.Delay(StartPollInterval, limit.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Only the timeout fired: the caller asked "is it running yet", and the answer is no.
            return false;
        }
    }

    private static readonly TimeSpan StartPollInterval = TimeSpan.FromMilliseconds(50);

    /// <summary>The number of Personas with a currently running host. Test seam only.</summary>
    internal int RunningHostCount
    {
        get
        {
            lock (this.gate)
            {
                return this.hosts.Count;
            }
        }
    }

    /// <summary>
    /// Restarts one Persona's host on demand - the Human-triggered counterpart to the restart
    /// <see cref="OnPersonasChanged"/> performs automatically when a Persona's file, Model, Effort or
    /// Work Mode changes. Wraps <see cref="RestartHostAsync"/> directly, so it shares the same <see cref="gate"/>-protected
    /// <see cref="restarting"/> set that already stops two concurrent restarts of the same Persona - a
    /// Restart button is exactly the new way to provoke that race. A Persona with no host currently
    /// running still starts one: <see cref="RestartHostAsync"/> removes whatever host is running (none,
    /// here) and then calls <see cref="StartHostIfMissingAsync"/> unconditionally, which is the whole
    /// point for a Persona that failed to start in the first place - a Restart on it must not be a
    /// no-op.
    /// </summary>
    /// <param name="personaName">The Persona to restart.</param>
    /// <param name="cancellationToken">Cancels the restart.</param>
    public Task RestartAsync(string personaName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(personaName);

        return this.RestartHostAsync(personaName, cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Starting an agent process spends real money on the user's Claude subscription
        // (acp-agent-guide.md §3.1), so off is the default and code must never turn it on itself.
        if (!this.options.Acp.Enabled)
        {
            return;
        }

        this.runningToken = stoppingToken;
        this.personaStore.PersonasChanged += this.OnPersonasChanged;
        this.subscribed = true;

        try
        {
            var startTasks = this.personaStore.ListNames()
                .Select(name => this.StartHostIfMissingAsync(name, stoppingToken))
                .ToArray();
            await Task.WhenAll(startTasks).ConfigureAwait(false);

            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        lock (this.gate)
        {
            this.stopping = true;
        }

        if (this.subscribed)
        {
            this.personaStore.PersonasChanged -= this.OnPersonasChanged;
            this.subscribed = false;
        }

        await base.StopAsync(cancellationToken).ConfigureAwait(false);

        List<(PersonaRunner Host, Action<PersonaStatus>? Handler)> hostsToDispose;
        lock (this.gate)
        {
            hostsToDispose = [.. this.hosts.Select(pair => (pair.Value, this.statusHandlers.GetValueOrDefault(pair.Key)))];
            this.hosts.Clear();
            this.startedByName.Clear();
            this.statusHandlers.Clear();
        }

        // Concurrent, because each Adapter host allows a stdin grace period before it kills its process:
        // one after another, N Personas cost N grace periods and a stop window can be outrun, leaving
        // adapter processes behind. Each runner is stopped inside its own try/catch, so one that throws
        // is logged and never skips the others.
        await Task.WhenAll(hostsToDispose.Select(entry => this.StopRunnerForShutdownAsync(entry.Host, entry.Handler))).ConfigureAwait(false);
    }

    /// <summary>
    /// Stops one runner during shutdown and never throws: the process is exiting either way, and a
    /// failure here must not stop <see cref="StopAsync"/> from reaching the runners after it.
    /// </summary>
    /// <param name="host">The runner to dispose.</param>
    /// <param name="handler">The status handler to detach first, if one was attached.</param>
    private async Task StopRunnerForShutdownAsync(PersonaRunner host, Action<PersonaStatus>? handler)
    {
        if (handler is not null)
        {
            host.StatusChanged -= handler;
        }

        try
        {
            await host.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            this.logger.LogWarning(exception, "A Persona runner failed to stop cleanly during shutdown; stopping the others regardless.");
        }
    }

    private void OnPersonasChanged()
    {
        // An invocation captured just before StopAsync's `-=` still reaches here after shutdown
        // has begun clearing `hosts` - see the comment on `stopping`. Refusing to act on it, rather
        // than relying on the unsubscribe alone, is what closes that window.
        lock (this.gate)
        {
            if (this.stopping)
            {
                return;
            }
        }

        var currentNames = new HashSet<string>(this.personaStore.ListNames(), StringComparer.Ordinal);

        List<string> removedNames;
        lock (this.gate)
        {
            removedNames = [.. this.hosts.Keys.Where(name => !currentNames.Contains(name))];
        }

        foreach (var name in removedNames)
        {
            _ = this.StopHostAsync(name);
        }

        foreach (var name in currentNames)
        {
            this.ReconcilePersona(name);
        }
    }

    /// <summary>Starts, restarts or defers the runner for one Persona that still exists, according to what is running now.</summary>
    /// <param name="name">The Persona's Name.</param>
    private void ReconcilePersona(string name)
    {
        bool isBusy;
        bool isRunning;
        Persona? started;
        lock (this.gate)
        {
            isBusy = this.starting.Contains(name) || this.restarting.Contains(name);
            isRunning = this.hosts.ContainsKey(name);
            this.startedByName.TryGetValue(name, out started);
            if (isBusy)
            {
                this.pendingChange.Add(name);
            }
        }

        if (isBusy)
        {
            // A start or restart for this Persona is in flight. Acting now would race it: a
            // Persona claimed via `starting` has no `startedByName` entry yet, so NeedsRestart's
            // `started is null` short-circuit would restart a host that never finished starting
            // (a second factory.StartAsync under the same agent id), and RestartHostAsync would
            // refuse anyway while `restarting` holds the name. Doing nothing at all is wrong too:
            // the start read the Persona BEFORE awaiting the Adapter, so this edit is not in the
            // host it is starting. It used to be dropped here - the flaky *_RestartsItsHost
            // tests were this, losing the edit whenever it landed during the start's await. The
            // mark above is settled by SettlePendingChange when the in-flight work ends.
            return;
        }

        if (!isRunning)
        {
            _ = this.StartHostIfMissingAsync(name, this.runningToken);
            return;
        }

        var persona = this.personaStore.Get(name);
        if (persona is not null && NeedsRestart(persona, started))
        {
            _ = this.RestartHostAsync(name, this.runningToken);
        }
    }

    // Record value equality rather than a hand-rolled field-by-field comparison: this dictionary is
    // keyed by Name, so Name is always equal by the time this runs, which makes "persona != started"
    // exactly "text, Model, Effort or Work Mode changed" - and, unlike a chain of string.Equals calls, it
    // cannot go stale the next time Persona grows a field (Work Mode, ADR-0033, needed no edit here). A
    // system prompt, a Model, an Effort AND a Work Mode are all fixed when a session opens, so any one of
    // them changing means stop-and-restart; there is no other way for an edit to take effect. The `started is null` clause is defensive only and should now be
    // unreachable: OnPersonasChanged calls this exclusively for a name present in `hosts`, and
    // StartHostIfMissingAsync assigns `hosts[name]` and `startedByName[name]` together under the same
    // lock, so `started` is non-null here by construction.
    private static bool NeedsRestart(Persona persona, Persona? started) =>
        started is null || persona != started;

    /// <summary>
    /// Classifies a start failure into interface copy for <see cref="PersonaHealth.Report"/> —
    /// read by the Human, so <c>docs/engineering/language.md</c> is binding for it. Order matters:
    /// <see cref="AgentAuthenticationRequiredException"/> and <see cref="AgentProcessStartException"/>
    /// are both sealed subtypes of <see cref="AgentException"/>, so they are matched before the base
    /// type, and <see cref="OperationCanceledException"/> never reaches here at all - the caller's own
    /// <c>when</c> clause excludes it, because that exception means shutdown, not a failure.
    /// </summary>
    /// <param name="ex">The exception <see cref="IAgentHostFactory.StartAsync"/> or the runner's own <c>OpenAsync</c> threw.</param>
    /// <returns>A Human-facing reason naming what went wrong, when the exception itself does not already say so plainly.</returns>
    private static string DescribeStartFailure(Exception ex) => ex switch
    {
        AgentAuthenticationRequiredException authEx => authEx.AuthMethods.Count > 0
            ? $"The Adapter needs authentication: {string.Join(", ", authEx.AuthMethods.Select(m => m.Name))}."
            : "The Adapter needs authentication.",
        AgentProcessStartException processEx => processEx.Message,
        InvalidOperationException invalidOperationEx => invalidOperationEx.Message,
        AgentException agentEx => agentEx.Message,
        _ => $"{ex.GetType().Name}: {ex.Message}",
    };

    /// <summary>
    /// Stops and disposes the runner for a Persona whose file is gone (Remove, or an external
    /// delete noticed by the watcher). Only the runner goes away; per PersonaStore.Remove's
    /// documented semantics, the Agent, its Rooms and its Transcripts are untouched.
    /// </summary>
    private async Task StopHostAsync(string name)
    {
        PersonaRunner? host;
        Action<PersonaStatus>? handler;
        lock (this.gate)
        {
            if (!this.hosts.Remove(name, out host))
            {
                return;
            }

            this.startedByName.Remove(name);
            this.statusHandlers.Remove(name, out handler);
        }

        if (handler is not null)
        {
            host.StatusChanged -= handler;
        }

        try
        {
            await host.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // One Persona failing to stop cleanly must not affect any other Persona.
            this.logger.LogWarning(ex, "Persona '{PersonaName}' host failed to stop cleanly after removal.", name);
        }

        // This is both the removal path AND the rename path: OnPersonasChanged sees a rename as the
        // old Name disappearing from ListNames(), so it reaches here too. Forgetting AFTER dispose -
        // never before - beats the resurrection race: the old runner's own final Turn-end write
        // (RoomSessionPool.Put) can otherwise land moments after this method decided the Persona is
        // gone and resurrect an entry under a Name nothing will ever look up again (D29 correction 16,
        // RS §6.13). A rename has already moved the file to the new Name by the time this runs
        // (PersonaRenameCascade.OnPersonaRenamed runs synchronously, before PersonasChanged), so this
        // is a harmless no-op there - it exists purely to close the race, not to do the rename itself.
        this.roomSessions?.ForgetAll(name);

        // The Persona is gone, and PersonaRunner.StopAsync above (via DisposeAsync) sees its own
        // run token already cancelled, so its loops report nothing on their way out (T4.4) - this is
        // the one place that removes the now-stale health entry rather than leaving it to rot.
        this.health.Remove(name);
    }

    /// <summary>
    /// Restarts the runner for a Persona whose text changed, or whose Model changed. Because a
    /// system prompt is fixed at <c>session/new</c> (acp-agent-guide.md §3.5), this is the only way an
    /// edit takes effect, and it unavoidably loses the session's conversation memory.
    /// </summary>
    private async Task RestartHostAsync(string name, CancellationToken cancellationToken)
    {
        lock (this.gate)
        {
            // `stopping` is checked here too: OnPersonasChanged is fire-and-forget, so a call
            // already in flight when StopAsync begins must not restart a host shutdown is about to
            // (or already did) tear down.
            if (this.stopping || !this.restarting.Add(name))
            {
                return;
            }
        }

        try
        {
            await this.DisposeOldHostAsync(name).ConfigureAwait(false);

            // RS §6.13: a Restart - the button, or the automatic one a Persona edit / Model / Effort /
            // Work Mode / Adapter change triggers - forgets every stored Room Session for this Persona,
            // so its next Turn in every Room starts fresh with Catch-up rather than resuming a session
            // tied to a system prompt, Model, Effort or Work Mode that may no longer match (U7). Deliberately AFTER
            // the whole block above, not inside it (D29 correction 16): this must still run when
            // oldHost was null (a Restart of a Persona that failed to start), and placing it after the
            // old host's dispose - rather than before - beats the same resurrection race StopHostAsync
            // closes for removal and rename.
            this.roomSessions?.ForgetAll(name);

            await this.StartHostIfMissingAsync(name, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            lock (this.gate)
            {
                this.restarting.Remove(name);
            }

            this.SettlePendingChange(name, cancellationToken);
        }
    }

    /// <summary>Detaches and disposes the Persona's current runner, if it has one, logging rather than throwing when it fails to stop.</summary>
    /// <param name="name">The Persona being restarted.</param>
    private async Task DisposeOldHostAsync(string name)
    {
        PersonaRunner? oldHost;
        Action<PersonaStatus>? oldHandler;
        lock (this.gate)
        {
            this.hosts.Remove(name, out oldHost);
            this.startedByName.Remove(name);
            this.statusHandlers.Remove(name, out oldHandler);
        }

        if (oldHost is not null)
        {
            if (oldHandler is not null)
            {
                oldHost.StatusChanged -= oldHandler;
            }

            try
            {
                await oldHost.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                this.logger.LogWarning(ex, "Persona '{PersonaName}' old host failed to stop cleanly during restart.", name);
            }
        }
    }

    /// <summary>
    /// Acts on a change to <paramref name="name"/> that <see cref="OnPersonasChanged"/> had to set aside
    /// because a start or restart was in flight, once nothing is in flight any more: starts the Persona
    /// if that start failed, and restarts it if what is running differs from the Persona now on disk.
    /// Called from the end of every start and restart, so whichever one finishes last settles it; a
    /// start inside a restart leaves it to the restart, which still holds the name.
    /// </summary>
    /// <param name="name">The Persona whose start or restart just ended.</param>
    /// <param name="cancellationToken">The supervisor's running token, handed on to any start or restart this begins.</param>
    private void SettlePendingChange(string name, CancellationToken cancellationToken)
    {
        bool isRunning;
        Persona? started;
        lock (this.gate)
        {
            if (this.stopping || cancellationToken.IsCancellationRequested || this.starting.Contains(name) || this.restarting.Contains(name) || !this.pendingChange.Remove(name))
            {
                return;
            }

            isRunning = this.hosts.ContainsKey(name);
            this.startedByName.TryGetValue(name, out started);
        }

        // Null when the Persona was removed meanwhile; OnPersonasChanged's own removal pass owns that.
        var persona = this.personaStore.Get(name);
        if (persona is null)
        {
            return;
        }

        if (!isRunning)
        {
            // The start failed, and the edit set aside during it may be the fix: try again with it.
            _ = this.StartHostIfMissingAsync(name, cancellationToken);
        }
        else if (NeedsRestart(persona, started))
        {
            _ = this.RestartHostAsync(name, cancellationToken);
        }
    }

    private async Task StartHostIfMissingAsync(string name, CancellationToken cancellationToken)
    {
        if (this.TryRefuseWhenAcpDisabled(name) || !this.TryClaimStart(name))
        {
            return;
        }

        try
        {
            var persona = this.personaStore.Get(name);
            if (persona is null)
            {
                return;
            }

            StartPlan plan = this.PlanStart(persona);
            var host = this.CreateRunner(persona, plan);

            // Forwards every health signal the runner itself observes (T4.3) - a session/Turn
            // fact, arriving over the wire - into the one table every UI surface reads.
            // Unsubscribed in StopHostAsync, RestartHostAsync's old-host teardown, and StopAsync.
            void OnStatusChanged(PersonaStatus status) => this.health.Report(name, status.State, status.Reason);
            host.StatusChanged += OnStatusChanged;

            if (!await this.TryStartRunnerAsync(name, host, plan.Warnings, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            this.ReportOnlineUnlessReportedDuringStart(name);

            lock (this.gate)
            {
                this.hosts[name] = host;
                this.startedByName[name] = persona;
                this.statusHandlers[name] = OnStatusChanged;
            }
        }
        finally
        {
            lock (this.gate)
            {
                this.starting.Remove(name);
            }

            this.SettlePendingChange(name, cancellationToken);
        }
    }

    /// <summary>What <see cref="PlanStart"/> decided for one start: the file-reading flag, the tracker and the Degraded warnings.</summary>
    /// <param name="ReadsFiles">Whether the resolved Adapter Profile can read files.</param>
    /// <param name="Warnings">The Adapter, Skill and watch warnings to report as one Degraded reason; empty when there are none.</param>
    /// <param name="Tracker">The File Changes tracker the runner gets, or <see langword="null"/> when File Changes is off for it.</param>
    private readonly record struct StartPlan(bool ReadsFiles, IReadOnlyList<string> Warnings, FileChangeTracker? Tracker);

    /// <summary>
    /// The cost guard lives here, not only in ExecuteAsync, because this is the one place a host
    /// is ever created - every route in (startup, a Persona file changing, the Restart button)
    /// arrives through it. ExecuteAsync's own early return covers startup and stops the
    /// PersonasChanged subscription ever being made; Restart is a direct call and reached this
    /// method with the guard off, bringing a Persona fully Online and leaving every later Message
    /// to it billable, with nothing on screen having said so.
    /// </summary>
    /// <remarks>
    /// Reported rather than returned silently: a button that does nothing at all is the failure
    /// this area keeps producing, and an Offline badge with a reason is the surface that already
    /// exists for "it is not running, and here is why".
    /// </remarks>
    /// <param name="name">The Persona being started.</param>
    /// <returns><see langword="true"/> when Agents are switched off and the start must not go ahead.</returns>
    private bool TryRefuseWhenAcpDisabled(string name)
    {
        if (!this.options.Acp.Enabled)
        {
            this.health.Report(name, PersonaState.Offline, "Agents are switched off in this configuration, so nothing was started.");
            return true;
        }

        return false;
    }

    /// <summary>Claims <paramref name="name"/> as starting, unless shutdown has begun, it already runs or a start is in flight.</summary>
    /// <param name="name">The Persona being started.</param>
    /// <returns><see langword="true"/> when this caller now owns the start.</returns>
    private bool TryClaimStart(string name)
    {
        lock (this.gate)
        {
            // `stopping` closes the exact window that produced the duplicate-start flake: an
            // OnPersonasChanged invocation that read `stopping == false` just before StopAsync set
            // it, then reached this check after `hosts` was already cleared, would otherwise see no
            // "nova" host and start a second one under the same agent id.
            return !(this.stopping || this.hosts.ContainsKey(name) || !this.starting.Add(name));
        }
    }

    /// <summary>Resolves the Adapter, Skills and File Changes wiring for one start and combines their warnings.</summary>
    /// <param name="persona">The Persona being started.</param>
    /// <returns>The plan the runner is created and reported from.</returns>
    private StartPlan PlanStart(Persona persona)
    {
        // Spec §8.2: DotAcpAgentHostFactory resolves the same Persona's Adapter again when it
        // starts the host, so this looks like duplicated work - it is not. Widening
        // IAgentHostFactory.StartAsync's return type to also carry this diagnostic would touch
        // FakeAgentHostFactory and every supervisor test call site for one string. The resolver
        // is pure and touches no state, so calling it twice costs nothing and keeps that
        // signature stable here - two calls, one truth. (RS §6.3 changed IAgentHostFactory's
        // signature once, deliberately, splitting CreateAsync into StartAsync/OpenAsync; the
        // "frozen" reasoning below still holds for the resolver call, just not for the factory
        // interface itself.) The Degraded report itself is issued after
        // "Starting" below rather than here, so the "whatever was said during the start wins"
        // rule a few lines down does not immediately overwrite it back to Starting/Online. The
        // resolved profile itself is kept (not discarded) because FC §6.11's ReadsFiles decides
        // both the tracker below and the warning wording.
        var (profile, adapterWarning) = this.resolver.Resolve(persona.Adapter);

        // Same argument as the Adapter warning immediately above, for Skills (Spec §6.4, §12
        // F-1): DotAcpAgentHostFactory resolves the same names again itself, for the tool
        // grants and the Skill Index, and a Persona with no frontmatter - or malformed
        // frontmatter - simply holds no Skills rather than failing the start.
        IReadOnlyList<string> skillNames = PersonaFrontmatter.TryReadIdentity(persona.Text, out var identity, out _)
            ? identity.Skills ?? []
            : [];
        SkillResolution skillResolution = this.skills.Resolve(skillNames);

        // FC §6.10, §6.11: File Changes is off for this Persona when the tracker itself is
        // absent (finding P-13), Team:FileChanges:Enabled is false, or the resolved Adapter
        // Profile cannot read files - any of the three, the runner gets no tracker.
        FileChangeTracker? tracker = this.options.FileChanges.Enabled && profile.ReadsFiles ? this.fileChanges : null;
        IReadOnlyList<string> declaredWatches = identity?.Watches ?? [];

        // The "ignored" warning fires whenever File Changes is on but this Adapter cannot read
        // files and the Persona declared a watch anyway - independent of whether a tracker was
        // ever injected into this supervisor, because the fact it reports is about the Adapter,
        // not about this process' wiring. CheckDeclared's own warnings need all three: a
        // tracker, FileChanges.Enabled and ReadsFiles - so the two warnings are mutually
        // exclusive by construction.
        IReadOnlyList<string> watchWarnings = this.options.FileChanges.Enabled switch
        {
            false => [],
            true when !profile.ReadsFiles && declaredWatches.Count > 0 =>
                [$"Watched folders are ignored: the Adapter '{profile.Id}' cannot read files."],
            true => tracker is not null ? tracker.CheckDeclared(declaredWatches) : [],
        };

        // The Adapter warning and every unresolved Skill name are independent sources of the
        // same Degraded state, so a Persona hit by both is reported once, not one overwriting
        // the other - the Adapter warning first, then each Skill warning, joined with a
        // single space.
        IReadOnlyList<string> warnings = adapterWarning is null
            ? [.. skillResolution.Warnings, .. watchWarnings]
            : [adapterWarning, .. skillResolution.Warnings, .. watchWarnings];

        return new StartPlan(profile.ReadsFiles, warnings, tracker);
    }

    /// <summary>Creates the runner for a Persona from its start plan.</summary>
    /// <param name="persona">The Persona being started.</param>
    /// <param name="plan">The plan <see cref="PlanStart"/> produced.</param>
    /// <returns>The not yet started runner.</returns>
    private PersonaRunner CreateRunner(Persona persona, StartPlan plan)
    {
        // Spec §6.14, corrections-B6 item 8: the Library.Enabled gate lives in exactly this one
        // place - the collector itself carries no IOptions gate of its own - so a disabled
        // Library reaches every runner as a null collector, the same "absent means off" shape
        // File Changes and every other optional collaborator here already use.
        return new PersonaRunner(
            persona, Options.Create(this.options), this.factory, this.prompts, this.roomFollows, this.loggerFactory.CreateLogger<PersonaRunner>(), plan.Tracker, roomSessions: this.roomSessions, ownPosts: this.ownPosts, turnActivity: this.turnActivity, libraryDocs: this.options.Library.Enabled ? this.libraryDocs : null, readsFiles: plan.ReadsFiles, spend: this.spend, commands: this.commands, elicitationBridge: this.elicitationBridge, references: this.references, contextFill: this.contextFill, sessionSettings: this.sessionSettings);
    }

    /// <summary>Reports Starting, then any Degraded warnings, then starts the runner; a start failure is reported Offline and the runner disposed.</summary>
    /// <param name="name">The Persona being started.</param>
    /// <param name="host">The runner to start.</param>
    /// <param name="warnings">The combined start warnings; empty when there are none.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns><see langword="true"/> when the start returned; <see langword="false"/> when it failed.</returns>
    private async Task<bool> TryStartRunnerAsync(string name, PersonaRunner host, IReadOnlyList<string> warnings, CancellationToken cancellationToken)
    {
        try
        {
            this.health.Report(name, PersonaState.Starting, null);

            if (warnings.Count > 0)
            {
                this.health.Report(name, PersonaState.Degraded, string.Join(' ', warnings));
            }

            await host.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // One Persona failing to start (bad config, adapter missing, agent process dies)
            // must not stop any other Persona from coming online. Classified most-derived
            // exception type first: AgentAuthenticationRequiredException and
            // AgentProcessStartException are both sealed AgentException subtypes, so a bare
            // AgentException arm ahead of them would swallow both.
            this.health.Report(name, PersonaState.Offline, DescribeStartFailure(ex));
            this.logger.LogWarning(ex, "Persona '{PersonaName}' failed to start.", name);
            await host.DisposeAsync().ConfigureAwait(false);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Online here means "the start returned and said nothing about itself", never "this
    /// Agent is healthy" - a fact this method does not have. PersonaRunner.StartAsync can
    /// report during the await above (a stored Model the Adapter does not advertise is the
    /// case today), and PersonaHealth.Report is last-write-wins with no notion of a state
    /// that outranks another - so reporting Online unconditionally destroyed that report
    /// microseconds after it was made, before any surface could read it. Reading the table
    /// back rather than tracking a flag keeps the rule in one expression: whatever was said
    /// during the start wins, because it knows something this line does not.
    /// </summary>
    /// <param name="name">The Persona whose start just returned.</param>
    private void ReportOnlineUnlessReportedDuringStart(string name)
    {
        var afterStart = this.health.Get(name);
        if (afterStart is null || afterStart.State == PersonaState.Starting)
        {
            this.health.Report(name, PersonaState.Online, null);
        }
    }
}

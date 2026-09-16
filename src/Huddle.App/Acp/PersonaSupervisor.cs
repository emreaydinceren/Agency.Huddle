using Microsoft.Extensions.Options;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Hooks;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Starts and owns one <see cref="PersonaRunner"/> per Persona. This is the only hosted service in the
/// ACP bridge because <see cref="PersonaRunner"/> instances are created per Persona at runtime, while
/// hosted services themselves are fixed at build time.
/// </summary>
internal sealed class PersonaSupervisor : BackgroundService
{
    private readonly TeamOptions options;
    private readonly PersonaStore personaStore;
    private readonly IAgentHostFactory factory;
    private readonly PersonaHealth health;
    private readonly IHookSource hooks;
    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger<PersonaSupervisor> logger;

    private readonly Lock gate = new();
    private readonly Dictionary<string, PersonaRunner> hosts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Action<PersonaStatus>> statusHandlers = new(StringComparer.Ordinal);
    private readonly HashSet<string> starting = new(StringComparer.Ordinal);
    private readonly HashSet<string> restarting = new(StringComparer.Ordinal);

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
        PersonaHealth health,
        IHookSource hooks,
        ILoggerFactory loggerFactory,
        ILogger<PersonaSupervisor> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(personaStore);
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(health);
        ArgumentNullException.ThrowIfNull(hooks);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(logger);

        this.options = options.Value;
        this.personaStore = personaStore;
        this.factory = factory;
        this.health = health;
        this.hooks = hooks;
        this.loggerFactory = loggerFactory;
        this.logger = logger;
    }

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
    /// <see cref="OnPersonasChanged"/> performs automatically when a Persona's file, Model or Effort
    /// changes. Wraps <see cref="RestartHostAsync"/> directly, so it shares the same <see cref="gate"/>-protected
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
        // (agent-guide.md §3.1), so off is the default and code must never turn it on itself.
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

        foreach (var (host, handler) in hostsToDispose)
        {
            if (handler is not null)
            {
                host.StatusChanged -= handler;
            }

            await host.DisposeAsync().ConfigureAwait(false);
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
            bool isStarting;
            bool isRunning;
            Persona? started;
            lock (this.gate)
            {
                isStarting = this.starting.Contains(name);
                isRunning = this.hosts.ContainsKey(name);
                this.startedByName.TryGetValue(name, out started);
            }

            if (isStarting)
            {
                // A Persona claimed via `starting` but not yet committed to `hosts` has no
                // `startedByName` entry, so `started` above is null. The old code folded "starting"
                // and "running" into one `alreadyKnown` state, fell through to the restart decision,
                // read `started = null`, took NeedsRestart's `started is null` short-circuit, and
                // restarted a host that had never finished starting - producing a second
                // factory.CreateAsync under the same agent id. The in-flight start reads current
                // file and DB state when it runs, so a restart here has nothing to achieve: do
                // nothing and let it finish. Not startup-specific - any second PersonasChanged
                // arriving during a slow first start (a real `node` adapter) hits this same branch.
                continue;
            }

            if (!isRunning)
            {
                _ = this.StartHostIfMissingAsync(name, this.runningToken);
                continue;
            }

            var persona = this.personaStore.Get(name);
            if (persona is not null && NeedsRestart(persona, started))
            {
                _ = this.RestartHostAsync(name, this.runningToken);
            }
        }
    }

    // Record value equality rather than a hand-rolled field-by-field comparison: this dictionary is
    // keyed by Name, so Name is always equal by the time this runs, which makes "persona != started"
    // exactly "text, Model or Effort changed" - and, unlike a chain of string.Equals calls, it cannot
    // go stale the next time Persona grows a field. A system prompt, a Model AND an Effort are all
    // fixed at session/new, so any one of them changing means stop-and-restart; there is no other way
    // for an edit to take effect. The `started is null` clause is defensive only and should now be
    // unreachable: OnPersonasChanged calls this exclusively for a name present in `hosts`, and
    // StartHostIfMissingAsync assigns `hosts[name]` and `startedByName[name]` together under the same
    // lock, so `started` is non-null here by construction.
    private static bool NeedsRestart(Persona persona, Persona? started) =>
        started is null || persona != started;

    /// <summary>
    /// Classifies a start failure into interface copy for <see cref="PersonaHealth.Report"/> —
    /// read by the Human, so <c>docs/agencyteam/language.md</c> is binding for it. Order matters:
    /// <see cref="AgentAuthenticationRequiredException"/> and <see cref="AgentProcessStartException"/>
    /// are both sealed subtypes of <see cref="AgentException"/>, so they are matched before the base
    /// type, and <see cref="OperationCanceledException"/> never reaches here at all - the caller's own
    /// <c>when</c> clause excludes it, because that exception means shutdown, not a failure.
    /// </summary>
    /// <param name="ex">The exception <see cref="IAgentHostFactory.CreateAsync"/> or <c>StartAsync</c> threw.</param>
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

        // The Persona is gone, and PersonaRunner.StopAsync above (via DisposeAsync) sees its own
        // run token already cancelled, so its loops report nothing on their way out (T4.4) - this is
        // the one place that removes the now-stale health entry rather than leaving it to rot.
        this.health.Remove(name);
    }

    /// <summary>
    /// Restarts the runner for a Persona whose text changed, or whose Model changed. Because a
    /// system prompt is fixed at <c>session/new</c> (agent-guide.md §3.5), this is the only way an
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

            await this.StartHostIfMissingAsync(name, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            lock (this.gate)
            {
                this.restarting.Remove(name);
            }
        }
    }

    private async Task StartHostIfMissingAsync(string name, CancellationToken cancellationToken)
    {
        // The cost guard lives here, not only in ExecuteAsync, because this is the one place a host
        // is ever created - every route in (startup, a Persona file changing, the Restart button)
        // arrives through it. ExecuteAsync's own early return covers startup and stops the
        // PersonasChanged subscription ever being made; Restart is a direct call and reached this
        // method with the guard off, bringing a Persona fully Online and leaving every later Message
        // to it billable, with nothing on screen having said so.
        //
        // Reported rather than returned silently: a button that does nothing at all is the failure
        // this area keeps producing, and an Offline badge with a reason is the surface that already
        // exists for "it is not running, and here is why".
        if (!this.options.Acp.Enabled)
        {
            this.health.Report(name, PersonaState.Offline, "Teammates are switched off in this configuration, so nothing was started.");
            return;
        }

        lock (this.gate)
        {
            // `stopping` closes the exact window that produced the duplicate-start flake: an
            // OnPersonasChanged invocation that read `stopping == false` just before StopAsync set
            // it, then reached this check after `hosts` was already cleared, would otherwise see no
            // "nova" host and start a second one under the same agent id.
            if (this.stopping || this.hosts.ContainsKey(name) || !this.starting.Add(name))
            {
                return;
            }
        }

        try
        {
            var persona = this.personaStore.Get(name);
            if (persona is null)
            {
                return;
            }

            var host = new PersonaRunner(persona, Options.Create(this.options), this.factory, this.hooks, this.loggerFactory.CreateLogger<PersonaRunner>());

            // Forwards every health signal the runner itself observes (T4.3) - a session/Turn
            // fact, arriving over the wire - into the one table every UI surface reads.
            // Unsubscribed in StopHostAsync, RestartHostAsync's old-host teardown, and StopAsync.
            void OnStatusChanged(PersonaStatus status) => this.health.Report(name, status.State, status.Reason);
            host.StatusChanged += OnStatusChanged;

            try
            {
                this.health.Report(name, PersonaState.Starting, null);
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
                return;
            }

            // Online here means "the start returned and said nothing about itself", never "this
            // Agent is healthy" - a fact this method does not have. PersonaRunner.StartAsync can
            // report during the await above (a stored Model the Adapter does not advertise is the
            // case today), and PersonaHealth.Report is last-write-wins with no notion of a state
            // that outranks another - so reporting Online unconditionally destroyed that report
            // microseconds after it was made, before any surface could read it. Reading the table
            // back rather than tracking a flag keeps the rule in one expression: whatever was said
            // during the start wins, because it knows something this line does not.
            var afterStart = this.health.Get(name);
            if (afterStart is null || afterStart.State == PersonaState.Starting)
            {
                this.health.Report(name, PersonaState.Online, null);
            }

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
        }
    }
}

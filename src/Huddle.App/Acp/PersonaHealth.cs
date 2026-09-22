namespace Agency.Huddle.App.Acp;

/// <summary>
/// What is known about one Persona's Agent right now. Pipe liveness — whether
/// <c>AgentGateway.IsOnline</c> reports a connection — and health — whether the Agent is doing what
/// was asked of it — are different facts; see <see cref="PersonaStatusResolver"/> for how the two
/// combine into the single value every UI surface renders.
/// </summary>
public enum PersonaState
{
    /// <summary>The Adapter is launching and the session is not yet created.</summary>
    Starting,

    /// <summary>Connected and working.</summary>
    Online,

    /// <summary>Connected, but not doing what was asked of it. The Reason says what.</summary>
    Degraded,

    /// <summary>Not working. The Reason says why, when one is known.</summary>
    Offline,
}

/// <summary>One Persona's Agent as it stands, and since when.</summary>
/// <param name="State">What is currently known about this Agent.</param>
/// <param name="Reason">
/// Why the Agent is in <paramref name="State"/>, when one is known; <see langword="null"/> when it is
/// not. This text is read by the Human — in a tooltip or a Room banner — not by a model, so it is
/// interface copy: <c>docs/agencyteam/language.md</c> is binding for it. "Adapter", "Model", "Turn",
/// "Budget", "Persona" and "Agent" are its defined words; "bot", "backend", "LLM", "sandbox", "rate
/// limit", "cap" and "quota" are on its avoid list. A Reason is deliberately not a Prompt: roadmap item
/// 13 moved model-facing text into <c>PromptCatalog</c>/<c>prompts.json</c>, but this text is read by the
/// Human, never by a model, so it stays in code and must never be routed through
/// <c>PromptCatalog</c>.
/// </param>
/// <param name="Since">
/// When <paramref name="State"/> — or, with the State unchanged, <paramref name="Reason"/> — last
/// changed for this Agent.
/// </param>
public sealed record PersonaStatus(PersonaState State, string? Reason, DateTimeOffset Since);

/// <summary>
/// Holds the latest known <see cref="PersonaStatus"/> for every Persona, keyed by Name rather than by
/// Agent id or file path — the same key <c>persona_models</c> and <c>persona_efforts</c> use, and for
/// the same reason: rules.md's "A Persona's identity is its frontmatter, never its filename or its
/// folder." Comparison is <see cref="StringComparer.OrdinalIgnoreCase"/>, matching
/// <c>persona_models</c>'s <c>COLLATE NOCASE</c> and <c>MentionParser</c>'s own comparison of a Name.
/// </summary>
/// <remarks>
/// Intended as a DI Singleton: one process-lifetime table of health facts, read by every UI surface
/// through <see cref="Get"/> or <see cref="All"/>, and written by whichever component observes a
/// change — a <c>PersonaRunner</c> reporting a dead loop, a <c>PersonaSupervisor</c> reporting a
/// launch. Mutation happens only behind <see cref="gate"/>; <see cref="All"/> hands back a copy, never
/// a live view, so a caller enumerating it is never racing a concurrent <see cref="Report"/> or
/// <see cref="Remove"/>.
/// </remarks>
internal sealed class PersonaHealth
{
    private readonly TimeProvider clock;
    private readonly ILogger<PersonaHealth> logger;
    private readonly Lock gate = new();
    private readonly Dictionary<string, PersonaStatus> statuses = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Creates the health table.</summary>
    /// <param name="clock">
    /// The clock <see cref="Report"/> stamps a changed status's <see cref="PersonaStatus.Since"/>
    /// with. Injected rather than read from <see cref="DateTimeOffset.UtcNow"/> directly so a test
    /// can control time exactly, rather than racing the real clock's resolution, when it proves that
    /// a no-op report leaves <c>Since</c> alone.
    /// </param>
    /// <param name="logger">Used only to record a throwing <see cref="Changed"/> subscriber; see <see cref="RaiseChanged"/>.</param>
    public PersonaHealth(TimeProvider clock, ILogger<PersonaHealth> logger)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        this.clock = clock;
        this.logger = logger;
    }

    /// <summary>
    /// Raised after a <see cref="Report"/> actually changes a Persona's recorded state or reason —
    /// never for a no-op report. A later task reports <see cref="PersonaState.Online"/> after every
    /// successful Turn, and an event per Message would repaint every tile in the app continuously;
    /// this is what keeps that cheap.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// Every Persona's current <see cref="PersonaStatus"/>, as of this call. A snapshot, not a live
    /// view: a <see cref="Report"/> or <see cref="Remove"/> made after this call never changes a
    /// dictionary already returned by it.
    /// </summary>
    public IReadOnlyDictionary<string, PersonaStatus> All
    {
        get
        {
            lock (this.gate)
            {
                Dictionary<string, PersonaStatus> snapshot = new(this.statuses, StringComparer.OrdinalIgnoreCase);
                return snapshot;
            }
        }
    }

    /// <summary>
    /// Records what is currently known about <paramref name="personaName"/>'s Agent. A no-op report —
    /// the same <paramref name="state"/> and the same <paramref name="reason"/> as already recorded —
    /// updates nothing at all: it does not raise <see cref="Changed"/>, and it does not move
    /// <see cref="PersonaStatus.Since"/> forward, because <c>Since</c> means "since when has it been
    /// like this" and a no-op report must not reset that clock.
    /// </summary>
    /// <param name="personaName">The Persona's Name, compared case-insensitively.</param>
    /// <param name="state">What is currently known about this Agent.</param>
    /// <param name="reason">
    /// Why, when known; otherwise <see langword="null"/>. This is interface copy read by the Human —
    /// see the identical note on <see cref="PersonaStatus.Reason"/> for the vocabulary it must use and
    /// why it is not a Prompt. This task does not write any reason strings; the caller that does must
    /// follow that note.
    /// </param>
    public void Report(string personaName, PersonaState state, string? reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(personaName);

        bool raiseChanged;
        lock (this.gate)
        {
            if (this.statuses.TryGetValue(personaName, out var existing) &&
                existing.State == state &&
                string.Equals(existing.Reason, reason, StringComparison.Ordinal))
            {
                raiseChanged = false;
            }
            else
            {
                this.statuses[personaName] = new PersonaStatus(state, reason, this.clock.GetUtcNow());
                raiseChanged = true;
            }
        }

        if (raiseChanged)
        {
            this.RaiseChanged();
        }
    }

    /// <summary>
    /// The current <see cref="PersonaStatus"/> for <paramref name="personaName"/>, or
    /// <see langword="null"/> if none has ever been reported.
    /// </summary>
    /// <param name="personaName">The Persona's Name, compared case-insensitively.</param>
    public PersonaStatus? Get(string personaName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(personaName);

        lock (this.gate)
        {
            return this.statuses.TryGetValue(personaName, out var status) ? status : null;
        }
    }

    /// <summary>
    /// Drops any recorded status for <paramref name="personaName"/>. Harmless to call for a Persona
    /// with no recorded status.
    /// </summary>
    /// <param name="personaName">The Persona's Name, compared case-insensitively.</param>
    public void Remove(string personaName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(personaName);

        lock (this.gate)
        {
            this.statuses.Remove(personaName);
        }
    }

    /// <summary>
    /// Raises <see cref="Changed"/> one handler at a time, each in its own try/catch, so a throwing
    /// subscriber neither stops the others nor breaks the caller of <see cref="Report"/> —
    /// <c>RoomEvents</c>'s established pattern for this exact problem.
    /// </summary>
    private void RaiseChanged()
    {
        if (this.Changed is not { } handlers)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList())
        {
            try
            {
                ((Action)handler).Invoke();
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.Changed));
            }
        }
    }
}

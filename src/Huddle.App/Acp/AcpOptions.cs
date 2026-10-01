namespace Agency.Huddle.App.Acp;

public sealed class AcpOptions
{
    // Off by default: starting an agent process spends real money on the user's Claude
    // subscription (docs/acp/agent-guide.md §3.1), so code must never turn this on and test
    // fixtures state it explicitly (Team:Acp:Enabled=false) rather than relying on this default.
    public bool Enabled { get; set; }

    public string Command { get; set; } = "node";

    public string? AdapterPath { get; set; }

    // Nullable, no initialiser: ConfigurationBinder appends bound array elements to an
    // already-populated list/array property instead of replacing it (see TeamOptions.DemoAgent.Names
    // and the "Collection options need no initialiser" rule). Consumers default when this is null
    // or empty.
    public IReadOnlyList<string>? Args { get; set; }

    /// <summary>
    /// The Adapters this installation can launch (Spec §7.4). Nullable, no initialiser: the same
    /// "Collection options need no initialiser" rule as <see cref="Args"/> — ConfigurationBinder
    /// appends to an already-populated list rather than replacing it. When null or empty,
    /// <see cref="AdapterCatalog"/> synthesises exactly one profile from <see cref="Command"/>,
    /// <see cref="Args"/> and <see cref="AdapterPath"/> above, so an installation that configures
    /// no Adapters keeps today's behaviour (Spec §4, P6).
    /// </summary>
    public IReadOnlyList<AdapterProfileOptions>? Adapters { get; set; }

    /// <summary>
    /// The Teammate folders directory, relative to <see cref="TeamOptions.DataDir"/>, that
    /// <see cref="PersonaStore"/> scans one level deep for Teammate definition files (Spec §6.15,
    /// §7). Replaces the retired old key, which now throws at start-up naming this one.
    /// </summary>
    public string TeammatesDir { get; set; } = "Teammates";

    /// <summary>
    /// The Skill Library directory, relative to <see cref="TeamOptions.DataDir"/>, that
    /// <see cref="Agency.Huddle.App.Skills.SkillStore"/> scans for Skill folders (Spec §7.3).
    /// Created at startup if missing.
    /// </summary>
    public string SkillsDir { get; set; } = "Skills";

    // The per-Persona working directory handed to the agent process as its cwd. Not a jail:
    // agent-side Bash and Write run against the real disk.
    public string WorkDir { get; set; } = "work";

    // Warning: wire traces dump the tool server's bearer token (docs/acp/agent-guide.md §7.5).
    // Never enable this outside a throwaway, trusted debugging session.
    public bool TraceWire { get; set; }

    // Per-Room catch-up buffer size (docs/adr/0004-direct-rooms-reply-without-mention.md): the most
    // recent unmentioned Messages that ride along once the Agent is finally Mentioned in that Room.
    public int CatchUpMessages { get; set; } = 20;

    // The third layer of the cap (docs/agencyteam/roadmap.md item 2): a per-Persona token Budget,
    // accumulated in PersonaRunner from UsageUpdated.
    //
    // UsageUpdated.Used is how full the context window is, not a running bill - Huddle.Console's
    // ConsoleRenderer prints it as "{Used}/{Size}" - so it FALLS when the session compacts. Only the
    // rises are summed, which makes this a proxy for tokens fed through the session and never an
    // invoice. Like the per-Room Budget it resets when the Persona sees a Human Message, so it caps
    // unattended spend rather than the session's whole life.
    //
    // Per Persona because one ACP session spans every Room its Agent is in, which is also what lets
    // it catch what TeamOptions.AgentMessageBudget cannot: an Agent that loops by creating fresh
    // Rooms, each with a fresh Budget of its own.
    //
    // Roughly five full context refills. Zero or less disables it. A local Model emits no
    // UsageUpdated at all, so this is inert there (roadmap item 12).
    public long TokenBudget { get; set; } = 1_000_000;

    /// <summary>
    /// How many seconds of silence <see cref="Agency.Huddle.App.Acp.PersonaRunner"/> tolerates from
    /// the Adapter during one Turn before treating it as hung and cancelling it. This bounds SILENCE,
    /// not the Turn's total duration: any event on the session's event stream — a chunk, a tool call,
    /// a usage update, even one this runner otherwise ignores — restarts the clock, so a long
    /// tool-using Turn that keeps reporting progress never trips this however long it runs.
    /// </summary>
    /// <remarks>
    /// An <see cref="int"/> of seconds, deliberately not a <see cref="TimeSpan"/>: a configured value
    /// such as <c>"180"</c> binds through <see cref="TimeSpan.Parse(string)"/> as 180 DAYS, not 180
    /// seconds, which would silently bind to a bound nobody ever reaches. 180 is three times the
    /// worst cold local-model load the spec budgets at 10-60 seconds, and far below what a genuine
    /// tool-using Claude Turn approaches, since the Adapter reports every tool call as it starts.
    /// Zero or less disables the bound entirely. Firing it is a FAILURE, not a Stop: it reports
    /// <see cref="PersonaState.Degraded"/>, exactly as any other Turn failure does, never the silent,
    /// Information-only path a Human pressing Stop takes.
    /// </remarks>
    public int TurnIdleTimeoutSeconds { get; set; } = 180;

    /// <summary>
    /// The most Teammates this installation allows - every loaded <see cref="PersonaStore"/> entry,
    /// rejected files excluded (Spec §7.3). Checked by <c>propose_teammates</c> and again at Approve,
    /// never on the Teammate card, which has no count of its own to enforce against. Zero or less
    /// disables the limit entirely.
    /// </summary>
    public int MaxTeammates { get; set; } = 8;

    /// <summary>
    /// How many minutes an <c>Idle</c> Room Session with an empty queue may sit open before
    /// <c>RoomSessionPool</c>'s sweep closes it (RS §6.14). Applies in per-Room mode only (finding
    /// P-20): in shared mode the sweep never runs, since there is exactly one session and closing it
    /// would lose the whole conversation. Zero or less never closes an idle Room Session.
    /// </summary>
    public int SessionIdleMinutes { get; set; } = 30;

    /// <summary>
    /// The most Room Sessions one Persona keeps open at once (RS §6.14), per-Room mode only (finding
    /// P-20). Opening beyond this first closes the least recently used <c>Idle</c> Room Session; a
    /// value below <see cref="MaxConcurrentTurns"/> is raised to it, with a startup warning, since a
    /// running Turn always needs its Room Session open.
    /// </summary>
    public int MaxLiveSessions { get; set; } = 3;

    /// <summary>
    /// The most Turns one Persona runs at once, across every Room Session (RS §6.14, finding P-4).
    /// The default, 1, is today's behaviour: Turns run one at a time, in arrival order, across every
    /// Room.
    /// </summary>
    public int MaxConcurrentTurns { get; set; } = 1;

    /// <summary>
    /// The most Messages a Room Session's first Turn carries as Transcript Catch-up (RS §6.5, §6.14):
    /// the latest ones when the session opens fresh, or the ones after its stored <c>LastMessageId</c>
    /// when it resumes.
    /// </summary>
    public int TranscriptCatchUpMessages { get; set; } = 20;

    /// <summary>
    /// The Work Mode ids (ADR-0033) that are never offered in the Teammate card and never sent to an
    /// Adapter, bound from <c>Team:Acp:HiddenModes</c>. Null, the key being absent, means the default
    /// that <see cref="WorkModePolicy"/> holds; a list means exactly that list. To hide nothing, give a
    /// list holding one empty string: configuration cannot express an empty list, since the binder
    /// reads an empty value as absent. Nullable with no initialiser, for the reason <see cref="Args"/> gives:
    /// <c>ConfigurationBinder</c> writes a bound array into an already-populated one by index, so a
    /// default here would make <c>["auto"]</c> give <c>["auto", "auto"]</c> and silently unhide the
    /// second default.
    /// </summary>
    public IReadOnlyList<string>? HiddenModes { get; set; }
}
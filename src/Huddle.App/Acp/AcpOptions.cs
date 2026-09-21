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
    /// The Team Library directory, relative to <see cref="TeamOptions.DataDir"/>, that
    /// <see cref="PersonaStore"/> scans recursively for Persona markdown files. Team sub-folders
    /// under it are purely organisational — <c>Teams/Business/coo.md</c> is exactly as much a
    /// Persona as <c>Teams/coo.md</c> — because team membership is a front-matter field a later
    /// phase adds, not a filesystem convention this phase gives meaning to.
    /// </summary>
    public string TeamsDir { get; set; } = "Teams";

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
}
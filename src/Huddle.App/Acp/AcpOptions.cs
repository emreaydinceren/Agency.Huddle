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
}
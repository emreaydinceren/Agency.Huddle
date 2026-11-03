namespace Agency.Huddle.App.Acp;

/// <summary>The AGENTS.md files an Agent starts a session with, and those left out (Spec §6.2).</summary>
/// <param name="Sections">The sections read, from general to specific.</param>
/// <param name="Omitted">The paths of files left out of the session.</param>
internal sealed record AgentsMdSnapshot(IReadOnlyList<AgentsMdSection> Sections, IReadOnlyList<string> Omitted)
{
    /// <summary>A snapshot with no sections and nothing omitted.</summary>
    public static AgentsMdSnapshot Empty { get; } = new([], []);
}

namespace Agency.Huddle.App;

/// <summary>Bound from <c>Team:AgentsMd</c>, per Spec §6.2 and §7.4: the size limits on the AGENTS.md files Agents receive.</summary>
public sealed class AgentsMdOptions
{
    /// <summary>The per-file cut, in characters (<c>Team:AgentsMd:MaxCharsPerFile</c>). Replaces <c>Team:Instructions:MaxChars</c>.</summary>
    public int MaxCharsPerFile { get; set; } = 8000;

    /// <summary>The budget for the whole AGENTS.md block, in characters (<c>Team:AgentsMd:MaxTotalChars</c>). Project files are dropped from the end first, then cut.</summary>
    public int MaxTotalChars { get; set; } = 32000;
}

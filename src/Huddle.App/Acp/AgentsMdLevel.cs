namespace Agency.Huddle.App.Acp;

/// <summary>The level an AGENTS.md file belongs to (Spec §6.2), from general to specific.</summary>
internal enum AgentsMdLevel
{
    /// <summary>The Huddle-wide AGENTS.md, read for every Agent.</summary>
    HuddleWide,

    /// <summary>A Team's AGENTS.md, read for each Team the Agent is in.</summary>
    Team,

    /// <summary>A Project's AGENTS.md, the most specific level.</summary>
    Project,
}

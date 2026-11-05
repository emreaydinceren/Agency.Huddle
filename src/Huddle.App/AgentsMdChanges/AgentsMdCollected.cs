namespace Agency.Huddle.App.AgentsMdChanges;

/// <summary>What a Turn collected from the AGENTS.md files: the change-notice block and what the session has heard afterwards (Spec §8.3).</summary>
/// <param name="Block">The rendered block; null when there is nothing to tell.</param>
/// <param name="Heard">The hash per relative path the session has now heard.</param>
internal sealed record AgentsMdCollected(string? Block, IReadOnlyDictionary<string, string> Heard);

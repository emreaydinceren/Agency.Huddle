namespace Agency.Huddle.App.Acp;

/// <summary>
/// The file guards one Agent session's permission handler consults, in this order (Spec §6.6): <c>AGENTS.md</c>, Raw, Prompts, Team Memory.
/// </summary>
/// <param name="AgentsMd">Refuses a write to the Human's <c>AGENTS.md</c> files.</param>
/// <param name="TeamMemory">Refuses a write into a Team's Memory by anyone but its manager.</param>
/// <param name="Raw">Refuses a change to an original in a <c>raw/</c> position.</param>
/// <param name="Prompts">Refuses a write to the Prompts folder, which only the Human changes.</param>
/// <param name="Settings">Refuses a write to the Agent's local settings file and the merge gate hook, consulted last (issue #432); <see langword="null"/> leaves those writes approved.</param>
internal sealed record PermissionGuards(AgentsMdGuard AgentsMd, TeamMemoryWriteGuard TeamMemory, RawGuard Raw, PromptsRootGuard Prompts, AgentSettingsGuard? Settings = null);

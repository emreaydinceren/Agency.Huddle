namespace Agency.Huddle.App.Acp;

/// <summary>
/// The file guards one Agent session's permission handler consults, in this order: <c>AGENTS.md</c>, Team Memory, Raw.
/// </summary>
/// <remarks>Task 81 adds the prompts-root guard as a fourth member.</remarks>
/// <param name="AgentsMd">Refuses a write to the Human's <c>AGENTS.md</c> files.</param>
/// <param name="TeamMemory">Refuses a write into a Team's Memory by anyone but its manager.</param>
/// <param name="Raw">Refuses a change to an original in a <c>raw/</c> position.</param>
internal sealed record PermissionGuards(AgentsMdGuard AgentsMd, TeamMemoryWriteGuard TeamMemory, RawGuard Raw);

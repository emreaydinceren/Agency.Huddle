using Agency.Huddle.App.Memory;

namespace Agency.Huddle.App.Acp;

/// <summary>The collaborators that fill a session's system prompt and guard its file tools from what the Human wrote: AGENTS.md files, Team Memory access and the warnings about malformed memory files.</summary>
/// <param name="AgentsMd">Reads the Human's AGENTS.md files afresh at each session start (Spec §6.2).</param>
/// <param name="TeamMemory">Decides who may write a Team's Memory with the Agent's own file tools (Instructions spec §5.6).</param>
/// <param name="Warnings">Warns once per file and content about a memory file with malformed frontmatter.</param>
/// <param name="AgentPaths">Locates every Agent's Work Dir, so the guard on <c>Instructions.md</c> covers the other Agents' files as well as the session's own.</param>
/// <param name="MaxOutputsEntries">The cap on the outputs index across the Agent's Teams, <c>Team:Outputs:MaxIndexEntries</c> (Spec §6.3).</param>
internal sealed record SessionPromptSources(AgentsMdReader AgentsMd, TeamMemoryAccess TeamMemory, MemoryWarnings Warnings, AgentPaths AgentPaths, int MaxOutputsEntries);
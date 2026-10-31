using Agency.Huddle.App.Memory;

namespace Agency.Huddle.App.Acp;

/// <summary>The collaborators that fill a session's system prompt and guard its file tools from what the Human wrote: Instructions, Team Memory access and the warnings about malformed memory files.</summary>
/// <param name="Instructions">Reads the Human's standing Instructions afresh at each session start (Instructions spec §3.2).</param>
/// <param name="TeamMemory">Decides who may write a Team's Memory with the Agent's own file tools (Instructions spec §5.6).</param>
/// <param name="Warnings">Warns once per file and content about a memory file with malformed frontmatter.</param>
/// <param name="AgentPaths">Locates every Agent's Work Dir, so the guard on <c>Instructions.md</c> covers the other Agents' files as well as the session's own.</param>
internal sealed record SessionPromptSources(InstructionsReader Instructions, TeamMemoryAccess TeamMemory, MemoryWarnings Warnings, AgentPaths AgentPaths);
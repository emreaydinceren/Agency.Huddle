using Agency.Huddle.App.Memory;
using Agency.Huddle.App.SecondBrain;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Acp;

/// <summary>The collaborators that fill a session's system prompt and guard its file tools from what the Human wrote: AGENTS.md files, Team Memory access and the warnings about malformed memory files.</summary>
/// <param name="AgentsMd">Reads the Human's AGENTS.md files afresh at each session start (Spec §6.2).</param>
/// <param name="TeamMemory">Decides who may write a Team's Memory with the Agent's own file tools (Instructions spec §5.6).</param>
/// <param name="Warnings">Warns once per file and content about a memory file with malformed frontmatter.</param>
/// <param name="AgentPaths">Locates every Agent's Work Dir, so the guard on <c>Instructions.md</c> covers the other Agents' files as well as the session's own.</param>
/// <param name="MaxOutputsEntries">The cap on the outputs index across the Agent's Teams, <c>Team:Outputs:MaxIndexEntries</c> (Spec §6.3).</param>
/// <param name="OverCap">Told when an index leaves files out (Spec §6.8), or <see langword="null"/> where no Feed is wired.</param>
/// <param name="Levels">Resolves the Needs You level of each Team and Project at session start (ADR-0088), or <see langword="null"/> where none is wired; then the prompt has no Needs You section.</param>
internal sealed record SessionPromptSources(AgentsMdReader AgentsMd, TeamMemoryAccess TeamMemory, MemoryWarnings Warnings, AgentPaths AgentPaths, int MaxOutputsEntries, OverCapNotifier? OverCap = null, NeedsYouLevels? Levels = null);
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Acp;

/// <summary>The snapshots a session's system prompt is composed from, read once so a session and the "What this Agent reads" preview see the same inputs (Spec §6.5, D-18).</summary>
/// <param name="Memory">The Persona's own Memory index.</param>
/// <param name="TeamMemory">The Persona's Team Memory, or <see langword="null"/> when it has none.</param>
/// <param name="AgentsMd">The Human's AGENTS.md files for the Persona's Teams.</param>
/// <param name="HuddleMemory">The Huddle-wide Memory index of <c>Teams/memory/</c>, which every Persona gets; its entries are empty when the folder has no topics (Spec §6.3).</param>
internal sealed record SessionPromptSnapshot(MemorySnapshot Memory, TeamMemorySnapshot? TeamMemory, AgentsMdSnapshot AgentsMd, MemorySnapshot HuddleMemory);

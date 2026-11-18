using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Acp;

/// <summary>The snapshots a session's system prompt is composed from, read once so a session and the "What this Agent reads" preview see the same inputs (Spec §6.5, D-18).</summary>
/// <param name="Memory">The Persona's own Memory index.</param>
/// <param name="TeamMemory">The Persona's Team Memory, or <see langword="null"/> when it has none.</param>
/// <param name="AgentsMd">The Human's AGENTS.md files for the Persona's Teams.</param>
/// <param name="HuddleMemory">The Huddle-wide Memory index of <c>Teams/memory/</c>, which every Persona gets; its entries are empty when the folder has no topics (Spec §6.3).</param>
/// <param name="Outputs">The outputs index of the Persona's Teams, capped by <c>Team:Outputs:MaxIndexEntries</c>; its entries are empty when no Team has outputs (Spec §6.3, §8.1 slot 13).</param>
/// <param name="Workspaces">The <c>(team, path)</c> of each of the Persona's Teams whose workspace is readable at session start, in <c>teams</c> order (Spec §6.4, §8.1 slot 7).</param>
/// <param name="NeedsYou">The <c>(scope, level)</c> of the Persona's Teams and their Projects when any of them runs inside the Team, else <see langword="null"/> (ADR-0088).</param>
/// <param name="Playbooks">The Persona's Team Playbooks, or <see langword="null"/> when no Playbook source is wired (Team Playbooks spec §6.1).</param>
internal sealed record SessionPromptSnapshot(MemorySnapshot Memory, TeamMemorySnapshot? TeamMemory, AgentsMdSnapshot AgentsMd, MemorySnapshot HuddleMemory, OutputsSnapshot Outputs, IReadOnlyList<(string Team, string Path)> Workspaces, IReadOnlyList<(string Scope, string Level)>? NeedsYou = null, PlaybookSnapshot? Playbooks = null);

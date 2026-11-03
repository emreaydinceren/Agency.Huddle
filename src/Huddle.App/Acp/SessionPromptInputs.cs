using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Memory;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Acp;

/// <summary>Reads, afresh, the snapshots a Persona's system prompt is composed from. The one place both a session start and the prompt preview build them (Spec §6.5, D-18).</summary>
internal static class SessionPromptInputs
{
    /// <summary>Builds the Memory, Team Memory, AGENTS.md and Huddle-wide Memory snapshots for one Persona.</summary>
    /// <param name="agentsMd">Reads the Human's AGENTS.md files.</param>
    /// <param name="warnings">Warns once per file and content about malformed memory frontmatter.</param>
    /// <param name="catalogTeams">The catalog's Teams, read once by the caller so the build sees one consistent view.</param>
    /// <param name="personaName">The Persona the snapshots are for; its Team Memory groups mark it as manager where it is.</param>
    /// <param name="memoryDir">The Persona's memory folder.</param>
    /// <param name="maxMemoryEntries">The cap on the Persona's own Memory entries.</param>
    /// <param name="teamLabels">The Team labels of the Persona's Teams line, in the order written; empty for a Persona in no Team.</param>
    /// <param name="teamsRoot">The full path of the Teams root folder.</param>
    /// <param name="maxTeamMemoryEntries">The cap on Team Memory entries across all of the Persona's Teams.</param>
    /// <param name="maxHuddleMemoryEntries">The cap on the Huddle-wide Memory entries of <c>Teams/memory/</c>, which every Persona gets.</param>
    /// <param name="maxOutputsEntries">The cap on the outputs index across the Persona's Teams, in the order they are listed.</param>
    /// <returns>The five snapshots.</returns>
    internal static SessionPromptSnapshot Build(
        AgentsMdReader agentsMd,
        MemoryWarnings warnings,
        IReadOnlyList<TeamSummary> catalogTeams,
        string personaName,
        string memoryDir,
        int maxMemoryEntries,
        IReadOnlyList<string> teamLabels,
        string teamsRoot,
        int maxTeamMemoryEntries,
        int maxHuddleMemoryEntries,
        int maxOutputsEntries)
    {
        var (entries, notListed) = MemoryIndex.Build(memoryDir, maxMemoryEntries, warnings);
        MemorySnapshot memory = new(memoryDir, entries, notListed);

        TeamMemorySnapshot? teamMemory = null;
        if (teamLabels.Count > 0)
        {
            TeamMemorySnapshot built = TeamMemoryIndex.Build(teamsRoot, teamLabels, catalogTeams, maxTeamMemoryEntries, personaName, warnings);
            teamMemory = built.Groups.Count > 0 ? built : null;
        }

        // The catalog's folder spelling names each Team, so an AGENTS.md path matches the folder on a case-sensitive file system.
        List<string> teamNames =
        [
            .. teamLabels.Select(label =>
                catalogTeams.FirstOrDefault(team => string.Equals(team.Name, label, StringComparison.OrdinalIgnoreCase))?.Name ?? label),
        ];

        string huddleMemoryDir = Path.Combine(teamsRoot, TeamNames.MemoryFolder);
        var (huddleEntries, huddleNotListed) = MemoryIndex.Build(huddleMemoryDir, maxHuddleMemoryEntries, warnings);
        MemorySnapshot huddleMemory = new(huddleMemoryDir, huddleEntries, huddleNotListed);

        OutputsSnapshot outputs = OutputsIndex.Build(teamsRoot, teamNames, maxOutputsEntries, warnings);

        return new SessionPromptSnapshot(memory, teamMemory, agentsMd.Read(teamsRoot, teamNames), huddleMemory, outputs);
    }
}

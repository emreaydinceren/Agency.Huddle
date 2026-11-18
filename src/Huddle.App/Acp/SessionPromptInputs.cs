using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Memory;
using Agency.Huddle.App.SecondBrain;
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
    /// <param name="overCap">Told about each folder whose index left files out, or <see langword="null"/> where nobody is told (a preview).</param>
    /// <param name="levels">Resolves each Team's and Project's Needs You level; <see langword="null"/> where none is wired, which leaves the prompt without that section.</param>
    /// <param name="playbooks">What builds the Team Playbook snapshot; <see langword="null"/> where none is wired, which leaves the prompt without Playbook parts.</param>
    /// <param name="filesReadable">Whether the Adapter can read files, so a cut Playbook may point at its file.</param>
    /// <returns>The snapshots.</returns>
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
        int maxOutputsEntries,
        OverCapNotifier? overCap = null,
        NeedsYouLevels? levels = null,
        PlaybookInputs? playbooks = null,
        bool filesReadable = true)
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

        if (overCap is not null)
        {
            ReportOverCap(overCap, personaName, memory, teamMemory, huddleMemory, outputs, (maxMemoryEntries, maxTeamMemoryEntries, maxHuddleMemoryEntries, maxOutputsEntries));
        }

        PlaybookSnapshot? playbookSnapshot = playbooks is null
            ? null
            : PlaybookSnapshotBuilder.Build(playbooks.Prompts, catalogTeams, personaName, teamLabels, teamsRoot, playbooks.Options, filesReadable, playbooks.Logger);

        return new SessionPromptSnapshot(memory, teamMemory, agentsMd.Read(teamsRoot, teamNames), huddleMemory, outputs, ReadableWorkspaces(catalogTeams, teamNames), NeedsYouScopes(levels, catalogTeams, teamNames), playbookSnapshot);
    }

    /// <summary>
    /// Lists the effective Needs You level of each of the Persona's Teams and their Projects, but only when one of them runs inside the Team;
    /// when every scope is Ask me it returns <see langword="null"/>, so the default prompt is unchanged (ADR-0088).
    /// </summary>
    /// <param name="levels">Resolves the levels; <see langword="null"/> means none is known and nothing is listed.</param>
    /// <param name="catalogTeams">The catalog's Teams, which name each Team's Projects.</param>
    /// <param name="teamNames">The Persona's Teams, in the order written, spelled as the catalog spells them.</param>
    /// <returns>The <c>(scope, level)</c> pairs in Team then Project order, or <see langword="null"/>.</returns>
    private static List<(string Scope, string Level)>? NeedsYouScopes(NeedsYouLevels? levels, IReadOnlyList<TeamSummary> catalogTeams, IReadOnlyList<string> teamNames)
    {
        if (levels is null)
        {
            return null;
        }

        List<(string Scope, string Level)> scopes = [];
        bool anyRunsInside = false;
        foreach (string name in teamNames)
        {
            if (catalogTeams.FirstOrDefault(team => string.Equals(team.Name, name, StringComparison.OrdinalIgnoreCase)) is not { } summary)
            {
                continue;
            }

            NeedsYouLevel teamLevel = levels.For(summary.Name, null).Level;
            scopes.Add((summary.Name, teamLevel.ToDisplay()));
            anyRunsInside |= teamLevel == NeedsYouLevel.RunInsideTheTeam;
            foreach (string project in summary.Projects)
            {
                NeedsYouLevel projectLevel = levels.For(summary.Name, project).Level;
                scopes.Add(($"{summary.Name} › {project}", projectLevel.ToDisplay()));
                anyRunsInside |= projectLevel == NeedsYouLevel.RunInsideTheTeam;
            }
        }

        return anyRunsInside ? scopes : null;
    }

    /// <summary>Lists, in the given order, each Team whose catalog entry names a workspace that is a repository or a readable folder (Spec §6.4).</summary>
    /// <param name="catalogTeams">The catalog's Teams.</param>
    /// <param name="teamNames">The Persona's Teams, in the order written, spelled as the catalog spells them.</param>
    /// <returns>The <c>(team, path)</c> pairs; a Team without a workspace or with an unreadable one is left out.</returns>
    private static List<(string Team, string Path)> ReadableWorkspaces(IReadOnlyList<TeamSummary> catalogTeams, IReadOnlyList<string> teamNames)
    {
        List<(string Team, string Path)> workspaces = [];
        foreach (string name in teamNames)
        {
            string? path = catalogTeams.FirstOrDefault(team => string.Equals(team.Name, name, StringComparison.OrdinalIgnoreCase))?.Workspace;
            if (path is not null && WorkspaceProbe.StateOf(path) != WorkspaceState.Unreadable)
            {
                workspaces.Add((name, path));
            }
        }

        return workspaces;
    }

    /// <summary>Tells the notifier about every folder whose index left files out (SB-R13, Spec §6.8). Spec T14 is the one text for all four indexes.</summary>
    /// <param name="overCap">The notifier.</param>
    /// <param name="personaName">The Persona, which names its own memory folder.</param>
    /// <param name="memory">The Persona's Memory.</param>
    /// <param name="teamMemory">The Team Memory, or <see langword="null"/>.</param>
    /// <param name="huddleMemory">The Huddle-wide Memory.</param>
    /// <param name="outputs">The outputs index.</param>
    /// <param name="caps">The caps of the Persona's, the Team, the Huddle-wide and the outputs indexes, in that order.</param>
    private static void ReportOverCap(
        OverCapNotifier overCap,
        string personaName,
        MemorySnapshot memory,
        TeamMemorySnapshot? teamMemory,
        MemorySnapshot huddleMemory,
        OutputsSnapshot outputs,
        (int Own, int Team, int Huddle, int Outputs) caps)
    {
        ReportScope(overCap, $"Agents/{personaName}/memory", memory, caps.Own);
        ReportScope(overCap, $"Teams/{TeamNames.MemoryFolder}", huddleMemory, caps.Huddle);
        foreach (TeamMemoryGroup group in teamMemory?.Groups ?? [])
        {
            ReportScope(overCap, $"Teams/{group.Team}/{TeamNames.MemoryFolder}", group.TeamWide, caps.Team);
            foreach ((string project, MemorySnapshot projectMemory) in group.Projects)
            {
                ReportScope(overCap, $"Teams/{group.Team}/{project}/{TeamNames.MemoryFolder}", projectMemory, caps.Team);
            }
        }

        foreach (OutputsOverflow overflow in outputs.Overflows)
        {
            int listed = outputs.Entries.Count(entry => entry.RelativePath.StartsWith(overflow.Folder + "/", StringComparison.Ordinal));
            overCap.Report($"Teams/{overflow.Folder}", listed, caps.Outputs);
        }
    }

    private static void ReportScope(OverCapNotifier overCap, string folder, MemorySnapshot snapshot, int cap)
    {
        if (snapshot.NotListed > 0)
        {
            overCap.Report(folder, snapshot.Entries.Count, cap);
        }
    }
}

using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Acp;

/// <summary>Builds the <see cref="PlaybookSnapshot"/> of one Persona's session start (Team Playbooks spec §6.1, §6.2).</summary>
internal static class PlaybookSnapshotBuilder
{
    /// <summary>Builds the snapshot: for each kind, one default section for the Teams with no file, then one section per Team's own file.</summary>
    /// <param name="prompts">Resolves the default Playbook texts.</param>
    /// <param name="catalogTeams">The catalog's Teams, read once by the caller.</param>
    /// <param name="personaName">The Persona; it is a manager of the Teams whose catalog entry names it.</param>
    /// <param name="teamLabels">The Team labels of the Persona's Teams line, in the order written.</param>
    /// <param name="teamsRoot">The full path of the Teams root folder.</param>
    /// <param name="options">The size limits.</param>
    /// <param name="filesReadable">Whether the Adapter can read files, so a cut Playbook may point at its file.</param>
    /// <param name="logger">Told about a Playbook file that cannot be read.</param>
    /// <returns>The snapshot; <see cref="PlaybookSnapshot.Empty"/> for a Persona in no catalog Team.</returns>
    internal static PlaybookSnapshot Build(
        IPromptSource prompts,
        IReadOnlyList<TeamSummary> catalogTeams,
        string personaName,
        IReadOnlyList<string> teamLabels,
        string teamsRoot,
        PlaybookOptions options,
        bool filesReadable,
        ILogger logger)
    {
        List<TeamSummary> teams = [];
        foreach (string label in teamLabels)
        {
            TeamSummary? team = catalogTeams.FirstOrDefault(candidate => string.Equals(candidate.Name, label, StringComparison.OrdinalIgnoreCase));
            if (team is not null && !teams.Contains(team))
            {
                teams.Add(team);
            }
        }

        if (teams.Count == 0)
        {
            return PlaybookSnapshot.Empty;
        }

        List<PlaybookSection> sections = [];
        AddKind(sections, prompts, PlaybookKind.Member, teams, teamsRoot, logger);
        List<TeamSummary> managed = [.. teams.Where(team => string.Equals(team.Manager, personaName, StringComparison.OrdinalIgnoreCase))];
        if (managed.Count > 0)
        {
            AddKind(sections, prompts, PlaybookKind.Manager, managed, teamsRoot, logger);
        }

        return new PlaybookSnapshot(sections)
        {
            MaxChars = options.MaxChars,
            MaxTotalChars = options.MaxTotalChars,
            FilesReadable = filesReadable,
        };
    }

    private static void AddKind(List<PlaybookSection> sections, IPromptSource prompts, PlaybookKind kind, List<TeamSummary> teams, string teamsRoot, ILogger logger)
    {
        string fileName = PlaybookFile.FileName(kind == PlaybookKind.Manager);
        string key = kind == PlaybookKind.Manager ? "systemPrompt.managerPlaybook" : "systemPrompt.memberPlaybook";
        List<string> defaultTeams = [];
        List<PlaybookSection> own = [];
        foreach (TeamSummary team in teams)
        {
            string path = Path.Combine(teamsRoot, team.Name, fileName);
            string? body = ReadBody(path, logger);
            if (body is null)
            {
                defaultTeams.Add(team.Name);
            }
            else
            {
                own.Add(new PlaybookSection(kind, [team.Name], body, new PromptPartSource.File(path)));
            }
        }

        if (defaultTeams.Count > 0)
        {
            sections.Add(new PlaybookSection(kind, defaultTeams, prompts.Raw(key), new PromptPartSource.Prompt(key, prompts.IsOverridden(key))));
        }

        sections.AddRange(own);
    }

    private static string? ReadBody(string path, ILogger logger)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return PlaybookFile.Parse(File.ReadAllText(path)).Body;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "The Playbook file {Path} could not be read; the Team gets the default Playbook.", path);
            return null;
        }
    }
}

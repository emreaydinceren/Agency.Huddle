using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Teams;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.AgentsMdChanges;

/// <summary>Says which <c>AGENTS.md</c> files are in scope for a named Agent, from its Team labels (Spec §6.7).</summary>
/// <param name="personas">Looks the Agent up to read its Team labels.</param>
/// <param name="catalog">Spells each Team as its folder is spelled.</param>
/// <param name="options">Supplies the Teams root.</param>
internal sealed class AgentsMdScopes(PersonaStore personas, ITeamCatalog catalog, IOptions<TeamOptions> options)
{
    /// <summary>Lists the files that apply to an Agent, Huddle-wide first, then each Team and its Projects.</summary>
    /// <param name="personaName">The Agent's name; a name no Persona carries has only the Huddle-wide file.</param>
    /// <returns>Every level's file, whether or not it exists.</returns>
    public IReadOnlyList<AgentsMdFile> For(string personaName) => this.Scan(personaName).Files;

    /// <summary>Lists the same files as <see cref="For"/>, and says whether every Team could be listed.</summary>
    /// <param name="personaName">The Agent's name; a name no Persona carries has only the Huddle-wide file.</param>
    /// <returns>Every level's file, and false when a Team's Projects are unknown because its folder could not be listed.</returns>
    public (IReadOnlyList<AgentsMdFile> Files, bool Complete) Scan(string personaName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(personaName);

        IReadOnlyList<string> labels = personas.ResolveByNameOrAlias(personaName)?.Teams ?? [];

        // The catalog's folder spelling names each Team, as SessionPromptInputs does, so a path matches the folder on a case-sensitive file system.
        IReadOnlyList<TeamSummary> catalogTeams = catalog.Teams;
        List<string> teamNames =
        [
            .. labels.Select(label =>
                catalogTeams.FirstOrDefault(team => string.Equals(team.Name, label, StringComparison.OrdinalIgnoreCase))?.Name ?? label),
        ];

        string teamsRoot = Path.GetFullPath(Path.Combine(options.Value.DataDir, options.Value.Teams.Dir));
        bool complete = true;
        AgentsMdFile[] files = [.. AgentsMdLocator.Enumerate(teamsRoot, teamNames, _ => complete = false)];
        return (files, complete);
    }
}

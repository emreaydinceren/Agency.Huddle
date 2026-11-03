using Agency.Huddle.App.Skills;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Acp;

/// <summary>Builds the <see cref="RoleBlockInput"/> for a hired member from its identity, its Team and its skills (Task 29).</summary>
internal static class RoleBlockBuilder
{
    private const string ConsultWhenPrefix = "Consult When: ";

    /// <summary>Builds the role block for one member.</summary>
    /// <param name="identity">The member's identity, read from its file.</param>
    /// <param name="skills">The member's resolved skills.</param>
    /// <param name="teamCatalog">The Teams, read for the member's colleagues.</param>
    /// <param name="getPersona">Finds a colleague's Persona by Name, or <see langword="null"/>.</param>
    /// <returns>The block, or <see langword="null"/> when the member has no <c>_role</c> or no Team.</returns>
    internal static RoleBlockInput? Build(
        PersonaIdentity identity,
        IReadOnlyList<Skill> skills,
        ITeamCatalog teamCatalog,
        Func<string, Persona?> getPersona)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(skills);
        ArgumentNullException.ThrowIfNull(teamCatalog);
        ArgumentNullException.ThrowIfNull(getPersona);

        if (identity.Role is null || identity.Teams.Count == 0)
        {
            return null;
        }

        string teamName = identity.Teams[0];
        TeamSummary? team = teamCatalog.Find(teamName);

        List<string> colleagues = [];
        if (team is not null)
        {
            if (team.Manager is not null)
            {
                colleagues.Add(team.Manager);
            }

            colleagues.AddRange(team.Members);
        }

        List<ConsultLine> consult = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase) { identity.Name };
        foreach (string colleague in colleagues)
        {
            if (!seen.Add(colleague) || getPersona(colleague) is not { } persona)
            {
                continue;
            }

            if (PersonaFrontmatter.TryReadIdentity(persona.Text, out PersonaIdentity? colleagueIdentity, out _))
            {
                consult.Add(new ConsultLine(persona.Name, colleagueIdentity.Title, ConsultWhen(persona.Text)));
            }
        }

        string displayTeam = team?.Name ?? teamName;
        return new RoleBlockInput(
            identity.Name,
            identity.Title,
            displayTeam,
            consult,
            [.. skills.Select(static skill => new SkillLine(skill.Name, skill.Description))]);
    }

    /// <summary>Reads the <c>consult_when</c> line out of a colleague's public job description.</summary>
    /// <param name="personaText">The colleague's file text.</param>
    /// <returns>The line's value, or an empty string when the colleague states none.</returns>
    private static string ConsultWhen(string personaText)
    {
        foreach (string line in PersonaFrontmatter.ComposeJobDescription(personaText).Split('\n'))
        {
            if (line.StartsWith(ConsultWhenPrefix, StringComparison.Ordinal))
            {
                return line[ConsultWhenPrefix.Length..].Trim();
            }
        }

        return string.Empty;
    }
}

using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Tasks;

/// <summary>
/// <see cref="IRequestConfirmers"/> over the Team catalog, the Persona files and the process gates' role coverage.
/// </summary>
/// <param name="teams">The Teams and their members.</param>
/// <param name="personas">The Persona files, for each member's role.</param>
/// <param name="gates">The process gates, for what a role covers.</param>
internal sealed class RequestConfirmers(ITeamCatalog teams, PersonaStore personas, ProcessGateService gates) : IRequestConfirmers
{
    /// <summary>The role coverage step a confirming member must have.</summary>
    private const string VerifyCoverage = "verify";

    /// <inheritdoc />
    public bool IsVerifierOf(string team, string member)
    {
        TeamSummary? summary = teams.Find(team);
        if (summary is null || !summary.Members.Contains(member, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        return personas.Get(member) is { } persona
            && PersonaFrontmatter.TryReadIdentity(persona.Text, out PersonaIdentity? identity, out _)
            && identity.Role is { } role
            && gates.CoverageOf(role) is { } coverage
            && coverage.Split(',', StringSplitOptions.TrimEntries).Contains(VerifyCoverage, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public bool TeamHasConfirmer(string team)
    {
        TeamSummary? summary = teams.Find(team);
        return summary is not null && summary.Members.Any(member => this.IsVerifierOf(team, member));
    }
}

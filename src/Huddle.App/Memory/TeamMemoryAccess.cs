using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Agents;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Memory;

/// <summary>
/// Who may read and who may write a Team's Memory (Instructions spec §5.3, §5.5): every member and the manager read it, and
/// only the manager writes it. The Chief of Staff, which is in no Team, may resolve and write every Team, as
/// <c>list_teams</c> already lets it see every Team.
/// </summary>
internal sealed class TeamMemoryAccess
{
    private readonly ITeamCatalog teams;
    private readonly Func<string, bool> isChiefOfStaff;

    /// <summary>Initializes a new instance of the <see cref="TeamMemoryAccess"/> class.</summary>
    /// <param name="teams">The Teams, their members, managers and Projects.</param>
    /// <param name="isChiefOfStaff">Whether the named Persona is the built-in Chief of Staff.</param>
    public TeamMemoryAccess(ITeamCatalog teams, Func<string, bool> isChiefOfStaff)
    {
        ArgumentNullException.ThrowIfNull(teams);
        ArgumentNullException.ThrowIfNull(isChiefOfStaff);

        this.teams = teams;
        this.isChiefOfStaff = isChiefOfStaff;
    }

    /// <summary>Builds the access rules over the live Persona list, where the Chief of Staff is the Persona carrying its marker.</summary>
    /// <param name="teams">The Teams.</param>
    /// <param name="personas">The Personas.</param>
    /// <returns>The access rules.</returns>
    public static TeamMemoryAccess ForPersonas(ITeamCatalog teams, PersonaStore personas)
    {
        ArgumentNullException.ThrowIfNull(personas);

        return new TeamMemoryAccess(
            teams,
            name => personas.Entries.Any(entry =>
                string.Equals(entry.Builtin, BuiltinAgent.ChiefOfStaffMarker, StringComparison.Ordinal)
                && string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>Finds a Team by name, ignoring case.</summary>
    /// <param name="team">The Team's name as written.</param>
    /// <returns>The Team, or <see langword="null"/> when there is none.</returns>
    public TeamSummary? Find(string team) => this.teams.Find(team);

    /// <summary>Whether the Persona is the Chief of Staff.</summary>
    /// <param name="persona">The Persona's name.</param>
    public bool IsChiefOfStaff(string persona) => this.isChiefOfStaff(persona);

    /// <summary>Whether the Persona may read the Team's Memory: a member, the manager, or the Chief of Staff.</summary>
    /// <param name="persona">The Persona's name.</param>
    /// <param name="team">The Team.</param>
    public bool CanRead(string persona, TeamSummary team)
    {
        ArgumentNullException.ThrowIfNull(team);

        return CanRead(persona, team, this.isChiefOfStaff(persona));
    }

    /// <summary>Whether the Persona may write the Team's Memory: its manager, or the Chief of Staff.</summary>
    /// <param name="persona">The Persona's name.</param>
    /// <param name="team">The Team.</param>
    public bool CanWrite(string persona, TeamSummary team)
    {
        ArgumentNullException.ThrowIfNull(team);

        return CanWrite(persona, team, this.isChiefOfStaff(persona));
    }

    /// <summary>
    /// A test of whether the Persona may write a Team's Memory that looks the Persona up once, for a caller that asks about several Teams in one
    /// decision (the permission guard on the hot path of every file tool call).
    /// </summary>
    /// <param name="persona">The Persona's name.</param>
    /// <returns>A predicate over a Team.</returns>
    public Func<TeamSummary, bool> WriterFor(string persona)
    {
        bool chief = this.isChiefOfStaff(persona);
        return team => CanWrite(persona, team, chief);
    }

    /// <summary>Every Team in the catalog, in its order.</summary>
    public IReadOnlyList<TeamSummary> AllTeams => this.teams.Teams;

    /// <summary>The Teams the Persona may read, in the catalog's order.</summary>
    /// <param name="persona">The Persona's name.</param>
    public IReadOnlyList<TeamSummary> Readable(string persona)
    {
        bool chief = this.isChiefOfStaff(persona);
        return [.. this.teams.Teams.Where(team => team.HasFolder && CanRead(persona, team, chief))];
    }

    /// <summary>Whether the Persona may read the Team's Memory, given whether it is the Chief of Staff.</summary>
    /// <param name="persona">The Persona's name.</param>
    /// <param name="team">The Team.</param>
    /// <param name="chief">Whether the Persona is the Chief of Staff, resolved once by the caller.</param>
    private static bool CanRead(string persona, TeamSummary team, bool chief) =>
        CanWrite(persona, team, chief) || team.Members.Contains(persona, StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether the Persona may write the Team's Memory, given whether it is the Chief of Staff.</summary>
    /// <param name="persona">The Persona's name.</param>
    /// <param name="team">The Team.</param>
    /// <param name="chief">Whether the Persona is the Chief of Staff, resolved once by the caller.</param>
    private static bool CanWrite(string persona, TeamSummary team, bool chief) =>
        chief || (team.Manager is { } manager && string.Equals(manager, persona, StringComparison.OrdinalIgnoreCase));
}

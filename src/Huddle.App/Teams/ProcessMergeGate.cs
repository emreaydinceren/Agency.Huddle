using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// Backs <see cref="IMergeGate"/> with the Team's process: resolves the Persona's Teams from its
/// <c>teams</c> line, collects the Evidence on each Team's Task files and asks
/// <see cref="ProcessGateService.CheckMerge"/> (PR-7, Spec §Gates).
/// </summary>
/// <param name="personas">Finds the Persona and its Teams line.</param>
/// <param name="tasks">Supplies the Team's Task files, whose Evidence sections the gate reads.</param>
/// <param name="gates">Decides the <c>checks-before-merge</c> gate.</param>
/// <param name="catalog">Maps a Teams-line label to the Team, so a stale label is ignored.</param>
internal sealed class ProcessMergeGate(PersonaStore personas, TaskStore tasks, ProcessGateService gates, ITeamCatalog catalog) : IMergeGate
{
    /// <inheritdoc />
    public string? RefusalFor(string personaName, string command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(personaName);
        ArgumentNullException.ThrowIfNull(command);

        if (personas.Get(personaName) is not { } persona
            || !PersonaFrontmatter.TryReadIdentity(persona.Text, out PersonaIdentity? identity, out _))
        {
            return null;
        }

        foreach (string label in identity.Teams)
        {
            if (catalog.Find(label) is not { } team)
            {
                continue;
            }

            if (gates.CheckMerge(team.Name, this.EvidenceOf(team.Name)) is { } refusal)
            {
                return refusal.Message;
            }
        }

        return null;
    }

    /// <summary>Reads the Evidence lines of every Task of the Team.</summary>
    /// <param name="team">The Team name.</param>
    /// <returns>All Evidence lines, in no particular order.</returns>
    private List<EvidenceLine> EvidenceOf(string team)
    {
        List<EvidenceLine> evidence = [];
        foreach (TaskItem task in tasks.All)
        {
            if (string.Equals(task.Location.Team, team, StringComparison.OrdinalIgnoreCase)
                && tasks.ReadText(task.Id) is { } text)
            {
                evidence.AddRange(EvidenceLine.ParseSection(text));
            }
        }

        return evidence;
    }
}

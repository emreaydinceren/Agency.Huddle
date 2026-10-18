using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teammates;

namespace Agency.Huddle.App.Teams;

/// <summary>What <see cref="CardResolver"/> needs to know about the machine: which adapters answered and which connectors are connected.</summary>
internal interface IHireContext
{
    /// <summary>Gets the adapters that answered their model probe.</summary>
    IReadOnlyList<AdapterAvailability> Answered { get; }

    /// <summary>Gets the configured model ids by adapter id, then tier.</summary>
    IReadOnlyDictionary<string, IReadOnlyDictionary<ModelTier, string>> TierMap { get; }

    /// <summary>Gets the adapter ids to avoid when another adapter answered.</summary>
    IReadOnlyCollection<string> AvoidAdapters { get; }

    /// <summary>Gets the connector status.</summary>
    IConnectorStatus Connectors { get; }
}

/// <summary>
/// Hires a Candidate that carries a role (HR-13): the card is resolved from the role through
/// <see cref="CardResolver"/> and written by <see cref="HireService"/>, so the Teammate gets the
/// role's skills, packs and model tier instead of the Candidate's own text.
/// </summary>
/// <param name="hires">Writes the Teammate.</param>
/// <param name="types">The team types, for the role.</param>
/// <param name="skills">The registry a card draws its Packs and Skills from.</param>
/// <param name="context">The adapters and connectors the card resolves against.</param>
internal sealed class RoleHirer(HireService hires, TeamTypeCatalog types, ISkillAvailability skills, IHireContext context)
{
    /// <summary>Hires <paramref name="candidate"/> into its first Team as its role.</summary>
    /// <param name="candidate">A Candidate with a <see cref="Candidate.Role"/> and at least one Team.</param>
    /// <returns>The hire outcome; a refusal writes nothing.</returns>
    public HireResult Hire(Candidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        string[] parts = candidate.Role?.Split('/') ?? [];
        TeamTypeManifest? type = parts.Length == 2 ? types.Find(parts[0]) : null;
        RoleSpec? role = type?.Roles.FirstOrDefault(r => string.Equals(r.Id, parts[1], StringComparison.Ordinal));
        if (type is null || role is null || candidate.Teams.Count == 0)
        {
            return new HireResult(false, $"{candidate.Name} does not name a known role and Team.", null);
        }

        CardState card = CardResolver.Resolve(role, [], context.Answered, context.TierMap, context.AvoidAdapters, skills, context.Connectors, candidate.Name);
        return hires.Hire(new HireRequest(candidate.Teams[0], type.Id, role.Id, card));
    }
}

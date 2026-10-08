using Agency.Huddle.App.TeamTypes;

namespace Agency.Huddle.App.Teams;

/// <summary>Whether a hire card can be hired as it stands.</summary>
internal enum CardReadiness
{
    /// <summary>Every default resolved; the card can be hired.</summary>
    Ready,

    /// <summary>A default is missing; the user must complete setup first.</summary>
    NeedsSetup,
}

/// <summary>Reports whether a connector, such as <c>email:read</c>, is connected.</summary>
internal interface IConnectorStatus
{
    /// <summary>Tells whether <paramref name="connector"/> is connected.</summary>
    /// <param name="connector">The connector id from a role's <c>requires</c>.</param>
    /// <returns><see langword="true"/> when the connector is connected.</returns>
    bool IsConnected(string connector);
}

/// <summary>A connector status that reports nothing connected; Task 82 replaces it.</summary>
internal sealed class EmptyConnectorStatus : IConnectorStatus
{
    /// <inheritdoc />
    public bool IsConnected(string connector) => false;
}

/// <summary>A hire card with every default resolved (HR-6, HR-7).</summary>
/// <param name="RoleId">The role the card hires.</param>
/// <param name="Name">The resolved Teammate name.</param>
/// <param name="Pick">The adapter and model, or <see langword="null"/> when no adapter qualified.</param>
/// <param name="Skills">The role's skills plus its packs' skills, without duplicates.</param>
/// <param name="Packs">The role's packs that exist, at their version.</param>
/// <param name="Readiness">Ready, or Needs setup when a default is missing.</param>
/// <param name="Reasons">Why the card needs setup; empty when Ready.</param>
internal sealed record CardState(string RoleId, string Name, ModelPick? Pick, IReadOnlyList<SkillRef> Skills,
    IReadOnlyList<VersionedRef> Packs, CardReadiness Readiness, IReadOnlyList<string> Reasons);

/// <summary>Resolves every default on a hire card and decides Ready or Needs setup (HR-6, HR-7).</summary>
internal static class CardResolver
{
    /// <summary>Resolves a role's card. Needs setup only when no default exists: a name conflict, no adapter for the tier, or an unmet connector.</summary>
    /// <param name="role">The role being hired.</param>
    /// <param name="namesInUse">The Teammate names already taken, compared without case.</param>
    /// <param name="answered">The adapters that answered their model probe.</param>
    /// <param name="tierMap">Configured model ids by adapter id, then tier.</param>
    /// <param name="avoidAdapters">Adapter ids to avoid when another adapter answered.</param>
    /// <param name="packs">The known skill packs.</param>
    /// <param name="connectors">The connector status.</param>
    /// <param name="chosenName">A name the user chose, or <see langword="null"/> to take the first free suggestion.</param>
    /// <returns>The resolved card.</returns>
    public static CardState Resolve(RoleSpec role, IReadOnlyCollection<string> namesInUse, IReadOnlyList<AdapterAvailability> answered,
        IReadOnlyDictionary<string, IReadOnlyDictionary<ModelTier, string>> tierMap, IReadOnlyCollection<string> avoidAdapters,
        IReadOnlyList<SkillPack> packs, IConnectorStatus connectors, string? chosenName = null)
    {
        ArgumentNullException.ThrowIfNull(role);
        ArgumentNullException.ThrowIfNull(namesInUse);
        ArgumentNullException.ThrowIfNull(packs);
        ArgumentNullException.ThrowIfNull(connectors);

        List<string> reasons = [];

        string name;
        if (chosenName is not null)
        {
            name = chosenName;
            if (CardResolver.InUse(namesInUse, name))
            {
                reasons.Add("name conflict");
            }
        }
        else
        {
            string? free = role.SuggestedNames.FirstOrDefault(suggested => !CardResolver.InUse(namesInUse, suggested));
            name = free ?? (role.SuggestedNames.Count > 0 ? role.SuggestedNames[0] : string.Empty);
            if (free is null)
            {
                reasons.Add("name conflict");
            }
        }

        ModelPick? pick = TierModelPicker.Pick(role.Model.Tier, answered, tierMap, avoidAdapters);
        if (pick is null)
        {
            reasons.Add($"no logged-in adapter for tier {role.Model.Tier}");
        }

        foreach (string connector in role.Requires)
        {
            if (!connectors.IsConnected(connector))
            {
                reasons.Add($"connector {connector} not connected");
            }
        }

        List<SkillRef> skills = [.. role.Skills];
        List<VersionedRef> usedPacks = [];
        foreach (string packId in role.Packs)
        {
            SkillPack? pack = packs.FirstOrDefault(candidate => string.Equals(candidate.Id, packId, StringComparison.Ordinal));
            if (pack is null)
            {
                continue;
            }

            usedPacks.Add(new VersionedRef(pack.Id, pack.Version));
            skills.AddRange(pack.Skills);
        }

        IReadOnlyList<SkillRef> distinctSkills = [.. skills.DistinctBy(static skill => skill.Name, StringComparer.Ordinal)];
        CardReadiness readiness = reasons.Count == 0 ? CardReadiness.Ready : CardReadiness.NeedsSetup;
        return new CardState(role.Id, name, pick, distinctSkills, usedPacks, readiness, reasons);
    }

    /// <summary>Tells whether <paramref name="name"/> is in <paramref name="namesInUse"/>, without regard to case.</summary>
    private static bool InUse(IReadOnlyCollection<string> namesInUse, string name)
    {
        return namesInUse.Contains(name, StringComparer.OrdinalIgnoreCase);
    }
}

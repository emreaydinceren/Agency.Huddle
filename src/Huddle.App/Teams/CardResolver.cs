using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.SkillPacks.Resolving;

namespace Agency.Huddle.App.Teams;

/// <summary>Resolves every default on a hire card and decides Ready or Needs setup (HR-6, HR-7).</summary>
internal static class CardResolver
{
    /// <summary>The <see cref="SkillRef.Source"/> every resolved Skill carries: Skills resolve by name through the registry,
    /// so a ref's own source and path are ignored (Skill Packs spec §6.10.2).</summary>
    public const string RegistrySource = "registry";

    /// <summary>Resolves a role's card. Needs setup when a default is missing (a name conflict, no adapter for the tier) or a
    /// Pack or Skill is not ready to hire. A connector that is not connected is listed in <see cref="CardState.Unconnected"/>
    /// and never blocks.</summary>
    /// <param name="role">The role being hired.</param>
    /// <param name="namesInUse">The Agent names already taken, compared without case.</param>
    /// <param name="answered">The adapters that answered their model probe.</param>
    /// <param name="tierMap">Configured model ids by adapter id, then tier.</param>
    /// <param name="avoidAdapters">Adapter ids to avoid when another adapter answered.</param>
    /// <param name="skills">The registry: which Packs exist and which Skills are ready.</param>
    /// <param name="connectors">The connector status.</param>
    /// <param name="chosenName">A name the user chose, or <see langword="null"/> to take the first free suggestion.</param>
    /// <returns>The resolved card.</returns>
    public static CardState Resolve(RoleSpec role, IReadOnlyCollection<string> namesInUse, IReadOnlyList<AdapterAvailability> answered,
        IReadOnlyDictionary<string, IReadOnlyDictionary<ModelTier, string>> tierMap, IReadOnlyCollection<string> avoidAdapters,
        ISkillAvailability skills, IConnectorStatus connectors, string? chosenName = null)
    {
        ArgumentNullException.ThrowIfNull(role);
        ArgumentNullException.ThrowIfNull(namesInUse);
        ArgumentNullException.ThrowIfNull(skills);
        ArgumentNullException.ThrowIfNull(connectors);

        List<string> reasons = [];

        string name = CardResolver.ChooseName(role, namesInUse, chosenName, reasons);

        ModelPick? pick = TierModelPicker.Pick(role.Model.Tier, answered, tierMap, avoidAdapters);
        if (pick is null)
        {
            reasons.Add($"no logged-in adapter for tier {role.Model.Tier}");
        }

        List<string> unconnected = [.. role.Requires.Where(connector => !connectors.IsConnected(connector))];

        List<string> names = [.. role.Skills.Select(static skill => skill.Name)];
        List<VersionedRef> usedPacks = [];
        foreach (PackEntry pack in CardResolver.UsedPacks(role, skills))
        {
            usedPacks.Add(new VersionedRef(pack.Id, pack.Version));
            names.AddRange(pack.SkillNames);
        }

        IReadOnlyList<SkillSetupItem> setup = CardResolver.SkillSetupOf(role, skills);
        reasons.AddRange(setup.Select(SkillSetupText.Reason));
        IReadOnlyList<SkillRef> distinctSkills = [.. names.Distinct(StringComparer.Ordinal).Select(static skillName => new SkillRef(skillName, RegistrySource, null))];
        CardReadiness readiness = reasons.Count == 0 ? CardReadiness.Ready : CardReadiness.NeedsSetup;
        return new CardState(role.Id, name, pick, distinctSkills, usedPacks, readiness, reasons, setup, unconnected);
    }

    /// <summary>What a role needs set up before it can be hired: the one rule <see cref="Resolve"/> and the Candidate check share.</summary>
    /// <param name="role">The role being hired.</param>
    /// <param name="skills">The registry: which Packs exist and which Skills are ready.</param>
    /// <returns>One item per Skill or Pack that is not ready; empty when the role is ready.</returns>
    public static IReadOnlyList<SkillSetupItem> SkillSetupOf(RoleSpec role, ISkillAvailability skills)
    {
        ArgumentNullException.ThrowIfNull(role);
        ArgumentNullException.ThrowIfNull(skills);

        List<VersionedRef> usedPacks = [.. CardResolver.UsedPacks(role, skills).Select(static pack => new VersionedRef(pack.Id, pack.Version))];
        return SkillSetupCheck.For([.. role.Skills.Select(static skill => skill.Name)], usedPacks, skills);
    }

    /// <summary>The role's Packs the registry finds. A Pack no row provides is skipped silently: R008 warns on the Packs tab, and a
    /// stock install with no skills repository must still be able to hire a role that names one.</summary>
    private static List<PackEntry> UsedPacks(RoleSpec role, ISkillAvailability skills)
    {
        List<PackEntry> found = [];
        foreach (string packId in role.Packs)
        {
            if (skills.FindPack(packId) is { } pack)
            {
                found.Add(pack);
            }
        }

        return found;
    }

    /// <summary>Picks the card's name: the chosen one, else the first free suggestion, else the first suggestion.</summary>
    /// <param name="role">The role being hired.</param>
    /// <param name="namesInUse">The Agent names already taken, compared without case.</param>
    /// <param name="chosenName">A name the user chose, or <see langword="null"/> to take the first free suggestion.</param>
    /// <param name="reasons">Receives "name conflict" when the name is taken.</param>
    /// <returns>The name for the card.</returns>
    private static string ChooseName(RoleSpec role, IReadOnlyCollection<string> namesInUse, string? chosenName, List<string> reasons)
    {
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

        return name;
    }

    /// <summary>Tells whether <paramref name="name"/> is in <paramref name="namesInUse"/>, without regard to case.</summary>
    private static bool InUse(IReadOnlyCollection<string> namesInUse, string name)
    {
        return namesInUse.Contains(name, StringComparer.OrdinalIgnoreCase);
    }
}

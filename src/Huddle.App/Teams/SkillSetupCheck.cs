using Agency.Huddle.SkillPacks.Resolving;

namespace Agency.Huddle.App.Teams;

/// <summary>The one function that decides what a card or draft needs before it can be hired (Skill Packs spec §6.10.2):
/// used by <see cref="CardResolver"/> for a role's defaults and by Build a team again after a draft is edited.</summary>
internal static class SkillSetupCheck
{
    /// <summary>One item per Pack that is not ready and one per other Skill that is not ready, in the order given.</summary>
    /// <param name="skillNames">The Skill names the card carries. Those a listed Pack provides are judged as part of that Pack.</param>
    /// <param name="packs">The Packs the card carries. One no row provides is skipped.</param>
    /// <param name="skills">The registry.</param>
    /// <returns>Empty when everything is ready.</returns>
    public static IReadOnlyList<SkillSetupItem> For(IReadOnlyList<string> skillNames, IReadOnlyList<VersionedRef> packs, ISkillAvailability skills)
    {
        ArgumentNullException.ThrowIfNull(skillNames);
        ArgumentNullException.ThrowIfNull(packs);
        ArgumentNullException.ThrowIfNull(skills);

        List<SkillSetupItem> items = [];
        HashSet<string> coveredByPacks = new(StringComparer.Ordinal);
        foreach (VersionedRef reference in packs.DistinctBy(static pack => pack.Id, StringComparer.Ordinal))
        {
            if (skills.FindPack(reference.Id) is not { } pack)
            {
                continue;
            }

            coveredByPacks.UnionWith(pack.SkillNames);
            if ((skills.SetupItem(SkillSetupKind.Pack, pack.Id) ?? PackItemFromSkills(pack, skills)) is { } item)
            {
                items.Add(item);
            }
        }

        foreach (string name in skillNames.Distinct(StringComparer.Ordinal).Where(name => !coveredByPacks.Contains(name)))
        {
            if (skills.SetupItem(SkillSetupKind.Skill, name) is { } item)
            {
                items.Add(item);
            }
        }

        return items;
    }

    /// <summary>The first problem among a Pack's Skills, as one item about the Pack, so a Pack with two missing Skills is one
    /// item, not three.</summary>
    private static SkillSetupItem? PackItemFromSkills(PackEntry pack, ISkillAvailability skills)
    {
        foreach (string name in pack.SkillNames)
        {
            if (skills.SetupItem(SkillSetupKind.Skill, name) is not { } problem)
            {
                continue;
            }

            string detail = problem.Problem switch
            {
                SkillAvailability.NotDownloaded => pack.Title,
                SkillAvailability.Unknown => name,
                _ => problem.Detail,
            };
            return new SkillSetupItem(SkillSetupKind.Pack, pack.Id, problem.Problem, detail);
        }

        return null;
    }
}

using Agency.Huddle.App.TeamTypes;
using System.Globalization;

namespace Agency.Huddle.App.Teams;

/// <summary>Computes the Complete Setup warnings for one member (MS-3, MS-5, D8).</summary>
internal static class MemberWarnings
{
    private const int MaxSkills = 10;

    /// <summary>The warnings about what the Teammate holds from its Sources (Skill Packs spec §6.10.7): a withdrawn Pack or
    /// Skill, a deprecated Pack, and a Pack no SKILLS.md row provides any more. In that order.</summary>
    /// <param name="name">The Teammate's name.</param>
    /// <param name="skills">Its <c>skills:</c>.</param>
    /// <param name="packs">Its <c>_packs</c>.</param>
    /// <param name="availability">The registry.</param>
    /// <param name="deprecatedReason">The Source's reason when it marks a Pack deprecated, else null.</param>
    public static IReadOnlyList<string> ForSources(string name, IReadOnlyList<string> skills, IReadOnlyList<VersionedRef> packs, ISkillAvailability availability, Func<string, string?> deprecatedReason)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(skills);
        ArgumentNullException.ThrowIfNull(packs);
        ArgumentNullException.ThrowIfNull(availability);
        ArgumentNullException.ThrowIfNull(deprecatedReason);

        List<string> withdrawn = [];
        List<string> deprecated = [];
        List<string> gone = [];
        foreach (VersionedRef pack in packs)
        {
            if (availability.FindPack(pack.Id) is null)
            {
                gone.Add($"Pack \"{pack.Id}\" is no longer in SKILLS.md. {name} keeps its Skills; remove the Pack here, or ask your AI tool to add it back.");
            }
            else if (availability.SetupItem(SkillSetupKind.Pack, pack.Id) is { Problem: SkillAvailability.Withdrawn } item)
            {
                withdrawn.Add($"Pack \"{pack.Id}\" was withdrawn by its Source: {Reason(item.Detail)}.");
            }
            else if (deprecatedReason(pack.Id) is { } reason)
            {
                deprecated.Add($"Pack \"{pack.Id}\" is deprecated: {Reason(reason)}.");
            }
        }

        foreach (string skill in skills)
        {
            if (availability.SetupItem(SkillSetupKind.Skill, skill) is { Problem: SkillAvailability.Withdrawn } item)
            {
                withdrawn.Add($"Skill \"{skill}\" was withdrawn by its Source: {Reason(item.Detail)}.");
            }
        }

        return [.. withdrawn, .. deprecated, .. gone];
    }

    /// <summary>The reason without a trailing full stop, or "no reason given".</summary>
    private static string Reason(string? text)
    {
        return string.IsNullOrWhiteSpace(text) ? "no reason given" : text.Trim().TrimEnd('.');
    }

    /// <summary>Returns the warnings for a member: shared vendor, too many skills, then skills sharing a sub-category.</summary>
    /// <param name="member">The member to check.</param>
    /// <param name="role">The member's role.</param>
    /// <param name="teamMembers">Every member of the Team.</param>
    /// <param name='subCategoryOf'>Returns the catalog sub-category of a skill, or null when unknown.</param>
    /// <returns>Each warning as a full sentence; empty when the member is clean.</returns>
    public static IReadOnlyList<string> For(MemberView member, RoleSpec role, IReadOnlyList<MemberView> teamMembers, Func<string, string?> subCategoryOf)
    {
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(role);
        ArgumentNullException.ThrowIfNull(teamMembers);
        ArgumentNullException.ThrowIfNull(subCategoryOf);

        List<string> warnings = [];

        if (member.AdapterId is not null)
        {
            foreach (MemberView other in teamMembers)
            {
                if (!string.Equals(other.Name, member.Name, StringComparison.Ordinal)
                    && string.Equals(other.AdapterId, member.AdapterId, StringComparison.Ordinal)
                    && role.Model.DifferentVendorFrom.Contains(other.RoleId, StringComparer.Ordinal))
                {
                    warnings.Add($"{role.Title} {member.Name} runs on the same vendor as {MemberWarnings.Capitalise(other.RoleId)} {other.Name}.");
                }
            }
        }

        if (member.Skills.Count > MemberWarnings.MaxSkills)
        {
            warnings.Add(string.Create(CultureInfo.InvariantCulture, $"{member.Name} has {member.Skills.Count} skills; more than {MemberWarnings.MaxSkills} makes skill choice harder."));
        }

        List<string> subCategories = [];
        Dictionary<string, List<string>> bySubCategory = new(StringComparer.Ordinal);
        foreach (string skill in member.Skills.Distinct(StringComparer.Ordinal))
        {
            string? subCategory = subCategoryOf(skill);
            if (subCategory is null)
            {
                continue;
            }

            if (!bySubCategory.TryGetValue(subCategory, out List<string>? group))
            {
                group = [];
                bySubCategory[subCategory] = group;
                subCategories.Add(subCategory);
            }

            group.Add(skill);
        }

        foreach (string subCategory in subCategories.Where(name => bySubCategory[name].Count > 1))
        {
            List<string> group = bySubCategory[subCategory];
            string list = group.Count == 2
                ? $"{group[0]} and {group[1]} are both"
                : $"{string.Join(", ", group.Take(group.Count - 1))} and {group[^1]} are all";
            warnings.Add($"{list} in {subCategory}.");
        }

        return warnings;
    }

    private static string Capitalise(string roleId)
    {
        return roleId.Length == 0 ? roleId : string.Concat(char.ToUpperInvariant(roleId[0]).ToString(), roleId.AsSpan(1));
    }
}

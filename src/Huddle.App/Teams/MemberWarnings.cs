using System.Globalization;
using Agency.Huddle.App.TeamTypes;

namespace Agency.Huddle.App.Teams;

/// <summary>A member as the Complete Setup panel sees it, for warnings.</summary>
/// <param name="Name">The member's name.</param>
/// <param name="RoleId">The id of the member's role.</param>
/// <param name="AdapterId">The adapter the member runs on, or null when none is chosen.</param>
/// <param name="Skills">The names of the member's skills.</param>
internal sealed record MemberView(string Name, string RoleId, string? AdapterId, IReadOnlyList<string> Skills);

/// <summary>Computes the Complete Setup warnings for one member (MS-3, MS-5, D8).</summary>
internal static class MemberWarnings
{
    private const int MaxSkills = 10;

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

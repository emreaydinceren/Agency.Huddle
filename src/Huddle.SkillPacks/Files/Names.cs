using System.Text.RegularExpressions;

namespace Agency.Huddle.SkillPacks.Files;

/// <summary>The naming rules every file shares (§7.2): the Skill name rule and the id rule.</summary>
public static partial class Names
{
    /// <summary>The longest Skill name or id.</summary>
    public const int MaxLength = 64;

    /// <summary>Whether <paramref name="name"/> follows the Skill name rule: lowercase letters and digits in
    /// hyphen-separated words, 1 to 64 characters.</summary>
    public static bool IsSkillName(string? name)
    {
        return name is { Length: > 0 and <= MaxLength } && NameRule().IsMatch(name);
    }

    /// <summary>Whether <paramref name="id"/> follows the id rule (the same pattern as a Skill name).</summary>
    public static bool IsId(string? id)
    {
        return IsSkillName(id);
    }

    [GeneratedRegex(@"\A[a-z0-9]+(?:-[a-z0-9]+)*\z", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex NameRule();
}

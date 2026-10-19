using Agency.Huddle.SkillPacks.Checking;
using Agency.Huddle.SkillPacks.Resolving;

namespace Agency.Huddle.App.SkillSources;

/// <summary>One published picture of the registry. Replaced whole, never edited (§6.5).</summary>
/// <param name="Version">Starts at 1, +1 per publish; lets a component tell "same picture" cheaply.</param>
/// <param name="Resolved">What the resolver produced.</param>
/// <param name="Checked">What <c>Checks.Run</c> produced: every finding, and the state of every Pack, catalog and Skill.</param>
/// <param name="NewKeys">Row keys and <c>skill:&lt;name&gt;</c> keys in this snapshot and not in <c>seen</c>.</param>
/// <param name="PackHolders">Pack id to the names of the Teammates whose <c>_packs</c> names it.</param>
/// <param name="SkillHolders">Skill name to the names of the Teammates whose <c>skills:</c> holds it.</param>
/// <param name="HiddenPacks">Pack ids in <c>hidden.json</c>.</param>
/// <param name="HiddenSkills">Skill names in <c>hidden.json</c>.</param>
/// <param name="PackHeldVersions">Pack id to the versions its holders' <c>_packs</c> entries name.</param>
internal sealed record RegistrySnapshot(
    long Version,
    ResolvedRegistry Resolved,
    CheckResult Checked,
    IReadOnlySet<string> NewKeys,
    IReadOnlyDictionary<string, IReadOnlyList<string>> PackHolders,
    IReadOnlyDictionary<string, IReadOnlyList<string>> SkillHolders,
    IReadOnlySet<string> HiddenPacks,
    IReadOnlySet<string> HiddenSkills,
    IReadOnlyDictionary<string, IReadOnlySet<string>> PackHeldVersions)
{
    /// <summary>The empty picture before the first read.</summary>
    public static RegistrySnapshot Empty { get; } = new(
        0,
        ResolvedRegistry.Empty,
        CheckResult.Empty,
        new HashSet<string>(StringComparer.Ordinal),
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        new HashSet<string>(StringComparer.Ordinal),
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase));
}

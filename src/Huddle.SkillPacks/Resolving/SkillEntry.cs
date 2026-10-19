using Agency.Huddle.SkillPacks.Checking;

namespace Agency.Huddle.SkillPacks.Resolving;

/// <summary>One Skill, once per distinct name in the whole registry (§8.3 step 4).</summary>
/// <param name="Name">The Skill name; also the folder name in <c>Skills/</c>.</param>
/// <param name="Description">From the catalog entry, else from the folder's SKILL.md, else empty.</param>
/// <param name="Publisher">From the catalog entry.</param>
/// <param name="License">From the catalog entry.</param>
/// <param name="Category">From the catalog entry.</param>
/// <param name="SubCategory">From the catalog entry.</param>
/// <param name="Requires">Skill names this one depends on.</param>
/// <param name="Origin">The pinned URL of the first provider; the identity two places must share (D003).</param>
/// <param name="Source">The Source id of the first provider's file.</param>
/// <param name="FolderPath">The full path of the Skill's folder on this computer; null when it has no path here.</param>
/// <param name="Providers">Where the Skill is listed, in registry order. The first provider's data wins.</param>
/// <param name="PointerSha256">The hash of the folder at the pointer; null when not downloaded.</param>
/// <param name="InstalledSha256">The <c>sha256</c> of <c>Skills/&lt;name&gt;/.source.json</c> when that copy exists and has the same origin.</param>
/// <param name="Findings">The loading findings about this Skill.</param>
/// <param name="PathState">Where the first provider's path leaves the Skill on this computer.</param>
/// <param name="Claims">Every provider's claim, the first first, including ones whose origin differs (D003).</param>
public sealed record SkillEntry(
    string Name,
    string Description,
    string? Publisher,
    string? License,
    string? Category,
    string? SubCategory,
    IReadOnlyList<string> Requires,
    string Origin,
    string? Source,
    string? FolderPath,
    IReadOnlyList<string> Providers,
    string? PointerSha256,
    string? InstalledSha256,
    IReadOnlyList<Finding> Findings,
    SkillPathState PathState,
    IReadOnlyList<SkillClaim> Claims)
{
    /// <summary>The key of this Skill in <see cref="CheckResult.States"/>.</summary>
    public string StateKey => $"skill:{this.Name}";

    /// <summary>True when the pointer's files differ from the copy in <c>Skills/</c> (§8.4).</summary>
    public bool HasUpdate => this.PointerSha256 is not null && this.InstalledSha256 is not null
        && !string.Equals(this.PointerSha256, this.InstalledSha256, StringComparison.Ordinal);
}

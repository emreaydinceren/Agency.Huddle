using Agency.Huddle.SkillPacks.Reading;

namespace Agency.Huddle.SkillPacks.Files;

/// <summary>One Skill a catalog lists. <c>Path</c> is empty until the Skill is fetched.</summary>
/// <param name="Name">The Skill name.</param>
/// <param name="Description">One line on what the Skill does.</param>
/// <param name="Origin">The pinned URL.</param>
/// <param name="Publisher">Who publishes it.</param>
/// <param name="License">The licence the catalog declares.</param>
/// <param name="Category">The category, for grouping.</param>
/// <param name="SubCategory">The sub-category, for grouping.</param>
/// <param name="Requires">Skill names this one depends on.</param>
/// <param name="Path">Where the Skill's folder is on each computer; empty until fetched.</param>
/// <param name="Sha256">The <see cref="Folders.SkillFolderHash.ComputeCatalog"/> of the Skill's folder at the pinned commit; null when the
/// catalog does not pin content (a Human's own Source), and then nothing is compared.</param>
public sealed record CatalogEntry(
    string Name,
    string Description,
    string Origin,
    string? Publisher,
    string? License,
    string? Category,
    string? SubCategory,
    IReadOnlyList<string> Requires,
    PathEntries Path,
    string? Sha256 = null);

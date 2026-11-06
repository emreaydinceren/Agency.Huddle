namespace Agency.Huddle.App.SkillSources.Move;

/// <summary>One entry of the old <c>SkillsCatalog.json</c>.</summary>
/// <param name="Id">The old id, <c>owner/repo/skill</c>.</param>
/// <param name="Name">The Skill's folder name.</param>
/// <param name="Description">The description.</param>
/// <param name="Publisher">Who publishes it.</param>
/// <param name="Repo">The repository URL.</param>
/// <param name="Path">The folder inside the repository.</param>
/// <param name="Link">A commit-pinned browse URL.</param>
/// <param name="Commit">The commit the entry was generated from.</param>
/// <param name="License">The licence.</param>
/// <param name="Category">The category.</param>
/// <param name="SubCategory">The sub-category.</param>
/// <param name="Requires">What the Skill needs.</param>
/// <param name="Sha256">The folder hash at <paramref name="Commit"/> (<c>SkillFolderHash</c>); null in a catalog generated before it was recorded.</param>
internal sealed record LegacyCatalogEntry(
    string Id,
    string Name,
    string Description,
    string Publisher,
    string Repo,
    string Path,
    string Link,
    string Commit,
    string License,
    string Category,
    string SubCategory,
    IReadOnlyList<string> Requires,
    string? Sha256 = null);

using Agency.Huddle.SkillPacks.Checking;
using Agency.Huddle.SkillPacks.Files;
using Agency.Huddle.SkillPacks.Reading;

namespace Agency.Huddle.SkillPacks.Resolving;

/// <summary>A catalog as resolved against this computer.</summary>
/// <param name="Row">The Catalogs row.</param>
/// <param name="FilePath">The full path the row resolved to.</param>
/// <param name="File">The parsed file, or null.</param>
/// <param name="Id">The catalog id, or an empty string.</param>
/// <param name="Title">The title, or the file name.</param>
/// <param name="Source">The Source id.</param>
/// <param name="SkillNames">The names it lists, in file order.</param>
/// <param name="DownloadedCount">How many of them have a folder on this computer.</param>
/// <param name="Findings">The loading findings about this catalog's file.</param>
/// <param name="DuplicateId">True when an earlier Catalogs row already has this id (D005).</param>
public sealed record ResolvedCatalog(
    RegistryRow Row,
    string? FilePath,
    CatalogFile? File,
    string Id,
    string Title,
    string Source,
    IReadOnlyList<string> SkillNames,
    int DownloadedCount,
    IReadOnlyList<Finding> Findings,
    bool DuplicateId = false)
{
    /// <summary>The key of this catalog in <see cref="CheckResult.States"/>.</summary>
    public string StateKey => this.File is null || this.Id.Length == 0 || this.DuplicateId
        ? $"row:catalog:{this.Row.Index.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
        : $"catalog:{this.Id}";
}

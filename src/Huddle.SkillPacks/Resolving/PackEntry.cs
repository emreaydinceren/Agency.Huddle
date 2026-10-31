using Agency.Huddle.SkillPacks.Checking;
using Agency.Huddle.SkillPacks.Files;
using Agency.Huddle.SkillPacks.Reading;

namespace Agency.Huddle.SkillPacks.Resolving;

/// <summary>A Pack as resolved against this computer.</summary>
/// <param name="Row">The Packs row that led to it.</param>
/// <param name="FilePath">The full path the row resolved to; null when it has no path on this computer.</param>
/// <param name="File">The parsed file; null when the file is missing, unreadable, invalid or a duplicate of an earlier row's file.</param>
/// <param name="Id">The Pack id, or an empty string when the file gave none.</param>
/// <param name="Version">The Pack version.</param>
/// <param name="Title">The title, or the file name when the file gave none.</param>
/// <param name="Source">The Source id from the file's <c>source</c> field.</param>
/// <param name="AppliesTo">Role ids.</param>
/// <param name="ConsultWhenAdds">The phrase added to <c>consult_when</c>.</param>
/// <param name="SkillNames">The names of the Pack's Skills, in file order.</param>
/// <param name="MissingSkills">Skills that are Not downloaded, so a card can name them.</param>
/// <param name="Findings">The loading findings about this Pack's file.</param>
/// <param name="DuplicateId">True when an earlier Packs row already has this id (P004): this row keeps a row key.</param>
public sealed record PackEntry(
    RegistryRow Row,
    string? FilePath,
    PackFile? File,
    string Id,
    string Version,
    string Title,
    string Source,
    IReadOnlyList<string> AppliesTo,
    string? ConsultWhenAdds,
    IReadOnlyList<string> SkillNames,
    IReadOnlyList<string> MissingSkills,
    IReadOnlyList<Finding> Findings,
    bool DuplicateId = false)
{
    /// <summary>The key of this Pack in <see cref="CheckResult.States"/>: <c>pack:&lt;id&gt;</c>, or <c>row:pack:&lt;index&gt;</c>
    /// for a file too broken to give an id.</summary>
    public string StateKey => this.File is null || this.Id.Length == 0 || this.DuplicateId
        ? $"row:pack:{this.Row.Index.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
        : $"pack:{this.Id}";
}

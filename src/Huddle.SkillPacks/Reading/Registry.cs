using Agency.Huddle.SkillPacks.Checking;

namespace Agency.Huddle.SkillPacks.Reading;

/// <summary>What <see cref="RegistryReader"/> found in SKILLS.md.</summary>
/// <param name="Directory">The folder SKILLS.md is in; relative paths resolve against it.</param>
/// <param name="Packs">The Packs table's rows, in file order. Empty when the table is missing.</param>
/// <param name="Catalogs">The Catalogs table's rows, in file order.</param>
/// <param name="Findings">The reader's findings: R001, R002, R003 (empty path), R006, R007, R010, R011, R012, R013.</param>
/// <param name="RowFindings">The findings among <paramref name="Findings"/> that belong to one row, with that row.</param>
public sealed record Registry(
    string Directory,
    IReadOnlyList<RegistryRow> Packs,
    IReadOnlyList<RegistryRow> Catalogs,
    IReadOnlyList<Finding> Findings,
    IReadOnlyList<RowFinding>? RowFindings = null)
{
    /// <summary>The row findings, never null.</summary>
    public IReadOnlyList<RowFinding> FindingsOfRows => this.RowFindings ?? [];

    /// <summary>The full path of SKILLS.md in <see cref="Directory"/>.</summary>
    public string FilePath => Path.Combine(this.Directory, RegistryReader.FileName);

    /// <summary>Both tables' rows in file order.</summary>
    public IReadOnlyList<RegistryRow> AllRows =>
        [.. this.Packs.Concat(this.Catalogs).OrderBy(static row => row.IsVirtual).ThenBy(static row => row.Span.FirstLine).ThenBy(static row => row.Kind).ThenBy(static row => row.Index)];
}
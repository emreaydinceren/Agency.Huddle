using Agency.Huddle.SkillPacks.Checking;

namespace Agency.Huddle.SkillPacks.Reading;

/// <summary>Which table a row came from.</summary>
public enum RowKind
{
    /// <summary>The Packs table.</summary>
    Pack,

    /// <summary>The Catalogs table.</summary>
    Catalog,
}

/// <summary>First and last line of a row in SKILLS.md, 1-based and inclusive (§8.1 step 7).</summary>
/// <param name="FirstLine">The row's first line.</param>
/// <param name="LastLine">The row's last line.</param>
public readonly record struct LineSpan(int FirstLine, int LastLine);

/// <summary>One row of the Packs or Catalogs table.</summary>
/// <param name="Kind">Which table.</param>
/// <param name="Index">0-based position in its table; the tie-breaker everywhere ("the first row wins").</param>
/// <param name="LocalPath">The <c>Local path</c> cell, split into entries.</param>
/// <param name="OriginUrl">The <c>Origin URL</c> cell as text, or null when empty or absent.</param>
/// <param name="LastUpdatedText">The <c>Last updated</c> cell as written, or null.</param>
/// <param name="LastUpdated">The cell parsed as <c>yyyy-MM-dd</c>, or null when absent or not a date (R006).</param>
/// <param name="Notes">The <c>Notes</c> cell, line breaks joined with a space, or null.</param>
/// <param name="Span">The row's lines in SKILLS.md; <c>default</c> for a virtual row.</param>
/// <param name="IsVirtual">True for a row that is not in SKILLS.md (<c>huddle-packs validate &lt;path&gt;</c>).</param>
public sealed record RegistryRow(
    RowKind Kind,
    int Index,
    PathEntries LocalPath,
    string? OriginUrl,
    string? LastUpdatedText,
    DateOnly? LastUpdated,
    string? Notes,
    LineSpan Span,
    bool IsVirtual = false)
{
    /// <summary>A virtual row for <paramref name="path"/>: one plain path entry, no other cell, no span.</summary>
    public static RegistryRow Virtual(RowKind kind, int index, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new RegistryRow(kind, index, new PathEntries([new PathEntry(null, path)]), null, null, null, null, default, IsVirtual: true);
    }

    /// <summary>The table name as SKILLS.md and messages write it: <c>Packs</c> or <c>Catalogs</c>.</summary>
    public string TableName => this.Kind == RowKind.Pack ? "Packs" : "Catalogs";
}

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

/// <summary>A reader finding and the row it is about.</summary>
/// <param name="Kind">The row's table.</param>
/// <param name="Index">The row's index in its table.</param>
/// <param name="Finding">The finding.</param>
public sealed record RowFinding(RowKind Kind, int Index, Finding Finding);
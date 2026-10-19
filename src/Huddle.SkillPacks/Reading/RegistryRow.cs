namespace Agency.Huddle.SkillPacks.Reading;

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
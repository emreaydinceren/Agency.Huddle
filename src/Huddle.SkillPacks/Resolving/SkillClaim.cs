using Agency.Huddle.SkillPacks.Reading;

namespace Agency.Huddle.SkillPacks.Resolving;

/// <summary>One place that lists a Skill: a Pack (by its own path or through a catalog) or a catalog.</summary>
/// <param name="Provider"><c>pack:&lt;id&gt;</c> or <c>catalog:&lt;id&gt;</c>.</param>
/// <param name="Kind">The table of the row that reaches it.</param>
/// <param name="RowIndex">The index of that row in its table.</param>
/// <param name="ProviderFile">The pack or catalog file that lists the Skill.</param>
/// <param name="Origin">The origin this provider gives.</param>
/// <param name="License">The licence this provider's catalog entry declares, or null.</param>
/// <param name="Path">The path this provider gives, or null when it gives none.</param>
/// <param name="BaseDirectory">The folder relative paths resolve against: the folder of the file the path is written in.</param>
/// <param name="ViaCatalog">The catalog a Pack Skill names, or null.</param>
public sealed record SkillClaim(
    string Provider,
    RowKind Kind,
    int RowIndex,
    string ProviderFile,
    string Origin,
    string? License,
    PathEntries? Path,
    string BaseDirectory,
    string? ViaCatalog);

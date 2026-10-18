namespace Agency.Huddle.SkillPacks.Checking;

/// <summary>What the checks need to know about one Huddle. Huddle builds it from its services; huddle-packs reads it
/// from install-context.json (§6.3, §6.7).</summary>
/// <param name="HuddleVersion">The Huddle version that wrote the context, for X002.</param>
/// <param name="SchemaVersion">The context file's schema version.</param>
/// <param name="ComputerName">This computer's name, for per-computer paths.</param>
/// <param name="DataDir">Huddle's data folder.</param>
/// <param name="SkillSourcesDir">The Skill Sources folder.</param>
/// <param name="SkillsDir">The <c>Skills/</c> folder that holds the copies.</param>
/// <param name="Roles">The roles a Pack's <c>appliesTo</c> may name.</param>
/// <param name="InstalledSkills">The Skill folders in <c>Skills/</c>.</param>
/// <param name="AppToolNames">The tools only built-in Skills may grant (S011).</param>
/// <param name="Written">When the context was built.</param>
public sealed record CheckContext(
    string HuddleVersion,
    int SchemaVersion,
    string ComputerName,
    string DataDir,
    string SkillSourcesDir,
    string SkillsDir,
    IReadOnlyList<RoleInfo> Roles,
    IReadOnlyList<InstalledSkill> InstalledSkills,
    IReadOnlyList<string> AppToolNames,
    DateTimeOffset Written);

/// <summary>A role a Pack's <c>appliesTo</c> may name.</summary>
/// <param name="Id">The role id.</param>
/// <param name="Title">The role's title.</param>
/// <param name="Description">One line on what the role does.</param>
/// <param name="SuggestedPacks">The Pack ids the hiring pool suggests for it (check R008).</param>
public sealed record RoleInfo(string Id, string Title, string Description, IReadOnlyList<string> SuggestedPacks);

/// <summary>A Skill folder in <c>Skills/</c>.</summary>
/// <param name="Name">The Skill name.</param>
/// <param name="Origin">The pinned origin the copy came from; null for built-in Skills and Skills the Human wrote.</param>
/// <param name="Sha256">The hash recorded in <c>.source.json</c>; null for built-in Skills and Skills the Human wrote.</param>
/// <param name="BuiltIn">True for a Skill that ships with Huddle.</param>
public sealed record InstalledSkill(string Name, string? Origin, string? Sha256, bool BuiltIn);

/// <summary>Switches for one run of the checks.</summary>
/// <param name="Strict">Raises S001 and R009 from warning to error. The AI tool sets it on what it just added.</param>
/// <param name="OnlyPath">Check only the rows whose resolved file is this path or lies under it; <see langword="null"/>
/// checks all. Registry-level findings are always reported; R008 is skipped when this is set.</param>
/// <param name="ToolVersion">The version of the program calling Run, for X002. <see langword="null"/> skips X002.</param>
public sealed record CheckOptions(
    bool Strict = false,
    string? OnlyPath = null,
    string? ToolVersion = null);

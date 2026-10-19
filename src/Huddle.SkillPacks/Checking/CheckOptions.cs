namespace Agency.Huddle.SkillPacks.Checking;

/// <summary>Switches for one run of the checks.</summary>
/// <param name="Strict">Raises S001 and R009 from warning to error. The AI tool sets it on what it just added.</param>
/// <param name="OnlyPath">Check only the rows whose resolved file is this path or lies under it; <see langword="null"/>
/// checks all. Registry-level findings are always reported; R008 is skipped when this is set.</param>
/// <param name="ToolVersion">The version of the program calling Run, for X002. <see langword="null"/> skips X002.</param>
public sealed record CheckOptions(
    bool Strict = false,
    string? OnlyPath = null,
    string? ToolVersion = null);

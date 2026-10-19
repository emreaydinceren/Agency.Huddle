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
    DateTimeOffset Written)
{
    /// <summary>The context <c>huddle-packs</c> runs with: one to one with the context file Huddle wrote (§6.7).</summary>
    public static CheckContext From(InstallContext installContext)
    {
        ArgumentNullException.ThrowIfNull(installContext);
        return new CheckContext(
            installContext.HuddleVersion,
            installContext.SchemaVersion,
            installContext.Computer,
            installContext.DataDir,
            installContext.SkillSourcesDir,
            installContext.SkillsDir,
            installContext.Roles,
            installContext.Installed,
            installContext.AppTools,
            installContext.Written);
    }
}

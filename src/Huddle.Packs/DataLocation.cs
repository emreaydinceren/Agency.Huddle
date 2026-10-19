using Agency.Huddle.SkillPacks.Checking;

namespace Agency.Huddle.Packs;

/// <summary>The data folder, the context and the Skill Sources folder one run uses.</summary>
/// <param name="DataDir">Huddle's data folder.</param>
/// <param name="Context">What the checks need, from the context file.</param>
/// <param name="SkillSourcesDir">The Skill Sources folder.</param>
internal sealed record DataLocation(string DataDir, CheckContext Context, string SkillSourcesDir);

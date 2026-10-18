using Agency.Huddle.App.SkillSources;
using Agency.Huddle.SkillPacks.Checking;
using Agency.Huddle.SkillPacks.Resolving;

namespace Agency.Huddle.App.Skills.Ui;

/// <summary>The one number the Settings section menu's Skills badge shows (Skill Packs spec §6.9.2, <c>G2a</c>): the Packs,
/// catalogs and Skills shown <b>Broken</b>, plus the <see cref="SkillStore"/> issues. A tab label never shows problems.</summary>
internal static class SkillProblemCount
{
    /// <summary>Broken Packs, catalogs and Skills, plus the Skill store's issues.</summary>
    public static int Of(SkillRegistry registry, SkillStore store)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(store);
        RegistrySnapshot snapshot = registry.Current;
        int packs = snapshot.Resolved.Packs.Count(pack => registry.DisplayState(pack) == ItemState.Broken);
        int catalogs = snapshot.Resolved.Catalogs.Count(catalog => (snapshot.Checked.StateOf(catalog.StateKey) ?? ItemCheckState.Broken) == ItemCheckState.Broken);
        int skills = snapshot.Resolved.Skills.Count(skill => registry.DisplayState(skill) == ItemState.Broken);
        return packs + catalogs + skills + store.Issues.Count;
    }
}

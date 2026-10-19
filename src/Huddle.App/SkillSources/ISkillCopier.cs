using Agency.Huddle.App.Teams;
using Agency.Huddle.SkillPacks.Resolving;

namespace Agency.Huddle.App.SkillSources;

/// <summary>What the rest of the app may ask of the copier (§6.6).</summary>
internal interface ISkillCopier
{
    /// <summary>Copies each Skill into <c>Skills/&lt;name&gt;/</c>, one after another, and reports each outcome.</summary>
    /// <param name="skills">Entries taken from the current snapshot. Entries that are not usable are reported as failures, not thrown.</param>
    /// <param name="ct">Cancels the copy; any staging folder is removed and the exception propagates.</param>
    Task<CopyResult> CopyAsync(IReadOnlyList<SkillEntry> skills, CancellationToken ct);

    /// <summary>Makes sure every named Skill is in <c>Skills/</c>: a built-in name or a name already there is skipped, a
    /// copyable one is copied, and an existing folder is never replaced.</summary>
    /// <returns>One setup item per name that is not ready; empty when every Skill is present.</returns>
    Task<IReadOnlyList<SkillSetupItem>> EnsureAvailableAsync(IReadOnlyList<string> skillNames, CancellationToken ct);
}

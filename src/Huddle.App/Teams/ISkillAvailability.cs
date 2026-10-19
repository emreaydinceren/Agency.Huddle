using Agency.Huddle.SkillPacks.Resolving;

namespace Agency.Huddle.App.Teams;

/// <summary>The registry seam <c>CardResolver</c> takes, so it stays a pure static class tested without the registry.</summary>
internal interface ISkillAvailability
{
    /// <summary>What the registry knows about <paramref name="skillName"/>.</summary>
    SkillAvailability Of(string skillName);

    /// <summary>The Pack with this id, or null.</summary>
    PackEntry? FindPack(string packId);

    /// <summary>The sub-category of the first catalog entry of that name; null when none or empty.</summary>
    string? SubCategoryOf(string skillName);

    /// <summary>What stops a Pack or Skill from being handed to a new hire, with its detail filled in (the Pack title,
    /// the first error, or the Source's reason); null when it is ready. A Pack no row provides is null too: the caller
    /// skips it. A Pack's own state only; <see cref="SkillSetupCheck"/> folds in the state of its Skills.</summary>
    SkillSetupItem? SetupItem(SkillSetupKind kind, string name);
}

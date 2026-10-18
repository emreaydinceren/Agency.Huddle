using Agency.Huddle.SkillPacks.Resolving;

namespace Agency.Huddle.App.Teams;

/// <summary>What the registry knows about a Skill name (Skill Packs spec §6.10.1).</summary>
internal enum SkillAvailability
{
    /// <summary>Built in, or already in Skills/. Needs nothing.</summary>
    Present,

    /// <summary>In the registry, usable, with a folder here; a copy into Skills/ is all that is missing.</summary>
    Copyable,

    /// <summary>Listed in a catalog or Pack, but no folder here.</summary>
    NotDownloaded,

    /// <summary>A check failed for it.</summary>
    Broken,

    /// <summary>Its Source withdrew it.</summary>
    Withdrawn,

    /// <summary>Named by no catalog or Pack, and not in Skills/.</summary>
    Unknown,
}

/// <summary>Whether a setup item is about a Pack or a Skill.</summary>
internal enum SkillSetupKind
{
    /// <summary>A Pack id.</summary>
    Pack,

    /// <summary>A Skill name.</summary>
    Skill,
}

/// <summary>One thing a card or draft needs before it can be hired.</summary>
/// <param name="Kind">Pack or Skill.</param>
/// <param name="Name">The Pack id or Skill name.</param>
/// <param name="Problem">What is wrong.</param>
/// <param name="Detail">The Pack title, the first error message, or the Source's reason.</param>
internal sealed record SkillSetupItem(SkillSetupKind Kind, string Name, SkillAvailability Problem, string Detail);

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

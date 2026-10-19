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

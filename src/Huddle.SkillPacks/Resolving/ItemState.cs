namespace Agency.Huddle.SkillPacks.Resolving;

/// <summary>The state a Pack, catalog or Skill is shown in (§8.3, §8.4).</summary>
public enum ItemState
{
    /// <summary>Passes the checks and is downloaded; no Teammate holds it and nothing changed.</summary>
    Available,

    /// <summary>The files at the pointer differ from the copy in Skills/ (§8.4).</summary>
    UpdateAvailable,

    /// <summary>The file is fine but a Skill's path is empty, leads nowhere, or has no entry for this computer.</summary>
    NotDownloaded,

    /// <summary>The Source marks it deprecated. Still choosable.</summary>
    Deprecated,

    /// <summary>The Source pulled it. New hires cannot take it.</summary>
    Withdrawn,

    /// <summary>The pointer leads nowhere or an error check fails.</summary>
    Broken,

    /// <summary>Held by one or more Teammates. Never produced by the library: it needs Teammate files.</summary>
    InUse,

    /// <summary>The row appeared since the Human last opened its tab. Never produced by the library: a UI overlay.</summary>
    New,
}

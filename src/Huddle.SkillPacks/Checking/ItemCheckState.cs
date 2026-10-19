namespace Agency.Huddle.SkillPacks.Checking;

/// <summary>The state the checks leave an item in. Huddle refines Usable into New, Available, In use, Update
/// available, Deprecated and Withdrawn (§8.3, §8.4).</summary>
public enum ItemCheckState
{
    /// <summary>Passes every error check and is downloaded.</summary>
    Usable,

    /// <summary>The file is fine but a path is empty, leads nowhere, or has no entry for this computer.</summary>
    NotDownloaded,

    /// <summary>An error check failed.</summary>
    Broken,
}

namespace Agency.Huddle.SkillPacks.Folders;

/// <summary>The kind of thing a review flag points at.</summary>
public enum ReviewFlagKind
{
    /// <summary>A script or executable file.</summary>
    Script,

    /// <summary>A shell command in the skill text.</summary>
    ShellCommand,

    /// <summary>A network call.</summary>
    NetworkCall,

    /// <summary>A path outside the Work Dir.</summary>
    PathOutsideWorkDir,

    /// <summary>No license declared.</summary>
    NoLicense
}

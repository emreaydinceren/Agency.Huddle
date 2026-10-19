namespace Agency.Huddle.App.SkillSources;

/// <summary>Why a copy failed; the UI chooses its wording and fix from this.</summary>
internal enum CopyFailureKind
{
    /// <summary>The Skill is not in a usable state.</summary>
    NotUsable,

    /// <summary>The files kept changing during three attempts.</summary>
    Unstable,

    /// <summary>The S checks failed on the copy.</summary>
    CheckFailed,

    /// <summary>A different Skill of that name is in <c>Skills/</c>.</summary>
    NameTaken,

    /// <summary>A file could not be written.</summary>
    Io,
}

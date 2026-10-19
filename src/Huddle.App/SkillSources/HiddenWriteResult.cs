namespace Agency.Huddle.App.SkillSources;

/// <summary>The outcome of a hide or unhide.</summary>
internal enum HiddenWriteResult
{
    /// <summary>The file was written.</summary>
    Written,

    /// <summary>Nothing to change.</summary>
    Unchanged,

    /// <summary>The file could not be read, so it was left as it is (§6.12.4).</summary>
    Refused,
}

namespace Agency.Huddle.App.SkillSources;

/// <summary>What a successful copy did.</summary>
internal enum CopyOutcome
{
    /// <summary>A new folder.</summary>
    Created,

    /// <summary>New files swapped in for an older copy.</summary>
    Replaced,

    /// <summary>The copy already matched the pointer.</summary>
    AlreadyCurrent,
}

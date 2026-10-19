namespace Agency.Huddle.SkillPacks.Resolving;

/// <summary>Where a Skill's path left it on this computer, so the checks can tell R009, S001 and silence apart.</summary>
public enum SkillPathState
{
    /// <summary>The path leads to a folder.</summary>
    Present,

    /// <summary>The path is filled in but leads to no folder (S001).</summary>
    Missing,

    /// <summary>An entry applies to this computer but is empty: not fetched yet.</summary>
    Empty,

    /// <summary>The path is per computer and has no entry for this one and no <c>*</c> (R009).</summary>
    NoEntryForComputer,
}

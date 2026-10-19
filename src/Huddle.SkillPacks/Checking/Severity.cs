namespace Agency.Huddle.SkillPacks.Checking;

/// <summary>How serious a finding is. An error makes its item Broken; a warning is shown on the details card.</summary>
public enum Severity
{
    /// <summary>Shown, but the item stays usable.</summary>
    Warning = 1,

    /// <summary>The item the finding belongs to takes the state of its check.</summary>
    Error = 2,
}

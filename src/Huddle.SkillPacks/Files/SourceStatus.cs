namespace Agency.Huddle.SkillPacks.Files;

/// <summary>What the Source says about itself.</summary>
public enum SourceStatus
{
    /// <summary>In use.</summary>
    Active,

    /// <summary>Still choosable, but on its way out.</summary>
    Deprecated,

    /// <summary>Pulled: new hires cannot take it.</summary>
    Withdrawn,

    /// <summary>Moved elsewhere (<see cref="SourceFile.MovedTo"/>).</summary>
    Moved,
}

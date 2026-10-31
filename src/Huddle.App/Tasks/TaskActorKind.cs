namespace Agency.Huddle.App.Tasks;

/// <summary>The kind of actor who made a change to a Task.</summary>
public enum TaskActorKind
{
    /// <summary>The human user.</summary>
    Human,

    /// <summary>An AI agent.</summary>
    Agent,

    /// <summary>Someone outside Huddle who edited the file.</summary>
    OutsideHuddle,

    /// <summary>A Helper: a background judgement Huddle applied.</summary>
    Helper,
}

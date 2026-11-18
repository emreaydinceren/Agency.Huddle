namespace Agency.Huddle.Contracts;

/// <summary>What a runner reports about a goldfish possession.</summary>
public enum GoldfishReportKind
{
    /// <summary>The possession's session opened.</summary>
    Opened,

    /// <summary>A Turn of the possession started.</summary>
    TurnStarted,

    /// <summary>A Turn of the possession ended.</summary>
    TurnEnded,

    /// <summary>The possession failed.</summary>
    Failed,
}

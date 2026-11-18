namespace Agency.Huddle.Contracts;

/// <summary>How a goldfish possession ended.</summary>
public enum GoldfishExit
{
    /// <summary>The Agent finished the brief.</summary>
    Finished,

    /// <summary>The Agent got stuck and gave the work back.</summary>
    Stuck,

    /// <summary>A Member called the Agent back.</summary>
    CalledBack,

    /// <summary>The Adapter stopped or failed.</summary>
    Failed,

    /// <summary>Huddle restarted while the possession was open.</summary>
    Restarted,
}

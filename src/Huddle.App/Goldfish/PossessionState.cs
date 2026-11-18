namespace Agency.Huddle.App.Goldfish;

/// <summary>Where a possession is in its life; stored as lower-case text.</summary>
internal enum PossessionState
{
    /// <summary>The brief has been sent and the Agent has not yet answered.</summary>
    Starting,

    /// <summary>A Turn is running.</summary>
    Working,

    /// <summary>The Agent's Turn ended without finishing; it waits for the next Mention.</summary>
    Waiting,

    /// <summary>An exit is chosen and the return is being posted.</summary>
    Returning,

    /// <summary>The return is posted; the row is history.</summary>
    Ended,
}

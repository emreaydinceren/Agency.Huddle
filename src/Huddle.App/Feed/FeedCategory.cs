namespace Agency.Huddle.App.Feed;

/// <summary>A segment of the Feed Tab's Category control (spec 6.9).</summary>
internal enum FeedCategory
{
    /// <summary>Every item.</summary>
    All,

    /// <summary>Any item that is a Notification and not yet resolved.</summary>
    NeedsYou,

    /// <summary>Task activity.</summary>
    Tasks,

    /// <summary>Library document changes.</summary>
    Documents,

    /// <summary>Room activity and Room waits.</summary>
    Rooms,

    /// <summary>Gate activity.</summary>
    Gates,

    /// <summary>A Helper paused or failing.</summary>
    Helpers,

    /// <summary>Remote access: pairing requests and devices.</summary>
    Devices,

    /// <summary>Upkeep of the Second Brain: a Memory or outputs folder over its cap.</summary>
    SecondBrain,
}

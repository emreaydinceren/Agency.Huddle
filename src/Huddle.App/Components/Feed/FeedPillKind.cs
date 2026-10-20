namespace Agency.Huddle.App.Components.Feed;

/// <summary>Which status pill a Feed card shows (Feed spec §6.13).</summary>
internal enum FeedPillKind
{
    /// <summary>"Waiting for you", in the Warning outline.</summary>
    Waiting,

    /// <summary>"Assigned to you", in the Info outline.</summary>
    Assigned,

    /// <summary>"Done", once the Notification is resolved.</summary>
    Done,
}

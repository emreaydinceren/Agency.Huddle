namespace Agency.Huddle.Contracts;

/// <summary>How a Turn ended without a reply naming its Messages, as a client reports it.</summary>
public enum QueueOutcome
{
    /// <summary>Nothing to post, <c>NO_REPLY</c> included.</summary>
    Silent,

    /// <summary>The Turn failed.</summary>
    Failed,

    /// <summary>A Stop ended the Turn.</summary>
    Stopped,

    /// <summary>The client declined the Turn.</summary>
    Declined,

    /// <summary>The Message was drained unrun: a spent token Budget or a Stop mark.</summary>
    Dropped,
}

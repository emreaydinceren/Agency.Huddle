namespace Agency.Huddle.App.Data;

/// <summary>How a <c>done</c> Turn Queue row was closed.</summary>
internal enum TurnQueueOutcome
{
    /// <summary>A reply naming the Message reached the Transcript.</summary>
    Replied,

    /// <summary>Nothing to post, <c>NO_REPLY</c> included.</summary>
    Silent,

    /// <summary>The Turn failed.</summary>
    Failed,

    /// <summary>A Stop closed it.</summary>
    Stopped,

    /// <summary>The Agent declined the Turn.</summary>
    Declined,

    /// <summary>Drained unrun: a spent token Budget or a Stop mark.</summary>
    Dropped,

    /// <summary>Older than <c>ResumeWithin</c> when it would have been handed out.</summary>
    Expired,

    /// <summary>Cut off twice.</summary>
    Abandoned,

    /// <summary>The Room, the Membership or the Persona went away.</summary>
    Gone,
}

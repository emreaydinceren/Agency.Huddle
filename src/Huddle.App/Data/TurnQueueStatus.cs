namespace Agency.Huddle.App.Data;

/// <summary>Where a Turn Queue row stands.</summary>
internal enum TurnQueueStatus
{
    /// <summary>Owed, no prompt sent.</summary>
    Waiting,

    /// <summary>The prompt was sent over a connection.</summary>
    Running,

    /// <summary>Closed, with an outcome.</summary>
    Done,
}

namespace Agency.Huddle.App.Acp.Sessions.TurnHooks;

/// <summary>Why a session's state is being set.</summary>
internal enum SessionOpenReason
{
    /// <summary>A new session.</summary>
    Fresh,

    /// <summary>A stored session was resumed.</summary>
    Resumed,

    /// <summary>The session was just compacted.</summary>
    Compacted,
}

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Implemented by the scope of a Turn that remembers a question of its own going unanswered. After the bound ran out, the
/// connector approval prompt asks nothing more on that Turn: the Human is not there, so every further connector call is refused
/// rather than put to them one after another.
/// </summary>
internal interface IConnectorTurnLatch
{
    /// <summary>Gets a value indicating whether a question on the Turn now in flight ran out of time with no answer.</summary>
    bool ApprovalTimedOut { get; }

    /// <summary>Gets the Room of the Turn now in flight, or <see langword="null"/> when none is. Used to say where a connector call was decided.</summary>
    string? ActiveRoomId { get; }
}

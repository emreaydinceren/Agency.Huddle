using Agency.Huddle.Acp.Abstractions;

namespace Agency.Huddle.App.Acp.Sessions;

/// <summary>Where one Room Session is in its life.</summary>
internal enum RoomSessionState
{
    /// <summary>No <see cref="IAgentSession"/> is open. The next dequeued item opens one.</summary>
    Closed,

    /// <summary>An open is in flight.</summary>
    Opening,

    /// <summary>Open, with no Turn running.</summary>
    Idle,

    /// <summary>Open, running one Turn.</summary>
    Busy,
}

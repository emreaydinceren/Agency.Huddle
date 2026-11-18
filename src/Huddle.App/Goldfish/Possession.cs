using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Goldfish;

/// <summary>One goldfish possession: an Agent lent to a Room for a brief, from its start to its return.</summary>
/// <param name="Id">The possession id.</param>
/// <param name="AgentId">The possessed Agent.</param>
/// <param name="RoomId">The Room it works in.</param>
/// <param name="TaskId">The Task it works on, when it has one.</param>
/// <param name="Brief">The brief, word for word.</param>
/// <param name="State">Where it is.</param>
/// <param name="Exit">How it ended; set exactly when the state is Returning or Ended.</param>
/// <param name="ExitBy">Who chose the exit.</param>
/// <param name="StartedAt">When it started.</param>
/// <param name="EndedAt">When it ended.</param>
/// <param name="Turns">How many Turns it ran.</param>
/// <param name="Spend">What those Turns cost.</param>
/// <param name="Currency">The currency of <paramref name="Spend"/>.</param>
/// <param name="ResultMessageId">The posted result Message, once there is one.</param>
internal sealed record Possession(
    string Id,
    string AgentId,
    string RoomId,
    string? TaskId,
    string Brief,
    PossessionState State,
    GoldfishExit? Exit,
    string? ExitBy,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    int Turns,
    decimal Spend,
    string? Currency,
    string? ResultMessageId)
{
    /// <summary>Whether the possession has not yet ended.</summary>
    public bool IsOpen => this.State != PossessionState.Ended;
}

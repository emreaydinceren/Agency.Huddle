namespace Agency.Huddle.Contracts;

/// <summary>Starts a goldfish possession on a runner. Server to client.</summary>
/// <param name="PossessionId">The possession's id.</param>
/// <param name="RoomId">The Room the possession works in.</param>
/// <param name="Brief">The brief the Agent is given.</param>
/// <param name="TaskId">The Task the possession works on, when it has one.</param>
public sealed record GoldfishBegin(string PossessionId, string RoomId, string Brief, string? TaskId = null) : ProtocolMessage;

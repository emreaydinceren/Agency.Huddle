namespace Agency.Huddle.Contracts;

/// <summary>The Human asking one Agent to start its session in one Room afresh (LR-R15). Server to runner. Forgets that Room's session only.</summary>
/// <param name="RoomId">The Room whose stored session the Agent forgets; sessions in other Rooms are untouched.</param>
public sealed record StartAfresh(string RoomId) : ProtocolMessage;

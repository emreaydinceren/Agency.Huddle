namespace Agency.Huddle.Contracts;

// server -> client
public sealed record Welcome(string AgentId, string Name, IReadOnlyList<RoomInfo> Rooms) : ProtocolMessage;
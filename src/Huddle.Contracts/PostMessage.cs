namespace Agency.Huddle.Contracts;

public sealed record PostMessage(string RoomId, string? MessageId, string Text) : ProtocolMessage;
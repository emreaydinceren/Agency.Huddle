namespace Agency.Huddle.Contracts;

public sealed record ProtocolError(string Code, string Message, string? RelatedMessageId = null) : ProtocolMessage;
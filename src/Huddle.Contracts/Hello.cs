namespace Agency.Huddle.Contracts;

// client -> server
public sealed record Hello(string Name, string? Description) : ProtocolMessage;
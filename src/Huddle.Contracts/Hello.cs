namespace Agency.Huddle.Contracts;

// client -> server

/// <summary>The first line a client sends.</summary>
/// <param name="Name">The Name the client connects as.</param>
/// <param name="Description">What the client does, or <see langword="null"/>.</param>
/// <param name="DurableQueue">The client reports its Turns with <see cref="QueueAck"/> and wants waiting Messages re-sent after Welcome; <see langword="null"/> for every other client.</param>
public sealed record Hello(string Name, string? Description, bool? DurableQueue = null) : ProtocolMessage;

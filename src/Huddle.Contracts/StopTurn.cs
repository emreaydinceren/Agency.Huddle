namespace Agency.Huddle.Contracts;

/// <summary>The Human ending a Turn in progress.</summary>
/// <remarks>
/// Stops that Agent's Turn and queue in <paramref name="RoomId"/> only: a live Turn in a different
/// Room, and anything already queued for a different Room, is untouched. The wire itself is
/// unchanged — <paramref name="RoomId"/> has always been carried — only how the receiving Agent
/// acts on it (D16 P0-2). A Turn ends in one of three ways — completed, stopped, or failed — and a
/// stopped Turn is a normal outcome, not a failure.
/// </remarks>
/// <param name="RoomId">The Room the Human asked from, and the only Room this Stop affects.</param>
public sealed record StopTurn(string RoomId) : ProtocolMessage;
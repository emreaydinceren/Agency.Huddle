namespace Agency.Huddle.Contracts;

/// <summary>Ends a goldfish possession on a runner. Server to client.</summary>
/// <param name="PossessionId">The possession's id.</param>
/// <param name="Exit">Why it ended.</param>
public sealed record GoldfishEnd(string PossessionId, GoldfishExit Exit) : ProtocolMessage;

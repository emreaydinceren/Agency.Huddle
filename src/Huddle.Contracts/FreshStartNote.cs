namespace Agency.Huddle.Contracts;

/// <summary>The Agent's note to the server that it started a Room's session afresh (LR-R21). Runner to server.</summary>
/// <param name="RoomId">The Room whose session was started afresh.</param>
/// <param name="Reason">Why the session was started afresh. Phase 2a sends <c>"asked"</c> only; phase 2b adds trigger names.</param>
/// <param name="MessageCount">How many Messages the new session's prompt rereads from the transcript.</param>
/// <param name="EstimatedTokens">The estimated token size of what was reread.</param>
/// <param name="ReadFailed">True when the transcript could not be read, so the new session starts without it.</param>
public sealed record FreshStartNote(string RoomId, string Reason, int MessageCount, int EstimatedTokens, bool ReadFailed) : ProtocolMessage;

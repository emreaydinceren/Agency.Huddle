namespace Agency.Huddle.Contracts;

/// <summary>Asks for a Room's Messages between two points, for a Room Session's first Turn (RS §6.5). Client to server.</summary>
/// <param name="RequestId">Echoed on the answer, so the read loop can hand it to the waiting Turn.</param>
/// <param name="RoomId">A Room the sender is a Member of.</param>
/// <param name="AfterMessageId">Start after this Message, or <see langword="null"/> for the latest Messages.</param>
/// <param name="BeforeMessageId">Stop before this Message: the one that started the Turn, or an id not in the Transcript, to read to the end.</param>
/// <param name="Max">At most this many, the latest ones.</param>
public sealed record ReadTranscript(
    string RequestId, string RoomId, string? AfterMessageId, string BeforeMessageId, int Max) : ProtocolMessage;
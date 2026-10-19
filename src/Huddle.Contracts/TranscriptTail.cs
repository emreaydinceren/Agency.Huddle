namespace Agency.Huddle.Contracts;

/// <summary>The answer to one <see cref="ReadTranscript"/> (RS §6.5), sent only to the client that asked.</summary>
/// <param name="RequestId">Echoes the <see cref="ReadTranscript.RequestId"/> it answers.</param>
/// <param name="RoomId">Echoes the <see cref="ReadTranscript.RoomId"/> it answers.</param>
/// <param name="Messages">The Messages in range, oldest first.</param>
/// <param name="Omitted">How many earlier Messages in the range <see cref="ReadTranscript.Max"/> left out.</param>
public sealed record TranscriptTail(
    string RequestId, string RoomId, IReadOnlyList<ChatMessage> Messages, int Omitted) : ProtocolMessage;
namespace Agency.Huddle.Contracts;

/// <summary>What a client did with Messages the server queued for it. Client to server.</summary>
/// <param name="RoomId">The Room the Messages were posted to.</param>
/// <param name="MessageIds">The Messages concerned, oldest first; more than one only for a Folded Turn.</param>
/// <param name="Kind"><see cref="QueueAckKind.Started"/> carries <paramref name="ReplyMessageId"/>; <see cref="QueueAckKind.Finished"/> carries <paramref name="Outcome"/>; <see cref="QueueAckKind.Held"/> carries neither.</param>
/// <param name="ReplyMessageId">The id the runner minted for the reply; set only with <see cref="QueueAckKind.Started"/>.</param>
/// <param name="Outcome">How the Turn ended; set only with <see cref="QueueAckKind.Finished"/>.</param>
public sealed record QueueAck(
    string RoomId, IReadOnlyList<string> MessageIds, QueueAckKind Kind, string? ReplyMessageId = null, QueueOutcome? Outcome = null)
    : ProtocolMessage;

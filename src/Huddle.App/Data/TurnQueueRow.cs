namespace Agency.Huddle.App.Data;

/// <summary>One row of the Turn Queue: a Message an Agent owes a Turn for.</summary>
/// <param name="Seq">The strictly increasing row number; a Stop closes by it.</param>
/// <param name="AgentId">The Agent that owes the Turn.</param>
/// <param name="MessageId">The Message the Turn answers.</param>
/// <param name="RoomId">The Room the Message was posted to.</param>
/// <param name="PostedUtc">The Message's own timestamp.</param>
/// <param name="Status">Where the row stands.</param>
/// <param name="Outcome">How the row was closed; set only with <see cref="TurnQueueStatus.Done"/>.</param>
/// <param name="ReplyMessageId">The id the runner minted for the reply; set with <see cref="TurnQueueStatus.Running"/>.</param>
/// <param name="ConnectionId">The connection the prompt went over; set with <see cref="TurnQueueStatus.Running"/>.</param>
/// <param name="Attempts">How many times a prompt was sent.</param>
/// <param name="CreatedUtc">When the row was first written.</param>
/// <param name="UpdatedUtc">When the row last changed.</param>
internal sealed record TurnQueueRow(
    long Seq,
    string AgentId,
    string MessageId,
    string RoomId,
    DateTimeOffset PostedUtc,
    TurnQueueStatus Status,
    TurnQueueOutcome? Outcome,
    string? ReplyMessageId,
    string? ConnectionId,
    int Attempts,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc);

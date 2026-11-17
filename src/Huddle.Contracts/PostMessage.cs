namespace Agency.Huddle.Contracts;

/// <summary>A client posts a Message to a Room.</summary>
/// <param name="RoomId">The Room to post to.</param>
/// <param name="MessageId">The id the client minted for the Message, or <see langword="null"/> to let the server mint one.</param>
/// <param name="Text">The Message text.</param>
/// <param name="InReplyTo">The Messages this reply answers, oldest first; <see langword="null"/> for a post that answers nothing, such as a post_message call.</param>
public sealed record PostMessage(string RoomId, string? MessageId, string Text, IReadOnlyList<string>? InReplyTo = null) : ProtocolMessage;

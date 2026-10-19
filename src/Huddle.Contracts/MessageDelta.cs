namespace Agency.Huddle.Contracts;

/// <summary>
/// One increment of a Message as it streams in, or the terminator that ends the stream.
/// </summary>
/// <remarks>
/// <para>
/// <paramref name="Text"/> is the increment, not the running total: one chunk of text as it
/// arrives, not everything sent so far. A client accumulates the chunks itself if it wants the
/// running total.
/// </para>
/// <para>
/// <paramref name="IsFinal"/> <c>true</c> is a stream terminator carrying empty
/// <paramref name="Text"/>. It is written on every path a Turn can end — success, refusal,
/// exception, or the Human stopping it via <see cref="StopTurn"/> — so a partially-arrived reply
/// can never outlive its Turn on screen.
/// </para>
/// <para>
/// A <see cref="MessagePosted"/> carrying the same <paramref name="MessageId"/> is what turns the
/// partial text into a real Message. The terminator here is what discards one that never became
/// a Message.
/// </para>
/// </remarks>
/// <param name="RoomId">The Room the Message is streaming into.</param>
/// <param name="MessageId">The id the eventual <see cref="MessagePosted"/>, if any, will carry.</param>
/// <param name="Text">The increment of text delivered by this delta, empty when <paramref name="IsFinal"/> is <c>true</c>.</param>
/// <param name="IsFinal">Whether this delta ends the stream for <paramref name="MessageId"/>.</param>
public sealed record MessageDelta(string RoomId, string MessageId, string Text, bool IsFinal) : ProtocolMessage;
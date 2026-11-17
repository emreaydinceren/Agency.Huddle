namespace Agency.Huddle.Contracts;

/// <summary>
/// One Message delivered to one Agent. Everything on it is a label the server computed for this
/// recipient; none of it is a decision. What the Agent does with it is the client's to decide —
/// see <c>ReplyGate</c>, and ADR-0003's "the server labels; it never decides".
/// </summary>
/// <param name="RoomId">The Room the Message was posted to.</param>
/// <param name="RoomName">The Room's Name, so a client need not look it up to label a prompt.</param>
/// <param name="Message">The Message itself, exactly as it was persisted.</param>
/// <param name="Mentioned">Whether <em>this</em> recipient appears in <paramref name="Mentions"/>.</param>
/// <param name="Mentions">Every Member the Message Mentions, not only this recipient.</param>
/// <param name="Members">Every Member of the Room. Its count is the whole of the Reply Gate's Room rule.</param>
/// <param name="AgentMessagesSinceHuman">
/// How many agent-authored Messages this Room has taken since the Human last spoke there, counting
/// this one. Paired with <paramref name="Budget"/> it is the Room's Budget, and the two are labels
/// for the same reason a Member count is: comparing them stays the client's decision.
/// </param>
/// <param name="Budget">
/// How many that Room currently allows. Not the configured default: the Human can extend it, which
/// is why this arrives on every delivery rather than being read from a client's own configuration.
/// Zero or less means the Room is uncapped.
/// </param>
/// <param name="Team">
/// The Team the Room is scoped to, or <see langword="null"/> for a root Room. A label for the Turn
/// prompt, nothing more. Additive on the wire: omitted when <see langword="null"/>, and absent
/// deserialises as a root Room.
/// </param>
/// <param name="Redelivery">
/// Set only on a re-sent Message: it was waiting, or a Turn for it was cut off. Additive on the wire:
/// omitted when <see langword="null"/>.
/// </param>
public sealed record MessagePosted(
    string RoomId, string RoomName, ChatMessage Message, bool Mentioned,
    IReadOnlyList<MemberInfo> Mentions, IReadOnlyList<MemberInfo> Members,
    int AgentMessagesSinceHuman, int Budget, string? Team = null, Redelivery? Redelivery = null) : ProtocolMessage;
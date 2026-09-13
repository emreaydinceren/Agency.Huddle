using System.Text.Json.Serialization;

namespace Agency.Huddle.Contracts;

public sealed record MemberInfo(string Id, string Name, UserKind Kind);

public sealed record RoomInfo(string Id, string Name, IReadOnlyList<MemberInfo> Members);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(Hello), "hello")]
[JsonDerivedType(typeof(Welcome), "welcome")]
[JsonDerivedType(typeof(PostMessage), "postMessage")]
[JsonDerivedType(typeof(MessagePosted), "messagePosted")]
[JsonDerivedType(typeof(MessageDelta), "messageDelta")]
[JsonDerivedType(typeof(ProtocolError), "error")]
public abstract record ProtocolMessage
{
    public int Version { get; init; } = ProtocolVersion.Current;
}

// client -> server
public sealed record Hello(string Name, string? Description) : ProtocolMessage;

public sealed record PostMessage(string RoomId, string? MessageId, string Text) : ProtocolMessage;

public sealed record MessageDelta(string RoomId, string MessageId, string Text, bool IsFinal) : ProtocolMessage; // reserved, V2

// server -> client
public sealed record Welcome(string AgentId, string Name, IReadOnlyList<RoomInfo> Rooms) : ProtocolMessage;

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
public sealed record MessagePosted(
    string RoomId, string RoomName, ChatMessage Message, bool Mentioned,
    IReadOnlyList<MemberInfo> Mentions, IReadOnlyList<MemberInfo> Members,
    int AgentMessagesSinceHuman, int Budget) : ProtocolMessage;

public sealed record ProtocolError(string Code, string Message, string? RelatedMessageId = null) : ProtocolMessage;

public static class ErrorCodes
{
    public const string ExpectedHello = "expectedHello";
    public const string InvalidName = "invalidName";
    public const string NameReserved = "nameReserved";
    public const string BadMessage = "badMessage";
    public const string UnknownRoom = "unknownRoom";
    public const string NotMember = "notMember";
    public const string NotSupported = "notSupported";
    public const string VersionMismatch = "versionMismatch";

    /// <summary>
    /// The Room has spent its Budget of agent-authored Messages and will take no more until a Human
    /// speaks there. Terminal for the caller: retrying spends the Turn the refusal exists to save,
    /// so the accompanying message says so in words a model will read as final.
    /// </summary>
    public const string BudgetExhausted = "budgetExhausted";
}
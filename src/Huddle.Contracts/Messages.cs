using System.Text.Json.Serialization;

namespace Agency.Huddle.Contracts;

public sealed record MemberInfo(string Id, string Name, UserKind Kind);

/// <summary>One Room in a <see cref="Welcome"/>, and the Members currently in it.</summary>
/// <param name="Id">The Room's id.</param>
/// <param name="Name">The Room's Name.</param>
/// <param name="Members">Every Member of the Room.</param>
/// <param name="IsEmpty">
/// Whether the Room has no Messages yet. Additive on the wire: absent deserialises as
/// <see langword="false"/> — "not empty" — so a server built before this field existed can never
/// cause a Greeting.
/// </param>
public sealed record RoomInfo(string Id, string Name, IReadOnlyList<MemberInfo> Members, bool IsEmpty = false);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(Hello), "hello")]
[JsonDerivedType(typeof(Welcome), "welcome")]
[JsonDerivedType(typeof(PostMessage), "postMessage")]
[JsonDerivedType(typeof(MessagePosted), "messagePosted")]
[JsonDerivedType(typeof(MessageDelta), "messageDelta")]
[JsonDerivedType(typeof(ProtocolError), "error")]
[JsonDerivedType(typeof(ToolActivity), "toolActivity")]
[JsonDerivedType(typeof(StopTurn), "stopTurn")]
public abstract record ProtocolMessage
{
    public int Version { get; init; } = ProtocolVersion.Current;
}

// client -> server
public sealed record Hello(string Name, string? Description) : ProtocolMessage;

public sealed record PostMessage(string RoomId, string? MessageId, string Text) : ProtocolMessage;

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

/// <summary>
/// One tool call the Agent is making during a Turn, shown to the Human while it happens and never
/// persisted.
/// </summary>
/// <remarks>
/// It carries no raw input or output JSON, because neither is rendered, and no tool-kind, because
/// <paramref name="Title"/> is what a reader needs. This type deliberately does not reuse the names
/// <c>ToolCallStarted</c>/<c>ToolCallUpdated</c>: those already exist in
/// <c>Agency.Huddle.Acp.Abstractions</c>, and the one file that bridges the two namespaces would
/// otherwise have two types of the same name in scope.
/// </remarks>
/// <param name="RoomId">The Room the Turn making this call is running in.</param>
/// <param name="MessageId">The Message the eventual reply, if any, will carry this activity alongside.</param>
/// <param name="ToolCallId">The id of the tool call this activity reports on.</param>
/// <param name="Title">A human-readable label for the call, or <see langword="null"/> if none is available yet.</param>
/// <param name="Status">The call's current lifecycle state.</param>
public sealed record ToolActivity(
    string RoomId, string MessageId, string ToolCallId, string? Title, ToolActivityStatus Status) : ProtocolMessage;

// server -> client
public sealed record Welcome(string AgentId, string Name, IReadOnlyList<RoomInfo> Rooms) : ProtocolMessage;

/// <summary>The Human ending a Turn in progress.</summary>
/// <remarks>
/// Stops that Agent's Turn and queue in <paramref name="RoomId"/> only: a live Turn in a different
/// Room, and anything already queued for a different Room, is untouched. The wire itself is
/// unchanged — <paramref name="RoomId"/> has always been carried — only how the receiving Agent
/// acts on it (D16 P0-2). A Turn ends in one of three ways — completed, stopped, or failed — and a
/// stopped Turn is a normal outcome, not a failure.
/// </remarks>
/// <param name="RoomId">The Room the Human asked from, and the only Room this Stop affects.</param>
public sealed record StopTurn(string RoomId) : ProtocolMessage;

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
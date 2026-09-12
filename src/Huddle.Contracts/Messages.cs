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

public sealed record MessagePosted(
    string RoomId, string RoomName, ChatMessage Message, bool Mentioned,
    IReadOnlyList<MemberInfo> Mentions, IReadOnlyList<MemberInfo> Members) : ProtocolMessage;

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
}
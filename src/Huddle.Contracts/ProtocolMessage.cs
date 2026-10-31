using System.Text.Json.Serialization;

namespace Agency.Huddle.Contracts;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(Hello), "hello")]
[JsonDerivedType(typeof(Welcome), "welcome")]
[JsonDerivedType(typeof(PostMessage), "postMessage")]
[JsonDerivedType(typeof(MessagePosted), "messagePosted")]
[JsonDerivedType(typeof(MessageDelta), "messageDelta")]
[JsonDerivedType(typeof(ProtocolError), "error")]
[JsonDerivedType(typeof(ToolActivity), "toolActivity")]
[JsonDerivedType(typeof(StopTurn), "stopTurn")]
[JsonDerivedType(typeof(ReadTranscript), "readTranscript")]
[JsonDerivedType(typeof(TranscriptTail), "transcriptTail")]
[JsonDerivedType(typeof(StartAfresh), "startAfresh")]
[JsonDerivedType(typeof(FreshStartNote), "freshStartNote")]
public abstract record ProtocolMessage
{
    public int Version { get; init; } = ProtocolVersion.Current;
}
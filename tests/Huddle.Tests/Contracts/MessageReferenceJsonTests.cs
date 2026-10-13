using System.Globalization;
using System.Text.Json;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.Contracts;

/// <summary>
/// Pins the JSON shape of a Message's references on the wire and in the JSONL transcript (spec §8.1, §8.3, D-14).
/// </summary>
public sealed class MessageReferenceJsonTests
{
    private const string OldLine = "{\"id\":\"m1\",\"timestamp\":\"2026-09-09T10:00:00+00:00\",\"senderId\":\"human\",\"senderName\":\"You\",\"text\":\"hi\"}";

    private static readonly DateTimeOffset SentAt = DateTimeOffset.Parse("2026-09-09T10:00:00+00:00", CultureInfo.InvariantCulture);

    /// <summary>A Message with one Task reference serialises to the exact JSON the wire and the transcript carry.</summary>
    [Fact]
    public void Serialize_MessageWithOneTaskReference_IsTheLiteralJson()
    {
        ChatMessage message = new("m1", SentAt, "human", "You", "hi")
        {
            References = [new MessageReference(ReferenceKind.Task, "GROW-0002", "GROW-0002", "Fix login", "In Progress")],
        };

        string json = JsonSerializer.Serialize(message, ProtocolJson.Options);

        Assert.Equal(
            "{\"id\":\"m1\",\"timestamp\":\"2026-09-09T10:00:00+00:00\",\"senderId\":\"human\",\"senderName\":\"You\",\"text\":\"hi\",\"references\":[{\"kind\":\"task\",\"key\":\"GROW-0002\",\"label\":\"GROW-0002\",\"title\":\"Fix login\",\"status\":\"In Progress\"}]}",
            json);
    }

    /// <summary>A Message with no references omits the property, so its line stays as it was before references existed.</summary>
    [Fact]
    public void Serialize_NoReferences_OmitsTheProperty()
    {
        ChatMessage message = new("m1", SentAt, "human", "You", "hi");

        string json = JsonSerializer.Serialize(message, ProtocolJson.Options);

        Assert.Equal(OldLine, json);
    }

    /// <summary>A Room reference has no Title or Status, so both are omitted from its JSON.</summary>
    [Fact]
    public void Serialize_RoomReference_OmitsTitleAndStatus()
    {
        ChatMessage message = new("m1", SentAt, "human", "You", "hi")
        {
            References = [new MessageReference(ReferenceKind.Room, "room-1", "Launch room")],
        };

        string json = JsonSerializer.Serialize(message, ProtocolJson.Options);

        Assert.Equal(
            "{\"id\":\"m1\",\"timestamp\":\"2026-09-09T10:00:00+00:00\",\"senderId\":\"human\",\"senderName\":\"You\",\"text\":\"hi\",\"references\":[{\"kind\":\"room\",\"key\":\"room-1\",\"label\":\"Launch room\"}]}",
            json);
    }

    /// <summary>A line written before references existed deserialises with a null References list.</summary>
    [Fact]
    public void Deserialize_LineWrittenBeforeReferences_HasNullReferences()
    {
        ChatMessage? message = JsonSerializer.Deserialize<ChatMessage>(OldLine, ProtocolJson.Options);

        Assert.NotNull(message);
        Assert.Null(message.References);
        Assert.Equal("hi", message.Text);
    }

    /// <summary>Each reference kind is written under its camelCase wire name and reads back to the same kind and fields.</summary>
    [Theory]
    [InlineData(ReferenceKind.Task, "task")]
    [InlineData(ReferenceKind.Room, "room")]
    [InlineData(ReferenceKind.Team, "team")]
    [InlineData(ReferenceKind.LibraryFile, "libraryFile")]
    public void Deserialize_EveryKind_RoundTrips(ReferenceKind kind, string wireKind)
    {
        ChatMessage message = new("m1", SentAt, "human", "You", "hi")
        {
            References = [new MessageReference(kind, "the-key", "the-label")],
        };

        string json = JsonSerializer.Serialize(message, ProtocolJson.Options);
        ChatMessage? readBack = JsonSerializer.Deserialize<ChatMessage>(json, ProtocolJson.Options);

        string expected = "{\"id\":\"m1\",\"timestamp\":\"2026-09-09T10:00:00+00:00\",\"senderId\":\"human\",\"senderName\":\"You\",\"text\":\"hi\",\"references\":[{\"kind\":\"" + wireKind + "\",\"key\":\"the-key\",\"label\":\"the-label\"}]}";
        Assert.Equal(expected, json);
        Assert.NotNull(readBack);
        Assert.NotNull(readBack.References);
        MessageReference reference = Assert.Single(readBack.References);
        Assert.Equal(kind, reference.Kind);
        Assert.Equal("the-key", reference.Key);
        Assert.Equal("the-label", reference.Label);
    }

    /// <summary>A reference kind this build does not know is refused, not silently dropped.</summary>
    [Fact]
    public void Deserialize_UnknownKind_Throws()
    {
        string json = OldLine.Replace("\"text\":\"hi\"", "\"text\":\"hi\",\"references\":[{\"kind\":\"person\",\"key\":\"k\",\"label\":\"l\"}]", StringComparison.Ordinal);

        Assert.Throws<JsonException>(() =>
        {
            _ = JsonSerializer.Deserialize<ChatMessage>(json, ProtocolJson.Options);
        });
    }
}

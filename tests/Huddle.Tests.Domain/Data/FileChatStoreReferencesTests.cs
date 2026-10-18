using System.Globalization;
using Agency.Huddle.App.Data;
using Agency.Huddle.Contracts;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Data;

/// <summary>
/// Pins that a Message's references survive the JSONL transcript, and that a line written before references existed still reads (spec §8.3).
/// </summary>
public sealed class FileChatStoreReferencesTests
{
    private const string OldLine = "{\"id\":\"m1\",\"timestamp\":\"2026-09-09T10:00:00+00:00\",\"senderId\":\"human\",\"senderName\":\"You\",\"text\":\"first\"}";

    private static readonly DateTimeOffset SentAt = DateTimeOffset.Parse("2026-09-09T10:00:00+00:00", CultureInfo.InvariantCulture);

    /// <summary>A Message appended with references reads back with the same references, field by field.</summary>
    [Fact]
    public async Task AppendThenRead_KeepsReferences()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var store = CreateStore(dir);
        List<MessageReference> references =
        [
            new MessageReference(ReferenceKind.Task, "GROW-0002", "GROW-0002", "Fix login", "In Progress"),
            new MessageReference(ReferenceKind.Room, "room-1", "Launch room"),
        ];
        ChatMessage message = new("m2", SentAt, "human", "You", "see GROW-0002") { References = references };

        await store.AppendAsync("room-1", message, ct);
        var result = await store.ReadAllAsync("room-1", ct);

        ChatMessage readBack = Assert.Single(result);
        Assert.Equal(message.Id, readBack.Id);
        Assert.Equal(message.Text, readBack.Text);
        Assert.NotNull(readBack.References);
        Assert.Equal(references, readBack.References);
    }

    /// <summary>A transcript holding a pre-change line followed by a line with a reference reads both, the old one with null References.</summary>
    [Fact]
    public async Task ReadAll_OldLineThenNewLine_ReadsBoth()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var store = CreateStore(dir);
        var roomsDirectory = Path.Combine(dir.Path, "rooms");
        Directory.CreateDirectory(roomsDirectory);
        await File.WriteAllTextAsync(Path.Combine(roomsDirectory, "room-1.jsonl"), OldLine + "\n", ct);
        ChatMessage message = new("m2", SentAt, "human", "You", "see GROW-0002")
        {
            References = [new MessageReference(ReferenceKind.Task, "GROW-0002", "GROW-0002", "Fix login", "In Progress")],
        };
        await store.AppendAsync("room-1", message, ct);

        var result = await store.ReadAllAsync("room-1", ct);

        Assert.Equal(2, result.Count);
        Assert.Equal("first", result[0].Text);
        Assert.Null(result[0].References);
        IReadOnlyList<MessageReference>? references = result[1].References;
        Assert.NotNull(references);
        MessageReference reference = Assert.Single(references);
        Assert.Equal("GROW-0002", reference.Key);
    }

    private static FileChatStore CreateStore(TempDataDir dir)
    {
        return new FileChatStore(dir.Options(), NullLogger<FileChatStore>.Instance);
    }
}

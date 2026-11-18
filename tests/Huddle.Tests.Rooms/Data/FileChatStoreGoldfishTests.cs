using System.Globalization;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.Data;

/// <summary>
/// Pins that a Message's goldfish possession id survives the JSONL transcript, and that a line written before it existed still reads (spec §8.6).
/// </summary>
public sealed class FileChatStoreGoldfishTests
{
    private const string OldLine = "{\"id\":\"m1\",\"timestamp\":\"2026-09-09T10:00:00+00:00\",\"senderId\":\"human\",\"senderName\":\"You\",\"text\":\"first\"}";

    private static readonly DateTimeOffset SentAt = DateTimeOffset.Parse("2026-09-09T10:00:00+00:00", CultureInfo.InvariantCulture);

    /// <summary>A Message appended with a possession id reads back with the same id.</summary>
    [Fact]
    public async Task AppendThenRead_KeepsGoldfish()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var store = dir.CreateChatStore();
        ChatMessage message = new("m2", SentAt, "human", "You", "from goldfish") { Goldfish = "p1" };

        await store.AppendAsync("room-1", message, ct);
        var result = await store.ReadAllAsync("room-1", ct);

        ChatMessage readBack = Assert.Single(result);
        Assert.Equal("p1", readBack.Goldfish);
        Assert.Equal("from goldfish", readBack.Text);
    }

    /// <summary>A transcript holding a pre-change line then a goldfish line reads both, the old one with a null Goldfish.</summary>
    [Fact]
    public async Task ReadAll_OldLineThenGoldfishLine_ReadsBoth()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var store = dir.CreateChatStore();
        var roomsDirectory = Path.Combine(dir.Path, "rooms");
        Directory.CreateDirectory(roomsDirectory);
        await File.WriteAllTextAsync(Path.Combine(roomsDirectory, "room-1.jsonl"), OldLine + "\n", ct);
        await store.AppendAsync("room-1", new ChatMessage("m2", SentAt, "human", "You", "second") { Goldfish = "p1" }, ct);

        var result = await store.ReadAllAsync("room-1", ct);

        Assert.Equal(2, result.Count);
        Assert.Null(result[0].Goldfish);
        Assert.Equal("p1", result[1].Goldfish);
    }

    /// <summary>A Message appended without a possession id writes no goldfish property.</summary>
    [Fact]
    public async Task Append_WithoutGoldfish_WritesNoProperty()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var store = dir.CreateChatStore();

        await store.AppendAsync("room-1", new ChatMessage("m1", SentAt, "human", "You", "plain"), ct);

        string file = await File.ReadAllTextAsync(Path.Combine(dir.Path, "rooms", "room-1.jsonl"), ct);
        Assert.DoesNotContain("goldfish", file, StringComparison.Ordinal);
    }
}

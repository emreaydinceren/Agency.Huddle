using Microsoft.Extensions.Logging.Abstractions;
using System.Text;
using Agency.Huddle.App.Data;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.Data;

public sealed class FileChatStoreTests
{
    [Fact]
    public async Task ReadAll_UnknownRoom_ReturnsEmpty()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var store = CreateStore(dir);

        var messages = await store.ReadAllAsync("nope", ct);

        Assert.Empty(messages);
    }

    [Fact]
    public async Task Append_ThenReadAll_PreservesOrderAndFields()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var store = CreateStore(dir);
        var messages = new List<ChatMessage>
        {
            new("m1", DateTimeOffset.Parse("2026-09-09T10:00:00.1234567+00:00", System.Globalization.CultureInfo.InvariantCulture), "human", "You", "first"),
            new("m2", DateTimeOffset.Parse("2026-09-09T10:00:01.1234567+00:00", System.Globalization.CultureInfo.InvariantCulture), "agent-1", "echo", "second"),
            new("m3", DateTimeOffset.Parse("2026-09-09T10:00:02.1234567+00:00", System.Globalization.CultureInfo.InvariantCulture), "human", "You", "third"),
        };

        foreach (var message in messages)
        {
            await store.AppendAsync("room-1", message, ct);
        }

        var result = await store.ReadAllAsync("room-1", ct);

        Assert.Equal(messages.Count, result.Count);
        for (var i = 0; i < messages.Count; i++)
        {
            Assert.Equal(messages[i].Id, result[i].Id);
            Assert.Equal(messages[i].Timestamp, result[i].Timestamp);
            Assert.Equal(messages[i].SenderId, result[i].SenderId);
            Assert.Equal(messages[i].SenderName, result[i].SenderName);
            Assert.Equal(messages[i].Text, result[i].Text);
        }
    }

    [Fact]
    public async Task Append_MultilineText_IsOneLineInFile()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var store = CreateStore(dir);
        var messages = new[]
        {
            new ChatMessage("m1", DateTimeOffset.UtcNow, "human", "You", "line1\nline2"),
            new ChatMessage("m2", DateTimeOffset.UtcNow, "human", "You", "another\nmultiline\ntext"),
        };

        foreach (var message in messages)
        {
            await store.AppendAsync("room-1", message, ct);
        }

        var path = Path.Combine(dir.Path, "rooms", "room-1.jsonl");
        var bytes = await File.ReadAllBytesAsync(path, ct);

        Assert.Equal(messages.Length, bytes.Count(b => b == (byte)'\n'));
    }

    [Fact]
    public async Task ConcurrentAppends_AllPersisted()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var store = CreateStore(dir);

        await Parallel.ForEachAsync(Enumerable.Range(0, 100), ct, async (i, token) =>
        {
            var message = new ChatMessage($"m{i}", DateTimeOffset.UtcNow, "human", "You", $"text {i}");
            await store.AppendAsync("room-1", message, token);
        });

        var result = await store.ReadAllAsync("room-1", ct);

        Assert.Equal(100, result.Count);
    }

    [Fact]
    public async Task ReadAll_SkipsCorruptTrailingLine()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var store = CreateStore(dir);
        await store.AppendAsync("room-1", new ChatMessage("m1", DateTimeOffset.UtcNow, "human", "You", "first"), ct);
        await store.AppendAsync("room-1", new ChatMessage("m2", DateTimeOffset.UtcNow, "human", "You", "second"), ct);

        var path = Path.Combine(dir.Path, "rooms", "room-1.jsonl");
        await using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
        {
            var torn = Encoding.UTF8.GetBytes("{\"id\":\"x\"");
            await stream.WriteAsync(torn, ct);
        }

        var result = await store.ReadAllAsync("room-1", ct);

        Assert.Equal(2, result.Count);
    }

    [Theory]
    [InlineData("../x")]
    [InlineData("a b")]
    public async Task InvalidRoomId_Throws(string roomId)
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var store = CreateStore(dir);
        var message = new ChatMessage("m1", DateTimeOffset.UtcNow, "human", "You", "hi");

        await Assert.ThrowsAsync<ArgumentException>(() => store.AppendAsync(roomId, message, ct));
        await Assert.ThrowsAsync<ArgumentException>(() => store.ReadAllAsync(roomId, ct));
    }

    [Fact]
    public async Task Append_CreatesRoomsDirectory()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var store = CreateStore(dir);

        await store.AppendAsync("room-1", new ChatMessage("m1", DateTimeOffset.UtcNow, "human", "You", "hi"), ct);

        Assert.True(Directory.Exists(Path.Combine(dir.Path, "rooms")));
    }

    /// <summary>Deleting a Room's transcript removes the file, and later reads report empty rather than stale.</summary>
    [Fact]
    public async Task Delete_RemovesTheFile_AndReadAllThenReturnsEmpty()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var store = CreateStore(dir);
        await store.AppendAsync("room-1", new ChatMessage("m1", DateTimeOffset.UtcNow, "human", "You", "hi"), ct);
        var path = Path.Combine(dir.Path, "rooms", "room-1.jsonl");
        Assert.True(File.Exists(path));

        await store.DeleteAsync("room-1", ct);

        Assert.False(File.Exists(path));
        var result = await store.ReadAllAsync("room-1", ct);
        Assert.Empty(result);
    }

    /// <summary>A Room nobody has posted to yet has no transcript file, so deleting it is a silent no-op.</summary>
    [Fact]
    public async Task Delete_RoomWithNoFile_DoesNotThrow()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var store = CreateStore(dir);

        await store.DeleteAsync("never-posted-to", ct);

        var result = await store.ReadAllAsync("never-posted-to", ct);
        Assert.Empty(result);
    }

    /// <summary>DeleteAsync goes through the same path-traversal guard as AppendAsync and ReadAllAsync.</summary>
    [Theory]
    [InlineData("../x")]
    [InlineData("a b")]
    public async Task Delete_InvalidRoomId_Throws(string roomId)
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var store = CreateStore(dir);

        await Assert.ThrowsAsync<ArgumentException>(() => store.DeleteAsync(roomId, ct));
    }

    private static FileChatStore CreateStore(TempDataDir dir)
    {
        return new FileChatStore(dir.Options(), NullLogger<FileChatStore>.Instance);
    }
}
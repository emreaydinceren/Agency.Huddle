using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.NeedsYou;

/// <summary>Pins <see cref="FileChatStore.ReadTailAsync"/>: the last N Messages, read from the end of the file.</summary>
public sealed class ReadTailTests
{
    /// <summary>A Transcript shorter than the window returns all of it, in order.</summary>
    [Fact]
    public async Task FewerThanCount_ReturnsAll()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FileChatStore store = new(dir.Options(), NullLogger<FileChatStore>.Instance);
        await AppendAsync(store, 3, ct);

        IReadOnlyList<ChatMessage> tail = await store.ReadTailAsync("room-1", 10, ct);

        Assert.Equal(["m0", "m1", "m2"], tail.Select(message => message.Id).ToArray());
    }

    /// <summary>A Transcript of exactly the window returns all of it.</summary>
    [Fact]
    public async Task ExactlyCount_ReturnsAll()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FileChatStore store = new(dir.Options(), NullLogger<FileChatStore>.Instance);
        await AppendAsync(store, 4, ct);

        IReadOnlyList<ChatMessage> tail = await store.ReadTailAsync("room-1", 4, ct);

        Assert.Equal(4, tail.Count);
    }

    /// <summary>A long Transcript, spanning many read blocks and carrying multi-byte text, returns only the last N, whole.</summary>
    [Fact]
    public async Task LongTranscript_ReturnsOnlyTheLastN_WithLinesIntact()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FileChatStore store = new(dir.Options(), NullLogger<FileChatStore>.Instance);
        await AppendAsync(store, 400, ct, text: "héllo wörld ✓ " + new string('x', 200));

        IReadOnlyList<ChatMessage> tail = await store.ReadTailAsync("room-1", 5, ct);

        Assert.Equal(["m395", "m396", "m397", "m398", "m399"], tail.Select(message => message.Id).ToArray());
        Assert.All(tail, message => Assert.Equal("héllo wörld ✓ " + new string('x', 200), message.Text));
    }

    /// <summary>A line that is not JSON is skipped, as it is by the full read.</summary>
    [Fact]
    public async Task BadLine_IsSkipped()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FileChatStore store = new(dir.Options(), NullLogger<FileChatStore>.Instance);
        await AppendAsync(store, 2, ct);
        string path = Path.Combine(dir.Path, "rooms", "room-1.jsonl");
        await File.AppendAllTextAsync(path, "not json\n", Encoding.UTF8, ct);
        await AppendAsync(store, 1, ct, idPrefix: "n");

        IReadOnlyList<ChatMessage> tail = await store.ReadTailAsync("room-1", 10, ct);

        Assert.Equal(["m0", "m1", "n0"], tail.Select(message => message.Id).ToArray());
    }

    /// <summary>A Room with no Transcript, and a window of zero, both return nothing.</summary>
    [Fact]
    public async Task NoFile_OrZeroCount_ReturnsEmpty()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FileChatStore store = new(dir.Options(), NullLogger<FileChatStore>.Instance);
        await AppendAsync(store, 2, ct);

        Assert.Empty(await store.ReadTailAsync("room-2", 5, ct));
        Assert.Empty(await store.ReadTailAsync("room-1", 0, ct));
    }

    private static async Task AppendAsync(FileChatStore store, int count, CancellationToken ct, string text = "hi", string idPrefix = "m")
    {
        for (int index = 0; index < count; index++)
        {
            await store.AppendAsync("room-1", new ChatMessage($"{idPrefix}{index}", DateTimeOffset.UnixEpoch, "agent-1", "Nova", text), ct);
        }
    }
}

using Agency.Huddle.App.Data;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.Fakes;

/// <summary>Tests for <see cref="ChatStoreDecorator"/>, the pass-through base for Transcript store fakes.</summary>
public sealed class ChatStoreDecoratorTests
{
    /// <summary>Every member of an unmodified decorator reaches the wrapped store with the same arguments.</summary>
    [Fact]
    public async Task Members_NotOverridden_ForwardToInner()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RecordingStore inner = new();
        ChatStoreDecorator decorator = new(inner);
        ChatMessage message = new("m1", DateTimeOffset.UnixEpoch, "s-id", "s", "hi");

        await decorator.AppendAsync("r1", message, ct);
        IReadOnlyList<ChatMessage> all = await decorator.ReadAllAsync("r1", ct);
        IReadOnlyList<ChatMessage> tail = await decorator.ReadTailAsync("r1", 1, ct);
        bool has = await decorator.HasMessagesAsync("r1", ct);
        await decorator.DeleteAsync("r1", ct);

        Assert.Equal(["append:r1", "readAll:r1", "readTail:r1:1", "has:r1", "delete:r1"], inner.Calls);
        Assert.Single(all);
        Assert.Single(tail);
        Assert.True(has);
    }

    /// <summary>A subclass override of one member replaces only that member; the others still forward.</summary>
    [Fact]
    public async Task Override_ReplacesOnlyThatMember()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RecordingStore inner = new();
        AppendThrowingDecorator decorator = new(inner);
        ChatMessage message = new("m1", DateTimeOffset.UnixEpoch, "s-id", "s", "hi");

        await Assert.ThrowsAsync<InvalidOperationException>(() => decorator.AppendAsync("r1", message, ct));
        bool has = await decorator.HasMessagesAsync("r1", ct);

        Assert.True(has);
        Assert.Equal(["has:r1"], inner.Calls);
    }

    /// <summary>A decorator over nothing is rejected at construction.</summary>
    [Fact]
    public void Constructor_NullInner_Throws()
    {
        Assert.Throws<ArgumentNullException>(static () => new ChatStoreDecorator(null!));
    }

    /// <summary>A decorator whose append throws.</summary>
    /// <param name="inner">The wrapped store.</param>
    private sealed class AppendThrowingDecorator(IChatStore inner) : ChatStoreDecorator(inner)
    {
        /// <inheritdoc/>
        public override Task AppendAsync(string roomId, ChatMessage message, CancellationToken ct = default)
        {
            throw new InvalidOperationException("no append");
        }
    }

    /// <summary>A store that records each call and answers with one message.</summary>
    private sealed class RecordingStore : IChatStore
    {
        private readonly ChatMessage stored = new("m1", DateTimeOffset.UnixEpoch, "s-id", "s", "hi");

        /// <summary>The calls received, in order.</summary>
        public List<string> Calls { get; } = [];

        /// <inheritdoc/>
        public Task AppendAsync(string roomId, ChatMessage message, CancellationToken ct = default)
        {
            this.Calls.Add("append:" + roomId);
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<ChatMessage>> ReadAllAsync(string roomId, CancellationToken ct = default)
        {
            this.Calls.Add("readAll:" + roomId);
            return Task.FromResult<IReadOnlyList<ChatMessage>>([this.stored]);
        }

        /// <inheritdoc/>
        public Task DeleteAsync(string roomId, CancellationToken ct = default)
        {
            this.Calls.Add("delete:" + roomId);
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task<bool> HasMessagesAsync(string roomId, CancellationToken ct = default)
        {
            this.Calls.Add("has:" + roomId);
            return Task.FromResult(true);
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<ChatMessage>> ReadTailAsync(string roomId, int count, CancellationToken ct = default)
        {
            this.Calls.Add("readTail:" + roomId + ":" + count);
            return Task.FromResult<IReadOnlyList<ChatMessage>>([this.stored]);
        }
    }
}

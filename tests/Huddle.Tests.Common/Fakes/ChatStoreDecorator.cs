using Agency.Huddle.App.Data;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.Fakes;

/// <summary>
/// A pass-through <see cref="IChatStore"/>: every member forwards to the wrapped store and is virtual, so a
/// test double overrides only the one member it changes (a failing append, a gated append, a counted read)
/// instead of restating the other four.
/// </summary>
/// <param name="inner">The store every member forwards to.</param>
internal class ChatStoreDecorator(IChatStore inner) : IChatStore
{
    private readonly IChatStore inner = inner ?? throw new ArgumentNullException(nameof(inner));

    /// <summary>The wrapped store, for an override that does its own work and then forwards.</summary>
    protected IChatStore Inner => this.inner;

    /// <inheritdoc/>
    public virtual Task AppendAsync(string roomId, ChatMessage message, CancellationToken ct = default)
    {
        return this.inner.AppendAsync(roomId, message, ct);
    }

    /// <inheritdoc/>
    public virtual Task<IReadOnlyList<ChatMessage>> ReadAllAsync(string roomId, CancellationToken ct = default)
    {
        return this.inner.ReadAllAsync(roomId, ct);
    }

    /// <inheritdoc/>
    public virtual Task DeleteAsync(string roomId, CancellationToken ct = default)
    {
        return this.inner.DeleteAsync(roomId, ct);
    }

    /// <inheritdoc/>
    public virtual Task<bool> HasMessagesAsync(string roomId, CancellationToken ct = default)
    {
        return this.inner.HasMessagesAsync(roomId, ct);
    }

    /// <inheritdoc/>
    public virtual Task<IReadOnlyList<ChatMessage>> ReadTailAsync(string roomId, int count, CancellationToken ct = default)
    {
        return this.inner.ReadTailAsync(roomId, count, ct);
    }
}

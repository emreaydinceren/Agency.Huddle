namespace Agency.Huddle.App.Services;

/// <summary>
/// At most one pending item per Room, in memory: the shared core of <see cref="Agents.ProposalStore"/> and
/// <see cref="Questions.QuestionStore"/>. An empty Room stores a new item, the same owner replaces its own,
/// and any other owner is refused; <see cref="TryTake"/> removes an item only while its id still matches -
/// "first click wins", since the removal happens inside the lock. Every operation is O(1) under one
/// <see cref="Lock"/>. Raising events is left to the caller, which does it after the call returns and
/// therefore after the lock is released.
/// </summary>
/// <typeparam name="T">The pending item.</typeparam>
/// <param name="roomOf">The Room an item waits in.</param>
/// <param name="idOf">An item's id, matched by <see cref="TryTake"/>.</param>
/// <param name="ownerOf">The Agent that owns an item; only it may replace that item.</param>
internal sealed class PendingByRoom<T>(Func<T, string> roomOf, Func<T, string> idOf, Func<T, string> ownerOf)
    where T : class
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, T> items = new(StringComparer.Ordinal);

    /// <summary>Returns the item pending in <paramref name="roomId"/>, or <see langword="null"/> when none is.</summary>
    /// <param name="roomId">The Room to look up.</param>
    /// <returns>The pending item, or <see langword="null"/>.</returns>
    public T? Get(string roomId)
    {
        lock (this.gate)
        {
            return this.items.GetValueOrDefault(roomId);
        }
    }

    /// <summary>Returns every pending item, one per Room.</summary>
    /// <returns>A snapshot taken under the lock; later changes do not alter it.</returns>
    public IReadOnlyList<T> All()
    {
        lock (this.gate)
        {
            return [.. this.items.Values];
        }
    }

    /// <summary>Stores <paramref name="item"/> unless another owner's item is already pending in its Room.</summary>
    /// <param name="item">The item to store.</param>
    /// <returns>What happened, and, only when refused, the item that was already pending.</returns>
    public PendingPut<T> TryPut(T item)
    {
        string roomId = roomOf(item);

        lock (this.gate)
        {
            if (this.items.TryGetValue(roomId, out T? current)
                && !string.Equals(ownerOf(current), ownerOf(item), StringComparison.Ordinal))
            {
                return new PendingPut<T>(PendingPutResult.Refused, current);
            }

            this.items[roomId] = item;
            return new PendingPut<T>(current is null ? PendingPutResult.Stored : PendingPutResult.Replaced, null);
        }
    }

    /// <summary>Removes and returns the item pending in <paramref name="roomId"/>, but only while its id is <paramref name="id"/>.</summary>
    /// <param name="roomId">The Room to take from.</param>
    /// <param name="id">The id the caller expects to still be pending.</param>
    /// <returns>The removed item, or <see langword="null"/> when nothing matched.</returns>
    public T? TryTake(string roomId, string id)
    {
        lock (this.gate)
        {
            if (this.items.TryGetValue(roomId, out T? current) && string.Equals(idOf(current), id, StringComparison.Ordinal))
            {
                _ = this.items.Remove(roomId);
                return current;
            }

            return null;
        }
    }

    /// <summary>Removes any item pending in <paramref name="roomId"/>.</summary>
    /// <param name="roomId">The Room to clear.</param>
    /// <returns><see langword="true"/> when an item was removed.</returns>
    public bool Drop(string roomId)
    {
        lock (this.gate)
        {
            return this.items.Remove(roomId);
        }
    }
}

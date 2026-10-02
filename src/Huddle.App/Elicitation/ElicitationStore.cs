using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Services;

namespace Agency.Huddle.App.Elicitation;

/// <summary>
/// Holds every <see cref="PendingElicitation"/> waiting for the Human, in memory: one store, many cards
/// per Room, listed in the order they arrived (elicitation bridge, decision E-5). Every operation is
/// short under one <see cref="Lock"/>; completing a card's handler and raising
/// <see cref="RoomEvents.ElicitationsChanged"/> happen only after that lock is released. Dropping a card
/// resolves the request waiting on it as cancelled - it never just hides the card, because a request
/// nobody can answer would hold its Turn open for ever. The class is public only so
/// <see cref="ChatService"/>'s public constructor can take one (CS0051); every member stays
/// <see langword="internal"/>. A leaf singleton: it depends only on <see cref="RoomEvents"/>.
/// </summary>
/// <param name="events">The hub <see cref="RoomEvents.ElicitationsChanged"/> is raised on.</param>
public sealed class ElicitationStore(RoomEvents events)
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, List<PendingElicitation>> cards = new(StringComparer.Ordinal);
    private long lastSequence;

    /// <summary>
    /// Stores a new card for <paramref name="form"/> in <paramref name="roomId"/>, with a fresh id, the
    /// next sequence number and a handler that waits for the Human.
    /// </summary>
    /// <param name="roomId">The Room the card waits in.</param>
    /// <param name="askerAgentId">The id of the Agent that asked.</param>
    /// <param name="askerName">The asker's Name, as shown on the card.</param>
    /// <param name="form">The form to show.</param>
    /// <returns>The stored card.</returns>
    internal PendingElicitation Add(string roomId, string askerAgentId, string askerName, ElicitationForm form)
    {
        PendingElicitation card = new(
            Guid.NewGuid().ToString("N"),
            Interlocked.Increment(ref this.lastSequence),
            roomId,
            askerAgentId,
            askerName,
            form,
            new TaskCompletionSource<ElicitationResult>(TaskCreationOptions.RunContinuationsAsynchronously));

        lock (this.gate)
        {
            if (!this.cards.TryGetValue(roomId, out List<PendingElicitation>? inRoom))
            {
                inRoom = [];
                this.cards[roomId] = inRoom;
            }

            inRoom.Add(card);
        }

        events.PublishElicitationsChanged(roomId);
        return card;
    }

    /// <summary>Returns the cards waiting in <paramref name="roomId"/>, oldest first, as a snapshot.</summary>
    /// <param name="roomId">The Room to look up.</param>
    /// <returns>The waiting cards in sequence order; empty when none waits.</returns>
    internal IReadOnlyList<PendingElicitation> Get(string roomId)
    {
        lock (this.gate)
        {
            return this.cards.TryGetValue(roomId, out List<PendingElicitation>? inRoom)
                ? [.. inRoom.OrderBy(card => card.Sequence)]
                : [];
        }
    }

    /// <summary>
    /// Removes and returns the card <paramref name="id"/> waiting in <paramref name="roomId"/> - "first
    /// answer wins". The removal happens inside the lock, so of two callers racing the same id exactly one
    /// gets the card back. The card's handler is left waiting: the caller decides how the request ends
    /// and completes it.
    /// </summary>
    /// <param name="roomId">The Room to take from.</param>
    /// <param name="id">The card id the caller expects to still be waiting.</param>
    /// <returns>The removed card, or <see langword="null"/> when nothing matched.</returns>
    internal PendingElicitation? TryTake(string roomId, string id)
    {
        PendingElicitation? taken;

        lock (this.gate)
        {
            taken = this.RemoveLocked(roomId, id);
        }

        if (taken is not null)
        {
            events.PublishElicitationsChanged(roomId);
        }

        return taken;
    }

    /// <summary>
    /// Removes the card <paramref name="id"/> from <paramref name="roomId"/> and resolves the request
    /// waiting on it as <see cref="ElicitationCancelled"/>. Raises
    /// <see cref="RoomEvents.ElicitationsChanged"/> only when a card was removed.
    /// </summary>
    /// <param name="roomId">The Room the card waits in.</param>
    /// <param name="id">The card to drop.</param>
    /// <returns><see langword="true"/> when a card was removed.</returns>
    internal bool DropOne(string roomId, string id)
    {
        PendingElicitation? dropped;

        lock (this.gate)
        {
            dropped = this.RemoveLocked(roomId, id);
        }

        if (dropped is null)
        {
            return false;
        }

        _ = dropped.Completion.TrySetResult(new ElicitationCancelled());
        events.PublishElicitationsChanged(roomId);
        return true;
    }

    /// <summary>
    /// Removes every card waiting in <paramref name="roomId"/> and resolves each request waiting on one as
    /// <see cref="ElicitationCancelled"/>: a typed Human Message, an archive or a delete. Raises
    /// <see cref="RoomEvents.ElicitationsChanged"/> once, and only when something was removed.
    /// </summary>
    /// <param name="roomId">The Room to clear.</param>
    /// <returns><see langword="true"/> when at least one card was removed.</returns>
    internal bool Drop(string roomId)
    {
        List<PendingElicitation>? dropped;

        lock (this.gate)
        {
            _ = this.cards.Remove(roomId, out dropped);
        }

        if (dropped is null)
        {
            return false;
        }

        foreach (PendingElicitation card in dropped.OrderBy(card => card.Sequence))
        {
            _ = card.Completion.TrySetResult(new ElicitationCancelled());
        }

        events.PublishElicitationsChanged(roomId);
        return true;
    }

    /// <summary>Removes the card <paramref name="id"/> from <paramref name="roomId"/>. The caller holds <see cref="gate"/>.</summary>
    /// <param name="roomId">The Room the card waits in.</param>
    /// <param name="id">The card to remove.</param>
    /// <returns>The removed card, or <see langword="null"/> when nothing matched.</returns>
    private PendingElicitation? RemoveLocked(string roomId, string id)
    {
        if (!this.cards.TryGetValue(roomId, out List<PendingElicitation>? inRoom))
        {
            return null;
        }

        int index = inRoom.FindIndex(card => string.Equals(card.Id, id, StringComparison.Ordinal));
        if (index < 0)
        {
            return null;
        }

        PendingElicitation removed = inRoom[index];
        inRoom.RemoveAt(index);
        if (inRoom.Count == 0)
        {
            _ = this.cards.Remove(roomId);
        }

        return removed;
    }
}

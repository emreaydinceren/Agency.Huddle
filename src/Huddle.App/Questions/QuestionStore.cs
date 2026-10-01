using Agency.Huddle.App.Services;

namespace Agency.Huddle.App.Questions;

/// <summary>
/// Holds at most one waiting <see cref="PendingQuestions"/> card per Room, in memory (Questions
/// spec §6.3). Every operation is O(1) under one <see cref="Lock"/>, and
/// <see cref="RoomEvents.QuestionsChanged"/> is raised only after that lock is released. The class
/// is public only so <see cref="ChatService"/>'s public constructor can take one (CS0051); every
/// member stays <see langword="internal"/>. A leaf singleton: it depends only on
/// <see cref="RoomEvents"/>, so <see cref="ChatService"/> can call it without a cycle.
/// </summary>
/// <param name="events">The hub <see cref="RoomEvents.QuestionsChanged"/> is raised on.</param>
public sealed class QuestionStore(RoomEvents events)
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, PendingQuestions> cards = new(StringComparer.Ordinal);

    /// <summary>Returns the card waiting in <paramref name="roomId"/>, or <see langword="null"/> when none is.</summary>
    /// <param name="roomId">The Room to look up.</param>
    /// <returns>The waiting card, or <see langword="null"/>.</returns>
    internal PendingQuestions? Get(string roomId)
    {
        lock (this.gate)
        {
            return this.cards.GetValueOrDefault(roomId);
        }
    }

    /// <summary>
    /// Stores <paramref name="pending"/> under Questions spec §8.1: an empty Room stores it, the
    /// same asker replaces its own card, and any other asker is refused with the card already
    /// waiting handed back so the caller can name its asker.
    /// </summary>
    /// <param name="pending">The card to store.</param>
    /// <returns>What happened, and, only when refused, the card that was already waiting.</returns>
    internal QuestionPut TryPut(PendingQuestions pending)
    {
        QuestionPutResult result;
        PendingQuestions? existing;

        lock (this.gate)
        {
            if (!this.cards.TryGetValue(pending.RoomId, out existing))
            {
                this.cards[pending.RoomId] = pending;
                result = QuestionPutResult.Stored;
            }
            else if (string.Equals(existing.AskerAgentId, pending.AskerAgentId, StringComparison.Ordinal))
            {
                this.cards[pending.RoomId] = pending;
                result = QuestionPutResult.Replaced;
                existing = null;
            }
            else
            {
                result = QuestionPutResult.Refused;
            }
        }

        if (result is QuestionPutResult.Stored or QuestionPutResult.Replaced)
        {
            events.PublishQuestionsChanged(pending.RoomId);
        }

        return new QuestionPut(result, result == QuestionPutResult.Refused ? existing : null);
    }

    /// <summary>
    /// Removes and returns the card waiting in <paramref name="roomId"/>, but only when its id still
    /// matches <paramref name="id"/> - "first tap wins". The removal happens inside the lock, so of
    /// two callers racing the same id exactly one gets the card back. A tap on a card that was
    /// replaced meanwhile finds nothing instead of answering the new Questions with the old indexes.
    /// </summary>
    /// <param name="roomId">The Room to take from.</param>
    /// <param name="id">The card id the caller expects to still be waiting.</param>
    /// <returns>The removed card, or <see langword="null"/> when nothing matched.</returns>
    internal PendingQuestions? TryTake(string roomId, string id)
    {
        PendingQuestions? taken = null;

        lock (this.gate)
        {
            if (this.cards.TryGetValue(roomId, out var current) &&
                string.Equals(current.Id, id, StringComparison.Ordinal))
            {
                _ = this.cards.Remove(roomId);
                taken = current;
            }
        }

        if (taken is not null)
        {
            events.PublishQuestionsChanged(roomId);
        }

        return taken;
    }

    /// <summary>
    /// Removes any card waiting in <paramref name="roomId"/>: a Human Message, Dismiss, an archive or
    /// a delete. Raises <see cref="RoomEvents.QuestionsChanged"/> only when something was removed.
    /// </summary>
    /// <param name="roomId">The Room to clear.</param>
    /// <returns><see langword="true"/> when a card was removed.</returns>
    internal bool Drop(string roomId)
    {
        bool removed;

        lock (this.gate)
        {
            removed = this.cards.Remove(roomId);
        }

        if (removed)
        {
            events.PublishQuestionsChanged(roomId);
        }

        return removed;
    }
}

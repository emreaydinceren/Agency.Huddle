using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Microsoft.Data.Sqlite;

namespace Agency.Huddle.App.Questions;

/// <summary>
/// Holds at most one waiting <see cref="PendingQuestions"/> card per Room, in memory (Questions
/// spec §6.3), on a <see cref="PendingByRoom{T}"/>. <see cref="RoomEvents.QuestionsChanged"/> is raised
/// only after that core's lock is released. The class is public only so <see cref="ChatService"/>'s
/// public constructor can take one (CS0051); every member stays <see langword="internal"/>. A leaf
/// singleton: it depends only on <see cref="RoomEvents"/>, so <see cref="ChatService"/> can call it
/// without a cycle.
/// </summary>
/// <remarks>
/// With a <see cref="WaitingQuestionStore"/> every stored card is also written to <c>team.db</c> and every
/// removal deletes its row, and the cards found there are loaded when the store is built (Turn Queue spec §5a).
/// A database failure is logged and never fails the in-memory operation.
/// </remarks>
public sealed class QuestionStore
{
    private readonly PendingByRoom<PendingQuestions> cards = new(
        static card => card.RoomId,
        static card => card.Id,
        static card => card.AskerAgentId);

    private readonly RoomEvents events;
    private readonly WaitingQuestionStore? persisted;
    private readonly ILogger logger;

    /// <summary>Creates a store that keeps its cards in memory only.</summary>
    /// <param name="events">The hub <see cref="RoomEvents.QuestionsChanged"/> is raised on.</param>
    public QuestionStore(RoomEvents events)
        : this(events, null)
    {
    }

    /// <summary>Creates a store that writes through to <paramref name="persisted"/> and loads its rows now.</summary>
    /// <param name="events">The hub <see cref="RoomEvents.QuestionsChanged"/> is raised on.</param>
    /// <param name="persisted">The table the cards survive in; <see langword="null"/> keeps them in memory only.</param>
    /// <param name="logger">Receives a failed database call; <see langword="null"/> logs nothing.</param>
    internal QuestionStore(RoomEvents events, WaitingQuestionStore? persisted, ILogger<QuestionStore>? logger = null)
    {
        this.events = events;
        this.persisted = persisted;
        this.logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<QuestionStore>.Instance;

        if (persisted is not null)
        {
            this.Load(persisted);
        }
    }

    /// <summary>Returns every waiting card, one per Room.</summary>
    /// <returns>A snapshot; later changes do not alter it.</returns>
    internal IReadOnlyList<PendingQuestions> All() => this.cards.All();

    /// <summary>Returns the card waiting in <paramref name="roomId"/>, or <see langword="null"/> when none is.</summary>
    /// <param name="roomId">The Room to look up.</param>
    /// <returns>The waiting card, or <see langword="null"/>.</returns>
    internal PendingQuestions? Get(string roomId) => this.cards.Get(roomId);

    /// <summary>
    /// Stores <paramref name="pending"/> under Questions spec §8.1: an empty Room stores it, the
    /// same asker replaces its own card, and any other asker is refused with the card already
    /// waiting handed back so the caller can name its asker.
    /// </summary>
    /// <param name="pending">The card to store.</param>
    /// <returns>What happened, and, only when refused, the card that was already waiting.</returns>
    internal PendingPut<PendingQuestions> TryPut(PendingQuestions pending)
    {
        PendingPut<PendingQuestions> put = this.cards.TryPut(pending);

        if (put.Result is not PendingPutResult.Refused)
        {
            this.Persist(static (store, card) => store.Put(card), pending);
            this.events.PublishQuestionsChanged(pending.RoomId);
        }

        return put;
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
        PendingQuestions? taken = this.cards.TryTake(roomId, id);

        if (taken is not null)
        {
            this.Persist(static (store, room) => store.Delete(room), roomId);
            this.events.PublishQuestionsChanged(roomId);
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
        bool removed = this.cards.Drop(roomId);

        if (removed)
        {
            this.Persist(static (store, room) => store.Delete(room), roomId);
            this.events.PublishQuestionsChanged(roomId);
        }

        return removed;
    }

    /// <summary>Loads every stored card into memory. A row for a Room that already holds a card is ignored.</summary>
    /// <param name="store">The table to read.</param>
    private void Load(WaitingQuestionStore store)
    {
        try
        {
            foreach (PendingQuestions card in store.LoadAll())
            {
                _ = this.cards.TryPut(card);
            }
        }
        catch (SqliteException ex)
        {
            this.logger.LogWarning(ex, "Could not load the waiting Question cards; starting without them.");
        }
    }

    /// <summary>Runs one write against the table, logging a database failure so the in-memory change stands.</summary>
    /// <typeparam name="TArg">What the write needs.</typeparam>
    /// <param name="write">The write.</param>
    /// <param name="argument">Passed to <paramref name="write"/>.</param>
    private void Persist<TArg>(Action<WaitingQuestionStore, TArg> write, TArg argument)
    {
        if (this.persisted is null)
        {
            return;
        }

        try
        {
            write(this.persisted, argument);
        }
        catch (SqliteException ex)
        {
            this.logger.LogWarning(ex, "Could not update the stored Question card; the waiting card is kept in memory only.");
        }
    }
}

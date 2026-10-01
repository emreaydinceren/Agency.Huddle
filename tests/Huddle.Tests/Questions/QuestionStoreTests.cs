using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.Services;

namespace Agency.Huddle.Tests.Questions;

/// <summary>
/// Pins <see cref="QuestionStore"/> against Questions spec §6.3 and §8.1, with a real
/// <see cref="RoomEvents"/> - the store's only collaborator - the same style
/// <c>ProposalStoreTests</c> uses.
/// </summary>
public sealed class QuestionStoreTests
{
    /// <summary>§8.1 row 1: nothing waits in the Room, so <c>TryPut</c> stores the card.</summary>
    [Fact]
    public void TryPut_Empty_Stored()
    {
        RoomEvents events = new(NullLogger<RoomEvents>.Instance);
        QuestionStore store = new(events);
        PendingQuestions card = MakeCard("room-1", "agent-coach", "card-1");

        QuestionPut put = store.TryPut(card);

        Assert.Equal(QuestionPutResult.Stored, put.Result);
        Assert.Null(put.Existing);
        Assert.Equal(card, store.Get("room-1"));
    }

    /// <summary>§8.1 row 2: the same Agent asks again, so the new card replaces its waiting one.</summary>
    [Fact]
    public void TryPut_SameAsker_Replaced()
    {
        RoomEvents events = new(NullLogger<RoomEvents>.Instance);
        QuestionStore store = new(events);
        _ = store.TryPut(MakeCard("room-1", "agent-coach", "card-1"));
        PendingQuestions second = MakeCard("room-1", "agent-coach", "card-2");

        QuestionPut put = store.TryPut(second);

        Assert.Equal(QuestionPutResult.Replaced, put.Result);
        Assert.Equal(second, store.Get("room-1"));
    }

    /// <summary>§8.1 row 3: another Agent asks while a card waits, so it is refused and the waiting card is handed back to name its asker.</summary>
    [Fact]
    public void TryPut_OtherAsker_RefusedWithExisting()
    {
        RoomEvents events = new(NullLogger<RoomEvents>.Instance);
        QuestionStore store = new(events);
        PendingQuestions first = MakeCard("room-1", "agent-coach", "card-1");
        _ = store.TryPut(first);

        QuestionPut put = store.TryPut(MakeCard("room-1", "agent-nova", "card-2"));

        Assert.Equal(QuestionPutResult.Refused, put.Result);
        Assert.Equal(first, put.Existing);
        Assert.Equal(first, store.Get("room-1"));
    }

    /// <summary>One card per Room, not one per store: two Rooms hold independent cards.</summary>
    [Fact]
    public void TryPut_OtherRoom_StoredIndependently()
    {
        RoomEvents events = new(NullLogger<RoomEvents>.Instance);
        QuestionStore store = new(events);
        _ = store.TryPut(MakeCard("room-1", "agent-coach", "card-1"));

        QuestionPut put = store.TryPut(MakeCard("room-2", "agent-nova", "card-2"));

        Assert.Equal(QuestionPutResult.Stored, put.Result);
        Assert.NotNull(store.Get("room-1"));
        Assert.NotNull(store.Get("room-2"));
    }

    /// <summary>"First tap wins": the matching id returns the card exactly once, and a second take finds nothing.</summary>
    [Fact]
    public void TryTake_MatchingId_ReturnsOnceThenNull()
    {
        RoomEvents events = new(NullLogger<RoomEvents>.Instance);
        QuestionStore store = new(events);
        PendingQuestions card = MakeCard("room-1", "agent-coach", "card-1");
        _ = store.TryPut(card);

        PendingQuestions? first = store.TryTake("room-1", "card-1");
        PendingQuestions? second = store.TryTake("room-1", "card-1");

        Assert.Equal(card, first);
        Assert.Null(second);
        Assert.Null(store.Get("room-1"));
    }

    /// <summary>E-2: a tap on a card that was replaced meanwhile carries the old id and takes nothing; the new card is untouched.</summary>
    [Fact]
    public void TryTake_StaleId_ReturnsNullAndLeavesTheCard()
    {
        RoomEvents events = new(NullLogger<RoomEvents>.Instance);
        QuestionStore store = new(events);
        _ = store.TryPut(MakeCard("room-1", "agent-coach", "card-1"));
        PendingQuestions replacement = MakeCard("room-1", "agent-coach", "card-2");
        _ = store.TryPut(replacement);

        PendingQuestions? taken = store.TryTake("room-1", "card-1");

        Assert.Null(taken);
        Assert.Equal(replacement, store.Get("room-1"));
    }

    /// <summary><c>Drop</c> removes the card and says so; dropping an empty Room removes nothing and says so.</summary>
    [Fact]
    public void Drop_ReportsWhetherItRemovedAnything()
    {
        RoomEvents events = new(NullLogger<RoomEvents>.Instance);
        QuestionStore store = new(events);
        _ = store.TryPut(MakeCard("room-1", "agent-coach", "card-1"));

        bool first = store.Drop("room-1");
        bool second = store.Drop("room-1");

        Assert.True(first);
        Assert.False(second);
        Assert.Null(store.Get("room-1"));
    }

    /// <summary>
    /// <see cref="RoomEvents.QuestionsChanged"/> is raised for a stored card, a replacement, a take
    /// and a <c>Drop</c> that removed something, and not for a refused put, a stale take or a
    /// <c>Drop</c> that found nothing.
    /// </summary>
    [Fact]
    public void QuestionsChanged_RaisedOnlyWhenSomethingChanged()
    {
        RoomEvents events = new(NullLogger<RoomEvents>.Instance);
        QuestionStore store = new(events);
        List<string> raised = [];
        events.QuestionsChanged += raised.Add;

        _ = store.TryPut(MakeCard("room-1", "agent-coach", "card-1"));
        _ = store.TryPut(MakeCard("room-1", "agent-coach", "card-2"));
        Assert.Equal(["room-1", "room-1"], raised);

        _ = store.TryPut(MakeCard("room-1", "agent-nova", "card-3"));
        _ = store.TryTake("room-1", "stale");
        _ = store.Drop("room-9");
        Assert.Equal(2, raised.Count);

        _ = store.TryTake("room-1", "card-2");
        Assert.Equal(3, raised.Count);

        _ = store.TryPut(MakeCard("room-1", "agent-coach", "card-4"));
        _ = store.Drop("room-1");
        Assert.Equal(5, raised.Count);
    }

    /// <summary>
    /// The event is raised outside the store's lock: a handler that calls back into the store from
    /// the same thread must not deadlock, and sees the change already made.
    /// </summary>
    [Fact]
    public async Task QuestionsChanged_HandlerReadsTheStore_SeesTheChangeAndDoesNotDeadlock()
    {
        RoomEvents events = new(NullLogger<RoomEvents>.Instance);
        QuestionStore store = new(events);
        PendingQuestions? seen = null;
        events.QuestionsChanged += roomId => seen = store.Get(roomId);
        PendingQuestions card = MakeCard("room-1", "agent-coach", "card-1");

        Task put = Task.Run(() => store.TryPut(card), TestContext.Current.CancellationToken);

        await put.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(card, seen);
    }

    /// <summary>Builds a minimal valid card waiting in <paramref name="roomId"/>.</summary>
    /// <param name="roomId">The Room the card waits in.</param>
    /// <param name="askerAgentId">The asking Agent's id.</param>
    /// <param name="id">The card's own id.</param>
    private static PendingQuestions MakeCard(string roomId, string askerAgentId, string id)
    {
        Question question = new("What is your main goal?", ["Strength", "Cardio"], QuestionKind.SingleSelect);
        return new PendingQuestions(id, roomId, askerAgentId, "Coach", [question], DateTimeOffset.UnixEpoch);
    }
}

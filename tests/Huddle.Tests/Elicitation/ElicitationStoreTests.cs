using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Elicitation;
using Agency.Huddle.App.Services;
using static Agency.Huddle.Tests.Elicitation.ElicitationTestSupport;

namespace Agency.Huddle.Tests.Elicitation;

/// <summary>
/// Pins <see cref="ElicitationStore"/> (elicitation bridge, decision E-5): one store holds many waiting
/// cards per Room in arrival order, the first answer to a card wins, and dropping a card resolves the
/// handler waiting on it - never just hides it - with the change event raised after the lock is released.
/// </summary>
public sealed class ElicitationStoreTests
{
    private static readonly TimeSpan BoundedWait = TimeSpan.FromSeconds(5);

    /// <summary>A new card carries the Room, the asker, the form as given, a fresh id and a handler that is still waiting.</summary>
    [Fact]
    public void Add_ReturnsACardForTheRoomAndAsker()
    {
        ElicitationStore store = NewStore(out _);
        ElicitationForm form = FormOf(SingleQuestionSchema, SingleQuestionMessage);

        PendingElicitation card = store.Add("room-1", "agent-coach", "Coach", form);

        Assert.Matches("^[0-9a-f]{32}$", card.Id);
        Assert.Equal("room-1", card.RoomId);
        Assert.Equal("agent-coach", card.AskerAgentId);
        Assert.Equal("Coach", card.AskerName);
        Assert.Same(form, card.Form);
        Assert.False(card.Completion.Task.IsCompleted);
    }

    /// <summary>Two cards in one Room come back in the order they arrived, with strictly increasing sequence numbers, and another Room's card is not among them.</summary>
    [Fact]
    public void Add_TwoCardsInOneRoom_GetReturnsThemInArrivalOrder()
    {
        ElicitationStore store = NewStore(out _);
        PendingElicitation first = AddCard(store, "room-1");
        PendingElicitation other = AddCard(store, "room-2");
        PendingElicitation second = AddCard(store, "room-1");

        Assert.Equal([first.Id, second.Id], store.Get("room-1").Select(card => card.Id).ToArray());
        Assert.True(first.Sequence < other.Sequence);
        Assert.True(other.Sequence < second.Sequence);
    }

    /// <summary>Cards added from many threads at once all survive, with unique sequence numbers, and <c>Get</c> lists them in sequence order.</summary>
    [Fact]
    public void Add_ConcurrentThreads_KeepsEveryCardInSequenceOrder()
    {
        ElicitationStore store = NewStore(out _);

        RunOnThreads(32, _ => AddCard(store, "room-1"));

        long[] sequences = [.. store.Get("room-1").Select(card => card.Sequence)];
        Assert.Equal(32, sequences.Length);
        Assert.Equal(sequences.Order().ToArray(), sequences);
        Assert.Equal(32, sequences.Distinct().Count());
    }

    /// <summary>A Room nothing was asked in has no cards.</summary>
    [Fact]
    public void Get_UnknownRoom_IsEmpty()
    {
        ElicitationStore store = NewStore(out _);

        Assert.Empty(store.Get("room-1"));
    }

    /// <summary>First answer wins: the matching id returns the card once, and a second take of it finds nothing.</summary>
    [Fact]
    public void TryTake_MatchingId_ReturnsTheCardOnceThenNull()
    {
        ElicitationStore store = NewStore(out _);
        PendingElicitation card = AddCard(store, "room-1");

        PendingElicitation? first = store.TryTake("room-1", card.Id);
        PendingElicitation? second = store.TryTake("room-1", card.Id);

        Assert.Same(card, first);
        Assert.Null(second);
        Assert.Empty(store.Get("room-1"));
    }

    /// <summary>Answering one card leaves the Room's other card waiting, in place.</summary>
    [Fact]
    public void TryTake_OneOfTwoCards_LeavesTheOther()
    {
        ElicitationStore store = NewStore(out _);
        PendingElicitation first = AddCard(store, "room-1");
        PendingElicitation second = AddCard(store, "room-1");

        PendingElicitation? taken = store.TryTake("room-1", first.Id);

        Assert.Same(first, taken);
        Assert.Same(second, Assert.Single(store.Get("room-1")));
    }

    /// <summary>A tap carrying an id nothing holds takes nothing and disturbs nothing.</summary>
    [Fact]
    public void TryTake_StaleId_ReturnsNullAndLeavesTheCards()
    {
        ElicitationStore store = NewStore(out _);
        PendingElicitation card = AddCard(store, "room-1");

        PendingElicitation? taken = store.TryTake("room-1", "not-a-card");

        Assert.Null(taken);
        Assert.Same(card, Assert.Single(store.Get("room-1")));
    }

    /// <summary>A card's id answers only in its own Room: the same id asked of another Room finds nothing.</summary>
    [Fact]
    public void TryTake_ARoomThatDoesNotHoldTheCard_ReturnsNull()
    {
        ElicitationStore store = NewStore(out _);
        PendingElicitation card = AddCard(store, "room-1");

        PendingElicitation? taken = store.TryTake("room-2", card.Id);

        Assert.Null(taken);
        Assert.Same(card, Assert.Single(store.Get("room-1")));
    }

    /// <summary>Of sixteen threads racing to take one card, exactly one gets it.</summary>
    [Fact]
    public void TryTake_Concurrent_ExactlyOneCallerWins()
    {
        ElicitationStore store = NewStore(out _);
        PendingElicitation card = AddCard(store, "room-1");
        int winners = 0;

        RunOnThreads(
            16,
            worker =>
            {
                if (store.TryTake("room-1", card.Id) is not null)
                {
                    _ = Interlocked.Increment(ref winners);
                }
            });

        Assert.Equal(1, winners);
    }

    /// <summary>Taking a card does not resolve its handler: the caller decides how the request ends (accepted, declined) and completes it itself.</summary>
    [Fact]
    public void TryTake_DoesNotCompleteTheHandler()
    {
        ElicitationStore store = NewStore(out _);
        PendingElicitation card = AddCard(store, "room-1");

        _ = store.TryTake("room-1", card.Id);

        Assert.False(card.Completion.Task.IsCompleted);
    }

    /// <summary>Dropping a Room resolves every waiting handler of that Room as cancelled, and empties the Room.</summary>
    [Fact]
    public async Task Drop_CancelsEveryHandlerOfTheRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        ElicitationStore store = NewStore(out _);
        PendingElicitation first = AddCard(store, "room-1");
        PendingElicitation second = AddCard(store, "room-1");

        bool dropped = store.Drop("room-1");

        Assert.True(dropped);
        Assert.IsType<ElicitationCancelled>(await first.Completion.Task.WaitAsync(BoundedWait, ct));
        Assert.IsType<ElicitationCancelled>(await second.Completion.Task.WaitAsync(BoundedWait, ct));
        Assert.Empty(store.Get("room-1"));
    }

    /// <summary>Dropping one Room leaves another Room's cards, and the handlers waiting on them, alone.</summary>
    [Fact]
    public void Drop_LeavesOtherRoomsUntouched()
    {
        ElicitationStore store = NewStore(out _);
        _ = AddCard(store, "room-1");
        PendingElicitation other = AddCard(store, "room-2");

        _ = store.Drop("room-1");

        Assert.Same(other, Assert.Single(store.Get("room-2")));
        Assert.False(other.Completion.Task.IsCompleted);
    }

    /// <summary>Dropping a Room with nothing waiting reports that and raises no change event.</summary>
    [Fact]
    public void Drop_EmptyRoom_ReturnsFalseAndRaisesNoEvent()
    {
        ElicitationStore store = NewStore(out RoomEvents events);
        List<string> raised = [];
        events.ElicitationsChanged += raised.Add;

        bool dropped = store.Drop("room-1");

        Assert.False(dropped);
        Assert.Empty(raised);
    }

    /// <summary>Dropping one card resolves that handler as cancelled and leaves the Room's other card waiting.</summary>
    [Fact]
    public async Task DropOne_CancelsThatHandlerOnly()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        ElicitationStore store = NewStore(out _);
        PendingElicitation first = AddCard(store, "room-1");
        PendingElicitation second = AddCard(store, "room-1");

        bool dropped = store.DropOne("room-1", first.Id);

        Assert.True(dropped);
        Assert.IsType<ElicitationCancelled>(await first.Completion.Task.WaitAsync(BoundedWait, ct));
        Assert.Same(second, Assert.Single(store.Get("room-1")));
        Assert.False(second.Completion.Task.IsCompleted);
    }

    /// <summary>Dropping an id nothing holds - in a Room that has other cards, or in no Room - reports that, raises nothing and cancels nothing.</summary>
    [Fact]
    public void DropOne_UnknownId_ReturnsFalseAndRaisesNoEvent()
    {
        ElicitationStore store = NewStore(out RoomEvents events);
        PendingElicitation card = AddCard(store, "room-1");
        List<string> raised = [];
        events.ElicitationsChanged += raised.Add;

        bool inRoom = store.DropOne("room-1", "not-a-card");
        bool inOtherRoom = store.DropOne("room-2", card.Id);

        Assert.False(inRoom);
        Assert.False(inOtherRoom);
        Assert.Empty(raised);
        Assert.False(card.Completion.Task.IsCompleted);
    }

    /// <summary>Adding a card raises the change event once, with the Room's id.</summary>
    [Fact]
    public void Add_RaisesTheChangeEventOnce()
    {
        ElicitationStore store = NewStore(out RoomEvents events);
        List<string> raised = [];
        events.ElicitationsChanged += raised.Add;

        _ = AddCard(store, "room-1");

        Assert.Equal(["room-1"], raised);
    }

    /// <summary>Taking a card raises the change event once; a take that finds nothing raises none.</summary>
    [Fact]
    public void TryTake_RaisesTheChangeEventOnlyWhenItTookACard()
    {
        ElicitationStore store = NewStore(out RoomEvents events);
        PendingElicitation card = AddCard(store, "room-1");
        List<string> raised = [];
        events.ElicitationsChanged += raised.Add;

        _ = store.TryTake("room-1", "not-a-card");
        _ = store.TryTake("room-1", card.Id);
        _ = store.TryTake("room-1", card.Id);

        Assert.Equal(["room-1"], raised);
    }

    /// <summary>Dropping a Room with two cards raises the change event once, not once per card.</summary>
    [Fact]
    public void Drop_RaisesTheChangeEventOnce()
    {
        ElicitationStore store = NewStore(out RoomEvents events);
        _ = AddCard(store, "room-1");
        _ = AddCard(store, "room-1");
        List<string> raised = [];
        events.ElicitationsChanged += raised.Add;

        _ = store.Drop("room-1");

        Assert.Equal(["room-1"], raised);
    }

    /// <summary>Dropping one card raises the change event once.</summary>
    [Fact]
    public void DropOne_RaisesTheChangeEventOnce()
    {
        ElicitationStore store = NewStore(out RoomEvents events);
        PendingElicitation card = AddCard(store, "room-1");
        List<string> raised = [];
        events.ElicitationsChanged += raised.Add;

        _ = store.DropOne("room-1", card.Id);

        Assert.Equal(["room-1"], raised);
    }

    /// <summary>
    /// The change event is raised after the store's lock is released, for every operation that raises it:
    /// a handler that asks the store a question from another thread is answered. Raised inside the lock,
    /// that other thread would wait for a lock the handler's own thread still holds.
    /// </summary>
    /// <param name="operation">Which operation raises the event: add, take, drop or dropOne.</param>
    [Theory]
    [InlineData("add")]
    [InlineData("take")]
    [InlineData("drop")]
    [InlineData("dropOne")]
    public void ChangeEvent_IsRaisedOutsideTheLock(string operation)
    {
        ElicitationStore store = NewStore(out RoomEvents events);
        PendingElicitation card = AddCard(store, "room-1");
        bool answered = true;
        bool raised = false;
        events.ElicitationsChanged += room =>
        {
            raised = true;
            Thread probe = new(() => _ = store.Get(room));
            probe.Start();
            answered &= probe.Join(BoundedWait);
        };

        switch (operation)
        {
            case "add":
                _ = AddCard(store, "room-1");
                break;
            case "take":
                _ = store.TryTake("room-1", card.Id);
                break;
            case "drop":
                _ = store.Drop("room-1");
                break;
            default:
                _ = store.DropOne("room-1", card.Id);
                break;
        }

        Assert.True(raised);
        Assert.True(answered, "a handler's question to the store went unanswered while the event was being raised");
    }

    /// <summary>
    /// A handler's continuation never runs inside the store call that completes it: completing from
    /// <c>Drop</c> must not run the asker's code on the Human's thread, under the Room's post lock.
    /// The continuation here waits for a signal given only after the completing call returned; run
    /// inline it would wait out its timeout.
    /// </summary>
    [Fact]
    public async Task Add_CompletionContinuations_DoNotRunInline()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        ElicitationStore store = NewStore(out _);
        PendingElicitation card = AddCard(store, "room-1");
        using ManualResetEventSlim afterCompleting = new();
        Task<bool> continuation = card.Completion.Task.ContinueWith(
            _ => afterCompleting.Wait(BoundedWait),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        _ = store.Drop("room-1");
        afterCompleting.Set();

        Assert.True(await continuation.WaitAsync(TimeSpan.FromSeconds(10), ct));
    }

    /// <summary>Builds a store over a real hub, handing the hub back for tests that watch its events.</summary>
    /// <param name="events">The hub the store raises on.</param>
    private static ElicitationStore NewStore(out RoomEvents events)
    {
        events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        return new ElicitationStore(events);
    }

    /// <summary>Adds a card for the one-question form in <paramref name="roomId"/>.</summary>
    /// <param name="store">The store to add to.</param>
    /// <param name="roomId">The Room the card waits in.</param>
    private static PendingElicitation AddCard(ElicitationStore store, string roomId)
    {
        return store.Add(roomId, "agent-coach", "Coach", FormOf(SingleQuestionSchema, SingleQuestionMessage));
    }

    /// <summary>Runs <paramref name="body"/> on <paramref name="count"/> real threads released together, and fails if any does not finish in time.</summary>
    /// <param name="count">How many threads.</param>
    /// <param name="body">The work each thread does, given its index.</param>
    private static void RunOnThreads(int count, Action<int> body)
    {
        using ManualResetEventSlim start = new();
        List<Thread> threads = [];
        for (int index = 0; index < count; index++)
        {
            int captured = index;
            Thread thread = new(() =>
            {
                start.Wait();
                body(captured);
            });
            threads.Add(thread);
            thread.Start();
        }

        start.Set();
        foreach (Thread thread in threads)
        {
            Assert.True(thread.Join(BoundedWait), "a worker thread did not finish");
        }
    }
}

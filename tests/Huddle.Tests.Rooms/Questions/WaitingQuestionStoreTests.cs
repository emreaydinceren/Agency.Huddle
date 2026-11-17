using Agency.Huddle.App.Data;
using Agency.Huddle.App.NeedsYou;
using Agency.Huddle.App.Elicitation;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.Services;
using Microsoft.Data.Sqlite;
using Agency.Huddle.Tests.NeedsYou;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Questions;

/// <summary>
/// Pins slice 0 of the Turn Queue (spec §5a): a waiting <c>ask_human</c> card is written through to
/// <c>waiting_questions</c> in <c>team.db</c> and loaded again when the next <see cref="QuestionStore"/> starts.
/// </summary>
public sealed class WaitingQuestionStoreTests
{
    private static readonly DateTimeOffset Asked = new(2026, 11, 16, 8, 30, 15, 123, TimeSpan.Zero);

    /// <summary>A card put in one store comes back, field for field, from a new store over the same database.</summary>
    [Fact]
    public void Get_AfterRestart_ReturnsTheSameCard()
    {
        using TempDataDir dir = new();
        PendingQuestions card = MakeCard("room-1", "agent-coach", "card-1");
        _ = NewStore(dir).TryPut(card);

        QuestionStore restarted = NewStore(dir);

        PendingQuestions? loaded = restarted.Get("room-1");
        Assert.NotNull(loaded);
        Assert.Equal(card.Id, loaded.Id);
        Assert.Equal(card.AskerAgentId, loaded.AskerAgentId);
        Assert.Equal(card.AskerName, loaded.AskerName);
        Assert.Equal(card.AskedAt, loaded.AskedAt);
        Assert.Equal(card.Questions.Count, loaded.Questions.Count);
        for (int i = 0; i < card.Questions.Count; i++)
        {
            Assert.Equal(card.Questions[i].Text, loaded.Questions[i].Text);
            Assert.Equal(card.Questions[i].Options, loaded.Questions[i].Options);
            Assert.Equal(card.Questions[i].Kind, loaded.Questions[i].Kind);
        }
    }

    /// <summary>An answered card (TryTake) deletes its row, so the next start finds nothing.</summary>
    [Fact]
    public void TryTake_DeletesTheRow()
    {
        using TempDataDir dir = new();
        QuestionStore store = NewStore(dir);
        _ = store.TryPut(MakeCard("room-1", "agent-coach", "card-1"));

        _ = store.TryTake("room-1", "card-1");

        Assert.Empty(new WaitingQuestionStore(dir.Options()).LoadAll());
        Assert.Null(NewStore(dir).Get("room-1"));
    }

    /// <summary>A take with a stale id removes nothing, so the waiting row stays.</summary>
    [Fact]
    public void TryTake_StaleId_KeepsTheRow()
    {
        using TempDataDir dir = new();
        QuestionStore store = NewStore(dir);
        _ = store.TryPut(MakeCard("room-1", "agent-coach", "card-1"));

        _ = store.TryTake("room-1", "stale");

        Assert.Single(new WaitingQuestionStore(dir.Options()).LoadAll());
    }

    /// <summary>Drop (dismiss, a typed Human Message, archive and delete all call it) deletes the row.</summary>
    [Fact]
    public void Drop_DeletesTheRow()
    {
        using TempDataDir dir = new();
        QuestionStore store = NewStore(dir);
        _ = store.TryPut(MakeCard("room-1", "agent-coach", "card-1"));

        _ = store.Drop("room-1");

        Assert.Empty(new WaitingQuestionStore(dir.Options()).LoadAll());
        Assert.Null(NewStore(dir).Get("room-1"));
    }

    /// <summary>The same asker replacing its card leaves one row, holding the new card.</summary>
    [Fact]
    public void TryPut_SameAsker_KeepsOneRowWithTheNewCard()
    {
        using TempDataDir dir = new();
        QuestionStore store = NewStore(dir);
        _ = store.TryPut(MakeCard("room-1", "agent-coach", "card-1"));
        _ = store.TryPut(MakeCard("room-1", "agent-coach", "card-2"));

        PendingQuestions row = Assert.Single(new WaitingQuestionStore(dir.Options()).LoadAll());

        Assert.Equal("card-2", row.Id);
    }

    /// <summary>A refused put (another asker is waiting) writes nothing: the first card's row stays.</summary>
    [Fact]
    public void TryPut_OtherAsker_WritesNothing()
    {
        using TempDataDir dir = new();
        QuestionStore store = NewStore(dir);
        _ = store.TryPut(MakeCard("room-1", "agent-coach", "card-1"));
        _ = store.TryPut(MakeCard("room-1", "agent-nova", "card-2"));

        PendingQuestions row = Assert.Single(new WaitingQuestionStore(dir.Options()).LoadAll());

        Assert.Equal("card-1", row.Id);
    }

    /// <summary>A <c>team.db</c> made before the feature, with no <c>waiting_questions</c> table, gains it.</summary>
    [Fact]
    public void Open_DatabaseWithoutTheTable_CreatesIt()
    {
        using TempDataDir dir = new();
        _ = new JobStateStore(dir.Options());

        QuestionStore store = NewStore(dir);
        _ = store.TryPut(MakeCard("room-1", "agent-coach", "card-1"));

        Assert.Single(new WaitingQuestionStore(dir.Options()).LoadAll());
    }

    /// <summary>A row whose <c>questions_json</c> cannot be parsed is skipped; the other rows still load.</summary>
    [Fact]
    public void LoadAll_CorruptRow_SkipsItAndLoadsTheOthers()
    {
        using TempDataDir dir = new();
        QuestionStore store = NewStore(dir);
        _ = store.TryPut(MakeCard("room-1", "agent-coach", "card-1"));
        _ = store.TryPut(MakeCard("room-2", "agent-nova", "card-2"));
        using (SqliteConnection connection = new($"Data Source={Path.Combine(dir.Path, "team.db")}"))
        {
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "UPDATE waiting_questions SET questions_json = '{not json' WHERE room_id = 'room-1';";
            _ = command.ExecuteNonQuery();
        }

        QuestionStore restarted = NewStore(dir);

        Assert.Null(restarted.Get("room-1"));
        Assert.NotNull(restarted.Get("room-2"));
    }

    /// <summary>A loaded card is listed in Needs You at start-up, with no <c>QuestionsChanged</c> event raised.</summary>
    [Fact]
    public async Task NeedsYouTracker_OverLoadedCard_ListsTheQuestionReason()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        RoomEvents events = new(NullLogger<RoomEvents>.Instance);
        _ = new QuestionStore(events, new WaitingQuestionStore(fixture.Directory.Options()))
            .TryPut(MakeCard("room-1", "agent-coach", "card-1"));
        QuestionStore restarted = new(new RoomEvents(NullLogger<RoomEvents>.Instance), new WaitingQuestionStore(fixture.Directory.Options()));

        NeedsYouTracker tracker = new(
            new RoomEvents(NullLogger<RoomEvents>.Instance),
            restarted,
            new ElicitationStore(new RoomEvents(NullLogger<RoomEvents>.Instance)),
            fixture.Team,
            NullLogger<NeedsYouTracker>.Instance);

        NeedsYouEntry entry = Assert.Single(tracker.Snapshot());
        Assert.Equal("room-1", entry.RoomId);
        Assert.Equal(NeedsYouReason.Question, entry.Reasons);
    }

    /// <summary>A database that fails on write (its table is gone) is logged, and the in-memory put still succeeds.</summary>
    [Fact]
    public void TryPut_StoreFails_StillStoresInMemory()
    {
        using TempDataDir dir = new();
        QuestionStore store = NewStore(dir);
        using (SqliteConnection connection = new($"Data Source={Path.Combine(dir.Path, "team.db")}"))
        {
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "DROP TABLE waiting_questions;";
            _ = command.ExecuteNonQuery();
        }

        PendingPut<PendingQuestions> put = store.TryPut(MakeCard("room-1", "agent-coach", "card-1"));

        Assert.Equal(PendingPutResult.Stored, put.Result);
        Assert.NotNull(store.Get("room-1"));
        Assert.True(store.Drop("room-1"));
    }

    private static QuestionStore NewStore(TempDataDir dir) =>
        new(new RoomEvents(NullLogger<RoomEvents>.Instance), new WaitingQuestionStore(dir.Options()));

    private static PendingQuestions MakeCard(string roomId, string askerAgentId, string id)
    {
        Question first = new("What is your main goal?", ["Strength", "Cardio"], QuestionKind.SingleSelect);
        Question second = new("Which days?", ["Mon", "Wed", "Fri"], QuestionKind.MultiSelect);
        Question third = new("Rank these.", ["A", "B"], QuestionKind.RankPriorities);
        return new PendingQuestions(id, roomId, askerAgentId, "Coach", [first, second, third], Asked);
    }
}

using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Factories;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Agency.Huddle.Tests.Services;

/// <summary>
/// The Turn Queue ledger in <see cref="ChatService"/>'s post path (spec §5.4): who owes a Turn is written down before the
/// append, removed again if the append fails, and never able to fail the post itself.
/// </summary>
public sealed class ChatServiceTurnLedgerTests
{
    /// <summary>An owed Agent that is not in the Turn Queue's subscriber set gets no row.</summary>
    [Fact]
    public async Task Post_OwedNonSubscriber_WritesNoRow()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = await Harness.CreateAsync(ct);

        _ = await harness.Chat.PostAsync(harness.Room.Id, KnownIds.Human, "@Kai look", ct: ct);

        Assert.Empty(harness.Queue.OpenIn(harness.Room.Id));
    }

    /// <summary>Each owed subscriber gets one waiting row stamped with the Message's own time; an owed non-subscriber and an unowed subscriber get none.</summary>
    [Fact]
    public async Task Post_OwedSubscribers_WritesOneWaitingRowEach()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = await Harness.CreateAsync(ct);
        harness.Queue.Subscribe(harness.Nova.Id);
        harness.Queue.Subscribe(harness.Kai.Id);

        ChatMessage posted = await harness.Chat.PostAsync(harness.Room.Id, KnownIds.Human, "@Nova look", ct: ct);

        TurnQueueRow row = Assert.Single(harness.Queue.OpenIn(harness.Room.Id));
        Assert.Equal((harness.Nova.Id, posted.Id, harness.Room.Id), (row.AgentId, row.MessageId, row.RoomId));
        Assert.Equal(TurnQueueStatus.Waiting, row.Status);
        Assert.Equal(posted.Timestamp, row.PostedUtc);
        _ = await harness.Chat.PostAsync(harness.Room.Id, KnownIds.Human, "@Nova @Kai both", ct: ct);
        Assert.Equal(3, harness.Queue.OpenIn(harness.Room.Id).Count);
    }

    /// <summary>The rows are already in the ledger when the append runs, so a crash in between leaves a row, not a lost Message.</summary>
    [Fact]
    public async Task Post_RowsExistBeforeTheAppend()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        int rowsAtAppend = -1;
        using Harness harness = await Harness.CreateAsync(ct, onAppend: (queue, roomId) => rowsAtAppend = queue.OpenIn(roomId).Count);
        harness.Queue.Subscribe(harness.Nova.Id);

        _ = await harness.Chat.PostAsync(harness.Room.Id, KnownIds.Human, "@Nova look", ct: ct);

        Assert.Equal(1, rowsAtAppend);
    }

    /// <summary>An append that throws takes the rows back out and the post throws that same failure.</summary>
    [Fact]
    public async Task Post_AppendThrows_ForgetsTheRowsAndRethrows()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = await Harness.CreateAsync(ct, onAppend: (_, _) => throw new InvalidDataException("disk full"));
        harness.Queue.Subscribe(harness.Nova.Id);

        InvalidDataException failure = await Assert.ThrowsAsync<InvalidDataException>(
            () => harness.Chat.PostAsync(harness.Room.Id, KnownIds.Human, "@Nova look", ct: ct));

        Assert.Equal("disk full", failure.Message);
        Assert.Empty(harness.Queue.OpenIn(harness.Room.Id));
    }

    /// <summary>A ledger that throws on Record never fails the post: the Message is appended, just not owed.</summary>
    [Fact]
    public async Task Post_LedgerRecordThrows_PostStillSucceeds()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = await Harness.CreateAsync(ct, ledger: new ThrowingLedger());

        ChatMessage posted = await harness.Chat.PostAsync(harness.Room.Id, KnownIds.Human, "@Nova look", ct: ct);

        ChatMessage stored = Assert.Single(await harness.Transcript.ReadAllAsync(harness.Room.Id, ct));
        Assert.Equal(posted.Id, stored.Id);
    }

    /// <summary>A ledger whose Forget also throws does not hide the append's own failure.</summary>
    [Fact]
    public async Task Post_AppendAndForgetBothThrow_TheAppendFailureWins()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = await Harness.CreateAsync(ct, ledger: new ThrowingLedger(), onAppend: (_, _) => throw new InvalidDataException("disk full"));

        await Assert.ThrowsAsync<InvalidDataException>(() => harness.Chat.PostAsync(harness.Room.Id, KnownIds.Human, "@Nova look", ct: ct));
    }

    /// <summary>With <c>Team:TurnQueue:Enabled</c> false nothing is written.</summary>
    [Fact]
    public async Task Post_QueueDisabled_WritesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = await Harness.CreateAsync(ct, enabled: false);
        harness.Queue.Subscribe(harness.Nova.Id);

        _ = await harness.Chat.PostAsync(harness.Room.Id, KnownIds.Human, "@Nova look", ct: ct);

        Assert.Empty(harness.Queue.OpenIn(harness.Room.Id));
    }

    /// <summary>An Agent post that reaches the Budget owes nobody; the Human's next post, which resets it, owes again.</summary>
    [Fact]
    public async Task Post_BudgetReachedThenHumanPost_OwesOnlyAfterTheReset()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = await Harness.CreateAsync(ct);
        harness.Queue.Subscribe(harness.Kai.Id);
        await harness.ExhaustBudgetAsync(ct);
        Assert.Equal(2, harness.Queue.OpenIn(harness.Room.Id).Count);

        ChatMessage human = await harness.Chat.PostAsync(harness.Room.Id, KnownIds.Human, "@Kai go on", ct: ct);

        Assert.Equal(human.Id, harness.Queue.OpenIn(harness.Room.Id)[^1].MessageId);
        Assert.Equal(3, harness.Queue.OpenIn(harness.Room.Id).Count);
    }

    /// <summary>Continue records the re-delivered Message under the extended Budget, adding only the missing row.</summary>
    [Fact]
    public async Task ExtendBudget_RecordsForTheRedeliveredMessage()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = await Harness.CreateAsync(ct);
        harness.Queue.Subscribe(harness.Kai.Id);
        await harness.ExhaustBudgetAsync(ct);
        string lastId = (await harness.Transcript.ReadAllAsync(harness.Room.Id, ct))[^1].Id;
        Assert.DoesNotContain(harness.Queue.OpenIn(harness.Room.Id), row => string.Equals(row.MessageId, lastId, StringComparison.Ordinal));

        BudgetExtension extension = await harness.Chat.ExtendBudgetAsync(harness.Room.Id, ct);

        Assert.Equal(ExtendResult.Granted, extension.Result);
        TurnQueueRow added = Assert.Single(harness.Queue.OpenIn(harness.Room.Id), row => string.Equals(row.MessageId, lastId, StringComparison.Ordinal));
        Assert.Equal(harness.Kai.Id, added.AgentId);
        Assert.Equal(3, harness.Queue.OpenIn(harness.Room.Id).Count);
    }

    /// <summary>Continue on a spent Room raises <see cref="RoomEvents.BudgetUnpaused"/> once, for that Room.</summary>
    [Fact]
    public async Task ExtendBudget_RaisesBudgetUnpausedOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = await Harness.CreateAsync(ct);
        await harness.ExhaustBudgetAsync(ct);
        List<string> raised = [];
        harness.Events.BudgetUnpaused += raised.Add;

        _ = await harness.Chat.ExtendBudgetAsync(harness.Room.Id, ct);
        _ = await harness.Chat.ExtendBudgetAsync(harness.Room.Id, ct);

        Assert.Equal([harness.Room.Id], raised);
    }

    /// <summary>The Human speaking in a spent Room raises <see cref="RoomEvents.BudgetUnpaused"/> once.</summary>
    [Fact]
    public async Task HumanPost_InASpentRoom_RaisesBudgetUnpausedOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = await Harness.CreateAsync(ct);
        await harness.ExhaustBudgetAsync(ct);
        List<string> raised = [];
        harness.Events.BudgetUnpaused += raised.Add;

        _ = await harness.Chat.PostAsync(harness.Room.Id, KnownIds.Human, "go on", ct: ct);
        _ = await harness.Chat.PostAsync(harness.Room.Id, KnownIds.Human, "and again", ct: ct);

        Assert.Equal([harness.Room.Id], raised);
    }

    /// <summary>A post into an unpaused Room, an Agent's post and Huddle's post raise nothing.</summary>
    [Fact]
    public async Task OtherPosts_DoNotRaiseBudgetUnpaused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = await Harness.CreateAsync(ct);
        List<string> raised = [];
        harness.Events.BudgetUnpaused += raised.Add;

        _ = await harness.Chat.PostAsync(harness.Room.Id, KnownIds.Human, "hello", ct: ct);
        _ = await harness.Chat.PostAsync(harness.Room.Id, harness.Nova.Id, "hi", ct: ct);
        await harness.ExhaustBudgetAsync(ct);
        raised.Clear();
        _ = await harness.Chat.PostAsHuddleAsync(harness.Room.Id, "note", ct);

        Assert.Empty(raised);
    }

    private sealed class ThrowingLedger : ITurnLedger
    {
        public void Record(ChatMessage message, string roomId, IReadOnlyList<string> owedAgentIds) => throw new InvalidDataException("ledger down");

        public void Forget(string messageId) => throw new InvalidDataException("ledger down");
    }

    private sealed class HookedStore(IChatStore inner, Action<string> beforeAppend) : IChatStore
    {
        public Task AppendAsync(string roomId, ChatMessage message, CancellationToken ct = default)
        {
            beforeAppend(roomId);
            return inner.AppendAsync(roomId, message, ct);
        }

        public Task<IReadOnlyList<ChatMessage>> ReadAllAsync(string roomId, CancellationToken ct = default) => inner.ReadAllAsync(roomId, ct);

        public Task DeleteAsync(string roomId, CancellationToken ct = default) => inner.DeleteAsync(roomId, ct);

        public Task<bool> HasMessagesAsync(string roomId, CancellationToken ct = default) => inner.HasMessagesAsync(roomId, ct);
    }

    private sealed class Harness : IDisposable
    {
        private readonly TempDataDir dir;

        private Harness(
            TempDataDir dir, SqliteTeamDirectory team, (User Nova, User Kai, Room Room) seeded, bool enabled, ITurnLedger? ledger, Action<TurnQueueStore, string>? onAppend)
        {
            this.dir = dir;
            this.Team = team;
            this.Nova = seeded.Nova;
            this.Kai = seeded.Kai;
            this.Room = seeded.Room;
            this.Transcript = dir.CreateChatStore();
            this.Queue = new TurnQueueStore(dir.Options());
            Microsoft.Extensions.Options.IOptions<TeamOptions> options = dir.Options(o =>
            {
                o.AgentMessageBudget = 3;
                o.TurnQueue.Enabled = enabled;
            });
            FakeTimeProvider clock = new(new DateTimeOffset(2026, 11, 16, 9, 0, 0, TimeSpan.Zero));
            IChatStore store = onAppend is null
                ? this.Transcript
                : new HookedStore(this.Transcript, roomId => onAppend(this.Queue, roomId));
            RoomEvents events = new(NullLogger<RoomEvents>.Instance);
            this.Events = events;
            this.Chat = new ChatService(
                this.Team,
                store,
                events,
                new FakeMentionAliasSource(),
                options,
                new App.Agents.ProposalStore(events),
                new App.Questions.QuestionStore(events),
                NullLogger<ChatService>.Instance,
                timeProvider: clock)
            {
                OwedTurns = new OwedTurnPolicy(new RoomFollows()),
                Ledger = ledger ?? new TurnQueue(this.Queue, options, NullLogger<TurnQueue>.Instance, new Agency.Huddle.Tests.Desktop.FakeLifetime(), new FakeTimeProvider(clock.GetUtcNow().AddHours(1))),
            };
        }

        public SqliteTeamDirectory Team { get; }

        public FileChatStore Transcript { get; }

        public TurnQueueStore Queue { get; }

        public ChatService Chat { get; }

        public RoomEvents Events { get; }

        public User Nova { get; }

        public User Kai { get; }

        public Room Room { get; }

        public static async Task<Harness> CreateAsync(
            CancellationToken ct,
            bool enabled = true,
            ITurnLedger? ledger = null,
            Action<TurnQueueStore, string>? onAppend = null)
        {
            TempDataDir dir = new();
            SqliteTeamDirectory team = dir.CreateDirectory();
            await team.InitializeAsync("You", ct);
            User nova = await AddAgentAsync(team, "Nova", ct);
            User kai = await AddAgentAsync(team, "Kai", ct);
            User juno = await AddAgentAsync(team, "Juno", ct);
            Room room = await team.CreateRoomAsync("Pricing", [KnownIds.Human, nova.Id, kai.Id, juno.Id], ct);
            return new Harness(dir, team, (nova, kai, room), enabled, ledger, onAppend);
        }

        /// <summary>Nova posts three Mentions of Kai: the first two owe Kai, the third reaches the Budget and owes nobody.</summary>
        public async Task ExhaustBudgetAsync(CancellationToken ct)
        {
            _ = await this.Chat.PostAsync(this.Room.Id, KnownIds.Human, "start", ct: ct);
            for (int i = 0; i < 3; i++)
            {
                _ = await this.Chat.PostAsync(this.Room.Id, this.Nova.Id, "@Kai step " + i, ct: ct);
            }
        }

        public void Dispose() => this.dir.Dispose();

        private static async Task<User> AddAgentAsync(SqliteTeamDirectory team, string name, CancellationToken ct)
        {
            User? agent = await team.UpsertAgentUserAsync(name, null, ct);
            Assert.NotNull(agent);
            return agent;
        }
    }
}

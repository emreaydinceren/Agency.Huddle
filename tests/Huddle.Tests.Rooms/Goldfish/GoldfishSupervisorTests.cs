using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Goldfish;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Factories;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Agency.Huddle.Tests.Goldfish;

/// <summary>Pins <c>GoldfishSupervisor</c> (correction §3): the return sequence of spec §6.6, the state moves, the start-up pass, the failure paths, and <c>ChatService.PostAsHuddleQuietAsync</c>.</summary>
public sealed class GoldfishSupervisorTests
{
    /// <summary>Each of the five exits runs the return once: the runner is told, the result is posted before the return row, and the row ends with that exit.</summary>
    [Theory]
    [InlineData(GoldfishExit.Finished)]
    [InlineData(GoldfishExit.Stuck)]
    [InlineData(GoldfishExit.CalledBack)]
    [InlineData(GoldfishExit.Failed)]
    [InlineData(GoldfishExit.Restarted)]
    public async Task EndAsync_EveryExit_TellsRunnerPostsResultThenReturnRowAndEnds(GoldfishExit exit)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(Rig.Make("p1", rig.Nova.Id, rig.Room.Id, rig.Now));

        bool ended = await rig.Supervisor.EndAsync("p1", exit, null, ct);

        Assert.True(ended);
        Assert.Equal([(rig.Nova.Id, "p1", exit)], rig.Link.Ends);
        IReadOnlyList<ChatMessage> lines = await rig.HuddleLinesAsync(ct);
        Assert.Equal(
            [GoldfishLine.ResultText("Look into it", null, null, false), GoldfishLine.ReturnLine("Nova", exit, null, TimeSpan.Zero, true)],
            lines.Select(line => line.Text));
        Possession row = rig.Row("p1");
        Assert.Equal((PossessionState.Ended, exit, lines[0].Id), (row.State, row.Exit, row.ResultMessageId));
        Assert.NotNull(row.EndedAt);
    }

    /// <summary>The return row Mentions the Agent and so owes it a Turn; the result quotes the brief, which Mentions a Member, and owes nobody.</summary>
    [Fact]
    public async Task EndAsync_BriefMentionsMember_OnlyTheReturnRowOwesATurn()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Queue.Subscribe(rig.Nova.Id);
        rig.Queue.Subscribe(rig.Kai.Id);
        rig.Insert(Rig.Make("p1", rig.Nova.Id, rig.Room.Id, rig.Now) with { Brief = "Ask @Kai about the price" });

        _ = await rig.Supervisor.EndAsync("p1", GoldfishExit.Finished, null, ct);

        IReadOnlyList<ChatMessage> lines = await rig.HuddleLinesAsync(ct);
        TurnQueueRowSnapshot owed = Assert.Single(TurnQueueRows.Read(rig.DataDir));
        Assert.Equal((rig.Nova.Id, lines[1].Id), (owed.AgentId, owed.MessageId));
        Assert.Equal("Brief: Ask @Kai about the price\n\nNo result was posted.", lines[0].Text);
    }

    /// <summary>A second EndAsync for the same possession is a no-op: it returns false and posts, sends and writes nothing more.</summary>
    [Fact]
    public async Task EndAsync_Twice_SecondCallIsANoOp()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(Rig.Make("p1", rig.Nova.Id, rig.Room.Id, rig.Now));

        bool first = await rig.Supervisor.EndAsync("p1", GoldfishExit.Finished, null, ct);
        bool second = await rig.Supervisor.EndAsync("p1", GoldfishExit.CalledBack, "Quinn", ct);

        Assert.Equal((true, false), (first, second));
        Assert.Equal(2, (await rig.HuddleLinesAsync(ct)).Count);
        Assert.Single(rig.Link.Ends);
        Assert.Equal(GoldfishExit.Finished, rig.Row("p1").Exit);
    }

    /// <summary>Two calls at once return the possession exactly once.</summary>
    [Fact]
    public async Task EndAsync_Concurrent_ReturnsExactlyOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(Rig.Make("p1", rig.Nova.Id, rig.Room.Id, rig.Now));

        bool[] results = await Task.WhenAll(
            rig.Supervisor.EndAsync("p1", GoldfishExit.Finished, null, ct),
            rig.Supervisor.EndAsync("p1", GoldfishExit.Stuck, null, ct));

        Assert.Equal(1, results.Count(result => result));
        Assert.Equal(2, (await rig.HuddleLinesAsync(ct)).Count);
        Assert.Single(rig.Link.Ends);
    }

    /// <summary>A possession that does not exist is not returned: false, and the runner is not contacted.</summary>
    [Fact]
    public async Task EndAsync_UnknownPossession_ReturnsFalseAndDoesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);

        bool ended = await rig.Supervisor.EndAsync("nope", GoldfishExit.Finished, null, ct);

        Assert.False(ended);
        Assert.Empty(rig.Link.Ends);
        Assert.Empty(await rig.HuddleLinesAsync(ct));
    }

    /// <summary>A result already posted (its Message id is stored) is not posted again; the return row still is.</summary>
    [Fact]
    public async Task EndAsync_ResultMessageAlreadySet_DoesNotPostTheResultAgain()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(Rig.Make("p1", rig.Nova.Id, rig.Room.Id, rig.Now) with { ResultMessageId = "m-prior" });

        _ = await rig.Supervisor.EndAsync("p1", GoldfishExit.Finished, null, ct);

        ChatMessage line = Assert.Single(await rig.HuddleLinesAsync(ct));
        Assert.Equal(GoldfishLine.ReturnLine("Nova", GoldfishExit.Finished, null, TimeSpan.Zero, true), line.Text);
        Assert.Equal("m-prior", rig.Row("p1").ResultMessageId);
    }

    /// <summary>The exact words for a call back: who called, and the whole minutes rounded down.</summary>
    [Fact]
    public async Task EndAsync_CalledBackByQuinnAfterFourMinutes_PostsTheExactReturnLine()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(Rig.Make("p1", rig.Nova.Id, rig.Room.Id, rig.Now));
        rig.Clock.Advance(TimeSpan.FromSeconds(260));

        _ = await rig.Supervisor.EndAsync("p1", GoldfishExit.CalledBack, "Quinn", ct);

        IReadOnlyList<ChatMessage> lines = await rig.HuddleLinesAsync(ct);
        Assert.Equal("@Nova is back. Called back by Quinn after 4 minutes. Partial result above.", lines[1].Text);
        Assert.Equal("Quinn", rig.Row("p1").ExitBy);
    }

    /// <summary>The Room Budget is never consulted: with it exhausted, the result and the return row are still posted.</summary>
    [Fact]
    public async Task EndAsync_RoomBudgetExhausted_StillPostsBothMessages()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct, agentMessageBudget: 1);
        _ = await rig.Chat.PostAsync(rig.Room.Id, rig.Nova.Id, "spending the budget", ct: ct);
        Assert.True(rig.Chat.GetBudget(rig.Room.Id).Exhausted);
        rig.Insert(Rig.Make("p1", rig.Nova.Id, rig.Room.Id, rig.Now));

        bool ended = await rig.Supervisor.EndAsync("p1", GoldfishExit.Finished, null, ct);

        Assert.True(ended);
        Assert.Equal(2, (await rig.HuddleLinesAsync(ct)).Count);
    }

    /// <summary>An Agent that is no longer a Member gets a return row without the <c>@</c>, named by its stored id, and owes no Turn.</summary>
    [Fact]
    public async Task EndAsync_AgentNotAMember_PostsReturnRowWithoutMention()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Queue.Subscribe(rig.Outsider.Id);
        rig.Insert(Rig.Make("p1", rig.Outsider.Id, rig.Room.Id, rig.Now));

        _ = await rig.Supervisor.EndAsync("p1", GoldfishExit.Finished, null, ct);

        IReadOnlyList<ChatMessage> lines = await rig.HuddleLinesAsync(ct);
        Assert.Equal(GoldfishLine.ReturnLine(rig.Outsider.Id, GoldfishExit.Finished, null, TimeSpan.Zero, false), lines[1].Text);
        Assert.Empty(TurnQueueRows.Read(rig.DataDir));
        Assert.Equal(PossessionState.Ended, rig.Row("p1").State);
    }

    /// <summary>A Room that no longer exists cannot take the Messages, and the possession still ends.</summary>
    [Fact]
    public async Task EndAsync_RoomGone_StillWritesEnded()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(Rig.Make("p1", rig.Nova.Id, "no-such-room", rig.Now));

        bool ended = await rig.Supervisor.EndAsync("p1", GoldfishExit.Finished, null, ct);

        Assert.True(ended);
        Assert.Equal((PossessionState.Ended, GoldfishExit.Finished), (rig.Row("p1").State, rig.Row("p1").Exit));
        Assert.Empty(await rig.HuddleLinesAsync(ct));
    }

    /// <summary>A runner link that throws is logged and the return carries on to its Messages and its end.</summary>
    [Fact]
    public async Task EndAsync_LinkThrows_StillPostsAndEnds()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Link.Throws = true;
        rig.Insert(Rig.Make("p1", rig.Nova.Id, rig.Room.Id, rig.Now));

        bool ended = await rig.Supervisor.EndAsync("p1", GoldfishExit.Finished, null, ct);

        Assert.True(ended);
        Assert.Equal(2, (await rig.HuddleLinesAsync(ct)).Count);
        Assert.Equal(PossessionState.Ended, rig.Row("p1").State);
    }

    /// <summary>A Transcript write that throws something other than a chat error still ends the row, and the Agent is free to be possessed again.</summary>
    [Fact]
    public async Task EndAsync_PostThrowsIOException_StillEndsRowAndFreesTheAgent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(Rig.Make("p1", rig.Nova.Id, rig.Room.Id, rig.Now));
        rig.Transcript.FailAppend = true;

        bool ended = await rig.Supervisor.EndAsync("p1", GoldfishExit.Finished, null, ct);

        Assert.True(ended);
        Assert.Equal((PossessionState.Ended, GoldfishExit.Finished), (rig.Row("p1").State, rig.Row("p1").Exit));
        Assert.True(rig.Store.TryInsert(Rig.Make("p2", rig.Nova.Id, rig.Room.Id, rig.Now)));
    }

    /// <summary>A turn that ends without the Agent saying it is done moves a Starting or Working possession to Waiting, and sends nothing.</summary>
    [Theory]
    [InlineData("Starting")]
    [InlineData("Working")]
    public async Task OnTurnEndedAsync_NotDone_MovesToWaitingWithoutALinkCall(string fromName)
    {
        PossessionState from = Enum.Parse<PossessionState>(fromName);
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(Rig.Make("p1", rig.Nova.Id, rig.Room.Id, rig.Now) with { State = from });

        await rig.Supervisor.OnTurnEndedAsync("p1", false, ct);

        Assert.Equal(PossessionState.Waiting, rig.Row("p1").State);
        Assert.Empty(rig.Link.Ends);
        Assert.Empty(await rig.HuddleLinesAsync(ct));
    }

    /// <summary>A Mention of a Waiting Agent in its Room puts the possession back to Working; a Mention elsewhere leaves it.</summary>
    [Fact]
    public async Task OnMentionAsync_WaitingAgent_MovesToWorkingOnlyInItsRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(Rig.Make("p1", rig.Nova.Id, rig.Room.Id, rig.Now) with { State = PossessionState.Waiting });

        await rig.Supervisor.OnMentionAsync(rig.Nova.Id, "other-room", ct);
        Assert.Equal(PossessionState.Waiting, rig.Row("p1").State);
        await rig.Supervisor.OnMentionAsync(rig.Nova.Id, rig.Room.Id, ct);

        Assert.Equal(PossessionState.Working, rig.Row("p1").State);
    }

    /// <summary>There are no timers: a week on the clock leaves an open possession open, with nothing posted or sent.</summary>
    [Fact]
    public async Task OpenPossession_AWeekPasses_StaysOpenAndUntouched()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(Rig.Make("p1", rig.Nova.Id, rig.Room.Id, rig.Now));

        rig.Clock.Advance(TimeSpan.FromDays(7));

        Assert.Equal(PossessionState.Working, rig.Row("p1").State);
        Assert.Equal("p1", rig.Supervisor.FindOpen(rig.Nova.Id)?.Id);
        Assert.Empty(rig.Link.Ends);
        Assert.Empty(await rig.HuddleLinesAsync(ct));
    }

    /// <summary>A lost runner connection ends the Agent's possession as Failed, with the Adapter-stopped line.</summary>
    [Fact]
    public async Task OnRunnerLostAsync_OpenPossession_EndsFailed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(Rig.Make("p1", rig.Nova.Id, rig.Room.Id, rig.Now));

        await rig.Supervisor.OnRunnerLostAsync(rig.Nova.Id, ct);

        Possession row = rig.Row("p1");
        Assert.Equal((PossessionState.Ended, GoldfishExit.Failed, null), (row.State, row.Exit, row.ExitBy));
        Assert.Equal("@Nova is back. Didn't finish: the Adapter stopped.", (await rig.HuddleLinesAsync(ct))[1].Text);
        Assert.Null(rig.Supervisor.FindOpen(rig.Nova.Id));
    }

    /// <summary>The start-up pass ends every open row as Restarted, a Returning one too, whether or not goldfish is enabled.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StartAsync_OpenRows_AreEndedRestarted(bool enabled)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct, goldfishEnabled: enabled);
        rig.Insert(Rig.Make("working", rig.Nova.Id, rig.Room.Id, rig.Now));
        rig.Insert(Rig.Make("returning", rig.Kai.Id, rig.Room.Id, rig.Now) with { State = PossessionState.Returning, Exit = GoldfishExit.CalledBack, ExitBy = "Quinn" });

        await rig.Supervisor.StartAsync(ct);

        Assert.Empty(rig.Store.Open());
        Assert.Equal((PossessionState.Ended, GoldfishExit.Restarted), (rig.Row("working").State, rig.Row("working").Exit));
        Assert.Equal((PossessionState.Ended, GoldfishExit.Restarted), (rig.Row("returning").State, rig.Row("returning").Exit));
    }

    /// <summary>The start-up pass prunes ended rows older than KeepDays and keeps the newer ones.</summary>
    [Fact]
    public async Task StartAsync_EndedRows_AreKeptOnlyWithinKeepDays()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(Rig.Ended("old", rig.Nova.Id, rig.Now.AddDays(-31)));
        rig.Insert(Rig.Ended("recent", rig.Kai.Id, rig.Now.AddDays(-29)));

        await rig.Supervisor.StartAsync(ct);

        Assert.Null(rig.Store.Get("old"));
        Assert.NotNull(rig.Store.Get("recent"));
    }

    /// <summary>A runner link that fails during the start-up pass never stops it: StartAsync completes and the row is ended.</summary>
    [Fact]
    public async Task StartAsync_LinkThrows_StillCompletesAndEndsTheRow()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Link.Throws = true;
        rig.Insert(Rig.Make("p1", rig.Nova.Id, rig.Room.Id, rig.Now));

        await rig.Supervisor.StartAsync(ct);

        Assert.Equal(PossessionState.Ended, rig.Row("p1").State);
    }

    /// <summary>The quiet post is Huddle's own Message that Mentions nobody, so a <c>@Member</c> in it owes no Turn, where the ordinary Huddle post does.</summary>
    [Fact]
    public async Task PostAsHuddleQuietAsync_MemberMentioned_OwesNoTurn()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Queue.Subscribe(rig.Nova.Id);

        ChatMessage quiet = await rig.Chat.PostAsHuddleQuietAsync(rig.Room.Id, "@Nova quoted words", ct);
        Assert.Empty(TurnQueueRows.Read(rig.DataDir));
        ChatMessage loud = await rig.Chat.PostAsHuddleAsync(rig.Room.Id, "@Nova real words", ct);

        Assert.Equal((KnownIds.Huddle, "@Nova quoted words"), (quiet.SenderId, quiet.Text));
        TurnQueueRowSnapshot owed = Assert.Single(TurnQueueRows.Read(rig.DataDir));
        Assert.Equal(loud.Id, owed.MessageId);
    }

    private sealed class FailableChatStore(IChatStore inner) : IChatStore
    {
        public bool FailAppend { get; set; }

        public Task AppendAsync(string roomId, ChatMessage message, CancellationToken ct = default) =>
            this.FailAppend ? Task.FromException(new IOException("The Transcript could not be written.")) : inner.AppendAsync(roomId, message, ct);

        public Task<IReadOnlyList<ChatMessage>> ReadAllAsync(string roomId, CancellationToken ct = default) => inner.ReadAllAsync(roomId, ct);

        public Task DeleteAsync(string roomId, CancellationToken ct = default) => inner.DeleteAsync(roomId, ct);

        public Task<bool> HasMessagesAsync(string roomId, CancellationToken ct = default) => inner.HasMessagesAsync(roomId, ct);
    }

    private sealed class RecordingLink : IGoldfishRunnerLink
    {
        public List<(string AgentId, string PossessionId, GoldfishExit Exit)> Ends { get; } = [];

        public bool Throws { get; set; }

        public Task SendBeginAsync(string agentId, GoldfishBegin begin, CancellationToken ct) => Task.CompletedTask;

        public Task SendEndAsync(string agentId, string possessionId, GoldfishExit exit, CancellationToken ct)
        {
            if (this.Throws)
            {
                throw new InvalidOperationException("runner link down");
            }

            this.Ends.Add((agentId, possessionId, exit));
            return Task.CompletedTask;
        }
    }

    private sealed class Rig : IDisposable
    {
        private readonly TempDataDir dir;

        private Rig(TempDataDir dir, SqliteTeamDirectory team, (User Nova, User Kai, User Outsider, Room Room) seeded, int agentMessageBudget, bool goldfishEnabled)
        {
            this.dir = dir;
            this.Nova = seeded.Nova;
            this.Kai = seeded.Kai;
            this.Outsider = seeded.Outsider;
            this.Room = seeded.Room;
            this.Clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero));
            this.Queue = new TurnQueueStore(dir.Options());
            this.Options = dir.Options(o =>
            {
                o.AgentMessageBudget = agentMessageBudget;
                o.Goldfish.Enabled = goldfishEnabled;
                o.Goldfish.KeepDays = 30;
            });
            FileChatStore file = dir.CreateChatStore();
            this.File = file;
            this.Transcript = new FailableChatStore(file);
            RoomEvents events = new(NullLogger<RoomEvents>.Instance);
            this.Chat = new ChatService(
                team,
                this.Transcript,
                events,
                new FakeMentionAliasSource(),
                this.Options,
                new App.Agents.ProposalStore(events),
                new App.Questions.QuestionStore(events),
                NullLogger<ChatService>.Instance,
                timeProvider: this.Clock)
            {
                OwedTurns = new OwedTurnPolicy(new RoomFollows()),
                Ledger = new TurnQueue(this.Queue, this.Options, NullLogger<TurnQueue>.Instance, new Agency.Huddle.Tests.Desktop.FakeLifetime(), this.Clock),
            };
            this.Store = new GoldfishStore(this.Options);
            this.Supervisor = new GoldfishSupervisor(this.Store, team, this.Chat, this.Link, this.Options, NullLogger<GoldfishSupervisor>.Instance, this.Clock);
        }

        public FileChatStore File { get; }

        public FailableChatStore Transcript { get; }

        public TurnQueueStore Queue { get; }

        public ChatService Chat { get; }

        public FakeTimeProvider Clock { get; }

        public Microsoft.Extensions.Options.IOptions<TeamOptions> Options { get; }

        public GoldfishStore Store { get; }

        public RecordingLink Link { get; } = new();

        public GoldfishSupervisor Supervisor { get; }

        public User Nova { get; }

        public User Kai { get; }

        public User Outsider { get; }

        public Room Room { get; }

        public string DataDir => this.dir.Path;

        public DateTimeOffset Now => this.Clock.GetUtcNow();

        public static async Task<Rig> CreateAsync(CancellationToken ct, int agentMessageBudget = 40, bool goldfishEnabled = true)
        {
            TempDataDir dir = new();
            SqliteTeamDirectory team = dir.CreateDirectory();
            await team.InitializeAsync("You", ct);
            User nova = await AddAgentAsync(team, "Nova", ct);
            User kai = await AddAgentAsync(team, "Kai", ct);
            User outsider = await AddAgentAsync(team, "Juno", ct);
            Room room = await team.CreateRoomAsync("Pricing", [KnownIds.Human, nova.Id, kai.Id], ct);
            return new Rig(dir, team, (nova, kai, outsider, room), agentMessageBudget, goldfishEnabled);
        }

        /// <summary>An open possession in the Working state, brief "Look into it".</summary>
        public static Possession Make(string id, string agentId, string roomId, DateTimeOffset startedAt) =>
            new(id, agentId, roomId, null, "Look into it", PossessionState.Working, null, null, startedAt, null, 0, 0m, null, null);

        /// <summary>An ended possession whose end is the given time.</summary>
        public static Possession Ended(string id, string agentId, DateTimeOffset endedAt) =>
            new(id, agentId, "r1", null, "Look into it", PossessionState.Ended, GoldfishExit.Finished, null, endedAt.AddHours(-1), endedAt, 1, 0m, null, null);

        public void Insert(Possession possession) => Assert.True(this.Store.TryInsert(possession));

        public Possession Row(string id) => this.Store.Get(id) ?? throw new InvalidOperationException($"No possession '{id}'.");

        public async Task<IReadOnlyList<ChatMessage>> HuddleLinesAsync(CancellationToken ct) =>
            [.. (await this.File.ReadAllAsync(this.Room.Id, ct)).Where(message => string.Equals(message.SenderId, KnownIds.Huddle, StringComparison.Ordinal))];

        public void Dispose() => this.dir.Dispose();

        private static async Task<User> AddAgentAsync(SqliteTeamDirectory team, string name, CancellationToken ct)
        {
            User? agent = await team.UpsertAgentUserAsync(name, null, ct);
            Assert.NotNull(agent);
            return agent;
        }
    }
}

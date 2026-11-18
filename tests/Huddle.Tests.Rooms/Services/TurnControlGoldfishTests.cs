using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Goldfish;
using Agency.Huddle.App.Profile;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Desktop;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.Ui;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Agency.Huddle.Tests.Services;

/// <summary>Pins E-13 (Goldfish slice 4, step 5): a Human's Stop on a goldfish's Draft is a comeback by the Human, so the possession ends CalledBack and no StopTurn is sent.</summary>
public sealed class TurnControlGoldfishTests
{
    /// <summary>A Stop for an Agent Working in goldfish mode in that Room calls it back as the Human and does not reach the gateway.</summary>
    [Fact]
    public async Task StopAsync_WorkingPossessionInTheRoom_EndsCalledBackByTheHumanAndSendsNoStopTurn()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(PossessionState.Working);

        await rig.Control.StopAsync(rig.Nova.Id, rig.Room.Id, ct);

        Possession row = rig.Row();
        Assert.Equal((PossessionState.Ended, GoldfishExit.CalledBack, "Quinn"), (row.State, row.Exit, row.ExitBy));
        Assert.Equal([(rig.Nova.Id, "p1", GoldfishExit.CalledBack)], rig.Link.Ends);
        Assert.Empty(rig.Gateway.StopTurnCalls);
    }

    /// <summary>A possession that is Waiting for a Mention is live too, so a Stop calls it back the same way.</summary>
    [Fact]
    public async Task StopAsync_WaitingPossessionInTheRoom_EndsCalledBackByTheHumanAndSendsNoStopTurn()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(PossessionState.Waiting);

        await rig.Control.StopAsync(rig.Nova.Id, rig.Room.Id, ct);

        Assert.Equal((PossessionState.Ended, GoldfishExit.CalledBack, "Quinn"), (rig.Row().State, rig.Row().Exit, rig.Row().ExitBy));
        Assert.Empty(rig.Gateway.StopTurnCalls);
    }

    /// <summary>A possession already Returning is on its way back, so a Stop is the normal stop: it is not ended again and the gateway is asked.</summary>
    [Fact]
    public async Task StopAsync_ReturningPossession_IsANormalStop()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(PossessionState.Working);
        Assert.True(rig.Store.BeginReturn("p1", GoldfishExit.Finished, null));

        await rig.Control.StopAsync(rig.Nova.Id, rig.Room.Id, ct);

        Assert.Empty(rig.Link.Ends);
        Assert.Equal((rig.Nova.Id, rig.Room.Id), Assert.Single(rig.Gateway.StopTurnCalls));
        Assert.Equal(PossessionState.Returning, rig.Row().State);
    }

    /// <summary>The same Agent in goldfish mode in another Room is not what the Stop is about: the Stop here is a normal one.</summary>
    [Fact]
    public async Task StopAsync_SameAgentPossessedInAnotherRoom_IsANormalStop()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(PossessionState.Working, roomId: "other-room");

        await rig.Control.StopAsync(rig.Nova.Id, rig.Room.Id, ct);

        Assert.Empty(rig.Link.Ends);
        Assert.Equal((rig.Nova.Id, rig.Room.Id), Assert.Single(rig.Gateway.StopTurnCalls));
        Assert.Equal(PossessionState.Working, rig.Row().State);
    }

    /// <summary>Another Agent's possession in this Room does not make a Stop for a different Agent a comeback.</summary>
    [Fact]
    public async Task StopAsync_OtherAgentPossessed_IsANormalStop()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(PossessionState.Working);

        await rig.Control.StopAsync(rig.Kai.Id, rig.Room.Id, ct);

        Assert.Empty(rig.Link.Ends);
        Assert.Equal((rig.Kai.Id, rig.Room.Id), Assert.Single(rig.Gateway.StopTurnCalls));
        Assert.Equal(PossessionState.Working, rig.Row().State);
    }

    /// <summary>With no goldfish supervisor wired (the optional parameters left out), a Stop is the normal stop.</summary>
    [Fact]
    public async Task StopAsync_NoSupervisor_IsANormalStop()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(PossessionState.Working);
        TurnControl bare = new(rig.Queue, rig.Gateway, NullLogger<TurnControl>.Instance);

        await bare.StopAsync(rig.Nova.Id, rig.Room.Id, ct);

        Assert.Empty(rig.Link.Ends);
        Assert.Equal((rig.Nova.Id, rig.Room.Id), Assert.Single(rig.Gateway.StopTurnCalls));
    }

    /// <summary>A second Stop right after the first does not return the possession again: one runner message, one pair of rows.</summary>
    [Fact]
    public async Task StopAsync_Twice_ReturnsThePossessionOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(PossessionState.Working);

        await rig.Control.StopAsync(rig.Nova.Id, rig.Room.Id, ct);
        await rig.Control.StopAsync(rig.Nova.Id, rig.Room.Id, ct);

        Assert.Single(rig.Link.Ends);
        Assert.Equal(2, (await rig.HuddleLinesAsync(ct)).Count);
        Assert.Equal(GoldfishExit.CalledBack, rig.Row().Exit);
    }

    /// <summary>The Turn Queue rows close first in every case, comeback or normal stop, so a Turn that ends because of the Stop cannot start the next one.</summary>
    [Theory]
    [InlineData("Working")]
    [InlineData("Waiting")]
    [InlineData("Returning")]
    public async Task StopAsync_EveryPossessionState_ClosesTheRoomsQueueRowsAsStopped(string state)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(string.Equals(state, "Waiting", StringComparison.Ordinal) ? PossessionState.Waiting : PossessionState.Working);
        if (string.Equals(state, "Returning", StringComparison.Ordinal))
        {
            Assert.True(rig.Store.BeginReturn("p1", GoldfishExit.Finished, null));
        }

        rig.Record(rig.Nova, "m1");

        await rig.Control.StopAsync(rig.Nova.Id, rig.Room.Id, ct);

        TurnQueueRowSnapshot closed = Assert.Single(TurnQueueRows.Read(rig.Dir.Path), row => row.MessageId == "m1");
        Assert.Equal(("done", "stopped"), (closed.Status, closed.Outcome));
    }

    /// <summary>The return row names the Human and the whole minutes, and says the result is partial.</summary>
    [Fact]
    public async Task StopAsync_Comeback_PostsTheReturnRowCalledBackByTheHuman()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        rig.Insert(PossessionState.Working);
        rig.Clock.Advance(TimeSpan.FromMinutes(4));

        await rig.Control.StopAsync(rig.Nova.Id, rig.Room.Id, ct);

        IReadOnlyList<ChatMessage> lines = await rig.HuddleLinesAsync(ct);
        Assert.Equal("@Nova is back. Called back by Quinn after 4 minutes. Partial result above.", lines[^1].Text);
    }

    private sealed class RecordingLink : IGoldfishRunnerLink
    {
        public List<(string AgentId, string PossessionId, GoldfishExit Exit)> Ends { get; } = [];

        public Task SendBeginAsync(string agentId, GoldfishBegin begin, CancellationToken ct) => Task.CompletedTask;

        public Task SendEndAsync(string agentId, string possessionId, GoldfishExit exit, CancellationToken ct)
        {
            this.Ends.Add((agentId, possessionId, exit));
            return Task.CompletedTask;
        }
    }

    private sealed class Rig : IDisposable
    {
        private readonly FileChatStore file;

        private Rig(TempDataDir dir, SqliteTeamDirectory team, User nova, User kai, Room room)
        {
            this.Dir = dir;
            this.Nova = nova;
            this.Kai = kai;
            this.Room = room;
            this.Clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero));
            IOptions<TeamOptions> options = dir.Options(o =>
            {
                o.AgentMessageBudget = 40;
                o.Goldfish.Enabled = true;
                o.Goldfish.KeepDays = 30;
            });
            TurnQueueStore queueStore = new(dir.Options());
            this.QueueStore = queueStore;
            this.Queue = new TurnQueue(queueStore, options, NullLogger<TurnQueue>.Instance, new FakeLifetime(), this.Clock);
            this.file = dir.CreateChatStore();
            RoomEvents events = new(NullLogger<RoomEvents>.Instance);
            ChatService chat = new(
                team,
                this.file,
                events,
                new FakeMentionAliasSource(),
                options,
                new App.Agents.ProposalStore(events),
                new App.Questions.QuestionStore(events),
                NullLogger<ChatService>.Instance,
                timeProvider: this.Clock)
            {
                OwedTurns = new OwedTurnPolicy(new RoomFollows()),
            };
            this.Store = new GoldfishStore(options);
            GoldfishSupervisor supervisor = new(this.Store, team, chat, this.Link, options, NullLogger<GoldfishSupervisor>.Instance, this.Clock);
            this.Gateway = new FakeAgentGateway();
            this.Control = new TurnControl(this.Queue, this.Gateway, NullLogger<TurnControl>.Instance, supervisor, new ConfiguredHumanIdentity("Quinn"));
        }

        public TempDataDir Dir { get; }

        public User Nova { get; }

        public User Kai { get; }

        public Room Room { get; }

        public FakeTimeProvider Clock { get; }

        public TurnQueue Queue { get; }

        public GoldfishStore Store { get; }

        public RecordingLink Link { get; } = new();

        public FakeAgentGateway Gateway { get; }

        public TurnControl Control { get; }

        private TurnQueueStore QueueStore { get; }

        public static async Task<Rig> CreateAsync(CancellationToken ct)
        {
            TempDataDir dir = new();
            SqliteTeamDirectory team = dir.CreateDirectory();
            await team.InitializeAsync("You", ct);
            User nova = await AddAgentAsync(team, "Nova", ct);
            User kai = await AddAgentAsync(team, "Kai", ct);
            Room room = await team.CreateRoomAsync("Pricing", [KnownIds.Human, nova.Id, kai.Id], ct);
            return new Rig(dir, team, nova, kai, room);
        }

        /// <summary>Inserts the possession "p1" for Nova in the given state, in this Room unless another is named.</summary>
        public void Insert(PossessionState state, string? roomId = null) =>
            Assert.True(this.Store.TryInsert(new Possession("p1", this.Nova.Id, roomId ?? this.Room.Id, null, "Look into it", state, null, null, this.Clock.GetUtcNow(), null, 0, 0m, null, null)));

        public Possession Row() => this.Store.Get("p1") ?? throw new InvalidOperationException("No possession 'p1'.");

        /// <summary>Records one owed Turn through the real ledger, as a post does.</summary>
        public void Record(User agent, string messageId)
        {
            this.QueueStore.Subscribe(agent.Id);
            this.Queue.Record(new ChatMessage(messageId, this.Clock.GetUtcNow(), KnownIds.Human, "You", "hi"), this.Room.Id, [agent.Id]);
        }

        public async Task<IReadOnlyList<ChatMessage>> HuddleLinesAsync(CancellationToken ct) =>
            [.. (await this.file.ReadAllAsync(this.Room.Id, ct)).Where(message => string.Equals(message.SenderId, KnownIds.Huddle, StringComparison.Ordinal))];

        public void Dispose() => this.Dir.Dispose();

        private static async Task<User> AddAgentAsync(SqliteTeamDirectory team, string name, CancellationToken ct)
        {
            User? agent = await team.UpsertAgentUserAsync(name, null, ct);
            Assert.NotNull(agent);
            return agent;
        }
    }
}

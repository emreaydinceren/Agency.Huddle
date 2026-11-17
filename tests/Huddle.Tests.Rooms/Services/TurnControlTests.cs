using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Desktop;
using Agency.Huddle.Tests.Ui;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Services;

/// <summary>Stop, removal and the waiting counts of the Turn Queue (Turn Queue spec §5.9, §5.7, §5.11): what closes, when, and what the Room view reads.</summary>
public sealed class TurnControlTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A Stop for an Agent with no connection closes its waiting and running rows in that Room as stopped, leaves other Rooms and Agents alone, and then reaches the gateway.</summary>
    [Fact]
    public async Task StopAsync_OfflineAgent_ClosesItsRowsInThatRoomAsStoppedThenCallsTheGateway()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = await Harness.CreateAsync(ct);
        harness.Record(harness.Nova, "m1", harness.Room.Id);
        harness.Record(harness.Nova, "m2", harness.Room.Id);
        harness.Record(harness.Nova, "m3", "other-room");
        harness.Record(harness.Kai, "m4", harness.Room.Id);
        harness.Store.Started(harness.Nova.Id, harness.Room.Id, ["m1"], "reply-1", "c1", Now, Now);

        await harness.Control.StopAsync(harness.Nova.Id, harness.Room.Id, ct);

        Assert.Equal(
            [("done", "stopped"), ("done", "stopped"), ("waiting", null), ("waiting", null)],
            TurnQueueRows.Read(harness.Dir.Path).OrderBy(row => row.MessageId, StringComparer.Ordinal).Select(row => (row.Status, row.Outcome)).ToArray());
        Assert.Equal((harness.Nova.Id, harness.Room.Id), Assert.Single(harness.Gateway.StopTurnCalls));
    }

    /// <summary>A Message recorded after the Stop is a new Turn the Stop never covered, so it stays waiting.</summary>
    [Fact]
    public async Task StopAsync_ARowRecordedAfterTheStop_StaysWaiting()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = await Harness.CreateAsync(ct);
        harness.Record(harness.Nova, "m1", harness.Room.Id);

        await harness.Control.StopAsync(harness.Nova.Id, harness.Room.Id, ct);
        harness.Record(harness.Nova, "m2", harness.Room.Id);

        Assert.Equal("stopped", TurnQueueRows.Find(harness.Dir.Path, harness.Nova.Id, harness.Room.Id, "m1").Outcome);
        Assert.Equal("waiting", TurnQueueRows.Find(harness.Dir.Path, harness.Nova.Id, harness.Room.Id, "m2").Status);
    }

    /// <summary>A store that throws is logged and does not keep the live Turn from being stopped.</summary>
    [Fact]
    public async Task StopAsync_StoreThrows_StillCallsTheGateway()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentGateway gateway = new();
        TurnControl control = new(new OrderRecordingControl(gateway, throwOnStop: true), gateway, NullLogger<TurnControl>.Instance);

        await control.StopAsync("a1", "r1", ct);

        Assert.Equal(("a1", "r1"), Assert.Single(gateway.StopTurnCalls));
    }

    /// <summary>The rows close before the gateway is asked, so a Turn that ends because of the Stop cannot start the next waiting one.</summary>
    [Fact]
    public async Task StopAsync_ClosesTheRowsBeforeItCallsTheGateway()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentGateway gateway = new();
        OrderRecordingControl recording = new(gateway, throwOnStop: false);
        TurnControl control = new(recording, gateway, NullLogger<TurnControl>.Instance);

        await control.StopAsync("a1", "r1", ct);

        Assert.Equal(0, recording.GatewayCallsSeenAtClose);
        Assert.Single(gateway.StopTurnCalls);
    }

    /// <summary>Removing an Agent closes all its open rows as gone, drops its subscription and leaves another Agent's rows open.</summary>
    [Fact]
    public async Task CloseForAgent_ClosesItsOpenRowsAsGoneAndUnsubscribesIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = await Harness.CreateAsync(ct);
        harness.Record(harness.Nova, "m1", harness.Room.Id);
        harness.Record(harness.Nova, "m2", "other-room");
        harness.Record(harness.Kai, "m3", harness.Room.Id);

        harness.Queue.CloseForAgent(harness.Nova.Id);

        Assert.Equal(
            [("done", "gone"), ("done", "gone"), ("waiting", null)],
            TurnQueueRows.Read(harness.Dir.Path).OrderBy(row => row.MessageId, StringComparer.Ordinal).Select(row => (row.Status, row.Outcome)).ToArray());
        Assert.False(harness.Queue.IsSubscribed(harness.Nova.Id));
        Assert.True(harness.Queue.IsSubscribed(harness.Kai.Id));
    }

    /// <summary>The waiting counts include waiting rows only, per Agent, and read without changing a row.</summary>
    [Fact]
    public async Task WaitingIn_CountsWaitingRowsOnlyPerAgentAndChangesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = await Harness.CreateAsync(ct);
        harness.Record(harness.Nova, "m1", harness.Room.Id);
        harness.Record(harness.Nova, "m2", harness.Room.Id);
        harness.Record(harness.Nova, "m3", harness.Room.Id);
        harness.Record(harness.Kai, "m4", harness.Room.Id);
        harness.Record(harness.Kai, "m5", "other-room");
        harness.Store.Started(harness.Nova.Id, harness.Room.Id, ["m1"], "reply-1", "c1", Now, Now);
        _ = harness.Store.Finish(harness.Kai.Id, harness.Room.Id, ["m4"], TurnQueueOutcome.Silent, Now);
        IReadOnlyList<TurnQueueRowSnapshot> before = TurnQueueRows.Read(harness.Dir.Path);

        IReadOnlyList<WaitingCount> counts = await harness.View.WaitingInAsync(harness.Room.Id, ct);

        WaitingCount only = Assert.Single(counts);
        Assert.Equal((harness.Nova.Id, "Nova", 2, false), (only.AgentId, only.AgentName, only.Count, only.Held));
        Assert.Equal(before, TurnQueueRows.Read(harness.Dir.Path));
    }

    /// <summary>When the Room's Budget is spent, the counts say they are held.</summary>
    [Fact]
    public async Task WaitingIn_BudgetExhausted_IsHeld()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = await Harness.CreateAsync(ct);
        harness.Record(harness.Nova, "m1", harness.Room.Id);
        WaitingCount fresh = Assert.Single(await harness.View.WaitingInAsync(harness.Room.Id, ct));
        Assert.False(fresh.Held);

        _ = await harness.Chat.PostAsync(harness.Room.Id, KnownIds.Human, "start", ct: ct);
        for (int i = 0; i < 3; i++)
        {
            _ = await harness.Chat.PostAsync(harness.Room.Id, harness.Nova.Id, "@Kai step " + i, ct: ct);
        }

        WaitingCount held = Assert.Single(await harness.View.WaitingInAsync(harness.Room.Id, ct));
        Assert.True(held.Held);
    }

    /// <summary>Recording an owed Turn and closing rows both tell the Room's subscribers, and only that Room's.</summary>
    [Fact]
    public async Task TurnQueueChanged_IsRaisedByRecordAndByAClose()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = await Harness.CreateAsync(ct);
        List<string> raised = [];
        harness.Events.TurnQueueChanged += raised.Add;
        harness.Store.Subscribe(harness.Nova.Id);

        harness.Queue.Record(new ChatMessage("m1", Now, KnownIds.Human, "You", "hi"), harness.Room.Id, [harness.Nova.Id]);
        Assert.Equal([harness.Room.Id], raised);

        raised.Clear();
        harness.Queue.CloseForStop(harness.Nova.Id, harness.Room.Id);
        Assert.Equal([harness.Room.Id], raised);

        raised.Clear();
        harness.Queue.Record(new ChatMessage("m2", Now, KnownIds.Human, "You", "again"), harness.Room.Id, [harness.Nova.Id]);
        harness.Queue.CloseForAgent(harness.Nova.Id);
        Assert.Equal([harness.Room.Id, harness.Room.Id], raised);
        await Task.CompletedTask;
    }

    /// <summary>A control that notes how many gateway calls had happened when the rows were closed, and can throw as a failing store would.</summary>
    private sealed class OrderRecordingControl(FakeAgentGateway gateway, bool throwOnStop) : ITurnQueueControl
    {
        public int GatewayCallsSeenAtClose { get; private set; } = -1;

        public void CloseForStop(string agentId, string roomId)
        {
            this.GatewayCallsSeenAtClose = gateway.StopTurnCalls.Count;
            if (throwOnStop)
            {
                throw new IOException("the store is locked");
            }
        }

        public void CloseRunning(string connectionId) => throw new NotSupportedException();

        public IReadOnlyList<WaitingTurn> TakeWaiting(string agentId, DateTimeOffset now) => throw new NotSupportedException();

        public void CloseGone(long seq, DateTimeOffset now) => throw new NotSupportedException();

        public bool IsSubscribed(string agentId) => throw new NotSupportedException();

        public void CloseForAgent(string agentId) => throw new NotSupportedException();
    }

    private sealed class Harness : IDisposable
    {
        private Harness(TempDataDir dir, SqliteTeamDirectory team, User nova, User kai, Room room)
        {
            this.Dir = dir;
            this.Nova = nova;
            this.Kai = kai;
            this.Room = room;
            this.Store = new TurnQueueStore(dir.Options());
            IOptions<TeamOptions> options = dir.Options(o => o.AgentMessageBudget = 3);
            this.Events = new RoomEvents(NullLogger<RoomEvents>.Instance);
            this.Queue = new TurnQueue(this.Store, options, NullLogger<TurnQueue>.Instance, new FakeLifetime(), null, this.Events);
            this.Chat = new ChatService(
                team,
                dir.Options().CreateChatStore(),
                this.Events,
                new FakeMentionAliasSource(),
                options,
                new App.Agents.ProposalStore(this.Events),
                new App.Questions.QuestionStore(this.Events),
                NullLogger<ChatService>.Instance)
            {
                OwedTurns = new OwedTurnPolicy(new RoomFollows()),
            };
            this.View = new TurnQueueView(this.Store, this.Chat, team, options);
            this.Gateway = new FakeAgentGateway();
            this.Control = new TurnControl(this.Queue, this.Gateway, NullLogger<TurnControl>.Instance);
        }

        public TempDataDir Dir { get; }

        public User Nova { get; }

        public User Kai { get; }

        public Room Room { get; }

        public TurnQueueStore Store { get; }

        public RoomEvents Events { get; }

        public TurnQueue Queue { get; }

        public ChatService Chat { get; }

        public TurnQueueView View { get; }

        public FakeAgentGateway Gateway { get; }

        public TurnControl Control { get; }

        public static async Task<Harness> CreateAsync(CancellationToken ct)
        {
            TempDataDir dir = new();
            SqliteTeamDirectory team = await dir.Options().CreateDirectoryAsync(ct);
            User nova = await AddAgentAsync(team, "Nova", ct);
            User kai = await AddAgentAsync(team, "Kai", ct);
            Room room = await team.CreateRoomAsync("Pricing", [KnownIds.Human, nova.Id, kai.Id], ct);
            return new Harness(dir, team, nova, kai, room);
        }

        /// <summary>Subscribes the Agent and records one owed Turn through the real ledger, as a post does.</summary>
        public void Record(User agent, string messageId, string roomId)
        {
            this.Store.Subscribe(agent.Id);
            this.Queue.Record(new ChatMessage(messageId, Now, KnownIds.Human, "You", "hi"), roomId, [agent.Id]);
        }

        public void Dispose() => this.Dir.Dispose();

        private static async Task<User> AddAgentAsync(SqliteTeamDirectory team, string name, CancellationToken ct)
        {
            User? agent = await team.UpsertAgentUserAsync(name, null, ct);
            Assert.NotNull(agent);
            return agent;
        }
    }
}

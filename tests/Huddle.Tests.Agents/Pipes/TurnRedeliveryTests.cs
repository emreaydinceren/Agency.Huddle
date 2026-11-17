using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Waiting;

namespace Agency.Huddle.Tests.Pipes;

/// <summary>
/// Re-delivery of the Turns an Agent still owes (Turn Queue spec, sections 5.4 and 5.7): what is sent after the Welcome, in which order and
/// marked how, who is not sent anything, and the held Turns sent again when a Room's pause ends. Driven over the real pipe, so what a test
/// reads is exactly the <see cref="MessagePosted"/> a runner would. A send that must not happen is proved by order: the next live Message
/// is the next thing the client reads.
/// </summary>
public sealed class TurnRedeliveryTests
{
    private static readonly TimeSpan BoundedRead = TimeSpan.FromSeconds(5);

    /// <summary>After the Welcome of a durable client, every waiting Turn arrives oldest first, marked as waiting.</summary>
    [Fact]
    public async Task Reconnect_WaitingTurns_ArriveOldestFirstMarkedWaiting()
    {
        using BehaviourBudget cts = new(TimeSpan.FromSeconds(20));
        CancellationToken ct = cts.Token;
        await using Scenario scenario = await Scenario.StartAsync(null, cts);
        Welcome nova = await scenario.ConnectAsync("Nova", durable: true, ct, key: "old");
        Room room = await scenario.Chat.CreateRoomForAsync([nova.AgentId], ct);
        ChatMessage first = await scenario.Chat.PostAsync(room.Id, KnownIds.Human, "first", ct: ct);
        ChatMessage second = await scenario.Chat.PostAsync(room.Id, KnownIds.Human, "second", ct: ct);
        _ = await scenario.ReadAsync("old", ct);
        _ = await scenario.ReadAsync("old", ct);
        await scenario.Client("old").DisposeAsync();

        _ = await scenario.ConnectAsync("Nova", durable: true, ct, key: "new");
        MessagePosted a = await scenario.ReadAsync("new", ct);
        MessagePosted b = await scenario.ReadAsync("new", ct);

        Assert.Equal([first.Id, second.Id], [a.Message.Id, b.Message.Id]);
        Assert.Equal([Redelivery.Waiting, Redelivery.Waiting], [a.Redelivery, b.Redelivery]);
    }

    /// <summary>A row whose Turn was already started once is sent marked cut off.</summary>
    [Fact]
    public async Task Reconnect_RowStartedBefore_ArrivesMarkedCutOff()
    {
        using BehaviourBudget cts = new(TimeSpan.FromSeconds(20));
        CancellationToken ct = cts.Token;
        await using Scenario scenario = await Scenario.StartAsync(null, cts);
        Welcome nova = await scenario.ConnectAsync("Nova", durable: true, ct, key: "old");
        Room room = await scenario.Chat.CreateRoomForAsync([nova.AgentId], ct);
        ChatMessage posted = await scenario.Chat.PostAsync(room.Id, KnownIds.Human, "again", ct: ct);
        _ = await scenario.ReadAsync("old", ct);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        scenario.Store.Started(nova.AgentId, room.Id, [posted.Id], "reply-1", "gone-connection", now, now);
        TurnQueueRow started = Assert.Single(scenario.Store.Open(nova.AgentId));
        scenario.Store.Set(started.Seq, TurnQueueStatus.Waiting, null, now);
        await scenario.Client("old").DisposeAsync();

        _ = await scenario.ConnectAsync("Nova", durable: true, ct, key: "new");
        MessagePosted again = await scenario.ReadAsync("new", ct);

        Assert.Equal(posted.Id, again.Message.Id);
        Assert.Equal(Redelivery.CutOff, again.Redelivery);
    }

    /// <summary>A re-delivery is built by the same code as a live one: the two differ only in the Redelivery mark.</summary>
    [Fact]
    public async Task Redelivery_PayloadEqualsTheLiveDeliveryApartFromTheMark()
    {
        using BehaviourBudget cts = new(TimeSpan.FromSeconds(20));
        CancellationToken ct = cts.Token;
        await using Scenario scenario = await Scenario.StartAsync(null, cts);
        Welcome nova = await scenario.ConnectAsync("Nova", durable: true, ct, key: "old");
        Room room = await scenario.Chat.CreateRoomForAsync([nova.AgentId], ct);
        _ = await scenario.Chat.PostAsync(room.Id, KnownIds.Human, "@Nova look", ct: ct);
        MessagePosted live = await scenario.ReadAsync("old", ct);
        await scenario.Client("old").DisposeAsync();

        _ = await scenario.ConnectAsync("Nova", durable: true, ct, key: "new");
        MessagePosted again = await scenario.ReadAsync("new", ct);

        Assert.NotNull(again.Redelivery);
        Assert.Equal(JsonSerializer.Serialize(live), JsonSerializer.Serialize(again with { Redelivery = null }));
    }

    /// <summary>A client that does not keep a durable queue is sent nothing extra: the next live Message is the first thing it reads.</summary>
    [Fact]
    public async Task Reconnect_ClientWithoutDurableQueue_GetsNothingNew()
    {
        using BehaviourBudget cts = new(TimeSpan.FromSeconds(20));
        CancellationToken ct = cts.Token;
        await using Scenario scenario = await Scenario.StartAsync(null, cts);
        Welcome nova = await scenario.ConnectAsync("Nova", durable: true, ct, key: "old");
        Room room = await scenario.Chat.CreateRoomForAsync([nova.AgentId], ct);
        _ = await scenario.Chat.PostAsync(room.Id, KnownIds.Human, "waiting", ct: ct);
        _ = await scenario.ReadAsync("old", ct);
        await scenario.Client("old").DisposeAsync();

        _ = await scenario.ConnectAsync("Nova", durable: false, ct, key: "new");
        _ = await scenario.Chat.PostAsync(room.Id, KnownIds.Human, "marker", ct: ct);
        MessagePosted next = await scenario.ReadAsync("new", ct);

        Assert.Equal("marker", next.Message.Text);
        Assert.Null(next.Redelivery);
    }

    /// <summary>A waiting row of an Agent that is no longer a Member of its Room is closed as gone and nothing is sent for it.</summary>
    [Fact]
    public async Task Reconnect_AgentNoLongerMember_ClosesRowGoneAndSendsNothing()
    {
        using BehaviourBudget cts = new(TimeSpan.FromSeconds(20));
        CancellationToken ct = cts.Token;
        await using Scenario scenario = await Scenario.StartAsync(null, cts);
        Welcome nova = await scenario.ConnectAsync("Nova", durable: true, ct, key: "old");
        Welcome rio = await scenario.ConnectAsync("Rio", durable: true, ct);
        Room novaRoom = await scenario.Chat.CreateRoomForAsync([nova.AgentId], ct);
        Room rioRoom = await scenario.Chat.CreateRoomForAsync([rio.AgentId], ct);
        ChatMessage elsewhere = await scenario.Chat.PostAsync(rioRoom.Id, KnownIds.Human, "not for Nova", ct: ct);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        _ = scenario.Store.Record(nova.AgentId, elsewhere.Id, rioRoom.Id, elsewhere.Timestamp, now);
        await scenario.Client("old").DisposeAsync();

        _ = await scenario.ConnectAsync("Nova", durable: true, ct, key: "new");
        _ = await scenario.Chat.PostAsync(novaRoom.Id, KnownIds.Human, "marker", ct: ct);
        MessagePosted next = await scenario.ReadAsync("new", ct);

        Assert.Equal("marker", next.Message.Text);
        await Scenario.UntilAsync(() => scenario.Row(nova.AgentId, rioRoom.Id, elsewhere.Id).Status == "done", ct);
        Assert.Equal("gone", scenario.Row(nova.AgentId, rioRoom.Id, elsewhere.Id).Outcome);
    }

    /// <summary>With the queue switched off nothing is sent after the Welcome, even for a row left in the store.</summary>
    [Fact]
    public async Task Reconnect_QueueDisabled_SendsNothing()
    {
        using BehaviourBudget cts = new(TimeSpan.FromSeconds(20));
        CancellationToken ct = cts.Token;
        await using Scenario scenario = await Scenario.StartAsync(new Dictionary<string, string?> { ["Team:TurnQueue:Enabled"] = "false" }, cts);
        Welcome nova = await scenario.ConnectAsync("Nova", durable: true, ct, key: "old");
        Room room = await scenario.Chat.CreateRoomForAsync([nova.AgentId], ct);
        ChatMessage posted = await scenario.Chat.PostAsync(room.Id, KnownIds.Human, "waiting", ct: ct);
        _ = scenario.Store.Record(nova.AgentId, posted.Id, room.Id, posted.Timestamp, DateTimeOffset.UtcNow);
        _ = await scenario.ReadAsync("old", ct);
        await scenario.Client("old").DisposeAsync();

        _ = await scenario.ConnectAsync("Nova", durable: true, ct, key: "new");
        _ = await scenario.Chat.PostAsync(room.Id, KnownIds.Human, "marker", ct: ct);
        MessagePosted next = await scenario.ReadAsync("new", ct);

        Assert.Equal("marker", next.Message.Text);
        Assert.Null(next.Redelivery);
    }

    /// <summary>When the Human presses Continue, the Turn held for the spent Room is sent again.</summary>
    [Fact]
    public async Task Held_AfterContinue_IsSentAgain()
    {
        using BehaviourBudget cts = new(TimeSpan.FromSeconds(20));
        CancellationToken ct = cts.Token;
        await using Scenario scenario = await Scenario.StartAsync(new Dictionary<string, string?> { ["Team:AgentMessageBudget"] = "1" }, cts);
        Welcome nova = await scenario.ConnectAsync("Nova", durable: true, ct);
        Room room = await scenario.Chat.CreateRoomForAsync([nova.AgentId], ct);
        _ = await scenario.Chat.PostAsync(room.Id, KnownIds.Human, "held", ct: ct);
        _ = await scenario.ReadAsync("Nova", ct);
        _ = await scenario.Chat.PostAsync(room.Id, nova.AgentId, "framing", ct: ct);
        Assert.True(scenario.Chat.GetBudget(room.Id).Exhausted);

        BudgetExtension extension = await scenario.Chat.ExtendBudgetAsync(room.Id, ct);

        Assert.Equal(ExtendResult.Granted, extension.Result);
        MessagePosted again = await scenario.ReadUntilRedeliveredAsync("Nova", "held", ct);
        Assert.Equal(Redelivery.Waiting, again.Redelivery);
    }

    /// <summary>When the Human speaks in a spent Room, the Turn held for it is sent again as well.</summary>
    [Fact]
    public async Task Held_AfterTheHumanSpeaks_IsSentAgain()
    {
        using BehaviourBudget cts = new(TimeSpan.FromSeconds(20));
        CancellationToken ct = cts.Token;
        await using Scenario scenario = await Scenario.StartAsync(new Dictionary<string, string?> { ["Team:AgentMessageBudget"] = "1" }, cts);
        Welcome nova = await scenario.ConnectAsync("Nova", durable: true, ct);
        Room room = await scenario.Chat.CreateRoomForAsync([nova.AgentId], ct);
        _ = await scenario.Chat.PostAsync(room.Id, KnownIds.Human, "held", ct: ct);
        _ = await scenario.ReadAsync("Nova", ct);
        _ = await scenario.Chat.PostAsync(room.Id, nova.AgentId, "framing", ct: ct);
        Assert.True(scenario.Chat.GetBudget(room.Id).Exhausted);

        _ = await scenario.Chat.PostAsync(room.Id, KnownIds.Human, "go on", ct: ct);

        MessagePosted again = await scenario.ReadUntilRedeliveredAsync("Nova", "held", ct);
        Assert.Equal(Redelivery.Waiting, again.Redelivery);
    }

    /// <summary>A started host with raw clients connected by key.</summary>
    private sealed class Scenario : IAsyncDisposable
    {
        private readonly Dictionary<string, JsonLineStream> clients = new(StringComparer.Ordinal);

        private Scenario(PipeHostFixture fixture)
        {
            this.Fixture = fixture;
        }

        internal PipeHostFixture Fixture { get; }

        internal ChatService Chat => this.Fixture.Services.GetRequiredService<ChatService>();

        internal TurnQueueStore Store => this.Fixture.Services.GetRequiredService<TurnQueueStore>();

        private string DataDir => this.Fixture.Services.GetRequiredService<IOptions<TeamOptions>>().Value.DataDir;

        internal static async Task<Scenario> StartAsync(Dictionary<string, string?>? config, BehaviourBudget budget) =>
            new(config is null ? await PipeHostFixture.StartAsync(budget) : await PipeHostFixture.StartAsync(config, budget));

        internal JsonLineStream Client(string key) => this.clients[key];

        internal async Task<Welcome> ConnectAsync(string name, bool durable, CancellationToken ct, string? key = null)
        {
            JsonLineStream client = await this.Fixture.ConnectClientAsync(ct);
            this.clients[key ?? name] = client;
            await client.WriteAsync(new Hello(name, null, durable ? true : null), ct);
            return Assert.IsType<Welcome>(await client.ReadAsync(ct));
        }

        /// <summary>Reads the next Envelope from a client and requires a delivery; the read is bounded so a missing delivery fails instead of hanging.</summary>
        internal async Task<MessagePosted> ReadAsync(string key, CancellationToken ct)
        {
            try
            {
                return Assert.IsType<MessagePosted>(await this.clients[key].ReadAsync(ct).WaitAsync(BoundedRead, ct));
            }
            catch (TimeoutException)
            {
                Assert.Fail($"No delivery arrived within {BoundedRead.TotalSeconds:0} seconds.");
                throw;
            }
        }

        /// <summary>Reads deliveries until one marked as re-delivered carries <paramref name="text"/>.</summary>
        internal async Task<MessagePosted> ReadUntilRedeliveredAsync(string key, string text, CancellationToken ct)
        {
            for (int index = 0; index < 6; index++)
            {
                MessagePosted next = await this.ReadAsync(key, ct);
                if (next.Redelivery is not null && string.Equals(next.Message.Text, text, StringComparison.Ordinal))
                {
                    return next;
                }
            }

            Assert.Fail($"No re-delivery of '{text}' arrived.");
            throw new InvalidOperationException("Unreachable.");
        }

        internal TurnQueueRowSnapshot Row(string agentId, string roomId, string messageId) =>
            TurnQueueRows.Find(this.DataDir, agentId, roomId, messageId);

        internal static Task UntilAsync(Func<bool> condition, CancellationToken ct) =>
            Eventually.UntilAsync(condition, ct, timeout: TestBudgets.Standard, interval: TimeSpan.FromMilliseconds(20));

        public async ValueTask DisposeAsync()
        {
            foreach (JsonLineStream client in this.clients.Values)
            {
                await client.DisposeAsync();
            }

            await this.Fixture.DisposeAsync();
        }
    }
}

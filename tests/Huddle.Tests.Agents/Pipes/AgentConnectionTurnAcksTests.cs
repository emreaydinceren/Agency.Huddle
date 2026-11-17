using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Waiting;

namespace Agency.Huddle.Tests.Pipes;

/// <summary>
/// What <see cref="Agency.Huddle.App.Pipes.AgentConnection"/> does with the Turn Queue's wire messages (spec §5.5, §5.6): the Hello flag, acks,
/// <see cref="PostMessage.InReplyTo"/>, a store that fails, and a connection that ends, driven by a raw client over the real pipe.
/// </summary>
public sealed class AgentConnectionTurnAcksTests
{
    /// <summary>A Hello with the durable flag subscribes the Agent and a later Hello without it clears the subscription.</summary>
    [Fact]
    public async Task Hello_DurableFlag_SubscribesAndALaterHelloWithoutItClears()
    {
        using BehaviourBudget cts = new(TimeSpan.FromSeconds(20));
        CancellationToken ct = cts.Token;
        await using Scenario scenario = await Scenario.StartAsync(cts);

        Welcome first = await scenario.ConnectAsync("Nova", durable: true, ct);
        Assert.Contains(first.AgentId, scenario.Store.Subscribers());
        _ = await scenario.ConnectAsync("Nova", durable: false, ct);

        Assert.DoesNotContain(first.AgentId, scenario.Store.Subscribers());
    }

    /// <summary>An ack for a Room the Agent is not a Member of is refused with notMember and changes nothing.</summary>
    [Fact]
    public async Task Ack_NonMemberRoom_IsRefusedAndChangesNothing()
    {
        using BehaviourBudget cts = new(TimeSpan.FromSeconds(20));
        CancellationToken ct = cts.Token;
        await using Scenario scenario = await Scenario.StartAsync(cts);
        Welcome nova = await scenario.ConnectAsync("Nova", durable: true, ct);
        Welcome rio = await scenario.ConnectAsync("Rio", durable: true, ct);
        string rioRoom = Assert.Single(rio.Rooms).Id;
        scenario.Seed(nova.AgentId, rioRoom, "m1");

        await scenario.Client("Nova").WriteAsync(new QueueAck(rioRoom, ["m1"], QueueAckKind.Finished, Outcome: QueueOutcome.Silent), ct);
        ProtocolError error = Assert.IsType<ProtocolError>(await scenario.Client("Nova").ReadAsync(ct));

        Assert.Equal(ErrorCodes.NotMember, error.Code);
        Assert.Equal("waiting", scenario.Row(nova.AgentId, rioRoom, "m1").Status);
    }

    /// <summary>An ack for a Room that does not exist is refused with unknownRoom.</summary>
    [Fact]
    public async Task Ack_UnknownRoom_IsRefused()
    {
        using BehaviourBudget cts = new(TimeSpan.FromSeconds(20));
        CancellationToken ct = cts.Token;
        await using Scenario scenario = await Scenario.StartAsync(cts);
        _ = await scenario.ConnectAsync("Nova", durable: true, ct);

        await scenario.Client("Nova").WriteAsync(new QueueAck("no-such-room", ["m1"], QueueAckKind.Held), ct);
        ProtocolError error = Assert.IsType<ProtocolError>(await scenario.Client("Nova").ReadAsync(ct));

        Assert.Equal(ErrorCodes.UnknownRoom, error.Code);
    }

    /// <summary>An invalid id in an ack is ignored, and the valid ids in the same ack are applied.</summary>
    [Fact]
    public async Task Ack_InvalidIds_AreDroppedAndTheOthersApplied()
    {
        using BehaviourBudget cts = new(TimeSpan.FromSeconds(20));
        CancellationToken ct = cts.Token;
        await using Scenario scenario = await Scenario.StartAsync(cts);
        Welcome nova = await scenario.ConnectAsync("Nova", durable: true, ct);
        string room = Assert.Single(nova.Rooms).Id;
        scenario.Seed(nova.AgentId, room, "m1");

        await scenario.Client("Nova").WriteAsync(new QueueAck(room, ["bad id!", "m1"], QueueAckKind.Started, "reply-1"), ct);
        await scenario.Client("Nova").WriteAsync(new QueueAck(room, ["m2"], QueueAckKind.Started, "bad reply!"), ct);
        await scenario.Client("Nova").WriteAsync(new QueueAck(room, ["m3"], QueueAckKind.Started, "reply-3"), ct);
        await Scenario.UntilAsync(() => scenario.Rows().Any(row => row.MessageId == "m3"), ct);

        List<string> messages = [.. scenario.Rows().Select(row => row.MessageId).Order(StringComparer.Ordinal)];
        Assert.Equal(["m1", "m3"], messages);
        Assert.Equal("running", scenario.Row(nova.AgentId, room, "m1").Status);
    }

    /// <summary>An ack closes only this Agent's rows in the acked Room: another Agent's row for the same Message and this Agent's row in another Room stay open.</summary>
    [Fact]
    public async Task Ack_AppliesOnlyToThisAgentsRowsInThatRoom()
    {
        using BehaviourBudget cts = new(TimeSpan.FromSeconds(20));
        CancellationToken ct = cts.Token;
        await using Scenario scenario = await Scenario.StartAsync(cts);
        Welcome nova = await scenario.ConnectAsync("Nova", durable: true, ct);
        Welcome rio = await scenario.ConnectAsync("Rio", durable: true, ct);
        string novaRoom = Assert.Single(nova.Rooms).Id;
        string rioRoom = Assert.Single(rio.Rooms).Id;
        scenario.Seed(nova.AgentId, novaRoom, "m1");
        scenario.Seed(rio.AgentId, novaRoom, "m1");
        scenario.Seed(nova.AgentId, rioRoom, "m2");

        await scenario.Client("Nova").WriteAsync(new QueueAck(novaRoom, ["m1", "m2"], QueueAckKind.Finished, Outcome: QueueOutcome.Silent), ct);
        await Scenario.UntilAsync(() => scenario.Row(nova.AgentId, novaRoom, "m1").Status == "done", ct);

        Assert.Equal("waiting", scenario.Row(rio.AgentId, novaRoom, "m1").Status);
        Assert.Equal("waiting", scenario.Row(nova.AgentId, rioRoom, "m2").Status);
    }

    /// <summary>A Started ack naming this Agent's Room for a Message whose row is in another Room leaves that row waiting.</summary>
    [Fact]
    public async Task Ack_StartedNamingTheWrongRoom_LeavesTheOtherRoomsRowWaiting()
    {
        using BehaviourBudget cts = new(TimeSpan.FromSeconds(20));
        CancellationToken ct = cts.Token;
        await using Scenario scenario = await Scenario.StartAsync(cts);
        Welcome nova = await scenario.ConnectAsync("Nova", durable: true, ct);
        Welcome rio = await scenario.ConnectAsync("Rio", durable: true, ct);
        string novaRoom = Assert.Single(nova.Rooms).Id;
        string rioRoom = Assert.Single(rio.Rooms).Id;
        scenario.Seed(nova.AgentId, rioRoom, "m2");

        await scenario.Client("Nova").WriteAsync(new QueueAck(novaRoom, ["m2"], QueueAckKind.Started, "reply-2"), ct);
        await scenario.Client("Nova").WriteAsync(new QueueAck(novaRoom, ["m3"], QueueAckKind.Started, "reply-3"), ct);
        await Scenario.UntilAsync(() => scenario.Rows().Any(row => row.MessageId == "m3"), ct);

        Assert.Equal("waiting", scenario.Row(nova.AgentId, rioRoom, "m2").Status);
        Assert.Null(scenario.Row(nova.AgentId, rioRoom, "m2").ConnectionId);
    }

    /// <summary>A reply naming Messages closes their rows as replied even though no Started ack was ever sent.</summary>
    [Fact]
    public async Task Reply_WithInReplyTo_ClosesTheRowAsReplied()
    {
        using BehaviourBudget cts = new(TimeSpan.FromSeconds(20));
        CancellationToken ct = cts.Token;
        await using Scenario scenario = await Scenario.StartAsync(cts);
        Welcome nova = await scenario.ConnectAsync("Nova", durable: true, ct);
        string room = Assert.Single(nova.Rooms).Id;
        scenario.Seed(nova.AgentId, room, "m1");

        await scenario.Client("Nova").WriteAsync(new PostMessage(room, "reply-1", "on it", ["m1", "bad id!"]), ct);
        await Scenario.UntilAsync(() => scenario.Row(nova.AgentId, room, "m1").Status == "done", ct);

        Assert.Equal("replied", scenario.Row(nova.AgentId, room, "m1").Outcome);
        RecordingAcks recorder = Assert.IsType<RecordingAcks>(scenario.Fixture.Services.GetRequiredService<ITurnAcks>());
        Assert.Equal(["m1"], Assert.Single(recorder.Replied));
    }

    /// <summary>A reply the server refuses closes its open rows as failed, before the error reaches the client.</summary>
    [Fact]
    public async Task Reply_Refused_ClosesTheRowAsFailedBeforeTheError()
    {
        using BehaviourBudget cts = new(TimeSpan.FromSeconds(20));
        CancellationToken ct = cts.Token;
        await using Scenario scenario = await Scenario.StartAsync(cts);
        Welcome nova = await scenario.ConnectAsync("Nova", durable: true, ct);
        string room = Assert.Single(nova.Rooms).Id;
        scenario.Seed(nova.AgentId, room, "m1");

        await scenario.Client("Nova").WriteAsync(new PostMessage(room, "reply-1", " ", ["m1"]), ct);
        _ = Assert.IsType<ProtocolError>(await scenario.Client("Nova").ReadAsync(ct));

        TurnQueueRowSnapshot row = scenario.Row(nova.AgentId, room, "m1");
        Assert.Equal(("done", "failed"), (row.Status, row.Outcome));
    }

    /// <summary>A store that throws on every call never ends the connection: the client can still ask and be answered.</summary>
    [Fact]
    public async Task ThrowingStore_DoesNotCloseTheConnection()
    {
        using BehaviourBudget cts = new(TimeSpan.FromSeconds(20));
        CancellationToken ct = cts.Token;
        await using Scenario scenario = await Scenario.StartAsync(cts, throwing: true);
        Welcome nova = await scenario.ConnectAsync("Nova", durable: true, ct);
        string room = Assert.Single(nova.Rooms).Id;

        await scenario.Client("Nova").WriteAsync(new QueueAck(room, ["m1"], QueueAckKind.Started, "reply-1"), ct);
        await scenario.Client("Nova").WriteAsync(new PostMessage(room, "reply-2", "still here", ["m1"]), ct);
        await scenario.Client("Nova").WriteAsync(new ReadTranscript("r1", room, null, "none", 5), ct);
        TranscriptTail tail = Assert.IsType<TranscriptTail>(await scenario.Client("Nova").ReadAsync(ct));

        Assert.Contains(tail.Messages, message => message.Id == "reply-2");
        Assert.True(scenario.Recorder.Calls >= 3);
    }

    /// <summary>A connection that ends closes its own running rows as stopped.</summary>
    [Fact]
    public async Task ConnectionEnds_ClosesItsRunningRowsAsStopped()
    {
        using BehaviourBudget cts = new(TimeSpan.FromSeconds(20));
        CancellationToken ct = cts.Token;
        await using Scenario scenario = await Scenario.StartAsync(cts);
        Welcome nova = await scenario.ConnectAsync("Nova", durable: true, ct);
        string room = Assert.Single(nova.Rooms).Id;
        scenario.Seed(nova.AgentId, room, "m1");
        await scenario.Client("Nova").WriteAsync(new QueueAck(room, ["m1"], QueueAckKind.Started, "reply-1"), ct);
        await Scenario.UntilAsync(() => scenario.Row(nova.AgentId, room, "m1").Status == "running", ct);

        await scenario.Client("Nova").DisposeAsync();
        await Scenario.UntilAsync(() => scenario.Row(nova.AgentId, room, "m1").Status == "done", ct);

        Assert.Equal("stopped", scenario.Row(nova.AgentId, room, "m1").Outcome);
    }

    /// <summary>When the host is stopping, a connection that ends leaves its running rows running for the next start to resume.</summary>
    [Fact]
    public async Task ConnectionEnds_WhileHostStopping_LeavesRowsRunning()
    {
        using BehaviourBudget cts = new(TimeSpan.FromSeconds(20));
        CancellationToken ct = cts.Token;
        await using Scenario scenario = await Scenario.StartAsync(cts);
        Welcome nova = await scenario.ConnectAsync("Nova", durable: true, ct);
        string room = Assert.Single(nova.Rooms).Id;
        scenario.Seed(nova.AgentId, room, "m1");
        await scenario.Client("Nova").WriteAsync(new QueueAck(room, ["m1"], QueueAckKind.Started, "reply-1"), ct);
        await Scenario.UntilAsync(() => scenario.Row(nova.AgentId, room, "m1").Status == "running", ct);

        scenario.Fixture.Services.GetRequiredService<IHostApplicationLifetime>().StopApplication();
        await scenario.Client("Nova").DisposeAsync();
        await Scenario.UntilAsync(() => scenario.Disconnects() >= 1, ct);

        Assert.Equal("running", scenario.Row(nova.AgentId, room, "m1").Status);
    }

    /// <summary>A stale connection that ends after a newer one started a row leaves that row alone.</summary>
    [Fact]
    public async Task StaleConnectionEndsLate_LeavesTheNewConnectionsRowsAlone()
    {
        using BehaviourBudget cts = new(TimeSpan.FromSeconds(20));
        CancellationToken ct = cts.Token;
        await using Scenario scenario = await Scenario.StartAsync(cts);
        Welcome first = await scenario.ConnectAsync("Nova", durable: true, ct, key: "old");
        _ = await scenario.ConnectAsync("Nova", durable: true, ct, key: "new");
        string room = Assert.Single(first.Rooms).Id;
        scenario.Seed(first.AgentId, room, "m1");
        await scenario.Client("new").WriteAsync(new QueueAck(room, ["m1"], QueueAckKind.Started, "reply-1"), ct);
        await Scenario.UntilAsync(() => scenario.Row(first.AgentId, room, "m1").Status == "running", ct);

        await scenario.Client("old").DisposeAsync();
        await Scenario.UntilAsync(() => scenario.Disconnects() >= 1, ct);

        Assert.Equal("running", scenario.Row(first.AgentId, room, "m1").Status);
    }

    /// <summary>A recording <see cref="ITurnAcks"/> in front of the real queue, which can be told to throw on every call.</summary>
    private sealed class RecordingAcks(ITurnAcks inner, bool throws) : ITurnAcks
    {
        private readonly Lock gate = new();
        private readonly List<string[]> replied = [];
        private int calls;

        internal int Calls => Volatile.Read(ref this.calls);

        internal IReadOnlyList<string[]> Replied
        {
            get
            {
                lock (this.gate)
                {
                    return [.. this.replied];
                }
            }
        }

        public void Apply(string connectionId, string agentId, QueueAck ack)
        {
            this.Enter();
            inner.Apply(connectionId, agentId, ack);
        }

        public void MarkReplied(string agentId, string roomId, IReadOnlyList<string> inReplyTo)
        {
            this.Enter();
            lock (this.gate)
            {
                this.replied.Add([.. inReplyTo]);
            }

            inner.MarkReplied(agentId, roomId, inReplyTo);
        }

        public void MarkRefused(string agentId, string roomId, IReadOnlyList<string> inReplyTo)
        {
            this.Enter();
            inner.MarkRefused(agentId, roomId, inReplyTo);
        }

        public void SetSubscribed(string agentId, bool durableQueue)
        {
            this.Enter();
            inner.SetSubscribed(agentId, durableQueue);
        }

        private void Enter()
        {
            _ = Interlocked.Increment(ref this.calls);
            if (throws)
            {
                throw new InvalidOperationException("The store is unavailable.");
            }
        }
    }

    /// <summary>A started host whose <see cref="ITurnAcks"/> is a <see cref="RecordingAcks"/>, with raw clients connected by key.</summary>
    private sealed class Scenario : IAsyncDisposable
    {
        private readonly Dictionary<string, JsonLineStream> clients = new(StringComparer.Ordinal);

        private Scenario(PipeHostFixture fixture)
        {
            this.Fixture = fixture;
        }

        internal PipeHostFixture Fixture { get; }

        internal TurnQueueStore Store => this.Fixture.Services.GetRequiredService<TurnQueueStore>();

        internal RecordingAcks Recorder => (RecordingAcks)this.Fixture.Services.GetRequiredService<ITurnAcks>();

        internal static async Task<Scenario> StartAsync(BehaviourBudget budget, bool throwing = false)
        {
            PipeHostFixture fixture = await PipeHostFixture.StartAsync(
                null,
                services =>
                {
                    services.RemoveAll<ITurnAcks>();
                    services.AddSingleton<ITurnAcks>(sp => new RecordingAcks(sp.GetRequiredService<TurnQueue>(), throwing));
                },
                budget);
            return new Scenario(fixture);
        }

        internal JsonLineStream Client(string key) => this.clients[key];

        /// <summary>Connects a raw client, sends its Hello and returns the Welcome.</summary>
        internal async Task<Welcome> ConnectAsync(string name, bool durable, CancellationToken ct, string? key = null)
        {
            JsonLineStream client = await this.Fixture.ConnectClientAsync(ct);
            this.clients[key ?? name] = client;
            await client.WriteAsync(new Hello(name, null, durable ? true : null), ct);
            return Assert.IsType<Welcome>(await client.ReadAsync(ct));
        }

        /// <summary>Writes one waiting row.</summary>
        internal void Seed(string agentId, string roomId, string messageId)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            _ = this.Store.Record(agentId, messageId, roomId, now, now);
        }

        internal IReadOnlyList<TurnQueueRowSnapshot> Rows() => TurnQueueRows.Read(this.DataDir);

        internal TurnQueueRowSnapshot Row(string agentId, string roomId, string messageId) =>
            TurnQueueRows.Find(this.DataDir, agentId, roomId, messageId);

        /// <summary>How many connections have logged their end.</summary>
        internal int Disconnects() =>
            this.Fixture.LogEntries.Count(entry => entry.Message.EndsWith("disconnected.", StringComparison.Ordinal)
                && entry.Level == LogLevel.Information);

        internal static Task UntilAsync(Func<bool> condition, CancellationToken ct) =>
            Eventually.UntilAsync(condition, ct, timeout: TestBudgets.Standard, interval: TimeSpan.FromMilliseconds(20));

        private string DataDir => this.Fixture.Services.GetRequiredService<IOptions<TeamOptions>>().Value.DataDir;

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

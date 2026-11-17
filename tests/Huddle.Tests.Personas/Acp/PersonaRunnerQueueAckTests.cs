using Agency.Huddle.App.Acp;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Sessions;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.Waiting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Pins what a real <see cref="PersonaRunner"/> tells the server about the Messages it is given (Turn Queue spec,
/// section 5.5): it says it keeps a durable queue in its Hello, and it closes a delivery its Reply Gate declines.
/// </summary>
public sealed class PersonaRunnerQueueAckTests
{
    private static readonly RoomInfo GroupRoom = new(
        "room-g",
        "Group",
        [
            new MemberInfo("human", "Human", UserKind.Human),
            new MemberInfo("agent-1", "nova", UserKind.Agent),
            new MemberInfo("friend", "Friend", UserKind.Agent),
        ]);

    private static MessagePosted Posted(string senderId, bool mentioned, int agentMessagesSinceHuman, int budget) =>
        new(
            GroupRoom.Id,
            GroupRoom.Name,
            new ChatMessage(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, senderId, senderId, "hello there"),
            mentioned,
            Mentions: [],
            Members: GroupRoom.Members,
            AgentMessagesSinceHuman: agentMessagesSinceHuman,
            Budget: budget);

    private static PersonaRunner CreateRunner(PipeRunnerServer server, FakeAgentHostFactory factory)
    {
        var options = TestOptions.Create(opts => opts.PipeName = server.PipeName);
        return new PersonaRunner(TestPersonas.Nova, options, factory, new FakePromptSource(), new RoomFollows(), NullLogger<PersonaRunner>.Instance);
    }

    private static Task WaitForAckAsync(PipeRunnerServer server, QueueAckKind kind, CancellationToken ct) =>
        Eventually.UntilAsync(() => server.Received.OfType<QueueAck>().Any(ack => ack.Kind == kind), ct, timeout: TestBudgets.Standard);

    /// <summary>The Hello says the runner keeps a durable queue.</summary>
    [Fact]
    public async Task Hello_CarriesDurableQueue()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using var server = new PipeRunnerServer("runner-ack-tests");
        await using var runner = CreateRunner(server, new FakeAgentHostFactory());

        await server.HandshakeAsync(runner, [GroupRoom], ct);

        Assert.True(server.Hello?.DurableQueue);
    }

    /// <summary>A delivery the Reply Gate reads as Catch-up only is closed with Finished(Declined) naming the Message.</summary>
    [Fact]
    public async Task CatchUpDelivery_SendsFinishedDeclined()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using var server = new PipeRunnerServer("runner-ack-tests");
        await using var runner = CreateRunner(server, new FakeAgentHostFactory());
        await server.HandshakeAsync(runner, [GroupRoom], ct);
        MessagePosted posted = Posted("human", mentioned: false, agentMessagesSinceHuman: 0, budget: 0);

        await server.SendAsync(posted, ct);
        await WaitForAckAsync(server, QueueAckKind.Finished, ct);

        QueueAck ack = Assert.Single(server.Received.OfType<QueueAck>());
        Assert.Equal(GroupRoom.Id, ack.RoomId);
        Assert.Equal([posted.Message.Id], ack.MessageIds);
        Assert.Equal(QueueOutcome.Declined, ack.Outcome);
    }

    /// <summary>A delivery the Reply Gate holds because the Room's Budget is spent is closed with Finished(Declined).</summary>
    [Fact]
    public async Task BudgetExhaustedDelivery_SendsFinishedDeclined()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using var server = new PipeRunnerServer("runner-ack-tests");
        await using var runner = CreateRunner(server, new FakeAgentHostFactory());
        await server.HandshakeAsync(runner, [GroupRoom], ct);
        MessagePosted posted = Posted("friend", mentioned: true, agentMessagesSinceHuman: 3, budget: 3);

        await server.SendAsync(posted, ct);
        await WaitForAckAsync(server, QueueAckKind.Finished, ct);

        QueueAck ack = Assert.Single(server.Received.OfType<QueueAck>());
        Assert.Equal([posted.Message.Id], ack.MessageIds);
        Assert.Equal(QueueOutcome.Declined, ack.Outcome);
    }

    /// <summary>A Mentioned delivery that is answered sends Started, then a reply naming the Message, and never a Finished.</summary>
    [Fact]
    public async Task RepliedDelivery_SendsStartedThenReplyWithInReplyTo()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using var server = new PipeRunnerServer("runner-ack-tests");
        FakeAgentHostFactory factory = new() { SessionPerRoom = true };
        await using var runner = CreateRunner(server, factory);
        await server.HandshakeAsync(runner, [GroupRoom], ct);
        Assert.NotNull(factory.Host);
        factory.Host.OnOpen = session => session.EnqueueReply("on it");
        MessagePosted posted = Posted("human", mentioned: true, agentMessagesSinceHuman: 0, budget: 0);

        await server.SendAsync(posted, ct);
        PostMessage reply = await server.ReceiveUntilAsync<PostMessage>(null, ct);

        QueueAck started = Assert.Single(server.Received.OfType<QueueAck>());
        Assert.Equal(QueueAckKind.Started, started.Kind);
        Assert.Equal([posted.Message.Id], started.MessageIds);
        Assert.Equal(reply.MessageId, started.ReplyMessageId);
        Assert.Equal([posted.Message.Id], reply.InReplyTo);
    }

    /// <summary>Disposing the runner while a Turn runs cancels its run token, so no Finished is sent for that Turn.</summary>
    [Fact]
    public async Task DisposedMidTurn_SendsNoFinished()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using var server = new PipeRunnerServer("runner-ack-tests");
        FakeAgentHostFactory factory = new() { SessionPerRoom = true };
        PersonaRunner runner = CreateRunner(server, factory);
        await server.HandshakeAsync(runner, [GroupRoom], ct);
        Assert.NotNull(factory.Host);
        factory.Host.OnOpen = session => session.EnqueueDelayedReply(TimeSpan.FromSeconds(30), "never");

        await server.SendAsync(Posted("human", mentioned: true, agentMessagesSinceHuman: 0, budget: 0), ct);
        await WaitForAckAsync(server, QueueAckKind.Started, ct);
        await runner.DisposeAsync();

        Assert.DoesNotContain(server.Received.OfType<QueueAck>(), ack => ack.Kind == QueueAckKind.Finished);
        Assert.Equal(QueueAckKind.Started, Assert.Single(server.Received.OfType<QueueAck>()).Kind);
    }
}

using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Sessions;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.Waiting;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// What a real <see cref="PersonaRunner"/> does with a Message the server sends a second time (Turn Queue spec, sections 5.7 and 5.8): it is not
/// put through the Mention, Member count or following checks, a spent Budget holds it, and a Message already queued or running is not run twice.
/// </summary>
public sealed class PersonaRunnerRedeliveryTests
{
    private static readonly RoomInfo GroupRoom = new(
        "room-g",
        "Group",
        [
            new MemberInfo("human", "Human", UserKind.Human),
            new MemberInfo("agent-1", "nova", UserKind.Agent),
            new MemberInfo("friend", "Friend", UserKind.Agent),
        ]);

    private static MessagePosted Posted(string text, Redelivery? redelivery, int agentMessagesSinceHuman = 0, int budget = 0, string? id = null) =>
        new(
            GroupRoom.Id,
            GroupRoom.Name,
            new ChatMessage(id ?? Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, "human", "Human", text),
            Mentioned: false,
            Mentions: [],
            Members: GroupRoom.Members,
            AgentMessagesSinceHuman: agentMessagesSinceHuman,
            Budget: budget,
            Redelivery: redelivery);

    private static PersonaRunner CreateRunner(PipeRunnerServer server, FakeAgentHostFactory factory)
    {
        var options = TestOptions.Create(opts => opts.PipeName = server.PipeName);
        return new PersonaRunner(TestPersonas.Nova, options, factory, new FakePromptSource(), new RoomFollows(), NullLogger<PersonaRunner>.Instance);
    }

    /// <summary>A re-sent Message that names nobody in a group Room (its Mention was of a name the Agent has since left) is still answered.</summary>
    [Fact]
    public async Task Redelivery_NotMentionedInAGroupRoom_IsStillAnswered()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using var server = new PipeRunnerServer("runner-redelivery-tests");
        FakeAgentHostFactory factory = new() { SessionPerRoom = true };
        await using var runner = CreateRunner(server, factory);
        await server.HandshakeAsync(runner, [GroupRoom], ct);
        Assert.NotNull(factory.Host);
        factory.Host.OnOpen = session => session.EnqueueReply("on it");
        MessagePosted posted = Posted("@OldName look", Redelivery.Waiting);

        await server.SendAsync(posted, ct);
        PostMessage reply = await server.ReceiveUntilAsync<PostMessage>(null, ct);

        Assert.Equal([posted.Message.Id], reply.InReplyTo);
        Assert.Equal(QueueAckKind.Started, Assert.Single(server.Received.OfType<QueueAck>()).Kind);
    }

    /// <summary>The same Message sent without the mark is read as context only: the mark alone lifts the gate.</summary>
    [Fact]
    public async Task OrdinaryDelivery_NotMentionedInAGroupRoom_IsNotAnswered()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using var server = new PipeRunnerServer("runner-redelivery-tests");
        await using var runner = CreateRunner(server, new FakeAgentHostFactory());
        await server.HandshakeAsync(runner, [GroupRoom], ct);

        await server.SendAsync(Posted("@OldName look", null), ct);
        await Eventually.UntilAsync(() => server.Received.OfType<QueueAck>().Any(), ct, timeout: TestBudgets.Standard);

        Assert.Equal(QueueOutcome.Declined, Assert.Single(server.Received.OfType<QueueAck>()).Outcome);
    }

    /// <summary>A cut-off re-delivery whose Room's Budget is spent writes Held and starts nothing.</summary>
    [Fact]
    public async Task Redelivery_BudgetSpent_WritesHeldAndEnqueuesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using var server = new PipeRunnerServer("runner-redelivery-tests");
        await using var runner = CreateRunner(server, new FakeAgentHostFactory());
        await server.HandshakeAsync(runner, [GroupRoom], ct);
        MessagePosted posted = Posted("hello", Redelivery.CutOff, agentMessagesSinceHuman: 3, budget: 3);

        await server.SendAsync(posted, ct);
        await Eventually.UntilAsync(() => server.Received.OfType<QueueAck>().Any(), ct, timeout: TestBudgets.Standard);

        QueueAck ack = Assert.Single(server.Received.OfType<QueueAck>());
        Assert.Equal((QueueAckKind.Held, GroupRoom.Id), (ack.Kind, ack.RoomId));
        Assert.Equal([posted.Message.Id], ack.MessageIds);
    }

    /// <summary>The same Message delivered twice while its Turn is open runs one Turn; a later Message still runs after it.</summary>
    [Fact]
    public async Task SameMessageDeliveredTwice_RunsOneTurn()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using var server = new PipeRunnerServer("runner-redelivery-tests");
        FakeAgentHostFactory factory = new() { SessionPerRoom = true };
        await using var runner = CreateRunner(server, factory);
        await server.HandshakeAsync(runner, [GroupRoom], ct);
        Assert.NotNull(factory.Host);
        factory.Host.OnOpen = session =>
        {
            session.EnqueueDelayedReply(TimeSpan.FromMilliseconds(300), "first");
            session.EnqueueReply("marker reply");
        };
        MessagePosted original = Posted("hello", Redelivery.Waiting);

        await server.SendAsync(original, ct);
        await server.SendAsync(original, ct);
        await server.SendAsync(Posted("marker", Redelivery.Waiting), ct);
        _ = await server.ReceiveUntilAsync<PostMessage>(null, ct);
        _ = await server.ReceiveUntilAsync<PostMessage>(null, ct);

        Assert.Equal(2, server.Received.OfType<QueueAck>().Count(ack => ack.Kind == QueueAckKind.Started));
    }

    /// <summary>An ignored duplicate drains nothing: the Catch-up line that arrived before it still shows in the next Turn's prompt.</summary>
    [Fact]
    public async Task IgnoredDuplicate_DoesNotDrainTheCatchUp()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using var server = new PipeRunnerServer("runner-redelivery-tests");
        FakeAgentHostFactory factory = new() { SessionPerRoom = true };
        await using var runner = CreateRunner(server, factory);
        await server.HandshakeAsync(runner, [GroupRoom], ct);
        Assert.NotNull(factory.Host);
        factory.Host.OnOpen = session =>
        {
            session.EnqueueDelayedReply(TimeSpan.FromMilliseconds(600), "first");
            session.EnqueueReply("third reply");
        };
        MessagePosted original = Posted("hello", Redelivery.Waiting);

        await server.SendAsync(original, ct);
        await server.SendAsync(Posted("zebra-context-line", null), ct);
        await server.SendAsync(original, ct);
        await server.SendAsync(Posted("marker", Redelivery.Waiting), ct);
        _ = await server.ReceiveUntilAsync<PostMessage>(null, ct);
        _ = await server.ReceiveUntilAsync<PostMessage>(null, ct);

        string lastPrompt = factory.Host.Sessions.SelectMany(session => session.Prompts).Last();
        Assert.Contains("zebra-context-line", lastPrompt, StringComparison.Ordinal); // contains-ok: the Catch-up block's wording is not this test's subject; only that the line survived.
    }
}

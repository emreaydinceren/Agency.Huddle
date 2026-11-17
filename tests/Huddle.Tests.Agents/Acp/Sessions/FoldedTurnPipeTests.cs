using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.Acp.Sessions;

/// <summary>
/// Pins what a real <see cref="PersonaRunner"/> puts on the pipe for Folded Turns (Turn Queue spec, section 6): the Started ack and the
/// reply's <see cref="PostMessage.InReplyTo"/> name every Message a Turn answers, which is what lets the server close every row as replied.
/// Also pins which deliveries the runner marks Foldable.
/// </summary>
public sealed class FoldedTurnPipeTests
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(20);

    private static readonly MemberInfo Human = new("human", "Human", UserKind.Human);

    private static readonly MemberInfo Nova = new("agent-1", "nova", UserKind.Agent);

    private static readonly MemberInfo Rio = new("agent-2", "rio", UserKind.Agent);

    /// <summary>Three quick Human Messages in a one-to-one Room, queued behind a busy Turn, are one Turn naming all three ids in its Started ack and reply.</summary>
    [Fact]
    public async Task OneToOne_ThreeMessagesBehindABusyTurn_StartedAckAndReplyNameAllThree()
    {
        using CancellationTokenSource cts = new(Budget);
        CancellationToken ct = cts.Token;

        (IReadOnlyList<string[]> started, PostMessage reply) = await RunAsync(new RoomInfo("room-a", "Direct", [Human, Nova]), ct);

        Assert.Equal(["t1", "t2", "t3"], reply.InReplyTo);
        Assert.Equal(["t1", "t2", "t3"], Assert.Single(started));
    }

    /// <summary>The same three Messages in a group Room are three Turns, each with its own ack and reply.</summary>
    [Fact]
    public async Task Group_ThreeMessagesBehindABusyTurn_AreThreeTurns()
    {
        using CancellationTokenSource cts = new(Budget);
        CancellationToken ct = cts.Token;

        (IReadOnlyList<string[]> started, PostMessage reply) = await RunAsync(new RoomInfo("room-a", "Group", [Human, Nova, Rio]), ct);

        Assert.Equal(["t3"], reply.InReplyTo);
        Assert.Equal([["t1"], ["t2"], ["t3"]], started);
    }

    /// <summary>Only the Human's Message in a two-Member Room with <c>FoldOneToOne</c> on is Foldable.</summary>
    /// <param name="members">How many Members the Room has: 2 or 3.</param>
    /// <param name="humanSends">Whether the Human (or another Member) sent it.</param>
    /// <param name="foldOneToOne">The <c>Team:Acp:FoldOneToOne</c> switch.</param>
    /// <param name="expected">Whether the Message may fold.</param>
    [Theory]
    [InlineData(2, true, true, true)]
    [InlineData(3, true, true, false)]
    [InlineData(2, false, true, false)]
    [InlineData(2, true, false, false)]
    public void IsFoldable_ByRoomSenderAndSwitch_OnlyTheHumansOneToOneMessageFolds(int members, bool humanSends, bool foldOneToOne, bool expected)
    {
        MemberInfo[] roomMembers = members == 2 ? [Human, Nova] : [Human, Nova, Rio];
        MemberInfo sender = humanSends ? Human : Nova;
        MessagePosted posted = Posted(new RoomInfo("room-a", "Room", roomMembers), "t1", sender);

        Assert.Equal(expected, PersonaRunner.IsFoldable(posted, foldOneToOne));
    }

    /// <summary>Sends t0 (held), then t1..t3, and releases t0; returns the Started ack ids after t0.</summary>
    private static async Task<(IReadOnlyList<string[]> Started, PostMessage LastReply)> RunAsync(RoomInfo room, CancellationToken ct)
    {
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using PipeRunnerServer server = new("folded-turn-pipe-tests");
        FakeAgentHostFactory factory = new() { SessionPerRoom = true };
        factory.Session.EnqueueGatedReply(release.Task, "first");
        factory.Session.EnqueueReply("folded");
        factory.Session.EnqueueReply("second");
        factory.Session.EnqueueReply("third");
        await using PersonaRunner runner = CreateRunner(server, factory);
        RoomInfo sentinelRoom = new("room-s", "Sentinel", [Human, Nova, Rio]);
        await server.HandshakeAsync(runner, [room, sentinelRoom], ct);
        try
        {
            await server.SendAsync(Posted(room, "t0", Human), ct);
            _ = await server.ReceiveUntilAsync<QueueAck>(ack => ack.Kind == QueueAckKind.Started && ack.MessageIds.Contains("t0"), ct);
            foreach (string id in new[] { "t1", "t2", "t3" })
            {
                await server.SendAsync(Posted(room, id, Human), ct);
            }

            // The read loop handles envelopes in order, and the Reply Gate declines an unmentioned group delivery at once from it,
            // writing a Declined ack: that ack proves t1..t3 are already queued.
            await server.SendAsync(Posted(sentinelRoom, "s1", Human, mentioned: false), ct);
            _ = await server.ReceiveUntilAsync<QueueAck>(ack => ack.Outcome == QueueOutcome.Declined && ack.MessageIds.Contains("s1"), ct);
            release.TrySetResult();

            PostMessage reply = await server.ReceiveUntilAsync<PostMessage>(
                post => post.RoomId == room.Id && post.InReplyTo is { } ids && ids.Contains("t3"), ct);
            IReadOnlyList<string[]> started =
            [
                .. server.Received.OfType<QueueAck>()
                    .Where(ack => ack.Kind == QueueAckKind.Started && ack.RoomId == room.Id && !ack.MessageIds.Contains("t0"))
                    .Select(ack => ack.MessageIds.ToArray()),
            ];
            return (started, reply);
        }
        finally
        {
            release.TrySetResult();
        }
    }

    private static MessagePosted Posted(RoomInfo room, string id, MemberInfo sender, string? text = null, bool mentioned = true) =>
        new(
            room.Id,
            room.Name,
            new ChatMessage(id, DateTimeOffset.UtcNow, sender.Id, sender.Name, text ?? "text " + id),
            Mentioned: mentioned,
            Mentions: [],
            Members: room.Members,
            AgentMessagesSinceHuman: 0,
            Budget: 0);

    private static PersonaRunner CreateRunner(PipeRunnerServer server, FakeAgentHostFactory factory) =>
        new(
            TestPersonas.Nova,
            TestOptions.Create(opts => opts.PipeName = server.PipeName),
            factory,
            new FakePromptSource(),
            new RoomFollows(),
            NullLogger<PersonaRunner>.Instance);
}

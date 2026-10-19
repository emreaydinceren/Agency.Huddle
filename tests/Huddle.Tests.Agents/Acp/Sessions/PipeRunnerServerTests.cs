using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.Acp.Sessions;

/// <summary>
/// Pins <see cref="PipeRunnerServer"/> (R6 row 1, OP-7): the hand-scripted hub end of the runner pipe,
/// driven by a real <see cref="PersonaRunner"/> the way <c>RoomSessionPoolTests</c> drives its own copy.
/// </summary>
public sealed class PipeRunnerServerTests
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(20);

    /// <summary>The handshake answers the runner's Hello, so the runner starts and opens the Human Room's session.</summary>
    [Fact]
    public async Task HandshakeAsync_AnswersHello_RunnerStartsAndOpensHumanRoom()
    {
        using CancellationTokenSource cts = new(Budget);
        CancellationToken ct = cts.Token;

        await using PipeRunnerServer server = new("pipe-runner-server-tests");
        FakeAgentHostFactory factory = new() { SessionPerRoom = true };
        await using PersonaRunner runner = CreateRunner(server, factory);

        await server.HandshakeAsync(runner, [HumanRoom("room-a")], ct);

        Assert.Single(factory.Host!.Sessions);
        Assert.StartsWith("pipe-runner-server-tests", server.PipeName, StringComparison.Ordinal);
    }

    /// <summary>A first-Turn Transcript read is answered with an empty tail, so the Turn is not held up for the runner's own 10-second wait.</summary>
    [Fact]
    public async Task ReadTranscript_AnsweredWithEmptyTail_TurnCompletes()
    {
        using CancellationTokenSource cts = new(Budget);
        CancellationToken ct = cts.Token;

        await using PipeRunnerServer server = new("pipe-runner-server-tests");
        FakeAgentHostFactory factory = new() { SessionPerRoom = true };
        await using PersonaRunner runner = CreateRunner(server, factory);
        RoomInfo room = HumanRoom("room-a");
        await server.HandshakeAsync(runner, [room], ct);

        await server.SendAsync(Posted(room, "hello"), ct);
        PostMessage reply = await server.ReceiveUntilAsync<PostMessage>(null, ct);

        Assert.Equal(room.Id, reply.RoomId);
        Assert.Contains(server.Received, static message => message is ReadTranscript);
    }

    /// <summary>With <see cref="PipeRunnerServer.RefuseTranscriptReads"/> the read is answered with a refusal and the Turn still completes.</summary>
    [Fact]
    public async Task ReadTranscript_Refused_TurnStillCompletes()
    {
        using CancellationTokenSource cts = new(Budget);
        CancellationToken ct = cts.Token;

        await using PipeRunnerServer server = new("pipe-runner-server-tests") { RefuseTranscriptReads = true };
        FakeAgentHostFactory factory = new() { SessionPerRoom = true };
        await using PersonaRunner runner = CreateRunner(server, factory);
        RoomInfo room = HumanRoom("room-a");
        await server.HandshakeAsync(runner, [room], ct);

        await server.SendAsync(Posted(room, "hello"), ct);
        PostMessage reply = await server.ReceiveUntilAsync<PostMessage>(null, ct);

        Assert.Equal(room.Id, reply.RoomId);
        Assert.Contains(server.Received, static message => message is ReadTranscript);
        Assert.True(server.RefuseTranscriptReads);
    }

    /// <summary>ReceiveUntilAsync skips envelopes that fail the predicate and returns the first that matches.</summary>
    [Fact]
    public async Task ReceiveUntilAsync_Predicate_ReturnsFirstMatch()
    {
        using CancellationTokenSource cts = new(Budget);
        CancellationToken ct = cts.Token;

        await using PipeRunnerServer server = new("pipe-runner-server-tests");
        FakeAgentHostFactory factory = new() { SessionPerRoom = true };
        factory.Session.DefaultReplyText = "the answer";
        await using PersonaRunner runner = CreateRunner(server, factory);
        RoomInfo room = HumanRoom("room-a");
        await server.HandshakeAsync(runner, [room], ct);

        await server.SendAsync(Posted(room, "hello"), ct);
        MessageDelta last = await server.ReceiveUntilAsync<MessageDelta>(static delta => delta.IsFinal, ct);

        Assert.True(last.IsFinal);
        Assert.Equal(room.Id, last.RoomId);
    }

    /// <summary>Once the runner closes the pipe, a wait for an envelope that never comes fails promptly rather than running out the caller's token.</summary>
    [Fact]
    public async Task ReceiveUntilAsync_PipeClosed_FailsPromptly()
    {
        using CancellationTokenSource cts = new(Budget);
        CancellationToken ct = cts.Token;

        await using PipeRunnerServer server = new("pipe-runner-server-tests");
        FakeAgentHostFactory factory = new() { SessionPerRoom = true };
        PersonaRunner runner = CreateRunner(server, factory);
        await server.HandshakeAsync(runner, [HumanRoom("room-a")], ct);

        await runner.DisposeAsync();

        _ = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await server.ReceiveUntilAsync<PostMessage>(null, ct));
        Assert.False(ct.IsCancellationRequested);
    }

    /// <summary>A fault in the pump (here, a line over the protocol's size cap) is recorded and fails DisposeAsync instead of vanishing.</summary>
    [Fact]
    public async Task DisposeAsync_PumpFaulted_Throws()
    {
        using CancellationTokenSource cts = new(Budget);
        CancellationToken ct = cts.Token;

        PipeRunnerServer server = new("pipe-runner-server-tests");
        FakeAgentHostFactory factory = new() { SessionPerRoom = true };
        factory.Session.EnqueueReply(new string('x', JsonLineStream.MaxLineBytes + 1024));
        await using PersonaRunner runner = CreateRunner(server, factory);
        RoomInfo room = HumanRoom("room-a");
        await server.HandshakeAsync(runner, [room], ct);

        await server.SendAsync(Posted(room, "hello"), ct);
        _ = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await server.ReceiveUntilAsync<PostMessage>(null, ct));

        InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await server.DisposeAsync());
        Assert.NotNull(failure.InnerException);
    }

    private static RoomInfo HumanRoom(string id) =>
        new(id, "Direct", [new MemberInfo("human", "Human", UserKind.Human), new MemberInfo("agent-1", "nova", UserKind.Agent)]);

    private static MessagePosted Posted(RoomInfo room, string text) =>
        new(
            room.Id,
            room.Name,
            new ChatMessage(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, "human", "Human", text),
            Mentioned: true,
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

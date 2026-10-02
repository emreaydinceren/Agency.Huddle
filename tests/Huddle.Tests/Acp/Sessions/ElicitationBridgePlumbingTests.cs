using System.IO.Pipes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Sessions.Fakes;

namespace Agency.Huddle.Tests.Acp.Sessions;

/// <summary>
/// Pins that the <see cref="IElicitationBridge"/> handed to a <see cref="PersonaRunner"/> is the one
/// every Room Session it builds through its <c>RoomSessionPool</c> answers questions with, so the real
/// bridge can be supplied by DI without touching either. Harness: a real runner over a hand-scripted
/// pipe server (<see cref="FakeServer"/>, this file's own copy of the one the other runner tests keep).
/// </summary>
public sealed class ElicitationBridgePlumbingTests
{
    private static readonly ElicitationRequest Request = new("session-1", null, "Which one?", """{"type":"object","properties":{}}""");

    /// <summary>A question asked of the scope the runner's session bound reaches the runner's bridge, with the Turn's Room and the runner's Agent id.</summary>
    [Fact]
    public async Task PersonaRunner_PassesItsBridgeToTheRoomSessionsItBuilds()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(20));
        CancellationToken ct = cts.Token;

        await using FakeServer server = new();
        FakeAgentHostFactory factory = new() { SessionPerRoom = true };
        FakeElicitationBridge bridge = new();
        Persona persona = new("nova", "You are Nova.");
        RoomInfo room = new(
            "room-1",
            "Direct",
            [new MemberInfo("human", "Human", UserKind.Human), new MemberInfo("agent-1", "nova", UserKind.Agent)]);
        IOptions<TeamOptions> options = Options.Create(new TeamOptions { PipeName = server.PipeName });
        await using PersonaRunner runner = new(
            persona, options, factory, new FakePromptSource(), new RoomFollows(), NullLogger<PersonaRunner>.Instance, elicitationBridge: bridge);
        await server.HandshakeAsync(runner, [room], ct);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        factory.Session.EnqueueGatedReply(release.Task);
        try
        {
            await server.SendAsync(
                new MessagePosted(
                    room.Id,
                    room.Name,
                    new ChatMessage(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, "human", "Human", "hi"),
                    Mentioned: true,
                    Mentions: [],
                    Members: room.Members,
                    AgentMessagesSinceHuman: 0,
                    Budget: 0),
                ct);
            await WaitUntilAsync(() => factory.Session.Prompts.Count == 1, ct);

            IElicitationScope scope = Assert.Single(factory.Session.BoundScopes);
            Task<ElicitationResult> pending = scope.ElicitAsync(Request, ct);
            FakeElicitationBridge.BridgeCall call = (await bridge.WaitForCallsAsync(1, ct))[0];
            call.Answer.SetResult(new ElicitationDeclined());

            Assert.Equal(new ElicitationContext("room-1", "agent-1"), call.Context);
            Assert.IsType<ElicitationDeclined>(await pending.WaitAsync(TimeSpan.FromSeconds(5), ct));
        }
        finally
        {
            release.TrySetResult();
        }
    }

    /// <summary>Polls <paramref name="condition"/> until it is true, or fails the test after five seconds.</summary>
    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken ct)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("Condition was not met before the test's timeout.");
            }

            await Task.Delay(10, ct);
        }
    }

    /// <summary>A hand-scripted pipe server that answers the handshake and every <see cref="ReadTranscript"/>, and lets a test send envelopes to the runner.</summary>
    private sealed class FakeServer : IAsyncDisposable
    {
        private readonly NamedPipeServerStream pipe;

        private JsonLineStream? stream;
        private Task? pumpTask;

        /// <summary>Creates the server-side pipe instance, unconnected, under a unique per-test name.</summary>
        public FakeServer()
        {
            this.PipeName = "elicitation-bridge-plumbing-tests-" + Guid.NewGuid().ToString("N");
            this.pipe = new NamedPipeServerStream(
                this.PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        }

        /// <summary>The named pipe this server listens on; hand it to the runner's <see cref="TeamOptions.PipeName"/>.</summary>
        public string PipeName { get; }

        /// <summary>Starts <paramref name="runner"/>, accepts its connection, and answers the handshake with <paramref name="rooms"/>.</summary>
        /// <param name="runner">The runner to start against this server.</param>
        /// <param name="rooms">The Welcome's Room list.</param>
        /// <param name="ct">Cancels the accept, the handshake, and the runner's own start.</param>
        public async Task HandshakeAsync(PersonaRunner runner, IReadOnlyList<RoomInfo> rooms, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(runner);
            ArgumentNullException.ThrowIfNull(rooms);

            Task startTask = runner.StartAsync(ct);
            await this.pipe.WaitForConnectionAsync(ct);

            JsonLineStream lineStream = new(this.pipe);
            this.stream = lineStream;

            Hello hello = Assert.IsType<Hello>(await lineStream.ReadAsync(ct));
            await lineStream.WriteAsync(new Welcome("agent-1", hello.Name, rooms), ct);

            await startTask;

            this.pumpTask = PumpAsync(lineStream);
        }

        /// <summary>Writes one envelope to the connected runner.</summary>
        /// <param name="message">The envelope to send.</param>
        /// <param name="ct">Cancels the write.</param>
        public Task SendAsync(ProtocolMessage message, CancellationToken ct) =>
            (this.stream ?? throw new InvalidOperationException("Handshake before sending.")).WriteAsync(message, ct);

        /// <summary>Continuously reads from <paramref name="lineStream"/>, answering every <see cref="ReadTranscript"/> with an empty tail, until the stream ends or faults.</summary>
        /// <param name="lineStream">The connected stream to pump.</param>
        private static async Task PumpAsync(JsonLineStream lineStream)
        {
            try
            {
                while (true)
                {
                    ProtocolMessage? message = await lineStream.ReadAsync(CancellationToken.None);
                    if (message is null)
                    {
                        break;
                    }

                    if (message is ReadTranscript read)
                    {
                        await lineStream.WriteAsync(new TranscriptTail(read.RequestId, read.RoomId, [], Omitted: 0), CancellationToken.None);
                    }
                }
            }
            catch (Exception)
            {
                // The pipe closed (this server disposing, or the runner tearing down); nothing more
                // to pump.
            }
        }

        /// <summary>Disposes the line stream if the handshake reached it, otherwise the bare pipe.</summary>
        public async ValueTask DisposeAsync()
        {
            if (this.stream is not null)
            {
                await this.stream.DisposeAsync();
            }
            else
            {
                await this.pipe.DisposeAsync();
            }

            if (this.pumpTask is not null)
            {
                try
                {
                    await this.pumpTask;
                }
                catch (Exception)
                {
                    // Teardown must not throw regardless of how the pump ended.
                }
            }
        }
    }
}

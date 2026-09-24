namespace Agency.Huddle.Tests.Acp.Sessions;

using System.IO.Pipes;
using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;

/// <summary>
/// Pins <see cref="TurnActivity"/> at the runner level (D8 correction 6): a
/// <see cref="TurnActivity"/> passed to <see cref="PersonaRunner"/>'s constructor reaches the
/// <see cref="RoomSession"/> the runner opens. Harness: a real <see cref="PersonaRunner"/> over a
/// hand-scripted pipe server (<see cref="FakeServer"/>, this file's own copy of
/// <see cref="Agency.Huddle.Tests.Acp.Sessions.OwnPostsRunnerTests"/>'s), shared mode (the runner's
/// default), so the Turn is held open through <see cref="FakeAgentHostFactory.Session"/>.
/// </summary>
public sealed class TurnActivityRunnerTests
{
    /// <summary>
    /// A held-open Turn marks <see cref="TurnActivity"/> busy for this runner's Agent id in the Room
    /// the Message was posted into, and clears once the Turn completes.
    /// </summary>
    [Fact]
    public async Task HeldTurn_MarksTurnActivityBusy_ThenClears()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var ct = cts.Token;

        await using var server = new FakeServer();
        var factory = new FakeAgentHostFactory();
        var release = new TaskCompletionSource();
        factory.Session.EnqueueGatedReply(release.Task, "held reply");
        var persona = new Persona("nova", "You are Nova.");
        var room = GroupRoom("room-a", "Room A");
        var options = Options.Create(new TeamOptions { PipeName = server.PipeName });
        var turnActivity = new TurnActivity();

        await using var runner = new PersonaRunner(
            persona, options, factory, new FakePromptSource(), new RoomFollows(), NullLogger<PersonaRunner>.Instance, turnActivity: turnActivity);
        await server.HandshakeAsync(runner, [room], ct);

        await server.SendAsync(Posted(room, "hello"), ct);

        await WaitUntilAsync(() => turnActivity.IsBusyIn("agent-1", room.Id), ct);
        Assert.True(turnActivity.IsBusyIn("agent-1", room.Id));

        release.SetResult();

        await server.ReceiveUntilAsync<PostMessage>(ct);
        await WaitUntilAsync(() => !turnActivity.IsBusyIn("agent-1", room.Id), ct);
        Assert.False(turnActivity.IsBusyIn("agent-1", room.Id));
    }

    /// <summary>Polls <paramref name="condition"/> until it is true, or <paramref name="ct"/> fires.</summary>
    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken ct)
    {
        while (!condition())
        {
            await Task.Delay(20, ct);
        }
    }

    private static RoomInfo GroupRoom(string id, string name) =>
        new(
            id,
            name,
            [
                new MemberInfo("human", "Human", UserKind.Human),
                new MemberInfo("agent-1", "nova", UserKind.Agent),
            ]);

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

    /// <summary>A hand-scripted pipe server giving a test full control over the Welcome and every <see cref="ReadTranscript"/> answer.</summary>
    private sealed class FakeServer : IAsyncDisposable
    {
        private readonly NamedPipeServerStream pipe;
        private readonly Channel<ProtocolMessage> received = Channel.CreateUnbounded<ProtocolMessage>();

        private JsonLineStream? stream;
        private Task? pumpTask;

        /// <summary>Creates the server-side pipe instance, unconnected, under a unique per-test name.</summary>
        public FakeServer()
        {
            this.PipeName = "turn-activity-runner-tests-" + Guid.NewGuid().ToString("N");
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

            var startTask = runner.StartAsync(ct);
            await this.pipe.WaitForConnectionAsync(ct);

            var lineStream = new JsonLineStream(this.pipe);
            this.stream = lineStream;

            var hello = Assert.IsType<Hello>(await lineStream.ReadAsync(ct));
            await lineStream.WriteAsync(new Welcome("agent-1", hello.Name, rooms), ct);

            await startTask;

            this.pumpTask = this.PumpAsync();
        }

        /// <summary>Writes one envelope to the connected runner.</summary>
        /// <param name="message">The envelope to send.</param>
        /// <param name="ct">Cancels the write.</param>
        public Task SendAsync(ProtocolMessage message, CancellationToken ct) => this.stream!.WriteAsync(message, ct);

        /// <summary>Reads received envelopes until one of type <typeparamref name="T"/> arrives, discarding everything else.</summary>
        /// <typeparam name="T">The envelope type to wait for.</typeparam>
        /// <param name="ct">Bounds the read.</param>
        public async Task<T> ReceiveUntilAsync<T>(CancellationToken ct)
            where T : ProtocolMessage
        {
            while (true)
            {
                var message = await this.received.Reader.ReadAsync(ct);
                if (message is T typed)
                {
                    return typed;
                }
            }
        }

        /// <summary>Continuously reads from the pipe into <see cref="received"/>, answering every <see cref="ReadTranscript"/> itself.</summary>
        private async Task PumpAsync()
        {
            try
            {
                while (true)
                {
                    var message = await this.stream!.ReadAsync(CancellationToken.None);
                    if (message is null)
                    {
                        break;
                    }

                    if (message is ReadTranscript read)
                    {
                        ProtocolMessage answer = new TranscriptTail(read.RequestId, read.RoomId, [], Omitted: 0);
                        await this.stream.WriteAsync(answer, CancellationToken.None);
                        continue;
                    }

                    await this.received.Writer.WriteAsync(message, CancellationToken.None);
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

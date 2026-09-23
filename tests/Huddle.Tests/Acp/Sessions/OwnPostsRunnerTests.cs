namespace Agency.Huddle.Tests.Acp.Sessions;

using System.IO.Pipes;
using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;

/// <summary>
/// Pins <see cref="OwnPosts"/> at the runner level (D27 correction 10): the Take call the read loop
/// makes beside <c>TakeCatchUp</c>, and correction 11's drop rule. Harness: a real
/// <see cref="PersonaRunner"/> over a hand-scripted pipe server (<see cref="FakeServer"/>, this
/// file's own copy of <see cref="Agency.Huddle.Tests.Acp.Sessions.RoomSessionPoolTests"/>'s, kept
/// separate because these tests script the <see cref="ReadTranscript"/> answer per test rather than
/// always returning an empty tail), per-Room mode.
/// </summary>
public sealed class OwnPostsRunnerTests
{
    /// <summary>
    /// A post <see cref="OwnPosts.Record"/> makes into Room B while Room A's Turn is the one making
    /// it (U12: simulated directly, since the fake invokes no App Tool) appears as a
    /// <c>turn.ownPostLine</c> in Room B's own next Turn - its SECOND, so the drop rule (correction
    /// 11) does not eat it on the first.
    /// </summary>
    [Fact]
    public async Task PostFromRoomA_IntoRoomB_AppearsInBsNextTurn()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var ct = cts.Token;

        await using var server = new FakeServer();
        var factory = new FakeAgentHostFactory { SessionPerRoom = true };
        var persona = new Persona("nova", "You are Nova.");
        var roomB = GroupRoom("room-b", "Room B");
        var options = Options.Create(new TeamOptions { PipeName = server.PipeName });
        var ownPosts = new OwnPosts(options);

        await using var runner = new PersonaRunner(
            persona, options, factory, new FakePromptSource(), new RoomFollows(), NullLogger<PersonaRunner>.Instance, ownPosts: ownPosts);
        await server.HandshakeAsync(runner, [roomB], ct);

        await server.SendAsync(Posted(roomB, "b's first"), ct);
        await server.ReceiveUntilAsync<PostMessage>(ct);

        // Waits for the Turn's own final MessageDelta, not just its PostMessage reply: EndTurn runs
        // earlier in the same finally block, so only the final delta guarantees Room B's own Busy
        // mark has already cleared before this Record call - recording right after the reply text
        // would race RoomSession's own EndTurn call.
        await server.ReceiveUntilAsync<MessageDelta>(static d => d.IsFinal, ct);

        ownPosts.Record("agent-1", roomB.Id, "posted from room A");

        await server.SendAsync(Posted(roomB, "b's second"), ct);
        await server.ReceiveUntilAsync<PostMessage>(ct);

        var sessionB = Assert.Single(factory.Host!.Sessions);
        Assert.Contains("You, from another Room: posted from room A", sessionB.Prompts[^1], StringComparison.Ordinal);
    }

    /// <summary>
    /// Correction 11: an own post recorded before a Room Session's very first Turn is dropped -
    /// the Transcript range that Turn reads already holds it, even when that range turns out empty.
    /// </summary>
    [Fact]
    public async Task OwnPostsDroppedOnFirstTurnAfterOpen()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var ct = cts.Token;

        await using var server = new FakeServer();
        var factory = new FakeAgentHostFactory { SessionPerRoom = true };
        var persona = new Persona("nova", "You are Nova.");
        var roomB = GroupRoom("room-b", "Room B");
        var options = Options.Create(new TeamOptions { PipeName = server.PipeName });
        var ownPosts = new OwnPosts(options);

        await using var runner = new PersonaRunner(
            persona, options, factory, new FakePromptSource(), new RoomFollows(), NullLogger<PersonaRunner>.Instance, ownPosts: ownPosts);
        await server.HandshakeAsync(runner, [roomB], ct);

        ownPosts.Record("agent-1", roomB.Id, "should be dropped");

        await server.SendAsync(Posted(roomB, "b's first"), ct);
        await server.ReceiveUntilAsync<PostMessage>(ct);

        var sessionB = Assert.Single(factory.Host!.Sessions);
        Assert.DoesNotContain("should be dropped", sessionB.Prompts[^1], StringComparison.Ordinal);
    }

    /// <summary>
    /// Correction 11: when the first Turn's own Transcript read is refused (RS §9 E-3), own-post
    /// lines recorded before it still render - the range that would have held them was never read.
    /// </summary>
    [Fact]
    public async Task OwnPostsRender_WhenTranscriptReadFails()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var ct = cts.Token;

        await using var server = new FakeServer { RefuseTranscriptReads = true };
        var factory = new FakeAgentHostFactory { SessionPerRoom = true };
        var persona = new Persona("nova", "You are Nova.");
        var roomB = GroupRoom("room-b", "Room B");
        var options = Options.Create(new TeamOptions { PipeName = server.PipeName });
        var ownPosts = new OwnPosts(options);

        await using var runner = new PersonaRunner(
            persona, options, factory, new FakePromptSource(), new RoomFollows(), NullLogger<PersonaRunner>.Instance, ownPosts: ownPosts);
        await server.HandshakeAsync(runner, [roomB], ct);

        ownPosts.Record("agent-1", roomB.Id, "should still render");

        await server.SendAsync(Posted(roomB, "b's first"), ct);
        await server.ReceiveUntilAsync<PostMessage>(ct);

        var sessionB = Assert.Single(factory.Host!.Sessions);
        Assert.Contains("You, from another Room: should still render", sessionB.Prompts[^1], StringComparison.Ordinal);
    }

    private static RoomInfo GroupRoom(string id, string name) =>
        new(
            id,
            name,
            [
                new MemberInfo("human", "Human", UserKind.Human),
                new MemberInfo("agent-1", "nova", UserKind.Agent),
                new MemberInfo("friend", "Friend", UserKind.Agent),
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
            this.PipeName = "own-posts-runner-tests-" + Guid.NewGuid().ToString("N");
            this.pipe = new NamedPipeServerStream(
                this.PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        }

        /// <summary>The named pipe this server listens on; hand it to the runner's <see cref="TeamOptions.PipeName"/>.</summary>
        public string PipeName { get; }

        /// <summary>When true, every <see cref="ReadTranscript"/> is answered with a refusal instead of an empty tail.</summary>
        public bool RefuseTranscriptReads { get; init; }

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
        public Task<T> ReceiveUntilAsync<T>(CancellationToken ct)
            where T : ProtocolMessage =>
            this.ReceiveUntilAsync<T>(static _ => true, ct);

        /// <summary>Reads received envelopes until one of type <typeparamref name="T"/> also matching <paramref name="predicate"/> arrives, discarding everything else.</summary>
        /// <typeparam name="T">The envelope type to wait for.</typeparam>
        /// <param name="predicate">Which envelope of type <typeparamref name="T"/> to stop on.</param>
        /// <param name="ct">Bounds the read.</param>
        public async Task<T> ReceiveUntilAsync<T>(Func<T, bool> predicate, CancellationToken ct)
            where T : ProtocolMessage
        {
            ArgumentNullException.ThrowIfNull(predicate);

            while (true)
            {
                var message = await this.received.Reader.ReadAsync(ct);
                if (message is T typed && predicate(typed))
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
                        ProtocolMessage answer = this.RefuseTranscriptReads
                            ? new ProtocolError("transcriptRefused", "refused for this test", read.RequestId)
                            : new TranscriptTail(read.RequestId, read.RoomId, [], Omitted: 0);
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

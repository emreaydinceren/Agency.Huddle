namespace Agency.Huddle.Tests.Acp.Sessions;

using System.IO.Pipes;
using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Sessions.Fakes;

/// <summary>
/// Pins RS §6.8 "Stop" routed to a Room's own <see cref="RoomSession"/> (finding P-6, RS §9 E-4),
/// over a real <see cref="PersonaRunner"/> talking to a hand-scripted pipe server
/// (<see cref="FakeServer"/>) - the same harness <c>RoomSessionPoolTests</c> uses, so a test here can
/// script the Welcome's Room list and drive two Rooms' Turns independently. One test
/// (<see cref="StopDuringTranscriptRead_NoPromptSent"/>) instead builds a bare
/// <see cref="RoomSession"/> with a <see cref="FakeRoomSessionOwner"/>, since it needs to gate the
/// Transcript read itself rather than a whole Turn.
/// </summary>
public sealed class RoomSessionStopTests
{
    /// <summary>RS §2 U8: Stop in Room A ends only A's live Turn and clears only A's queue; a queued item in Room B still runs.</summary>
    [Fact]
    public async Task StopA_EndsAsTurnAndQueue_BsWaitingTurnRuns()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var ct = cts.Token;

        await using var server = new FakeServer();
        var factory = new FakeAgentHostFactory { SessionPerRoom = true };
        var persona = new Persona("nova", "You are Nova.");
        var roomA = HumanRoom("room-a");
        var roomB = GroupRoom("room-b", "Room B");

        await using var runner = CreateRunner(server, persona, factory, new AcpOptions { MaxConcurrentTurns = 1 });
        await server.HandshakeAsync(runner, [roomA, roomB], ct);

        // A delayed reply, not a gated one: FakeAgentSession's Task.Delay observes the Turn's own
        // cancellation token, so a Stop can actually interrupt it - a gated reply's own await does
        // not, and is for a Turn the test itself means to release, never one it means to cancel.
        factory.Host!.Sessions[0].EnqueueDelayedReply(TimeSpan.FromSeconds(10), "a reply");

        await server.SendAsync(Posted(roomA, "hold room a"), ct);
        await WaitUntilAsync(() => factory.Host.Sessions[0].Prompts.Count == 1, ct);

        // Queued behind A's live Turn: a group Room's first Mention lazily opens its own session,
        // but that open only ever happens inside an admitted ticket (P-5), so it cannot start until
        // room-a's ticket is released.
        await server.SendAsync(Posted(roomB, "hi from b"), ct);

        await server.SendAsync(new StopTurn(roomA.Id), ct);

        // room-a's stopped Turn writes no reply (its FakeAgentSession never got to complete),
        // but does still write the terminating (empty) MessageDelta.
        await server.ReceiveUntilAsync<MessageDelta>(delta => delta.RoomId == roomA.Id && delta.IsFinal, ct);
        var postedB = await server.ReceiveUntilAsync<PostMessage>(ct);

        Assert.Equal(roomB.Id, postedB.RoomId);
        Assert.DoesNotContain(server.Received.OfType<PostMessage>(), message => message.RoomId == roomA.Id);
    }

    /// <summary>A Stop in Room A sends <c>session/cancel</c> only to A's own session, never B's.</summary>
    [Fact]
    public async Task StopA_SendsSessionCancelOnlyToAsSession()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var ct = cts.Token;

        await using var server = new FakeServer();
        var factory = new FakeAgentHostFactory { SessionPerRoom = true };
        var persona = new Persona("nova", "You are Nova.");
        var roomA = HumanRoom("room-a");
        var roomB = GroupRoom("room-b", "Room B");

        await using var runner = CreateRunner(server, persona, factory, new AcpOptions { MaxConcurrentTurns = 2 });
        await server.HandshakeAsync(runner, [roomA, roomB], ct);

        factory.Host!.Sessions[0].EnqueueDelayedReply(TimeSpan.FromSeconds(10), "a reply");

        await server.SendAsync(Posted(roomA, "hold room a"), ct);
        await WaitUntilAsync(() => factory.Host.Sessions[0].Prompts.Count == 1, ct);

        // TrySetResult in a finally: an ungated await observes no cancellation token (unlike
        // EnqueueDelayedReply), so an assertion failing above must still release it, or Room B's
        // consumer is left stuck inside PromptAsync forever and teardown (awaiting that consumer
        // task) hangs instead of reporting the actual failure.
        TaskCompletionSource releaseB = new(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            factory.Host.OnOpen = session => session.EnqueueGatedReply(releaseB.Task, "b reply");
            await server.SendAsync(Posted(roomB, "hold room b"), ct);
            await WaitUntilAsync(() => factory.Host.Sessions.Count == 2 && factory.Host.Sessions[1].Prompts.Count == 1, ct);

            await server.SendAsync(new StopTurn(roomA.Id), ct);
            await WaitUntilAsync(() => factory.Host.Sessions[0].CancelCallCount == 1, ct);

            Assert.Equal(0, factory.Host.Sessions[1].CancelCallCount);
        }
        finally
        {
            releaseB.TrySetResult();
        }

        await server.ReceiveUntilAsync<PostMessage>(message => message.RoomId == roomB.Id, ct);
    }

    /// <summary>At <c>MaxConcurrentTurns</c> 2, stopping Room A while Room B's Turn also runs leaves B's Turn to complete untouched.</summary>
    [Fact]
    public async Task StopA_Max2_BRunningUntouched()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var ct = cts.Token;

        await using var server = new FakeServer();
        var factory = new FakeAgentHostFactory { SessionPerRoom = true };
        var persona = new Persona("nova", "You are Nova.");
        var roomA = HumanRoom("room-a");
        var roomB = GroupRoom("room-b", "Room B");

        await using var runner = CreateRunner(server, persona, factory, new AcpOptions { MaxConcurrentTurns = 2 });
        await server.HandshakeAsync(runner, [roomA, roomB], ct);

        factory.Host!.Sessions[0].EnqueueDelayedReply(TimeSpan.FromSeconds(10), "a reply");
        await server.SendAsync(Posted(roomA, "hold room a"), ct);
        await WaitUntilAsync(() => factory.Host.Sessions[0].Prompts.Count == 1, ct);

        factory.Host.OnOpen = session => session.EnqueueReply("b reply");
        await server.SendAsync(Posted(roomB, "hi b"), ct);

        var postedB = await server.ReceiveUntilAsync<PostMessage>(message => message.RoomId == roomB.Id, ct);
        Assert.Equal("b reply", postedB.Text);

        await server.SendAsync(new StopTurn(roomA.Id), ct);
        await server.ReceiveUntilAsync<MessageDelta>(delta => delta.RoomId == roomA.Id && delta.IsFinal, ct);

        Assert.DoesNotContain(server.Received.OfType<PostMessage>(), message => message.RoomId == roomA.Id);
    }

    /// <summary>RS §9 E-4: a Stop that lands while a Room's session is still <c>Opening</c> lets the open finish, then clears the queue so nothing runs.</summary>
    [Fact]
    public async Task StopWhileOpening_OpenCompletesQueueClearedNothingRuns()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var ct = cts.Token;

        await using var server = new FakeServer();
        var factory = new FakeAgentHostFactory { SessionPerRoom = true };
        TaskCompletionSource openGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        factory.OnHostStarted = host => host.OpenGate = openGate;
        var persona = new Persona("nova", "You are Nova.");
        var roomB = GroupRoom("room-b", "Room B");

        await using var runner = CreateRunner(server, persona, factory, new AcpOptions { MaxConcurrentTurns = 1 });
        await server.HandshakeAsync(runner, [roomB], ct);

        // No Human Room: nothing opened at start, so this Mention is the very first item, and its
        // own admission is what starts the (now gated) open.
        await server.SendAsync(Posted(roomB, "hi"), ct);
        await Task.Delay(100, ct);

        await server.SendAsync(new StopTurn(roomB.Id), ct);

        openGate.TrySetResult();

        // Give the now-unblocked open, and whatever it would have run, every chance to happen.
        await Task.Delay(300, ct);

        Assert.Empty(server.Received.OfType<PostMessage>());
    }

    /// <summary>TRAP 1 at pool level: a Stop never reports a failure, even though it lands as the same <see cref="OperationCanceledException"/> an idle timeout would.</summary>
    [Fact]
    public async Task StopA_LatchHolds_NotReportedAsFailure()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var ct = cts.Token;

        await using var server = new FakeServer();
        var factory = new FakeAgentHostFactory { SessionPerRoom = true };
        var persona = new Persona("nova", "You are Nova.");
        var roomA = HumanRoom("room-a");

        StatusRecorder statuses = new();
        await using var runner = CreateRunner(server, persona, factory, new AcpOptions { MaxConcurrentTurns = 1 });
        runner.StatusChanged += statuses.Record;
        await server.HandshakeAsync(runner, [roomA], ct);

        factory.Host!.Sessions[0].EnqueueDelayedReply(TimeSpan.FromSeconds(10), "a reply");
        await server.SendAsync(Posted(roomA, "hold room a"), ct);
        await WaitUntilAsync(() => factory.Host.Sessions[0].Prompts.Count == 1, ct);

        await server.SendAsync(new StopTurn(roomA.Id), ct);
        await server.ReceiveUntilAsync<MessageDelta>(delta => delta.RoomId == roomA.Id && delta.IsFinal, ct);

        // Give a misbehaving report every chance to arrive anyway.
        await Task.Delay(200, ct);

        var seen = statuses.Snapshot();
        Assert.DoesNotContain(seen, status => status.State == PersonaState.Degraded);
        Assert.DoesNotContain(seen, status => status.State == PersonaState.Offline);
    }

    /// <summary>Stopping a Room that never had a session opened for it is a no-op, not a throw.</summary>
    [Fact]
    public async Task StopForRoomWithNoSession_NoOp()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var server = new FakeServer();
        var factory = new FakeAgentHostFactory { SessionPerRoom = true };
        var persona = new Persona("nova", "You are Nova.");
        var roomA = HumanRoom("room-a");

        await using var runner = CreateRunner(server, persona, factory);
        await server.HandshakeAsync(runner, [roomA], ct);

        await server.SendAsync(new StopTurn("room-never-seen"), ct);
        await Task.Delay(200, ct);
    }

    /// <summary>
    /// D22 correction 3 / D25 correction 3: the locked check-and-publish that guards
    /// <c>PromptAsync</c> runs after the Transcript read, not just after the open - a Stop that
    /// arrives while that read is still in flight must still drop the Turn. Built directly at the
    /// <see cref="RoomSession"/> level with a <see cref="FakeRoomSessionOwner"/> whose
    /// <see cref="FakeRoomSessionOwner.ReadTranscriptHandler"/> waits on a gate this test controls,
    /// since the pipe-backed harness above has no seam to hold a Transcript read open.
    /// </summary>
    [Fact]
    public async Task StopDuringTranscriptRead_NoPromptSent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReply("should never be prompted");
        FakeRoomSessionOwner owner = new();
        TaskCompletionSource readGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var readStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        owner.ReadTranscriptHandler = async (roomId, _, _, _, _) =>
        {
            readStarted.TrySetResult();
            await readGate.Task;
            return new TranscriptTail("req", roomId, [], Omitted: 0);
        };

        RecordingTurnScheduler scheduler = new();
        CancellationTokenSource runCts = new();
        RoomSession room = new(
            roomId: "room-1",
            open: _ => Task.FromResult<IAgentSession>(session),
            owner: owner,
            scheduler: scheduler,
            prompts: new FakePromptSource(),
            options: new AcpOptions(),
            fileChanges: null,
            declaredWatches: [],
            logger: NullLogger.Instance,
            runToken: runCts.Token);
        try
        {
            room.Enqueue(new QueuedWork(1, new WorkItem("room-1", "Room 1", "Human", "hi", [], TriggerMessageId: "trig-1")));

            await readStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);

            await room.StopAsync("room-1", mark: 1, ct);
            readGate.TrySetResult();

            // The dropped item never reaches a Turn at all - the locked check-and-publish returns
            // before the try/finally that would write a terminating MessageDelta - so the ticket's
            // own completion, not a written Envelope, is what proves the consumer finished with it.
            await WaitUntilAsync(() => scheduler.CompleteCount == 1, ct);

            Assert.Empty(session.Prompts);
            Assert.Empty(owner.Written.OfType<PostMessage>());
            Assert.Empty(owner.Written.OfType<MessageDelta>());
        }
        finally
        {
            await runCts.CancelAsync();
            await room.DisposeAsync();
            runCts.Dispose();
        }
    }

    private static RoomInfo HumanRoom(string id) =>
        new(id, "Direct", [new MemberInfo("human", "Human", UserKind.Human), new MemberInfo("agent-1", "nova", UserKind.Agent)]);

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

    private static PersonaRunner CreateRunner(FakeServer server, Persona persona, FakeAgentHostFactory factory, AcpOptions? acp = null)
    {
        var options = Options.Create(new TeamOptions { PipeName = server.PipeName, Acp = acp ?? new AcpOptions() });
        return new PersonaRunner(persona, options, factory, new FakePromptSource(), new RoomFollows(), NullLogger<PersonaRunner>.Instance);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken ct, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("Condition was not met before the test's timeout.");
            }

            await Task.Delay(10, ct);
        }
    }

    /// <summary>A hand-scripted pipe server giving a test full control over the Welcome a <see cref="PersonaRunner"/> receives, and over what a Stop reaches (RS §9 E-4's own harness, duplicated from <c>RoomSessionPoolTests</c> per this plan's per-file fake convention).</summary>
    private sealed class FakeServer : IAsyncDisposable
    {
        private readonly NamedPipeServerStream pipe;
        private readonly Channel<ProtocolMessage> received = Channel.CreateUnbounded<ProtocolMessage>();
        private readonly List<ProtocolMessage> receivedLog = [];
        private readonly Lock gate = new();

        private JsonLineStream? stream;
        private Task? pumpTask;

        /// <summary>Creates the server-side pipe instance, unconnected, under a unique per-test name.</summary>
        public FakeServer()
        {
            this.PipeName = "room-session-stop-tests-" + Guid.NewGuid().ToString("N");
            this.pipe = new NamedPipeServerStream(
                this.PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        }

        /// <summary>The named pipe this server listens on; hand it to the runner's <see cref="TeamOptions.PipeName"/>.</summary>
        public string PipeName { get; }

        /// <summary>Every envelope received so far, in arrival order - a snapshot, safe to enumerate while the pump keeps writing.</summary>
        public IReadOnlyList<ProtocolMessage> Received
        {
            get
            {
                lock (this.gate)
                {
                    return [.. this.receivedLog];
                }
            }
        }

        /// <summary>Starts <paramref name="runner"/>, accepts its connection, and answers the handshake with <paramref name="rooms"/>.</summary>
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
        public Task SendAsync(ProtocolMessage message, CancellationToken ct) => this.stream!.WriteAsync(message, ct);

        /// <summary>Reads received envelopes until one of type <typeparamref name="T"/> arrives, discarding everything else.</summary>
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

        /// <summary>Reads received envelopes until one of type <typeparamref name="T"/> also matching <paramref name="predicate"/> arrives, discarding everything else.</summary>
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

        /// <summary>Continuously reads from the pipe into <see cref="received"/> and <see cref="Received"/> until the stream ends or faults.</summary>
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
                        await this.stream.WriteAsync(new TranscriptTail(read.RequestId, read.RoomId, [], Omitted: 0), CancellationToken.None);
                        continue;
                    }

                    lock (this.gate)
                    {
                        this.receivedLog.Add(message);
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

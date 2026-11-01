using System.Threading.Channels;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.DotAcp;
using Agency.Huddle.Acp.Hosting;
using Agency.Huddle.Acp.Tests.Fakes;
using Xunit;

namespace Agency.Huddle.Acp.Tests.DotAcp;

/// <summary>
/// Drives the real wire-order barrier end to end (fake agent, <see cref="NdjsonMethodRewriteStream"/>,
/// <see cref="DotAcpClientAdapter"/>, <see cref="DotAcpAgentSession"/>) with the one thing a test
/// cannot otherwise force: a <c>session/update</c> that was read off the wire before the response but
/// is dispatched to the session only after it.
/// </summary>
public sealed class WireOrderBarrierTests
{
    /// <summary>
    /// Replaces the timing-dependent PromptAsync_ChunkDispatchedAfterResponse_StillPrecedesTurnCompleted.
    /// The agent writes a chunk and then the response, as the protocol requires; the sink holds the
    /// chunk's dispatch back. PromptAsync must not publish TurnCompleted until that dispatch finishes,
    /// and then the chunk must come first. No pass depends on elapsed time; only the "still held"
    /// check waits, and waiting longer can only make a broken barrier easier to catch.
    /// </summary>
    [Fact(Timeout = 20000)]
    public async Task PromptAsync_UpdateDispatchHeldBack_TurnCompletedWaitsForIt()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using Pipeline pipeline = await Pipeline.CreateAsync(cancellationToken);
        pipeline.Launcher.Agent.OnPrompt = async context =>
        {
            await context.SendTextChunkAsync("held").ConfigureAwait(false);
            return "end_turn";
        };

        Task<PromptResult> promptTask = pipeline.Session.PromptAsync(new AgentPrompt("hello"), cancellationToken);
        await pipeline.Sink.Entered.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        Task first = await Task.WhenAny(promptTask, Task.Delay(750, cancellationToken));
        bool completedEarly = ReferenceEquals(first, promptTask);
        bool turnCompletedEarly = pipeline.Session.Events.TryPeek(out AgentEvent? early) && early is TurnCompleted;

        pipeline.Sink.Release();
        PromptResult result = await promptTask;
        List<AgentEvent> events = await WireOrderBarrierTests.ReadAllAsync(pipeline.Session.Events, 2, cancellationToken);

        Assert.False(completedEarly);
        Assert.False(turnCompletedEarly);
        Assert.Equal(StopReason.EndTurn, result.StopReason);
        Assert.Equal(new AgentEvent[] { new MessageChunk("sess-1", "held"), new TurnCompleted("sess-1", StopReason.EndTurn) }, events);
    }

    /// <summary>Cancelling the Turn (Stop) skips the barrier at once even while a dispatch is held back.</summary>
    [Fact(Timeout = 20000)]
    public async Task PromptAsync_StopWhileDispatchHeldBack_DoesNotWaitForBarrier()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using Pipeline pipeline = await Pipeline.CreateAsync(cancellationToken);
        pipeline.Launcher.Agent.OnPrompt = async context =>
        {
            await context.SendTextChunkAsync("held").ConfigureAwait(false);
            return "end_turn";
        };
        using CancellationTokenSource stop = new();

        Task<PromptResult> promptTask = pipeline.Session.PromptAsync(new AgentPrompt("hello"), stop.Token);
        await pipeline.Sink.Entered.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        await stop.CancelAsync();
        Task first = await Task.WhenAny(promptTask, Task.Delay(TimeSpan.FromSeconds(5), cancellationToken));
        pipeline.Sink.Release();

        Assert.Same(promptTask, first);
    }

    /// <summary>Reads exactly <paramref name="count"/> events from <paramref name="reader"/> within ten seconds.</summary>
    /// <param name="reader">The session's event channel.</param>
    /// <param name="count">How many events to read.</param>
    /// <param name="cancellationToken">The test's token.</param>
    private static async Task<List<AgentEvent>> ReadAllAsync(ChannelReader<AgentEvent> reader, int count, CancellationToken cancellationToken)
    {
        List<AgentEvent> events = [];
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        while (events.Count < count)
        {
            events.Add(await reader.ReadAsync(linked.Token));
        }

        return events;
    }

    /// <summary>A sink that forwards to the real session but parks the first update's dispatch until released.</summary>
    /// <param name="session">The session the sink forwards to.</param>
    private sealed class HoldingSink(DotAcpAgentSession session) : ISessionSink, IDisposable
    {
        private readonly ManualResetEventSlim released = new(false);

        private readonly TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int held;

        /// <summary>Gets a task that completes when the first update's dispatch has parked.</summary>
        internal Task Entered => this.entered.Task;

        /// <inheritdoc/>
        public string SessionId => session.SessionId;

        /// <inheritdoc/>
        public IPermissionHandler PermissionHandler => session.PermissionHandler;

        /// <inheritdoc/>
        public CancellationToken PromptCancellation => session.PromptCancellation;

        /// <inheritdoc/>
        public IElicitationScope? ElicitationScope => session.ElicitationScope;

        /// <inheritdoc/>
        public void Dispose()
        {
            this.released.Dispose();
        }

        /// <summary>Lets the parked dispatch continue.</summary>
        internal void Release()
        {
            this.released.Set();
        }

        /// <inheritdoc/>
        public bool TryPublish(AgentEvent agentEvent)
        {
            if (Interlocked.Increment(ref this.held) == 1)
            {
                this.entered.TrySetResult();
                _ = this.released.Wait(TimeSpan.FromSeconds(15), CancellationToken.None);
            }

            return session.TryPublish(agentEvent);
        }

        /// <inheritdoc/>
        public void Fault(Exception exception)
        {
            session.Fault(exception);
        }
    }

    /// <summary>The wire, stream, adapter, connection and session, assembled the way the host does but with the sink held.</summary>
    private sealed class Pipeline : IAsyncDisposable
    {
        private readonly dotacp.client.Connection connection;

        private readonly NdjsonMethodRewriteStream agentOutput;

        private Pipeline(FakeAgentProcessLauncher launcher, dotacp.client.Connection connection, NdjsonMethodRewriteStream agentOutput, DotAcpAgentSession session, HoldingSink sink)
        {
            this.Launcher = launcher;
            this.connection = connection;
            this.agentOutput = agentOutput;
            this.Session = session;
            this.Sink = sink;
        }

        /// <summary>Gets the launcher that owns the fake agent.</summary>
        internal FakeAgentProcessLauncher Launcher { get; }

        /// <summary>Gets the real session under test.</summary>
        internal DotAcpAgentSession Session { get; }

        /// <summary>Gets the sink that holds the first dispatch back.</summary>
        internal HoldingSink Sink { get; }

        /// <summary>Builds and initialises the pipeline and registers the holding sink for the new session.</summary>
        /// <param name="cancellationToken">The test's token.</param>
        internal static async Task<Pipeline> CreateAsync(CancellationToken cancellationToken)
        {
            FakeAgentProcessLauncher launcher = new();
            IAgentProcess process = launcher.Launch(new AgentProcessOptions("fake", []));
            WireOrderLedger ledger = new();
            NdjsonMethodRewriteStream agentOutput = new(process.StandardOutput, ledger, rewriteElicitation: false);
            DotAcpClientAdapter adapter = new(new ListLogger<DotAcpClientAdapter>(), ledger);
            dotacp.client.Connection connection = dotacp.client.Connection.RunClient(adapter, process.StandardInput, agentOutput, null)
                ?? throw new InvalidOperationException("Failed to create connection.");
            _ = await connection.InitializeAsync(
                new dotacp.protocol.InitializeRequest
                {
                    ProtocolVersion = dotacp.protocol.ProtocolMeta.Version,
                    ClientCapabilities = new dotacp.protocol.ClientCapabilities(),
                    ClientInfo = new dotacp.protocol.Implementation { Name = "test", Version = "0" },
                },
                cancellationToken);
            dotacp.protocol.NewSessionResponse created = await connection.NewSessionAsync(
                new dotacp.protocol.NewSessionRequest { Cwd = Path.GetTempPath(), McpServers = [] },
                cancellationToken);

#pragma warning disable HAA0601 // The protocol id is a struct with an implicit conversion to string: a method call, not a boxing conversion.
            string sessionId = (string)created.SessionId;
#pragma warning restore HAA0601
            DotAcpAgentSession session = new(
                sessionId,
                connection,
                new RecordingPermissionHandler(),
                id => adapter.Unregister(id),
                new ListLogger<DotAcpAgentHost>(),
                [],
                ledger);
            HoldingSink sink = new(session);
            adapter.Register(sink);
            return new Pipeline(launcher, connection, agentOutput, session, sink);
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            this.Sink.Release();
            await this.Session.DisposeAsync();
            this.connection.Dispose();
            await this.agentOutput.DisposeAsync();
        }
    }
}

using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.DotAcp;
using Agency.Huddle.Acp.Hosting;
using Agency.Huddle.Acp.Tests.Fakes;
using Xunit;

namespace Agency.Huddle.Acp.Tests.DotAcp;

/// <summary>
/// Covers the whole path of an agent's <c>elicitation/create</c> through a real
/// <see cref="DotAcpAgentHost"/> over <see cref="FakeAcpAgent"/> (Elicitation E-1, E-2): the rewrite,
/// dotacp's routing as an extension method, the adapter, the session's bound scope, and the JSON that
/// goes back on the wire.
/// </summary>
public sealed class DotAcpAgentHostElicitationHandlingTests
{
    private const int InternalError = -32000;

    /// <summary>Gets the answers that carry no content, with the exact reply text each must produce.</summary>
    public static TheoryData<string, string> ContentFreeAnswers()
    {
        return new TheoryData<string, string>
        {
            { "Declined", """{"action":"decline"}""" },
            { "Cancelled", """{"action":"cancel"}""" },
        };
    }

    /// <summary>A session with no scope bound is answered <c>{"action":"cancel"}</c> on the wire: the agent is never left waiting and never gets an error.</summary>
    [Fact(Timeout = 10000)]
    public async Task Elicitation_NoScope_RepliesCancel()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using HostFixture fixture = await HostFixture.StartAsync(cancellationToken);
        IAgentSession session = await fixture.StartSessionAsync(scope: null, cancellationToken);
        Task<JsonObject> answered = fixture.ScriptElicitation(DotAcpAgentHostElicitationHandlingTests.EmptySchema());

        _ = await session.PromptAsync("go", cancellationToken);

        JsonObject reply = await answered.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        Assert.Equal("""{"action":"cancel"}""", reply.ToJsonString());
    }

    /// <summary>A request naming a session the host never opened is answered <c>{"action":"cancel"}</c>, not an error.</summary>
    [Fact(Timeout = 10000)]
    public async Task Elicitation_UnknownSession_RepliesCancel()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using HostFixture fixture = await HostFixture.StartAsync(cancellationToken);
        RecordingElicitationScope scope = new();
        _ = await fixture.StartSessionAsync(scope, cancellationToken);

        JsonObject reply = await fixture.Launcher.Agent.SendRequestAsync(
            "elicitation/create", DotAcpAgentHostElicitationHandlingTests.Params("ghost-session")).WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

        Assert.Equal("""{"action":"cancel"}""", reply.ToJsonString());
        Assert.Empty(scope.Requests);
    }

    /// <summary>With two sessions open, a request reaches only the scope bound to the session it names.</summary>
    [Fact(Timeout = 10000)]
    public async Task Elicitation_TwoSessions_AreRoutedToTheirOwnScope()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using HostFixture fixture = await HostFixture.StartAsync(cancellationToken);
        RecordingElicitationScope firstScope = new()
        {
            Script = RecordingElicitationScope.Answer(new ElicitationAccepted(new Dictionary<string, object>(StringComparer.Ordinal) { ["who"] = "first" })),
        };
        RecordingElicitationScope secondScope = new()
        {
            Script = RecordingElicitationScope.Answer(new ElicitationAccepted(new Dictionary<string, object>(StringComparer.Ordinal) { ["who"] = "second" })),
        };
        IAgentSession first = await fixture.StartSessionAsync(firstScope, cancellationToken);
        IAgentSession second = await fixture.StartSessionAsync(secondScope, cancellationToken);

        JsonObject secondReply = await fixture.Launcher.Agent.SendRequestAsync(
            "elicitation/create", DotAcpAgentHostElicitationHandlingTests.Params(second.SessionId)).WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        JsonObject firstReply = await fixture.Launcher.Agent.SendRequestAsync(
            "elicitation/create", DotAcpAgentHostElicitationHandlingTests.Params(first.SessionId)).WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

        Assert.Equal("""{"action":"accept","content":{"who":"second"}}""", secondReply.ToJsonString());
        Assert.Equal("""{"action":"accept","content":{"who":"first"}}""", firstReply.ToJsonString());
        Assert.Equal(second.SessionId, Assert.Single(secondScope.Requests).SessionId);
        Assert.Equal(first.SessionId, Assert.Single(firstScope.Requests).SessionId);
    }

    /// <summary>The scope sees the request's session id, tool call id, message and schema as the agent sent them.</summary>
    [Fact(Timeout = 10000)]
    public async Task Elicitation_RequestFields_ReachTheScope()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using HostFixture fixture = await HostFixture.StartAsync(cancellationToken);
        RecordingElicitationScope scope = new();
        IAgentSession session = await fixture.StartSessionAsync(scope, cancellationToken);
        JsonObject schema = DotAcpAgentHostElicitationHandlingTests.EmptySchema();
        Task<JsonObject> answered = fixture.ScriptElicitation(schema, "toolu_1");

        _ = await session.PromptAsync("go", cancellationToken);
        _ = await answered.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

        ElicitationRequest request = Assert.Single(scope.Requests);
        Assert.Equal(new ElicitationRequest(session.SessionId, "toolu_1", "Pick one", """{"type":"object","properties":{}}"""), request);
    }

    /// <summary>The schema reaches the scope as one compact JSON string equal to what the agent sent, with nested objects, arrays, numbers and booleans intact.</summary>
    [Fact(Timeout = 10000)]
    public async Task Elicitation_RequestedSchema_ArrivesAsJsonString()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using HostFixture fixture = await HostFixture.StartAsync(cancellationToken);
        RecordingElicitationScope scope = new();
        IAgentSession session = await fixture.StartSessionAsync(scope, cancellationToken);
        JsonObject schema = new()
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["a"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1, ["maximum"] = 10 },
                ["b"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = new JsonObject
                    {
                        ["anyOf"] = new JsonArray(new JsonObject { ["const"] = "p", ["title"] = "P" }, new JsonObject { ["const"] = "q" }),
                    },
                },
                ["c"] = new JsonObject { ["type"] = "boolean", ["default"] = true },
            },
            ["required"] = new JsonArray("a"),
        };
        Task<JsonObject> answered = fixture.ScriptElicitation(schema);

        _ = await session.PromptAsync("go", cancellationToken);
        _ = await answered.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

        string received = Assert.Single(scope.Requests).RequestedSchemaJson;
        Assert.Equal(schema.ToJsonString(), received);
    }

    /// <summary>An accepted answer goes out as <c>{"action":"accept","content":{...}}</c> with numbers and booleans as JSON numbers and booleans (never strings) and a string list as a JSON array.</summary>
    [Fact(Timeout = 10000)]
    public async Task Elicitation_ResultOnWire_IsActionAndTypedContent()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using HostFixture fixture = await HostFixture.StartAsync(cancellationToken);
        Dictionary<string, object> content = new(StringComparer.Ordinal)
        {
            ["n"] = 3L,
            ["b"] = true,
            ["q"] = new[] { "a" },
            ["s"] = "x",
        };
        RecordingElicitationScope scope = new() { Script = RecordingElicitationScope.Answer(new ElicitationAccepted(content)) };
        IAgentSession session = await fixture.StartSessionAsync(scope, cancellationToken);
        Task<JsonObject> answered = fixture.ScriptElicitation(DotAcpAgentHostElicitationHandlingTests.EmptySchema());

        _ = await session.PromptAsync("go", cancellationToken);

        JsonObject reply = await answered.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        Assert.Equal("accept", (string?)reply["action"]);
        Assert.Equal("""{"n":3,"b":true,"q":["a"],"s":"x"}""", reply["content"]?.ToJsonString());
    }

    /// <summary>Negative whole numbers, large whole numbers and fractions stay JSON numbers on the wire.</summary>
    [Fact(Timeout = 10000)]
    public async Task Elicitation_NumberKinds_StayNumbersOnWire()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using HostFixture fixture = await HostFixture.StartAsync(cancellationToken);
        Dictionary<string, object> content = new(StringComparer.Ordinal)
        {
            ["i"] = -7L,
            ["d"] = 2.5,
            ["big"] = 9007199254740993L,
        };
        RecordingElicitationScope scope = new() { Script = RecordingElicitationScope.Answer(new ElicitationAccepted(content)) };
        IAgentSession session = await fixture.StartSessionAsync(scope, cancellationToken);
        Task<JsonObject> answered = fixture.ScriptElicitation(DotAcpAgentHostElicitationHandlingTests.EmptySchema());

        _ = await session.PromptAsync("go", cancellationToken);

        JsonObject reply = await answered.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        Assert.Equal("""{"i":-7,"d":2.5,"big":9007199254740993}""", reply["content"]?.ToJsonString());
    }

    /// <summary>Content keys go out exactly as the scope wrote them: upper-case and underscore keys are not renamed (a server validates its own property names).</summary>
    [Fact(Timeout = 10000)]
    public async Task Elicitation_ContentKeys_AreSentVerbatim()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using HostFixture fixture = await HostFixture.StartAsync(cancellationToken);
        Dictionary<string, object> content = new(StringComparer.Ordinal)
        {
            ["UserName"] = "ada",
            ["question_0_custom"] = "other",
            ["Mixed_Case"] = true,
        };
        RecordingElicitationScope scope = new() { Script = RecordingElicitationScope.Answer(new ElicitationAccepted(content)) };
        IAgentSession session = await fixture.StartSessionAsync(scope, cancellationToken);
        Task<JsonObject> answered = fixture.ScriptElicitation(DotAcpAgentHostElicitationHandlingTests.EmptySchema());

        _ = await session.PromptAsync("go", cancellationToken);

        JsonObject reply = await answered.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        Assert.Equal("""{"UserName":"ada","question_0_custom":"other","Mixed_Case":true}""", reply["content"]?.ToJsonString());
    }

    /// <summary>A declined or cancelled answer goes out as the action alone: the reply has no <c>content</c> key.</summary>
    /// <param name="kind">Which answer the scope gives.</param>
    /// <param name="expectedReply">The exact reply text.</param>
    [Theory(Timeout = 10000)]
    [MemberData(nameof(ContentFreeAnswers))]
    public async Task Elicitation_Decline_And_Cancel_OmitContent(string kind, string expectedReply)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using HostFixture fixture = await HostFixture.StartAsync(cancellationToken);
        ElicitationResult answer = kind switch
        {
            "Declined" => new ElicitationDeclined(),
            "Cancelled" => new ElicitationCancelled(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown answer kind."),
        };
        RecordingElicitationScope scope = new() { Script = RecordingElicitationScope.Answer(answer) };
        IAgentSession session = await fixture.StartSessionAsync(scope, cancellationToken);
        Task<JsonObject> answered = fixture.ScriptElicitation(DotAcpAgentHostElicitationHandlingTests.EmptySchema());

        _ = await session.PromptAsync("go", cancellationToken);

        JsonObject reply = await answered.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        Assert.Equal(expectedReply, reply.ToJsonString());
    }

    /// <summary>A scope that ends in <see cref="OperationCanceledException"/> is answered <c>{"action":"cancel"}</c> on the wire, not as a JSON-RPC error.</summary>
    [Fact(Timeout = 10000)]
    public async Task Elicitation_ScopeThrowsOce_RepliesCancel()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using HostFixture fixture = await HostFixture.StartAsync(cancellationToken);
        RecordingElicitationScope scope = new() { Script = RecordingElicitationScope.Fault(new OperationCanceledException()) };
        IAgentSession session = await fixture.StartSessionAsync(scope, cancellationToken);
        Task<JsonObject> answered = fixture.ScriptElicitation(DotAcpAgentHostElicitationHandlingTests.EmptySchema());

        _ = await session.PromptAsync("go", cancellationToken);

        JsonObject reply = await answered.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        Assert.Equal("""{"action":"cancel"}""", reply.ToJsonString());
    }

    /// <summary>A scope that throws anything else makes the request fail with JSON-RPC error -32000, which the agent reports as a form it could not present.</summary>
    [Fact(Timeout = 10000)]
    public async Task Elicitation_ScopeThrowsOther_ReturnsRpcError()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using HostFixture fixture = await HostFixture.StartAsync(cancellationToken);
        RecordingElicitationScope scope = new() { Script = RecordingElicitationScope.Fault(new InvalidOperationException("boom")) };
        IAgentSession session = await fixture.StartSessionAsync(scope, cancellationToken);
        Task<JsonObject> answered = fixture.ScriptElicitation(DotAcpAgentHostElicitationHandlingTests.EmptySchema());

        _ = await session.PromptAsync("go", cancellationToken);

        FakeRpcError error = await Assert.ThrowsAsync<FakeRpcError>(() => answered.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken));
        Assert.Equal(DotAcpAgentHostElicitationHandlingTests.InternalError, error.Code);
    }

    /// <summary>While a request waits for the Human, the same session's <c>session/update</c> notifications keep arriving: the handler does not hold up the read loop.</summary>
    [Fact(Timeout = 10000)]
    public async Task Elicitation_Pending_DoesNotBlockSessionUpdates()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using HostFixture fixture = await HostFixture.StartAsync(cancellationToken);
        TaskCompletionSource<ElicitationResult> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        RecordingElicitationScope scope = new() { Script = RecordingElicitationScope.WaitFor(release) };
        IAgentSession session = await fixture.StartSessionAsync(scope, cancellationToken);
        fixture.Launcher.Agent.OnPrompt = async context =>
        {
            Task<JsonObject> pending = context.RequestElicitationAsync("Pick one", DotAcpAgentHostElicitationHandlingTests.EmptySchema());
            await context.SendTextChunkAsync("still-flowing").ConfigureAwait(false);
            _ = await pending.ConfigureAwait(false);
            return "end_turn";
        };

        Task<PromptResult> prompt = session.PromptAsync("go", cancellationToken);
        MessageChunk chunk = await DotAcpAgentHostElicitationHandlingTests.ReadNextChunkAsync(session, cancellationToken);

        Assert.Equal("still-flowing", chunk.Text);
        Assert.False(release.Task.IsCompleted);
        Assert.Single(scope.Requests);

        release.SetResult(new ElicitationCancelled());
        _ = await prompt.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
    }

    /// <summary>When the agent process goes away (the peer closes), the token handed to the open request is cancelled.</summary>
    [Fact(Timeout = 15000)]
    public async Task Elicitation_PeerClose_CancelsScopeToken()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using HostFixture fixture = await HostFixture.StartAsync(cancellationToken);
        RecordingElicitationScope scope = new() { Script = RecordingElicitationScope.BlockUntilCancelled };
        IAgentSession session = await fixture.StartSessionAsync(scope, cancellationToken);
        _ = fixture.ScriptElicitation(DotAcpAgentHostElicitationHandlingTests.EmptySchema());
        Task<PromptResult> prompt = session.PromptAsync("go", cancellationToken);
        CancellationToken scopeToken = await scope.FirstEntered.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        Assert.False(scopeToken.IsCancellationRequested);

        fixture.Launcher.Agent.CloseOutput();

        await RecordingElicitationScope.WhenCancelled(scopeToken).WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        Assert.True(scopeToken.IsCancellationRequested);
        _ = await Record.ExceptionAsync(() => prompt.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken));
    }

    /// <summary>Disposing the session cancels the token handed to that session's open request.</summary>
    [Fact(Timeout = 15000)]
    public async Task Elicitation_SessionDispose_CancelsItsRequests()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using HostFixture fixture = await HostFixture.StartAsync(cancellationToken);
        RecordingElicitationScope scope = new() { Script = RecordingElicitationScope.BlockUntilCancelled };
        IAgentSession session = await fixture.StartSessionAsync(scope, cancellationToken);
        Task<JsonObject> answered = fixture.ScriptElicitation(DotAcpAgentHostElicitationHandlingTests.EmptySchema());
        Task<PromptResult> prompt = session.PromptAsync("go", cancellationToken);
        CancellationToken scopeToken = await scope.FirstEntered.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        Assert.False(scopeToken.IsCancellationRequested);

        await session.DisposeAsync();

        await RecordingElicitationScope.WhenCancelled(scopeToken).WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        JsonObject reply = await answered.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        Assert.Equal("""{"action":"cancel"}""", reply.ToJsonString());
        _ = await prompt.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
    }

    /// <summary>Cancelling the session's Turn (a Stop) cancels the token handed to the open request, and the agent gets <c>{"action":"cancel"}</c>.</summary>
    [Fact(Timeout = 15000)]
    public async Task Elicitation_PromptCancellation_CancelsScopeToken()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using HostFixture fixture = await HostFixture.StartAsync(cancellationToken);
        RecordingElicitationScope scope = new() { Script = RecordingElicitationScope.BlockUntilCancelled };
        IAgentSession session = await fixture.StartSessionAsync(scope, cancellationToken);
        Task<JsonObject> answered = fixture.ScriptElicitation(DotAcpAgentHostElicitationHandlingTests.EmptySchema());
        Task<PromptResult> prompt = session.PromptAsync("go", cancellationToken);
        CancellationToken scopeToken = await scope.FirstEntered.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        Assert.False(scopeToken.IsCancellationRequested);

        await session.CancelAsync(cancellationToken);

        await RecordingElicitationScope.WhenCancelled(scopeToken).WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        JsonObject reply = await answered.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        Assert.Equal("""{"action":"cancel"}""", reply.ToJsonString());
        _ = await prompt.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
    }

    /// <summary>Binding a scope stores it on the session's sink, where the adapter finds it; before binding there is none, and a second binding replaces the first.</summary>
    [Fact(Timeout = 10000)]
    public async Task BindElicitationScope_StoresTheScopeOnTheSink_AndRebindingReplacesIt()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using HostFixture fixture = await HostFixture.StartAsync(cancellationToken);
        RecordingElicitationScope first = new();
        RecordingElicitationScope second = new();
        IAgentSession session = await fixture.StartSessionAsync(scope: null, cancellationToken);
        DotAcpAgentSession sink = Assert.IsType<DotAcpAgentSession>(session);
        Assert.Null(sink.ElicitationScope);

        session.BindElicitationScope(first);
        Assert.Same(first, sink.ElicitationScope);

        session.BindElicitationScope(second);
        Assert.Same(second, sink.ElicitationScope);
    }

    /// <summary>A session type that does not know about elicitation accepts a scope and does nothing with it: the default interface member is a no-op, not a failure.</summary>
    [Fact]
    public void BindElicitationScope_DefaultImplementation_DoesNothing()
    {
        IAgentSession session = new FakeAgentSession();
        RecordingElicitationScope scope = new();

        Exception? exception = Record.Exception(() => session.BindElicitationScope(scope));

        Assert.Null(exception);
        Assert.Empty(scope.Requests);
    }

    /// <summary>Builds the <c>elicitation/create</c> parameters the adapter sends for an empty form.</summary>
    /// <param name="sessionId">The session the request names.</param>
    private static JsonObject Params(string sessionId)
    {
        return new JsonObject
        {
            ["mode"] = "form",
            ["sessionId"] = sessionId,
            ["message"] = "Pick one",
            ["requestedSchema"] = DotAcpAgentHostElicitationHandlingTests.EmptySchema(),
        };
    }

    /// <summary>Builds a schema with no properties.</summary>
    private static JsonObject EmptySchema()
    {
        return new JsonObject { ["type"] = "object", ["properties"] = new JsonObject() };
    }

    /// <summary>Reads the session's events until the next <see cref="MessageChunk"/> and returns it.</summary>
    /// <param name="session">The session to read.</param>
    /// <param name="cancellationToken">The test's token.</param>
    private static async Task<MessageChunk> ReadNextChunkAsync(IAgentSession session, CancellationToken cancellationToken)
    {
        while (true)
        {
            AgentEvent next = await session.Events.ReadAsync(cancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            if (next is MessageChunk chunk)
            {
                return chunk;
            }
        }
    }

    /// <summary>A started <see cref="DotAcpAgentHost"/> (advertising elicitation) over a <see cref="FakeAgentProcessLauncher"/>.</summary>
    private sealed class HostFixture : IAsyncDisposable
    {
        private readonly DotAcpAgentHost host;

        private HostFixture(FakeAgentProcessLauncher launcher, DotAcpAgentHost host)
        {
            this.Launcher = launcher;
            this.host = host;
        }

        /// <summary>Gets the launcher, and through it the fake agent.</summary>
        internal FakeAgentProcessLauncher Launcher { get; }

        /// <summary>Starts a host that advertises <c>elicitation.form</c>.</summary>
        /// <param name="cancellationToken">The test's token.</param>
        internal static async Task<HostFixture> StartAsync(CancellationToken cancellationToken)
        {
            FakeAgentProcessLauncher launcher = new();
            DotAcpAgentHost host = new(
                new AgentProcessOptions("fake", []),
                launcher,
                new DotAcpHostOptions(AdvertiseElicitationForm: true),
                new ListLoggerFactory());
            await host.StartAsync(cancellationToken);
            return new HostFixture(launcher, host);
        }

        /// <summary>Opens a session and binds <paramref name="scope"/> to it when one is given.</summary>
        /// <param name="scope">The scope to bind, or null to leave the session without one.</param>
        /// <param name="cancellationToken">The test's token.</param>
        internal async Task<IAgentSession> StartSessionAsync(IElicitationScope? scope, CancellationToken cancellationToken)
        {
            IAgentSession session = await this.host.StartSessionAsync(
                new AgentSessionOptions(Path.GetTempPath(), new RecordingPermissionHandler()),
                cancellationToken);

            if (scope is not null)
            {
                session.BindElicitationScope(scope);
            }

            return session;
        }

        /// <summary>
        /// Scripts the next prompt to send one <c>elicitation/create</c> request through the fake's
        /// helper and finish the Turn once it is answered.
        /// </summary>
        /// <param name="schema">The requested schema.</param>
        /// <param name="toolCallId">The tool call id, or null to send none.</param>
        /// <returns>A task that completes with the whole reply, or faults with the JSON-RPC error.</returns>
        internal Task<JsonObject> ScriptElicitation(JsonObject schema, string? toolCallId = null)
        {
            TaskCompletionSource<JsonObject> answered = new(TaskCreationOptions.RunContinuationsAsynchronously);
            this.Launcher.Agent.OnPrompt = async context =>
            {
                try
                {
                    JsonObject reply = await context.RequestElicitationAsync("Pick one", schema, toolCallId).ConfigureAwait(false);
                    _ = answered.TrySetResult(reply);
                }
                catch (Exception ex) when (ex is FakeRpcError or InvalidOperationException)
                {
                    _ = answered.TrySetException(ex);
                }

                return "end_turn";
            };

            return answered.Task;
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            await this.host.DisposeAsync();
        }
    }
}

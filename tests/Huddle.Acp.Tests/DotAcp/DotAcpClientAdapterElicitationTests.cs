using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.DotAcp;
using Agency.Huddle.Acp.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Nerdbank.Streams;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Agency.Huddle.Acp.Tests.DotAcp;

/// <summary>
/// Covers how <see cref="DotAcpClientAdapter"/> answers the extension method <c>elicitation/create</c>
/// (Elicitation E-2): it finds the session's <see cref="IElicitationScope"/>, hands it the request
/// with the schema as one JSON string, maps the answer to the wire dictionary, and ties every open
/// request to the token sources that must end it. The adapter is driven directly, with the argument
/// shapes dotacp hands it (a dictionary of Newtonsoft tokens).
/// </summary>
public sealed class DotAcpClientAdapterElicitationTests
{
    private const string SessionId = "sess-1";

    private const string Method = "elicitation/create";

    private const string EmptySchema = """{"type":"object","properties":{}}""";

    /// <summary>Gets the argument shapes that carry no usable session id, by case name.</summary>
    public static TheoryData<string> MalformedCases()
    {
        return new TheoryData<string>
        {
            "NotADictionary",
            "NoSessionId",
            "SessionIdNotAString",
        };
    }

    /// <summary>Gets the extension method names that are not <c>elicitation/create</c>.</summary>
    public static TheoryData<string> OtherMethods()
    {
        return new TheoryData<string>
        {
            "_ext/foo",
            "elicitation/complete",
            "_elicitation/create",
        };
    }

    /// <summary>A session with no scope bound is answered <c>cancel</c> rather than failing: nothing may leave the agent waiting, and nothing can show the Human a form.</summary>
    [Fact(Timeout = 10000)]
    public async Task Elicitation_NoScope_RepliesCancel()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Fixture fixture = DotAcpClientAdapterElicitationTests.CreateFixture(scope: null);

        Dictionary<string, object> reply = await DotAcpClientAdapterElicitationTests.ElicitAsync(
            fixture, DotAcpClientAdapterElicitationTests.Args(DotAcpClientAdapterElicitationTests.SessionId), cancellationToken);

        DotAcpClientAdapterElicitationTests.AssertAction("cancel", reply);
        Assert.Contains(fixture.Logger.Entries, entry => entry.Level == LogLevel.Warning);
    }

    /// <summary>A request for a session the adapter does not know is answered <c>cancel</c> and never reaches any other session's scope.</summary>
    [Fact(Timeout = 10000)]
    public async Task Elicitation_UnknownSession_RepliesCancel()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        RecordingElicitationScope scope = new();
        Fixture fixture = DotAcpClientAdapterElicitationTests.CreateFixture(scope);

        Dictionary<string, object> reply = await DotAcpClientAdapterElicitationTests.ElicitAsync(
            fixture, DotAcpClientAdapterElicitationTests.Args("unknown-session"), cancellationToken);

        DotAcpClientAdapterElicitationTests.AssertAction("cancel", reply);
        Assert.Empty(scope.Requests);
        Assert.Contains(fixture.Logger.Entries, entry => entry.Level == LogLevel.Warning);
    }

    /// <summary>A request that arrives after its session was unregistered is answered <c>cancel</c> and the scope is not called.</summary>
    [Fact(Timeout = 10000)]
    public async Task Elicitation_AfterUnregister_RepliesCancel()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        RecordingElicitationScope scope = new();
        Fixture fixture = DotAcpClientAdapterElicitationTests.CreateFixture(scope);
        _ = fixture.Adapter.Unregister(DotAcpClientAdapterElicitationTests.SessionId);

        Dictionary<string, object> reply = await DotAcpClientAdapterElicitationTests.ElicitAsync(
            fixture, DotAcpClientAdapterElicitationTests.Args(DotAcpClientAdapterElicitationTests.SessionId), cancellationToken);

        DotAcpClientAdapterElicitationTests.AssertAction("cancel", reply);
        Assert.Empty(scope.Requests);
    }

    /// <summary>Arguments without a string session id (not a dictionary, no id, an id of the wrong type) are answered <c>cancel</c> and reach no scope.</summary>
    /// <param name="caseName">Which malformed shape to send.</param>
    [Theory(Timeout = 10000)]
    [MemberData(nameof(MalformedCases))]
    public async Task Elicitation_MalformedArguments_RepliesCancel(string caseName)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        object arguments = caseName switch
        {
            "NotADictionary" => new JObject(),
            "NoSessionId" => new Dictionary<string, object> { ["message"] = new JValue("hi") },
            "SessionIdNotAString" => new Dictionary<string, object> { ["sessionId"] = new JValue(7) },
            _ => throw new ArgumentOutOfRangeException(nameof(caseName), caseName, "Unknown malformed case."),
        };
        RecordingElicitationScope scope = new();
        Fixture fixture = DotAcpClientAdapterElicitationTests.CreateFixture(scope);

        object result = await fixture.Adapter.ExtMethodAsync(DotAcpClientAdapterElicitationTests.Method, arguments, cancellationToken);

        DotAcpClientAdapterElicitationTests.AssertAction("cancel", Assert.IsType<Dictionary<string, object>>(result));
        Assert.Empty(scope.Requests);
        Assert.Contains(fixture.Logger.Entries, entry => entry.Level == LogLevel.Warning);
    }

    /// <summary>The scope receives the session id, tool call id, message and schema exactly as the agent sent them.</summary>
    [Fact(Timeout = 10000)]
    public async Task Elicitation_Arguments_ReachTheScope()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        RecordingElicitationScope scope = new();
        Fixture fixture = DotAcpClientAdapterElicitationTests.CreateFixture(scope);
        const string schema = """{"type":"object","properties":{"question_0":{"type":"string","oneOf":[{"const":"A","title":"A"}]}}}""";

        _ = await DotAcpClientAdapterElicitationTests.ElicitAsync(
            fixture,
            DotAcpClientAdapterElicitationTests.Args(DotAcpClientAdapterElicitationTests.SessionId, "toolu_1", "Which database?", schema),
            cancellationToken);

        ElicitationRequest request = Assert.Single(scope.Requests);
        Assert.Equal(new ElicitationRequest(DotAcpClientAdapterElicitationTests.SessionId, "toolu_1", "Which database?", schema), request);
    }

    /// <summary>With only a session id the scope still gets a request: no tool call id, an empty message and the empty schema object.</summary>
    [Fact(Timeout = 10000)]
    public async Task Elicitation_MinimalArguments_ArriveWithDefaults()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        RecordingElicitationScope scope = new();
        Fixture fixture = DotAcpClientAdapterElicitationTests.CreateFixture(scope);
        Dictionary<string, object> arguments = new(StringComparer.Ordinal)
        {
            ["sessionId"] = new JValue(DotAcpClientAdapterElicitationTests.SessionId),
        };

        _ = await DotAcpClientAdapterElicitationTests.ElicitAsync(fixture, arguments, cancellationToken);

        ElicitationRequest request = Assert.Single(scope.Requests);
        Assert.Equal(new ElicitationRequest(DotAcpClientAdapterElicitationTests.SessionId, null, string.Empty, "{}"), request);
    }

    /// <summary>The schema arrives as one compact JSON string with every nested object, array, number and boolean intact, not as a token tree and not pretty-printed.</summary>
    [Fact(Timeout = 10000)]
    public async Task Elicitation_RequestedSchema_ArrivesAsJsonString()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        RecordingElicitationScope scope = new();
        Fixture fixture = DotAcpClientAdapterElicitationTests.CreateFixture(scope);
        const string schema = """{"type":"object","properties":{"a":{"type":"integer","minimum":1,"maximum":10},"b":{"type":"array","items":{"anyOf":[{"const":"p","title":"P"},{"const":"q"}]}},"c":{"type":"boolean","default":true}},"required":["a"]}""";

        _ = await DotAcpClientAdapterElicitationTests.ElicitAsync(
            fixture,
            DotAcpClientAdapterElicitationTests.Args(DotAcpClientAdapterElicitationTests.SessionId, schemaJson: schema),
            cancellationToken);

        Assert.Equal(schema, Assert.Single(scope.Requests).RequestedSchemaJson);
    }

    /// <summary>An accepted answer becomes <c>action: accept</c> plus the content with its values still typed: a long stays a long, a bool a bool, a list of strings a string array, a double a double.</summary>
    [Fact(Timeout = 10000)]
    public async Task Elicitation_Accepted_ReturnsActionAcceptAndTypedContent()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Dictionary<string, object> content = new(StringComparer.Ordinal)
        {
            ["n"] = 3L,
            ["b"] = true,
            ["q"] = new[] { "a" },
            ["s"] = "x",
            ["d"] = 2.5,
        };
        RecordingElicitationScope scope = new() { Script = RecordingElicitationScope.Answer(new ElicitationAccepted(content)) };
        Fixture fixture = DotAcpClientAdapterElicitationTests.CreateFixture(scope);

        Dictionary<string, object> reply = await DotAcpClientAdapterElicitationTests.ElicitAsync(
            fixture, DotAcpClientAdapterElicitationTests.Args(DotAcpClientAdapterElicitationTests.SessionId), cancellationToken);

        Assert.Equal(2, reply.Count);
        Assert.Equal("accept", Assert.IsType<string>(reply["action"]));
        Dictionary<string, object> returned = Assert.IsType<Dictionary<string, object>>(reply["content"]);
        Assert.Equal(5, returned.Count);
        Assert.Equal(3L, Assert.IsType<long>(returned["n"]));
        Assert.True(Assert.IsType<bool>(returned["b"]));
        Assert.Equal(["a"], Assert.IsType<string[]>(returned["q"]));
        Assert.Equal("x", Assert.IsType<string>(returned["s"]));
        Assert.Equal(2.5, Assert.IsType<double>(returned["d"]));
    }

    /// <summary>A declined answer becomes exactly <c>action: decline</c>, with no content key.</summary>
    [Fact(Timeout = 10000)]
    public async Task Elicitation_Declined_RepliesDeclineWithoutContent()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        RecordingElicitationScope scope = new() { Script = RecordingElicitationScope.Answer(new ElicitationDeclined()) };
        Fixture fixture = DotAcpClientAdapterElicitationTests.CreateFixture(scope);

        Dictionary<string, object> reply = await DotAcpClientAdapterElicitationTests.ElicitAsync(
            fixture, DotAcpClientAdapterElicitationTests.Args(DotAcpClientAdapterElicitationTests.SessionId), cancellationToken);

        DotAcpClientAdapterElicitationTests.AssertAction("decline", reply);
    }

    /// <summary>A cancelled answer becomes exactly <c>action: cancel</c>, with no content key.</summary>
    [Fact(Timeout = 10000)]
    public async Task Elicitation_Cancelled_RepliesCancelWithoutContent()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        RecordingElicitationScope scope = new() { Script = RecordingElicitationScope.Answer(new ElicitationCancelled()) };
        Fixture fixture = DotAcpClientAdapterElicitationTests.CreateFixture(scope);

        Dictionary<string, object> reply = await DotAcpClientAdapterElicitationTests.ElicitAsync(
            fixture, DotAcpClientAdapterElicitationTests.Args(DotAcpClientAdapterElicitationTests.SessionId), cancellationToken);

        DotAcpClientAdapterElicitationTests.AssertAction("cancel", reply);
    }

    /// <summary>A scope that ends in <see cref="OperationCanceledException"/> is answered <c>cancel</c>: the cancellation is an answer, not a failure.</summary>
    [Fact(Timeout = 10000)]
    public async Task Elicitation_ScopeThrowsOce_RepliesCancel()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        RecordingElicitationScope scope = new() { Script = RecordingElicitationScope.Fault(new OperationCanceledException()) };
        Fixture fixture = DotAcpClientAdapterElicitationTests.CreateFixture(scope);

        Dictionary<string, object> reply = await DotAcpClientAdapterElicitationTests.ElicitAsync(
            fixture, DotAcpClientAdapterElicitationTests.Args(DotAcpClientAdapterElicitationTests.SessionId), cancellationToken);

        DotAcpClientAdapterElicitationTests.AssertAction("cancel", reply);
    }

    /// <summary>Any other exception from the scope reaches the caller (it becomes a JSON-RPC error the agent reports) and is logged as an error; it is never turned into an answer.</summary>
    [Fact(Timeout = 10000)]
    public async Task Elicitation_ScopeThrowsOther_Propagates()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        RecordingElicitationScope scope = new() { Script = RecordingElicitationScope.Fault(new InvalidOperationException("boom")) };
        Fixture fixture = DotAcpClientAdapterElicitationTests.CreateFixture(scope);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Adapter.ExtMethodAsync(
                DotAcpClientAdapterElicitationTests.Method,
                DotAcpClientAdapterElicitationTests.Args(DotAcpClientAdapterElicitationTests.SessionId),
                cancellationToken));

        Assert.Equal("boom", exception.Message);
        Assert.Contains(fixture.Logger.Entries, entry => entry.Level == LogLevel.Error);
    }

    /// <summary>The request's own token cancels the scope's token, and the reply is <c>cancel</c>.</summary>
    [Fact(Timeout = 10000)]
    public async Task Elicitation_RequestToken_CancelsScopeToken()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        RecordingElicitationScope scope = new() { Script = RecordingElicitationScope.BlockUntilCancelled };
        Fixture fixture = DotAcpClientAdapterElicitationTests.CreateFixture(scope);
        using CancellationTokenSource requestSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        Task<object> pending = fixture.Adapter.ExtMethodAsync(
            DotAcpClientAdapterElicitationTests.Method,
            DotAcpClientAdapterElicitationTests.Args(DotAcpClientAdapterElicitationTests.SessionId),
            requestSource.Token);
        CancellationToken scopeToken = await scope.FirstEntered.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        Assert.False(scopeToken.IsCancellationRequested);

        await requestSource.CancelAsync();
        object result = await pending.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

        DotAcpClientAdapterElicitationTests.AssertAction("cancel", Assert.IsType<Dictionary<string, object>>(result));
        Assert.True(scopeToken.IsCancellationRequested);
    }

    /// <summary>The session's prompt cancellation (a Stop, a Turn end) cancels the scope's token, and the reply is <c>cancel</c>.</summary>
    [Fact(Timeout = 10000)]
    public async Task Elicitation_PromptCancellation_CancelsScopeToken()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        RecordingElicitationScope scope = new() { Script = RecordingElicitationScope.BlockUntilCancelled };
        Fixture fixture = DotAcpClientAdapterElicitationTests.CreateFixture(scope);

        Task<object> pending = fixture.Adapter.ExtMethodAsync(
            DotAcpClientAdapterElicitationTests.Method,
            DotAcpClientAdapterElicitationTests.Args(DotAcpClientAdapterElicitationTests.SessionId),
            cancellationToken);
        CancellationToken scopeToken = await scope.FirstEntered.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        Assert.False(scopeToken.IsCancellationRequested);

        await fixture.Sink.PromptCts.CancelAsync();
        object result = await pending.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

        DotAcpClientAdapterElicitationTests.AssertAction("cancel", Assert.IsType<Dictionary<string, object>>(result));
        Assert.True(scopeToken.IsCancellationRequested);
    }

    /// <summary>Unregistering a session (its disposal) cancels that session's open request, and only that session's.</summary>
    [Fact(Timeout = 10000)]
    public async Task Elicitation_Unregister_CancelsThatSessionsRequestsOnly()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        RecordingElicitationScope firstScope = new() { Script = RecordingElicitationScope.BlockUntilCancelled };
        RecordingElicitationScope secondScope = new() { Script = RecordingElicitationScope.BlockUntilCancelled };
        Fixture fixture = DotAcpClientAdapterElicitationTests.CreateFixture(firstScope);
        FakeSessionSink secondSink = new("sess-2") { ElicitationScope = secondScope };
        fixture.Adapter.Register(secondSink);

        Task<object> firstPending = fixture.Adapter.ExtMethodAsync(
            DotAcpClientAdapterElicitationTests.Method, DotAcpClientAdapterElicitationTests.Args("sess-1"), cancellationToken);
        Task<object> secondPending = fixture.Adapter.ExtMethodAsync(
            DotAcpClientAdapterElicitationTests.Method, DotAcpClientAdapterElicitationTests.Args("sess-2"), cancellationToken);
        CancellationToken firstToken = await firstScope.FirstEntered.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        CancellationToken secondToken = await secondScope.FirstEntered.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

        _ = fixture.Adapter.Unregister("sess-1");
        object firstResult = await firstPending.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

        DotAcpClientAdapterElicitationTests.AssertAction("cancel", Assert.IsType<Dictionary<string, object>>(firstResult));
        Assert.True(firstToken.IsCancellationRequested);
        Assert.False(secondToken.IsCancellationRequested);
        Assert.False(secondPending.IsCompleted);

        await secondSink.PromptCts.CancelAsync();
        _ = await secondPending.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
    }

    /// <summary>Disposing the adapter (the host's disposal path) cancels every open request's token.</summary>
    [Fact(Timeout = 10000)]
    public async Task Elicitation_AdapterDispose_CancelsScopeToken()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        RecordingElicitationScope scope = new() { Script = RecordingElicitationScope.BlockUntilCancelled };
        Fixture fixture = DotAcpClientAdapterElicitationTests.CreateFixture(scope);

        Task<object> pending = fixture.Adapter.ExtMethodAsync(
            DotAcpClientAdapterElicitationTests.Method,
            DotAcpClientAdapterElicitationTests.Args(DotAcpClientAdapterElicitationTests.SessionId),
            cancellationToken);
        CancellationToken scopeToken = await scope.FirstEntered.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        Assert.False(scopeToken.IsCancellationRequested);

        fixture.Adapter.Dispose();
        object result = await pending.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

        DotAcpClientAdapterElicitationTests.AssertAction("cancel", Assert.IsType<Dictionary<string, object>>(result));
        Assert.True(scopeToken.IsCancellationRequested);
    }

    /// <summary>A request that arrives after the adapter was disposed is answered <c>cancel</c> without asking the scope.</summary>
    [Fact(Timeout = 10000)]
    public async Task Elicitation_AfterAdapterDispose_RepliesCancel()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        RecordingElicitationScope scope = new();
        Fixture fixture = DotAcpClientAdapterElicitationTests.CreateFixture(scope);
        fixture.Adapter.Dispose();

        Dictionary<string, object> reply = await DotAcpClientAdapterElicitationTests.ElicitAsync(
            fixture, DotAcpClientAdapterElicitationTests.Args(DotAcpClientAdapterElicitationTests.SessionId), cancellationToken);

        DotAcpClientAdapterElicitationTests.AssertAction("cancel", reply);
        Assert.Empty(scope.Requests);
    }

    /// <summary>A disconnect notice (the agent process went away) cancels every open request's token, and the reply is <c>cancel</c>.</summary>
    [Fact(Timeout = 10000)]
    public async Task Elicitation_OnDisconnected_CancelsScopeToken()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        RecordingElicitationScope scope = new() { Script = RecordingElicitationScope.BlockUntilCancelled };
        Fixture fixture = DotAcpClientAdapterElicitationTests.CreateFixture(scope);
        using dotacp.client.Connection connection = DotAcpClientAdapterElicitationTests.CreateConnection();

        Task<object> pending = fixture.Adapter.ExtMethodAsync(
            DotAcpClientAdapterElicitationTests.Method,
            DotAcpClientAdapterElicitationTests.Args(DotAcpClientAdapterElicitationTests.SessionId),
            cancellationToken);
        CancellationToken scopeToken = await scope.FirstEntered.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        Assert.False(scopeToken.IsCancellationRequested);

        fixture.Adapter.OnDisconnected(connection);
        object result = await pending.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

        DotAcpClientAdapterElicitationTests.AssertAction("cancel", Assert.IsType<Dictionary<string, object>>(result));
        Assert.True(scopeToken.IsCancellationRequested);
    }

    /// <summary>Disposing the adapter twice is harmless.</summary>
    [Fact]
    public void Dispose_Twice_DoesNotThrow()
    {
        Fixture fixture = DotAcpClientAdapterElicitationTests.CreateFixture(scope: null);

        fixture.Adapter.Dispose();
        Exception? exception = Record.Exception(fixture.Adapter.Dispose);

        Assert.Null(exception);
    }

    /// <summary>A disconnect notice that arrives after the adapter was disposed (the host watches the connection on its own task) is harmless.</summary>
    [Fact]
    public void OnDisconnected_AfterDispose_DoesNotThrow()
    {
        Fixture fixture = DotAcpClientAdapterElicitationTests.CreateFixture(scope: null);
        using dotacp.client.Connection connection = DotAcpClientAdapterElicitationTests.CreateConnection();

        fixture.Adapter.Dispose();
        Exception? exception = Record.Exception(() => fixture.Adapter.OnDisconnected(connection));

        Assert.Null(exception);
    }

    /// <summary>Every extension method other than <c>elicitation/create</c> is still unsupported, including near misses of its name.</summary>
    /// <param name="method">The method name to call.</param>
    [Theory(Timeout = 10000)]
    [MemberData(nameof(OtherMethods))]
    public async Task OtherExtMethods_StillThrow(string method)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        RecordingElicitationScope scope = new();
        Fixture fixture = DotAcpClientAdapterElicitationTests.CreateFixture(scope);

        await Assert.ThrowsAsync<NotSupportedException>(
            () => fixture.Adapter.ExtMethodAsync(method, DotAcpClientAdapterElicitationTests.Args(DotAcpClientAdapterElicitationTests.SessionId), cancellationToken));

        Assert.Empty(scope.Requests);
    }

    /// <summary>Asserts a reply is exactly one <c>action</c> entry with the given value and no <c>content</c> key.</summary>
    /// <param name="expectedAction">The expected action.</param>
    /// <param name="reply">The adapter's reply.</param>
    private static void AssertAction(string expectedAction, Dictionary<string, object> reply)
    {
        Assert.Equal(expectedAction, Assert.IsType<string>(reply["action"]));
        Assert.Single(reply);
    }

    /// <summary>Calls <c>elicitation/create</c> and returns the reply, which must be the wire dictionary.</summary>
    /// <param name="fixture">The adapter under test.</param>
    /// <param name="arguments">The arguments to send.</param>
    /// <param name="cancellationToken">The request token.</param>
    private static async Task<Dictionary<string, object>> ElicitAsync(Fixture fixture, Dictionary<string, object> arguments, CancellationToken cancellationToken)
    {
        object result = await fixture.Adapter.ExtMethodAsync(DotAcpClientAdapterElicitationTests.Method, arguments, cancellationToken);
        return Assert.IsType<Dictionary<string, object>>(result);
    }

    /// <summary>Builds arguments in the shape dotacp hands the adapter: a dictionary whose values are Newtonsoft tokens.</summary>
    /// <param name="sessionId">The session the request belongs to.</param>
    /// <param name="toolCallId">The tool call id, or null to omit it.</param>
    /// <param name="message">The message text.</param>
    /// <param name="schemaJson">The requested schema as JSON text.</param>
    private static Dictionary<string, object> Args(
        string sessionId,
        string? toolCallId = null,
        string message = "Pick one",
        string schemaJson = DotAcpClientAdapterElicitationTests.EmptySchema)
    {
        Dictionary<string, object> arguments = new(StringComparer.Ordinal)
        {
            ["mode"] = new JValue("form"),
            ["sessionId"] = new JValue(sessionId),
            ["message"] = new JValue(message),
            ["requestedSchema"] = JObject.Parse(schemaJson),
        };

        if (toolCallId is not null)
        {
            arguments["toolCallId"] = new JValue(toolCallId);
        }

        return arguments;
    }

    /// <summary>Opens a real dotacp connection over an in-memory stream nobody answers, only to have a <see cref="dotacp.client.Connection"/> to pass to <see cref="DotAcpClientAdapter.OnDisconnected"/>.</summary>
    private static dotacp.client.Connection CreateConnection()
    {
        (Stream clientEnd, _) = FullDuplexStream.CreatePair();
        return dotacp.client.Connection.RunClient(new DotAcpClientAdapter(new ListLogger<DotAcpClientAdapter>()), clientEnd, clientEnd, null)
            ?? throw new InvalidOperationException("Failed to create connection.");
    }

    private static Fixture CreateFixture(IElicitationScope? scope)
    {
        ListLogger<DotAcpClientAdapter> logger = new();
        DotAcpClientAdapter adapter = new(logger);
        FakeSessionSink sink = new(DotAcpClientAdapterElicitationTests.SessionId) { ElicitationScope = scope };
        adapter.Register(sink);
        return new Fixture(adapter, logger, sink);
    }

    private sealed record Fixture(
        DotAcpClientAdapter Adapter,
        ListLogger<DotAcpClientAdapter> Logger,
        FakeSessionSink Sink);
}

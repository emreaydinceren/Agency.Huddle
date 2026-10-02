using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Nerdbank.Streams;
using Xunit;

namespace Agency.Huddle.Acp.Tests.Fakes;

/// <summary>
/// Self-tests for the elicitation half of <see cref="FakeAcpAgent"/> and <see cref="PromptContext"/>
/// (Elicitation D1b fake contract): the helper sends the real wire method, refuses when the client never
/// advertised <c>elicitation.form</c>, emits <c>$/cancel_request</c> on <c>session/cancel</c> for each
/// open request as the real adapter does, and builds the request shape the vendored adapter builds.
/// The fake is driven from the other end of the stream pair with a plain reader and writer, so no
/// protocol library can hide a wire mismatch.
/// </summary>
public sealed class FakeAcpAgentElicitationTests
{
    private const string InitializeAdvertising = """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":1,"clientCapabilities":{"elicitation":{"form":{}}}}}""";

    private const string InitializeNotAdvertising = """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":1,"clientCapabilities":{}}}""";

    private const string PromptOne = """{"jsonrpc":"2.0","id":2,"method":"session/prompt","params":{"sessionId":"sess-1","prompt":[]}}""";

    private const string PromptTwo = """{"jsonrpc":"2.0","id":3,"method":"session/prompt","params":{"sessionId":"sess-2","prompt":[]}}""";

    /// <summary>
    /// What the vendored adapter's <c>askUserQuestionsToCreateRequest</c> builds for one single-select
    /// question with two options (one with a description) and a tool call id, taken from running
    /// <c>dist/elicitation.js</c> (claude-agent-acp 0.75.1), as the wire would carry it.
    /// </summary>
    private const string AdapterAskUserQuestionRequest = """{"mode":"form","sessionId":"sess-1","toolCallId":"toolu_1","message":"Which database?","requestedSchema":{"type":"object","properties":{"question_0":{"type":"string","title":"DB","oneOf":[{"const":"Postgres","title":"Postgres","description":"Relational"},{"const":"SQLite","title":"SQLite"}]},"question_0_custom":{"type":"string","title":"Other","description":"Type your own answer instead of choosing an option above (optional).","_meta":{"_askUserQuestionCustomAnswer":{"questionId":"question_0","isCustomAnswer":true}}}}}}""";

    /// <summary>The helper refuses to send <c>elicitation/create</c> when the client never advertised <c>elicitation.form</c> in <c>initialize</c>: it throws and puts nothing on the wire, so a test cannot pass against a request a real adapter would never make.</summary>
    [Fact(Timeout = 10000)]
    public async Task RequestElicitation_NotAdvertised_RefusesAndSendsNothing()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using Harness harness = new();
        InvalidOperationException? refusal = null;
        harness.Agent.OnPrompt = async context =>
        {
            try
            {
                _ = await context.RequestElicitationAsync("Pick one", FakeAcpAgentElicitationTests.EmptySchema()).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                refusal = ex;
            }

            return "end_turn";
        };
        _ = harness.Agent.RunAsync(cancellationToken);
        await harness.SendAsync(FakeAcpAgentElicitationTests.InitializeNotAdvertising, cancellationToken);
        _ = await harness.ReceiveAsync(cancellationToken);

        await harness.SendAsync(FakeAcpAgentElicitationTests.PromptOne, cancellationToken);
        JsonObject next = await harness.ReceiveAsync(cancellationToken);

        Assert.NotNull(refusal);
        Assert.Equal(
            "The client did not advertise elicitation.form in initialize, so a real adapter would not send elicitation/create; the fake refuses to.",
            refusal.Message);
        Assert.Equal(2, (int?)next["id"]);
        Assert.Equal("end_turn", (string?)next["result"]?["stopReason"]);
    }

    /// <summary>The helper sends the real wire method <c>elicitation/create</c> with the form parameters, omits <c>toolCallId</c> when none is given, and returns the whole reply object.</summary>
    [Fact(Timeout = 10000)]
    public async Task RequestElicitation_Advertised_SendsTheWireMethodAndReturnsTheWholeReply()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using Harness harness = new();
        JsonObject? reply = null;
        harness.Agent.OnPrompt = async context =>
        {
            reply = await context.RequestElicitationAsync("Pick one", FakeAcpAgentElicitationTests.EmptySchema()).ConfigureAwait(false);
            return "end_turn";
        };
        _ = harness.Agent.RunAsync(cancellationToken);
        await harness.SendAsync(FakeAcpAgentElicitationTests.InitializeAdvertising, cancellationToken);
        _ = await harness.ReceiveAsync(cancellationToken);

        await harness.SendAsync(FakeAcpAgentElicitationTests.PromptOne, cancellationToken);
        JsonObject request = await harness.ReceiveAsync(cancellationToken);
        await harness.SendAsync(FakeAcpAgentElicitationTests.ResultLine(FakeAcpAgentElicitationTests.IdOf(request), """{"action":"decline"}"""), cancellationToken);
        JsonObject promptResult = await harness.ReceiveAsync(cancellationToken);

        Assert.Equal("elicitation/create", (string?)request["method"]);
        Assert.Equal(
            """{"sessionId":"sess-1","mode":"form","message":"Pick one","requestedSchema":{"type":"object","properties":{}}}""",
            request["params"]?.ToJsonString());
        Assert.Equal("end_turn", (string?)promptResult["result"]?["stopReason"]);
        Assert.NotNull(reply);
        Assert.Equal("""{"action":"decline"}""", reply.ToJsonString());
    }

    /// <summary>The request the helper sends for an AskUserQuestion-shaped form equals, key for key, the literal JSON the vendored adapter's builder produces (including the tool call id), so the fake cannot drift from the real adapter's envelope.</summary>
    [Fact(Timeout = 10000)]
    public async Task RequestElicitation_AskUserQuestionShape_MatchesTheVendoredAdapterBuilder()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using Harness harness = new();
        harness.Agent.OnPrompt = async context =>
        {
            _ = await context.RequestElicitationAsync("Which database?", FakeAcpAgentElicitationTests.AskUserQuestionSchema(), "toolu_1").ConfigureAwait(false);
            return "end_turn";
        };
        _ = harness.Agent.RunAsync(cancellationToken);
        await harness.SendAsync(FakeAcpAgentElicitationTests.InitializeAdvertising, cancellationToken);
        _ = await harness.ReceiveAsync(cancellationToken);

        await harness.SendAsync(FakeAcpAgentElicitationTests.PromptOne, cancellationToken);
        JsonObject request = await harness.ReceiveAsync(cancellationToken);
        await harness.SendAsync(FakeAcpAgentElicitationTests.ResultLine(FakeAcpAgentElicitationTests.IdOf(request), """{"action":"cancel"}"""), cancellationToken);
        _ = await harness.ReceiveAsync(cancellationToken);

        JsonNode expected = JsonNode.Parse(FakeAcpAgentElicitationTests.AdapterAskUserQuestionRequest)
            ?? throw new InvalidOperationException("The adapter literal did not parse.");
        Assert.True(JsonNode.DeepEquals(expected, request["params"]), request["params"]?.ToJsonString());
    }

    /// <summary>On <c>session/cancel</c> the fake writes <c>$/cancel_request</c> for every open elicitation request of that session, as the real adapter does, in the exact line shape.</summary>
    [Fact(Timeout = 10000)]
    public async Task SessionCancel_EmitsCancelRequestForEachOutstandingElicitation()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using Harness harness = new();
        harness.Agent.OnPrompt = async context =>
        {
            Task<JsonObject> first = context.RequestElicitationAsync("one", FakeAcpAgentElicitationTests.EmptySchema());
            Task<JsonObject> second = context.RequestElicitationAsync("two", FakeAcpAgentElicitationTests.EmptySchema());
            _ = await Task.WhenAll(first, second).ConfigureAwait(false);
            return "end_turn";
        };
        _ = harness.Agent.RunAsync(cancellationToken);
        await harness.SendAsync(FakeAcpAgentElicitationTests.InitializeAdvertising, cancellationToken);
        _ = await harness.ReceiveAsync(cancellationToken);
        await harness.SendAsync(FakeAcpAgentElicitationTests.PromptOne, cancellationToken);
        JsonObject firstRequest = await harness.ReceiveAsync(cancellationToken);
        JsonObject secondRequest = await harness.ReceiveAsync(cancellationToken);
        int firstId = FakeAcpAgentElicitationTests.IdOf(firstRequest);
        int secondId = FakeAcpAgentElicitationTests.IdOf(secondRequest);

        await harness.SendAsync("""{"jsonrpc":"2.0","method":"session/cancel","params":{"sessionId":"sess-1"}}""", cancellationToken);
        JsonObject firstCancel = await harness.ReceiveAsync(cancellationToken);
        JsonObject secondCancel = await harness.ReceiveAsync(cancellationToken);

        string[] lines = [firstCancel.ToJsonString(), secondCancel.ToJsonString()];
        Array.Sort(lines, StringComparer.Ordinal);
        string[] expected = [FakeAcpAgentElicitationTests.CancelRequestLine(firstId), FakeAcpAgentElicitationTests.CancelRequestLine(secondId)];
        Array.Sort(expected, StringComparer.Ordinal);
        Assert.Equal(expected, lines);

        await harness.SendAsync(FakeAcpAgentElicitationTests.ResultLine(firstId, """{"action":"cancel"}"""), cancellationToken);
        await harness.SendAsync(FakeAcpAgentElicitationTests.ResultLine(secondId, """{"action":"cancel"}"""), cancellationToken);
        JsonObject promptResult = await harness.ReceiveAsync(cancellationToken);
        Assert.Equal("end_turn", (string?)promptResult["result"]?["stopReason"]);
    }

    /// <summary>A request that was already answered is not cancelled again: only the still-open request gets a <c>$/cancel_request</c>.</summary>
    [Fact(Timeout = 10000)]
    public async Task SessionCancel_AnsweredRequest_GetsNoCancelRequest()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using Harness harness = new();
        harness.Agent.OnPrompt = async context =>
        {
            Task<JsonObject> first = context.RequestElicitationAsync("one", FakeAcpAgentElicitationTests.EmptySchema());
            Task<JsonObject> second = context.RequestElicitationAsync("two", FakeAcpAgentElicitationTests.EmptySchema());
            _ = await Task.WhenAll(first, second).ConfigureAwait(false);
            return "end_turn";
        };
        _ = harness.Agent.RunAsync(cancellationToken);
        await harness.SendAsync(FakeAcpAgentElicitationTests.InitializeAdvertising, cancellationToken);
        _ = await harness.ReceiveAsync(cancellationToken);
        await harness.SendAsync(FakeAcpAgentElicitationTests.PromptOne, cancellationToken);
        JsonObject firstRequest = await harness.ReceiveAsync(cancellationToken);
        JsonObject secondRequest = await harness.ReceiveAsync(cancellationToken);
        int firstId = FakeAcpAgentElicitationTests.IdOf(firstRequest);
        int secondId = FakeAcpAgentElicitationTests.IdOf(secondRequest);
        await harness.SendAsync(FakeAcpAgentElicitationTests.ResultLine(firstId, """{"action":"decline"}"""), cancellationToken);

        await harness.SendAsync("""{"jsonrpc":"2.0","method":"session/cancel","params":{"sessionId":"sess-1"}}""", cancellationToken);
        JsonObject cancel = await harness.ReceiveAsync(cancellationToken);
        await harness.SendAsync(FakeAcpAgentElicitationTests.ResultLine(secondId, """{"action":"cancel"}"""), cancellationToken);
        JsonObject afterwards = await harness.ReceiveAsync(cancellationToken);

        Assert.Equal(FakeAcpAgentElicitationTests.CancelRequestLine(secondId), cancel.ToJsonString());
        Assert.Null(afterwards["method"]);
        Assert.Equal("end_turn", (string?)afterwards["result"]?["stopReason"]);
    }

    /// <summary>Cancelling one session does not cancel another session's open request.</summary>
    [Fact(Timeout = 10000)]
    public async Task SessionCancel_OtherSessionsRequest_IsNotCancelled()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using Harness harness = new();
        harness.Agent.OnPrompt = async context =>
        {
            _ = await context.RequestElicitationAsync("from " + context.SessionId, FakeAcpAgentElicitationTests.EmptySchema()).ConfigureAwait(false);
            return "end_turn";
        };
        _ = harness.Agent.RunAsync(cancellationToken);
        await harness.SendAsync(FakeAcpAgentElicitationTests.InitializeAdvertising, cancellationToken);
        _ = await harness.ReceiveAsync(cancellationToken);
        await harness.SendAsync(FakeAcpAgentElicitationTests.PromptOne, cancellationToken);
        await harness.SendAsync(FakeAcpAgentElicitationTests.PromptTwo, cancellationToken);
        JsonObject requestA = await harness.ReceiveAsync(cancellationToken);
        JsonObject requestB = await harness.ReceiveAsync(cancellationToken);
        JsonObject sessionOneRequest = string.Equals((string?)requestA["params"]?["sessionId"], "sess-1", StringComparison.Ordinal) ? requestA : requestB;
        JsonObject sessionTwoRequest = ReferenceEquals(sessionOneRequest, requestA) ? requestB : requestA;

        await harness.SendAsync("""{"jsonrpc":"2.0","method":"session/cancel","params":{"sessionId":"sess-1"}}""", cancellationToken);
        JsonObject cancel = await harness.ReceiveAsync(cancellationToken);
        await harness.SendAsync(FakeAcpAgentElicitationTests.ResultLine(FakeAcpAgentElicitationTests.IdOf(sessionOneRequest), """{"action":"cancel"}"""), cancellationToken);
        await harness.SendAsync(FakeAcpAgentElicitationTests.ResultLine(FakeAcpAgentElicitationTests.IdOf(sessionTwoRequest), """{"action":"cancel"}"""), cancellationToken);
        JsonObject firstResult = await harness.ReceiveAsync(cancellationToken);
        JsonObject secondResult = await harness.ReceiveAsync(cancellationToken);

        Assert.Equal(FakeAcpAgentElicitationTests.CancelRequestLine(FakeAcpAgentElicitationTests.IdOf(sessionOneRequest)), cancel.ToJsonString());
        Assert.Null(firstResult["method"]);
        Assert.Null(secondResult["method"]);
    }

    /// <summary>
    /// Pins the facts the fake mirrors to the vendored adapter's source: the AskUserQuestion builder and
    /// its form-mode envelope in <c>elicitation.js</c>, the <c>elicitation?.form</c> gate in
    /// <c>acp-agent.js</c>, and the <c>elicitation/create</c> method name in the ACP SDK. Skipped, not
    /// failed, when the adapter is not installed.
    /// </summary>
    [Fact(SkipUnless = nameof(VendoredAdapterFiles.Present), SkipType = typeof(VendoredAdapterFiles), Skip = "The vendored claude-agent-acp adapter is not installed (tools/acp/node_modules).")]
    public void VendoredAdapter_Sources_StillContainWhatTheFakeMirrors()
    {
        string elicitation = File.ReadAllText(VendoredAdapterFiles.ElicitationJs ?? throw new InvalidOperationException("elicitation.js is not installed."));
        string agent = File.ReadAllText(VendoredAdapterFiles.AcpAgentJs ?? throw new InvalidOperationException("acp-agent.js is not installed."));
        string schema = File.ReadAllText(VendoredAdapterFiles.SdkSchemaJs ?? throw new InvalidOperationException("The SDK schema is not installed."));

        Assert.Contains("export function askUserQuestionsToCreateRequest", elicitation, StringComparison.Ordinal); // contains-ok: JavaScript source text, not markup
        Assert.Contains("mode: \"form\"", elicitation, StringComparison.Ordinal); // contains-ok: JavaScript source text, not markup
        Assert.Contains("_askUserQuestionCustomAnswer", elicitation, StringComparison.Ordinal); // contains-ok: JavaScript source text, not markup
        Assert.Contains("this.clientCapabilities?.elicitation?.form", agent, StringComparison.Ordinal); // contains-ok: JavaScript source text, not markup
        Assert.Contains("elicitation/create", schema, StringComparison.Ordinal); // contains-ok: JavaScript source text, not markup
    }

    private static string ResultLine(int id, string resultJson)
    {
        return string.Create(CultureInfo.InvariantCulture, $$"""{"jsonrpc":"2.0","id":{{id}},"result":{{resultJson}}}""");
    }

    private static string CancelRequestLine(int requestId)
    {
        return string.Create(CultureInfo.InvariantCulture, $$$"""{"jsonrpc":"2.0","method":"$/cancel_request","params":{"requestId":{{{requestId}}}}}""");
    }

    private static int IdOf(JsonObject message)
    {
        return (int?)message["id"] ?? throw new InvalidOperationException("The message has no integer id: " + message.ToJsonString());
    }

    private static JsonObject EmptySchema()
    {
        return new JsonObject { ["type"] = "object", ["properties"] = new JsonObject() };
    }

    /// <summary>Builds, in code, the schema the vendored builder produces for the one-question case in <see cref="AdapterAskUserQuestionRequest"/>.</summary>
    private static JsonObject AskUserQuestionSchema()
    {
        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["question_0"] = new JsonObject
                {
                    ["type"] = "string",
                    ["title"] = "DB",
                    ["oneOf"] = new JsonArray(
                        new JsonObject { ["const"] = "Postgres", ["title"] = "Postgres", ["description"] = "Relational" },
                        new JsonObject { ["const"] = "SQLite", ["title"] = "SQLite" }),
                },
                ["question_0_custom"] = new JsonObject
                {
                    ["type"] = "string",
                    ["title"] = "Other",
                    ["description"] = "Type your own answer instead of choosing an option above (optional).",
                    ["_meta"] = new JsonObject
                    {
                        ["_askUserQuestionCustomAnswer"] = new JsonObject { ["questionId"] = "question_0", ["isCustomAnswer"] = true },
                    },
                },
            },
        };
    }

    /// <summary>Wires a stream pair, exposing the fake's end and a plain reader/writer over the test's end.</summary>
    private sealed class Harness : IAsyncDisposable
    {
        private readonly Stream testStream;

        private readonly StreamReader reader;

        internal Harness()
        {
            (Stream testEnd, Stream agentEnd) = FullDuplexStream.CreatePair();
            this.testStream = testEnd;
            this.reader = new StreamReader(testEnd, new UTF8Encoding(false), false, 1024, leaveOpen: true);
            this.Agent = new FakeAcpAgent(agentEnd);
        }

        internal FakeAcpAgent Agent { get; }

        internal async Task SendAsync(string json, CancellationToken cancellationToken)
        {
            byte[] bytes = new UTF8Encoding(false).GetBytes(json + "\n");
            await this.testStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await this.testStream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        internal async Task<JsonObject> ReceiveAsync(CancellationToken cancellationToken)
        {
            string? line = await this.reader.ReadLineAsync(cancellationToken).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(5), cancellationToken)
                .ConfigureAwait(false);
            Assert.NotNull(line);
            return JsonNode.Parse(line) as JsonObject ?? throw new InvalidOperationException("Received a line that is not a JSON object: " + line);
        }

        public async ValueTask DisposeAsync()
        {
            this.reader.Dispose();
            this.testStream.Dispose();
            await this.Agent.DisposeAsync().ConfigureAwait(false);
        }
    }
}

using System.Text;
using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Tests.Fakes;
using Agency.Huddle.MockAdapter;
using Nerdbank.Streams;

namespace Agency.Huddle.Tests.MockAdapter;

/// <summary>
/// Drives <see cref="MockBehaviour.ChunkedEchoAsync"/> in-process over a <see cref="FullDuplexStream"/>
/// pair, acting as the client half with raw newline-delimited JSON-RPC, to prove that a prompt carrying
/// an <c>[elicit:...]</c> marker makes the mock put a form to the Human (a real <c>elicitation/create</c>
/// request, in the shape the vendored adapter builds) and report back exactly what it received, so the
/// Elicitation card can be exercised without a model; and that a prompt without a marker still echoes.
/// </summary>
public sealed class MockElicitationTests
{
    private const string AskSchemaJson = """{"type":"object","properties":{"question_0":{"type":"string","title":"Colour","description":"Which colour do you prefer?","oneOf":[{"const":"Red","title":"Red","description":"Warm and bold"},{"const":"Green","title":"Green","description":"Calm and natural"},{"const":"Blue","title":"Blue","description":"Cool and steady"}]},"question_0_custom":{"type":"string","title":"Other","description":"Type your own answer instead of choosing an option above (optional).","_meta":{"_askUserQuestionCustomAnswer":{"questionId":"question_0","isCustomAnswer":true}}},"question_1":{"type":"array","title":"Extras","description":"Which extras do you want?","items":{"anyOf":[{"const":"Rugs","title":"Rugs","description":"Soft underfoot"},{"const":"Lamps","title":"Lamps","description":"Warm light"},{"const":"Plants","title":"Plants","description":"Something green"}]}},"question_1_custom":{"type":"string","title":"Other","description":"Type your own answer instead of choosing an option above (optional).","_meta":{"_askUserQuestionCustomAnswer":{"questionId":"question_1","isCustomAnswer":true}}}}}""";

    private const string OneSchemaJson = """{"type":"object","properties":{"question_0":{"type":"string","title":"Colour","oneOf":[{"const":"Red","title":"Red","description":"Warm and bold"},{"const":"Green","title":"Green","description":"Calm and natural"},{"const":"Blue","title":"Blue","description":"Cool and steady"}]},"question_0_custom":{"type":"string","title":"Other","description":"Type your own answer instead of choosing an option above (optional).","_meta":{"_askUserQuestionCustomAnswer":{"questionId":"question_0","isCustomAnswer":true}}}}}""";

    private const string FormSchemaJson = """{"type":"object","properties":{"title":{"type":"string","title":"Title"},"due":{"type":"string","format":"date","title":"Due date"},"count":{"type":"integer","minimum":1,"maximum":20,"title":"Boxes"},"budget":{"type":"number","minimum":0,"title":"Budget"},"agree":{"type":"boolean","title":"Insured"},"priority":{"type":"string","title":"Priority","oneOf":[{"const":"low","title":"Low"},{"const":"high","title":"High"}]},"tags":{"type":"array","title":"Handling","items":{"type":"string","anyOf":[{"const":"fragile","title":"Fragile"},{"const":"heavy","title":"Heavy"},{"const":"cold","title":"Cold chain"}]}},"notes":{"type":"string","title":"Notes","description":"Anything else we should know?"}},"required":["title","due","count"]}""";

    private const string RefusalSchemaJson = """{"type":"object","properties":{"choice":{"type":"string","oneOf":[{"const":"retry_fallback","title":"Retry with Model Y"},{"const":"cancelled","title":"Keep the refusal"}]}}}""";

    private const string UnsupportedSchemaJson = """{"type":"object","properties":{"address":{"type":"object","title":"Address","properties":{"street":{"type":"string","title":"Street"},"city":{"type":"string","title":"City"}}}}}""";

    private const string NotAdvertisedReply = "Elicitation unavailable: elicitation.form was not advertised";

    private static readonly UTF8Encoding Utf8NoBom = new(false);

    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(5);

    /// <summary>[elicit:ask] sends the AskUserQuestion form for two questions, exactly as the vendored adapter builds it (every key, the tool call id, the message, nothing required), and the reply states the accepted content on one line.</summary>
    [Fact(Timeout = 30000)]
    public async Task ElicitAsk_Accepted_SendsTheTwoQuestionAdapterShapeAndReportsTheContent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Client client = await Client.StartAsync(advertiseForm: true, ct);

        TurnResult turn = await client.RunTurnAsync(
            ["please ask [elicit:ask] now"],
            id => ResultLine(id, """{"action":"accept","content":{"question_0":"Red","question_1":["Rugs","Plants"]}}"""),
            ct);

        JsonObject request = RequireRequest(turn);
        JsonObject parameters = RequireObject(request["params"]);
        JsonObject schema = RequireObject(parameters["requestedSchema"]);
        Assert.Equal("form", (string?)parameters["mode"]);
        Assert.Equal(client.SessionId, (string?)parameters["sessionId"]);
        Assert.Equal("Please answer the following questions.", (string?)parameters["message"]);
        Assert.Equal("toolu_mock_ask", (string?)parameters["toolCallId"]);
        Assert.Equal(["question_0", "question_0_custom", "question_1", "question_1_custom"], PropertyKeys(schema));
        Assert.Null(schema["required"]);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(AskSchemaJson), schema), schema.ToJsonString());
        Assert.Equal("""Got action=accept content={"question_0":"Red","question_1":["Rugs","Plants"]}""", turn.Reply);
        Assert.True(turn.Chunks.Count > 1, $"Expected the reply in more than one chunk, got {turn.Chunks.Count}.");
        Assert.Equal("end_turn", turn.StopReason);
    }

    /// <summary>[elicit:ask] reports a declined form as <c>action=decline</c> with no content.</summary>
    [Fact(Timeout = 30000)]
    public async Task ElicitAsk_Declined_ReportsDeclineAndNoContent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Client client = await Client.StartAsync(advertiseForm: true, ct);

        TurnResult turn = await client.RunTurnAsync(["[elicit:ask]"], id => ResultLine(id, """{"action":"decline"}"""), ct);

        Assert.Equal("Got action=decline content=none", turn.Reply);
        Assert.Equal("end_turn", turn.StopReason);
    }

    /// <summary>[elicit:ask] reports a form the client answered with <c>cancel</c> as <c>action=cancel</c> with no content, and the turn still ends normally.</summary>
    [Fact(Timeout = 30000)]
    public async Task ElicitAsk_ClientAnswersCancel_ReportsCancelAndNoContent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Client client = await Client.StartAsync(advertiseForm: true, ct);

        TurnResult turn = await client.RunTurnAsync(["[elicit:ask]"], id => ResultLine(id, """{"action":"cancel"}"""), ct);

        Assert.Equal("Got action=cancel content=none", turn.Reply);
        Assert.Equal("end_turn", turn.StopReason);
    }

    /// <summary>[elicit:one] sends the single-question AskUserQuestion form: the question is the message, the field carries no description, and the tool call id is present.</summary>
    [Fact(Timeout = 30000)]
    public async Task ElicitOne_Accepted_SendsTheSingleQuestionAdapterShapeAndReportsTheContent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Client client = await Client.StartAsync(advertiseForm: true, ct);

        TurnResult turn = await client.RunTurnAsync(
            ["[elicit:one]"],
            id => ResultLine(id, """{"action":"accept","content":{"question_0":"Blue","question_0_custom":"Teal"}}"""),
            ct);

        JsonObject parameters = RequireObject(RequireRequest(turn)["params"]);
        JsonObject schema = RequireObject(parameters["requestedSchema"]);
        Assert.Equal("form", (string?)parameters["mode"]);
        Assert.Equal("Which colour do you prefer?", (string?)parameters["message"]);
        Assert.Equal("toolu_mock_ask", (string?)parameters["toolCallId"]);
        Assert.Equal(["question_0", "question_0_custom"], PropertyKeys(schema));
        Assert.Null(schema["required"]);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(OneSchemaJson), schema), schema.ToJsonString());
        Assert.Equal("""Got action=accept content={"question_0":"Blue","question_0_custom":"Teal"}""", turn.Reply);
        Assert.Equal("end_turn", turn.StopReason);
    }

    /// <summary>[elicit:one] reports a decline with no content.</summary>
    [Fact(Timeout = 30000)]
    public async Task ElicitOne_Declined_ReportsDeclineAndNoContent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Client client = await Client.StartAsync(advertiseForm: true, ct);

        TurnResult turn = await client.RunTurnAsync(["[elicit:one]"], id => ResultLine(id, """{"action":"decline"}"""), ct);

        Assert.Equal("Got action=decline content=none", turn.Reply);
    }

    /// <summary>[elicit:form] sends the MCP-style typed form: no tool call id, the message, every property with its type, and exactly title, due and count required; the reply carries the accepted content verbatim, with non-ASCII text and markup characters unescaped.</summary>
    [Fact(Timeout = 30000)]
    public async Task ElicitForm_Accepted_SendsTheTypedFormAndReportsTheContent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Client client = await Client.StartAsync(advertiseForm: true, ct);
        const string content = """{"title":"Move","due":"2026-10-05","count":3,"budget":12.5,"agree":true,"priority":"high","tags":["fragile","cold"],"notes":"Café & <fragile>"}""";

        TurnResult turn = await client.RunTurnAsync(
            ["[elicit:form]"],
            id => ResultLine(id, """{"action":"accept","content":""" + content + "}"),
            ct);

        JsonObject parameters = RequireObject(RequireRequest(turn)["params"]);
        JsonObject schema = RequireObject(parameters["requestedSchema"]);
        Assert.Equal("form", (string?)parameters["mode"]);
        Assert.Equal("Plan the delivery", (string?)parameters["message"]);
        Assert.False(parameters.ContainsKey("toolCallId"), parameters.ToJsonString());
        Assert.Equal(["title", "due", "count", "budget", "agree", "priority", "tags", "notes"], PropertyKeys(schema));
        Assert.Equal(
            ["title:string", "due:string", "count:integer", "budget:number", "agree:boolean", "priority:string", "tags:array", "notes:string"],
            PropertyKeyAndTypes(schema));
        Assert.Equal(["title", "due", "count"], RequireArray(schema["required"]).Select(node => (string?)node).ToList());
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(FormSchemaJson), schema), schema.ToJsonString());
        Assert.Equal("Got action=accept content=" + content, turn.Reply);
        Assert.Equal("end_turn", turn.StopReason);
    }

    /// <summary>[elicit:form] reports a decline with no content.</summary>
    [Fact(Timeout = 30000)]
    public async Task ElicitForm_Declined_ReportsDeclineAndNoContent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Client client = await Client.StartAsync(advertiseForm: true, ct);

        TurnResult turn = await client.RunTurnAsync(["[elicit:form]"], id => ResultLine(id, """{"action":"decline"}"""), ct);

        Assert.Equal("Got action=decline content=none", turn.Reply);
    }

    /// <summary>[elicit:refusal] sends the refusal-fallback dialog: the model-declined message, a single <c>choice</c> field with the two wire values, and no tool call id.</summary>
    [Fact(Timeout = 30000)]
    public async Task ElicitRefusal_Accepted_SendsTheRefusalDialogAndReportsTheChoice()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Client client = await Client.StartAsync(advertiseForm: true, ct);

        TurnResult turn = await client.RunTurnAsync(
            ["[elicit:refusal]"],
            id => ResultLine(id, """{"action":"accept","content":{"choice":"retry_fallback"}}"""),
            ct);

        JsonObject parameters = RequireObject(RequireRequest(turn)["params"]);
        JsonObject schema = RequireObject(parameters["requestedSchema"]);
        Assert.Equal("form", (string?)parameters["mode"]);
        Assert.Equal("Model X declined this request. Retry with Model Y?", (string?)parameters["message"]);
        Assert.False(parameters.ContainsKey("toolCallId"), parameters.ToJsonString());
        Assert.Equal(["choice"], PropertyKeys(schema));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(RefusalSchemaJson), schema), schema.ToJsonString());
        Assert.Equal("""Got action=accept content={"choice":"retry_fallback"}""", turn.Reply);
        Assert.Equal("end_turn", turn.StopReason);
    }

    /// <summary>[elicit:refusal] reports a client that answers decline.</summary>
    [Fact(Timeout = 30000)]
    public async Task ElicitRefusal_Declined_ReportsDeclineAndNoContent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Client client = await Client.StartAsync(advertiseForm: true, ct);

        TurnResult turn = await client.RunTurnAsync(["[elicit:refusal]"], id => ResultLine(id, """{"action":"decline"}"""), ct);

        Assert.Equal("Got action=decline content=none", turn.Reply);
    }

    /// <summary>[elicit:unsupported] sends a schema whose only property is a nested object, which the app is expected to answer with decline; the reply reports the decline.</summary>
    [Fact(Timeout = 30000)]
    public async Task ElicitUnsupported_Declined_SendsTheNestedObjectSchemaAndReportsDecline()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Client client = await Client.StartAsync(advertiseForm: true, ct);

        TurnResult turn = await client.RunTurnAsync(["[elicit:unsupported]"], id => ResultLine(id, """{"action":"decline"}"""), ct);

        JsonObject parameters = RequireObject(RequireRequest(turn)["params"]);
        JsonObject schema = RequireObject(parameters["requestedSchema"]);
        Assert.Equal("form", (string?)parameters["mode"]);
        Assert.Equal("Where should we deliver?", (string?)parameters["message"]);
        Assert.False(parameters.ContainsKey("toolCallId"), parameters.ToJsonString());
        Assert.Equal(["address:object"], PropertyKeyAndTypes(schema));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(UnsupportedSchemaJson), schema), schema.ToJsonString());
        Assert.Equal("Got action=decline content=none", turn.Reply);
        Assert.Equal("end_turn", turn.StopReason);
    }

    /// <summary>When the client never advertised <c>elicitation.form</c>, every marker sends nothing on the wire and the reply says elicitation is unavailable and why; the turn ends normally.</summary>
    [Theory(Timeout = 30000)]
    [InlineData("[elicit:ask]")]
    [InlineData("[elicit:one]")]
    [InlineData("[elicit:form]")]
    [InlineData("[elicit:refusal]")]
    [InlineData("[elicit:unsupported]")]
    public async Task Marker_FormNotAdvertised_RepliesElicitationUnavailableAndSendsNothing(string marker)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Client client = await Client.StartAsync(advertiseForm: false, ct);

        TurnResult turn = await client.RunTurnAsync([marker], answer: null, ct);

        Assert.Null(turn.Request);
        Assert.Equal(NotAdvertisedReply, turn.Reply);
        Assert.True(turn.Chunks.Count > 1, $"Expected the reply in more than one chunk, got {turn.Chunks.Count}.");
        Assert.Equal("end_turn", turn.StopReason);
    }

    /// <summary>A client that answers the form with a JSON-RPC error gets a reply that states the error code and message after "Elicitation unavailable:".</summary>
    [Fact(Timeout = 30000)]
    public async Task Marker_ClientAnswersWithAnError_RepliesElicitationUnavailableWithTheError()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Client client = await Client.StartAsync(advertiseForm: true, ct);

        TurnResult turn = await client.RunTurnAsync(
            ["[elicit:form]"],
            id => ErrorLine(id, -32601, "Method not found: elicitation/create"),
            ct);

        Assert.Equal("Elicitation unavailable: error -32601: Method not found: elicitation/create", turn.Reply);
        Assert.Equal("end_turn", turn.StopReason);
    }

    /// <summary>A marker in any text block of the prompt counts, and the echo of the prompt is replaced by the reply, so the prompt's own words never appear in it.</summary>
    [Fact(Timeout = 30000)]
    public async Task Marker_InTheSecondTextBlock_Elicits_AndTheReplyDoesNotEchoThePrompt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Client client = await Client.StartAsync(advertiseForm: true, ct);

        TurnResult turn = await client.RunTurnAsync(
            ["first block of words", "second [elicit:refusal] block"],
            id => ResultLine(id, """{"action":"decline"}"""),
            ct);

        Assert.NotNull(turn.Request);
        Assert.Equal("Got action=decline content=none", turn.Reply);
    }

    /// <summary>When a prompt holds two markers, the one that appears first in the text decides which form is sent, even when it is not first in the marker list.</summary>
    [Fact(Timeout = 30000)]
    public async Task Marker_TwoInOnePrompt_TheEarlierInTheTextWins()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Client client = await Client.StartAsync(advertiseForm: true, ct);

        TurnResult turn = await client.RunTurnAsync(
            ["[elicit:form] then [elicit:ask]"],
            id => ResultLine(id, """{"action":"decline"}"""),
            ct);

        JsonObject parameters = RequireObject(RequireRequest(turn)["params"]);
        Assert.Equal("Plan the delivery", (string?)parameters["message"]);
    }

    /// <summary>A prompt with no marker echoes exactly as before: no request on the wire, the words back in three chunks, end_turn.</summary>
    [Fact(Timeout = 30000)]
    public async Task NoMarker_EchoesThePromptInThreeChunks()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Client client = await Client.StartAsync(advertiseForm: true, ct);

        TurnResult turn = await client.RunTurnAsync(["hello there world"], answer: null, ct);

        Assert.Null(turn.Request);
        Assert.Equal(["hello", "there", "world"], turn.Chunks);
        Assert.Equal("end_turn", turn.StopReason);
    }

    /// <summary>Text that only resembles a marker (wrong case, no closing bracket, an unknown name) is not one: the prompt is echoed and nothing is asked.</summary>
    [Theory(Timeout = 30000)]
    [InlineData("[Elicit:ask] hi there", "[Elicit:ask]", "hi", "there")]
    [InlineData("[elicit:ASK] hi there", "[elicit:ASK]", "hi", "there")]
    [InlineData("[elicit:ask hi there", "[elicit:ask", "hi", "there")]
    [InlineData("[elicit:other] x y", "[elicit:other]", "x", "y")]
    public async Task NearMissMarker_IsEchoedNotElicited(string prompt, string first, string second, string third)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Client client = await Client.StartAsync(advertiseForm: true, ct);

        TurnResult turn = await client.RunTurnAsync([prompt], answer: null, ct);

        Assert.Null(turn.Request);
        Assert.Equal([first, second, third], turn.Chunks);
        Assert.Equal("end_turn", turn.StopReason);
    }

    /// <summary>A session/cancel while the form waits ends the turn with stopReason cancelled and streams no reply, even though the client never answers the form.</summary>
    [Fact(Timeout = 30000)]
    public async Task SessionCancel_WhileTheFormWaits_EndsTheTurnCancelledWithNoReply()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Client client = await Client.StartAsync(advertiseForm: true, ct);
        int turnId = await client.SendPromptAsync(["[elicit:ask]"], ct);
        JsonObject request = await client.ReceiveAsync(ct);
        Assert.Equal("elicitation/create", (string?)request["method"]);

        await client.SendAsync("""{"jsonrpc":"2.0","method":"session/cancel","params":{"sessionId":"%SESSION%"}}""".Replace("%SESSION%", client.SessionId, StringComparison.Ordinal), ct);
        TurnResult turn = await client.CompleteTurnAsync(turnId, answer: null, ct);

        Assert.Equal("cancelled", turn.StopReason);
        Assert.Empty(turn.Chunks);
    }

    /// <summary>One JSON-RPC success line answering request <paramref name="id"/>.</summary>
    private static string ResultLine(int id, string resultJson)
    {
        JsonObject line = new()
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["result"] = JsonNode.Parse(resultJson),
        };
        return line.ToJsonString();
    }

    /// <summary>One JSON-RPC error line answering request <paramref name="id"/>.</summary>
    private static string ErrorLine(int id, int code, string message)
    {
        JsonObject line = new()
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["error"] = new JsonObject { ["code"] = code, ["message"] = message },
        };
        return line.ToJsonString();
    }

    /// <summary>The <c>elicitation/create</c> request of <paramref name="turn"/>, failing the test when the mock sent none.</summary>
    private static JsonObject RequireRequest(TurnResult turn)
    {
        Assert.NotNull(turn.Request);
        Assert.Equal("elicitation/create", (string?)turn.Request["method"]);
        return turn.Request;
    }

    /// <summary><paramref name="node"/> as an object, failing the test when it is not one.</summary>
    private static JsonObject RequireObject(JsonNode? node)
    {
        JsonObject? result = node as JsonObject;
        Assert.NotNull(result);
        return result;
    }

    /// <summary><paramref name="node"/> as an array, failing the test when it is not one.</summary>
    private static JsonArray RequireArray(JsonNode? node)
    {
        JsonArray? result = node as JsonArray;
        Assert.NotNull(result);
        return result;
    }

    /// <summary>The property names of a form schema, in the order sent.</summary>
    private static List<string> PropertyKeys(JsonObject schema)
    {
        return RequireObject(schema["properties"]).Select(property => property.Key).ToList();
    }

    /// <summary>Each property of a form schema as <c>name:type</c>, in the order sent.</summary>
    private static List<string> PropertyKeyAndTypes(JsonObject schema)
    {
        return RequireObject(schema["properties"]).Select(property => property.Key + ":" + (string?)property.Value?["type"]).ToList();
    }

    /// <summary>What one prompt turn produced as seen by the client: the form request (if any), the streamed reply chunks and the stop reason.</summary>
    private sealed record TurnResult(JsonObject? Request, IReadOnlyList<string> Chunks, string? StopReason)
    {
        /// <summary>Gets the reply chunks joined exactly as the Transcript would join them.</summary>
        internal string Reply => string.Concat(this.Chunks);
    }

    /// <summary>The client half: handshakes, sends prompts and reads the turn, with every wait bounded.</summary>
    private sealed class Client : IAsyncDisposable
    {
        private readonly Stream stream;

        private readonly StreamReader reader;

        private readonly FakeAcpAgent agent;

        private readonly Task agentRun;

        private int nextId = 100;

        private Client(Stream stream, FakeAcpAgent agent, Task agentRun)
        {
            this.stream = stream;
            this.agent = agent;
            this.agentRun = agentRun;
            this.reader = new StreamReader(stream, Utf8NoBom, false, 1024, leaveOpen: true);
        }

        /// <summary>Gets the session id the mock minted for this client.</summary>
        internal string SessionId { get; private set; } = string.Empty;

        /// <summary>Starts a mock over an in-proc pair and performs initialize and session/new, advertising <c>elicitation.form</c> or not.</summary>
        internal static async Task<Client> StartAsync(bool advertiseForm, CancellationToken ct)
        {
            (Stream clientStream, Stream agentStream) = FullDuplexStream.CreatePair();
            FakeAcpAgent agent = new(agentStream);
            agent.OnPrompt = MockBehaviour.ChunkedEchoAsync;
            Task agentRun = agent.RunAsync(ct);
            Client client = new(clientStream, agent, agentRun);

            string capabilities = advertiseForm ? """{"elicitation":{"form":{}}}""" : "{}";
            await client.SendAsync("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":1,"clientCapabilities":""" + capabilities + "}}", ct);
            await client.ReceiveResponseAsync(1, ct);
            await client.SendAsync("""{"jsonrpc":"2.0","id":2,"method":"session/new","params":{}}""", ct);
            JsonObject session = await client.ReceiveResponseAsync(2, ct);
            client.SessionId = (string?)session["result"]?["sessionId"] ?? throw new InvalidOperationException("session/new returned no sessionId: " + session.ToJsonString());
            return client;
        }

        /// <summary>Sends one prompt made of <paramref name="textBlocks"/> and returns its request id.</summary>
        internal async Task<int> SendPromptAsync(string[] textBlocks, CancellationToken ct)
        {
            int id = Interlocked.Increment(ref this.nextId);
            JsonArray blocks = new();
            foreach (string text in textBlocks)
            {
                blocks.Add(new JsonObject { ["type"] = "text", ["text"] = text });
            }

            JsonObject request = new()
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["method"] = "session/prompt",
                ["params"] = new JsonObject { ["sessionId"] = this.SessionId, ["prompt"] = blocks },
            };
            await this.SendAsync(request.ToJsonString(), ct);
            return id;
        }

        /// <summary>Sends a prompt and reads its turn to the end, answering any <c>elicitation/create</c> with the line <paramref name="answer"/> builds from the request id (or leaving it unanswered when null).</summary>
        internal async Task<TurnResult> RunTurnAsync(string[] textBlocks, Func<int, string>? answer, CancellationToken ct)
        {
            int turnId = await this.SendPromptAsync(textBlocks, ct);
            return await this.CompleteTurnAsync(turnId, answer, ct);
        }

        /// <summary>Reads messages until the result of prompt <paramref name="turnId"/>, collecting the form request and the reply chunks along the way.</summary>
        internal async Task<TurnResult> CompleteTurnAsync(int turnId, Func<int, string>? answer, CancellationToken ct)
        {
            JsonObject? request = null;
            List<string> chunks = [];
            while (true)
            {
                JsonObject message = await this.ReceiveAsync(ct);
                string? method = (string?)message["method"];

                if (string.Equals(method, "elicitation/create", StringComparison.Ordinal))
                {
                    request = message;
                    if (answer is not null)
                    {
                        int requestId = (int?)message["id"] ?? throw new InvalidOperationException("The request has no id: " + message.ToJsonString());
                        await this.SendAsync(answer(requestId), ct);
                    }

                    continue;
                }

                if (method is null && (int?)message["id"] == turnId)
                {
                    return new TurnResult(request, chunks, (string?)message["result"]?["stopReason"]);
                }

                JsonObject? update = message["params"]?["update"] as JsonObject;
                if (string.Equals(method, "session/update", StringComparison.Ordinal)
                    && string.Equals((string?)update?["sessionUpdate"], "agent_message_chunk", StringComparison.Ordinal))
                {
                    chunks.Add((string?)update?["content"]?["text"] ?? string.Empty);
                }
            }
        }

        /// <summary>Writes one newline-terminated frame.</summary>
        internal async Task SendAsync(string json, CancellationToken ct)
        {
            byte[] bytes = Utf8NoBom.GetBytes(json + "\n");
            await this.stream.WriteAsync(bytes, ct);
            await this.stream.FlushAsync(ct);
        }

        /// <summary>Reads the next frame, failing the test when none arrives within the bound.</summary>
        internal async Task<JsonObject> ReceiveAsync(CancellationToken ct)
        {
            string? line = null;
            try
            {
                line = await this.reader.ReadLineAsync(ct).AsTask().WaitAsync(Bound, ct);
            }
            catch (TimeoutException)
            {
                Assert.Fail($"No frame arrived from mock-acp within {Bound.TotalSeconds:0} seconds.");
            }

            Assert.NotNull(line);
            return JsonNode.Parse(line) as JsonObject ?? throw new InvalidOperationException("Received a line that is not a JSON object: " + line);
        }

        /// <summary>Reads frames until the response with <paramref name="id"/> arrives.</summary>
        internal async Task<JsonObject> ReceiveResponseAsync(int id, CancellationToken ct)
        {
            while (true)
            {
                JsonObject message = await this.ReceiveAsync(ct);
                if (message["method"] is null && (int?)message["id"] == id)
                {
                    return message;
                }
            }
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            this.reader.Dispose();
            await this.stream.DisposeAsync();
            await this.agentRun.WaitAsync(Bound);
            await this.agent.DisposeAsync();
        }
    }
}

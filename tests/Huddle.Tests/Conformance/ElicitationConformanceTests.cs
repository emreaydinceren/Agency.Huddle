using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.Acp.Tests.Fakes;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Elicitation;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using static Agency.Huddle.Tests.Elicitation.ElicitationTestSupport;

namespace Agency.Huddle.Tests.Conformance;

/// <summary>
/// Elicitation bridge, end to end (decisions E-1 to E-6, the D4 conformance bullets): drives the real
/// <c>DotAcpAgentHostFactory</c> and host, the real <see cref="PersonaRunner"/> and Room Session, the real
/// <see cref="IElicitationBridge"/>, <see cref="ElicitationStore"/>, <see cref="ElicitationService"/> and
/// <see cref="ChatService"/>, against a scripted <see cref="FakeAcpAgent"/> that sends the very
/// <c>elicitation/create</c> requests the Claude Adapter sends. Each test reads what the agent got back on
/// the wire, what the Room showed and what the Transcript holds. Every wait is bounded at five seconds and
/// fails the test rather than hanging it, and time moves only by hand.
/// </summary>
/// <remarks>
/// Every test turns <c>Acp:AdvertiseElicitation</c> on explicitly, so none of them depends on the shipped
/// default: the fake agent refuses to send <c>elicitation/create</c> unless <c>initialize</c> advertised
/// <c>elicitation.form</c>, exactly as the real Adapter would not send it.
/// </remarks>
public sealed class ElicitationConformanceTests
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(5);

    /// <summary>The configuration key that switches advertising on, named from the option so a rename cannot leave a test advertising nothing.</summary>
    private static readonly string AdvertiseKey = "Team:Acp:" + nameof(AcpOptions.AdvertiseElicitation);

    /// <summary>An MCP server's form: a date, an integer, a boolean and an enum, with the boolean required.</summary>
    private const string McpTypedSchema = """{"type":"object","properties":{"due":{"type":"string","title":"Due","format":"date"},"count":{"type":"integer","title":"Count","minimum":1,"maximum":10},"agree":{"type":"boolean","title":"I agree"},"color":{"type":"string","title":"Color","enum":["red","green"]}},"required":["agree"]}""";

    /// <summary>A form the bridge cannot show faithfully: a property that is itself an object.</summary>
    private const string NestedObjectSchema = """{"type":"object","properties":{"nested":{"type":"object","properties":{"a":{"type":"string"}}}}}""";

    private static readonly FormAsk AskUserQuestion = new(SingleQuestionMessage, SingleQuestionSchema, "toolu_01AskUserQuestion");
    private static readonly FormAsk RefusalDialog = new(RefusalMessage, RefusalSchema, null);
    private static readonly FormAsk McpTypedForm = new("Where should the report go?", McpTypedSchema, null);
    private static readonly FormAsk UnsupportedForm = new("Which one?", NestedObjectSchema, null);

    /// <summary>
    /// The whole path: an AskUserQuestion-shaped request shows a card in the Room, the Human's answer reaches
    /// the agent as exactly <c>{action:accept,content:{question_0:...}}</c>, the Transcript gets the answer as
    /// a Message from the Human (the quoted question, a blank line, the answer) before the asker's own, and
    /// the asker never receives the answer as a delivery - its Turn is still open and a delivery would start a
    /// second one, so the fake agent sees exactly the two prompts the test itself caused.
    /// </summary>
    [Fact]
    public async Task AskUserQuestion_Answered_WireContentExact_MessageInTranscript_ExactlyOnePrompt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Scenario scenario = await Scenario.StartAsync(AskUserQuestion, leaveOpen: false, ct);

        await scenario.PostHumanAsync("pick a database", ct);
        PendingElicitation card = await scenario.NextCardAsync(ct);
        Assert.Equal(scenario.AgentId, card.AskerAgentId);
        string? posted = await scenario.Service.AnswerAsync(scenario.RoomId, card.Id, Values(Pair("question_0", "SQLite")), ct);
        JsonObject wire = await scenario.WireAsync(ct);

        Assert.Equal("> Which database?\n\nSQLite", posted);
        Assert.Equal("""{"action":"accept","content":{"question_0":"SQLite"}}""", wire.ToJsonString());

        // The Turn finishes on the answer, then a sentinel Message goes through the same queue: an answer
        // wrongly delivered to the asker would be a prompt of its own, queued ahead of the sentinel.
        await scenario.WaitForAgentRepliesAsync(1, ct);
        await scenario.PostHumanAsync("sentinel", ct);
        await scenario.WaitForAgentRepliesAsync(2, ct);

        Assert.Equal(
            ["Human: pick a database", "Human: > Which database?\n\nSQLite", "Agent: noted", "Human: sentinel", "Agent: ok"],
            await scenario.TranscriptAsync(ct));
        IReadOnlyList<string> prompts = scenario.PromptTexts();
        Assert.Equal(2, prompts.Count);
        Assert.DoesNotContain(prompts, prompt => prompt.Contains("SQLite", StringComparison.Ordinal));
        Assert.Empty(scenario.Store.Get(scenario.RoomId));
    }

    /// <summary>Skipping the card answers the agent <c>decline</c> with no content, posts nothing, and removes the card; the Turn goes on.</summary>
    [Fact]
    public async Task Skip_AnswersDecline_PostsNothing_RemovesTheCard()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Scenario scenario = await Scenario.StartAsync(AskUserQuestion, leaveOpen: false, ct);
        await scenario.PostHumanAsync("pick a database", ct);
        PendingElicitation card = await scenario.NextCardAsync(ct);

        bool declined = await scenario.Service.DeclineAsync(scenario.RoomId, card.Id);
        JsonObject wire = await scenario.WireAsync(ct);
        await scenario.WaitForAgentRepliesAsync(1, ct);

        Assert.True(declined);
        Assert.Equal("""{"action":"decline"}""", wire.ToJsonString());
        Assert.Equal(["Human: pick a database", "Agent: noted"], await scenario.TranscriptAsync(ct));
        Assert.Empty(scenario.Store.Get(scenario.RoomId));
    }

    /// <summary>A Stop of the Turn the card belongs to answers the agent <c>cancel</c>, removes the card from the Room, and reaches the Adapter as a <c>session/cancel</c>.</summary>
    [Fact]
    public async Task Stop_AnswersCancel_RemovesTheCard()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Scenario scenario = await Scenario.StartAsync(AskUserQuestion, leaveOpen: false, ct);
        await scenario.PostHumanAsync("pick a database", ct);
        _ = await scenario.NextCardAsync(ct);

        await scenario.Gateway.StopTurnAsync(scenario.AgentId, scenario.RoomId, ct);
        JsonObject wire = await scenario.WireAsync(ct);
        JsonObject cancel = await scenario.Fixture.Agent.WaitForAsync("session/cancel", Bound);

        Assert.Equal("""{"action":"cancel"}""", wire.ToJsonString());
        Assert.Empty(scenario.Store.Get(scenario.RoomId));
        Assert.Equal("2.0", (string?)cancel["jsonrpc"]);
    }

    /// <summary>Archiving the Room answers the waiting agent <c>cancel</c> and removes the card: an archived Room hides its cards, so a card left behind would hold the Turn open for nobody.</summary>
    [Fact]
    public async Task RoomArchived_AnswersCancel_RemovesTheCard()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Scenario scenario = await Scenario.StartAsync(AskUserQuestion, leaveOpen: false, ct);
        await scenario.PostHumanAsync("pick a database", ct);
        _ = await scenario.NextCardAsync(ct);

        _ = await scenario.Chat.SetRoomArchivedAsync(scenario.RoomId, true, ct);
        JsonObject wire = await scenario.WireAsync(ct);

        Assert.Equal("""{"action":"cancel"}""", wire.ToJsonString());
        Assert.Empty(scenario.Store.Get(scenario.RoomId));
    }

    /// <summary>Deleting the Room answers the waiting agent <c>cancel</c> and removes the card.</summary>
    [Fact]
    public async Task RoomDeleted_AnswersCancel_RemovesTheCard()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Scenario scenario = await Scenario.StartAsync(AskUserQuestion, leaveOpen: false, ct);
        await scenario.PostHumanAsync("pick a database", ct);
        _ = await scenario.NextCardAsync(ct);

        await scenario.Chat.DeleteRoomAsync(scenario.RoomId, ct);
        JsonObject wire = await scenario.WireAsync(ct);

        Assert.Equal("""{"action":"cancel"}""", wire.ToJsonString());
        Assert.Empty(scenario.Store.Get(scenario.RoomId));
    }

    /// <summary>
    /// A Message the Human types answers the waiting agent <c>cancel</c> and removes the card, and it is an
    /// ordinary Message: it starts a Turn of its own, so the fake agent sees both prompts.
    /// </summary>
    [Fact]
    public async Task TypedHumanMessage_AnswersCancel_RemovesTheCard()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Scenario scenario = await Scenario.StartAsync(AskUserQuestion, leaveOpen: false, ct);
        await scenario.PostHumanAsync("pick a database", ct);
        _ = await scenario.NextCardAsync(ct);

        await scenario.PostHumanAsync("never mind", ct);
        JsonObject wire = await scenario.WireAsync(ct);
        await scenario.WaitForAgentRepliesAsync(2, ct);

        Assert.Equal("""{"action":"cancel"}""", wire.ToJsonString());
        Assert.Empty(scenario.Store.Get(scenario.RoomId));
        Assert.Equal(["Human: pick a database", "Human: never mind", "Agent: noted", "Agent: ok"], await scenario.TranscriptAsync(ct));
        Assert.Equal(2, scenario.PromptTexts().Count);
    }

    /// <summary>
    /// A card nobody answers is dropped when <c>Acp:UserInputTimeoutSeconds</c> runs out, and not a second
    /// sooner: the agent gets <c>cancel</c> and the Room loses the card. The bound is 30 seconds, the
    /// smallest the option allows, on a clock the test moves by hand.
    /// </summary>
    [Fact]
    public async Task UserInputTimeout_AnswersCancel_RemovesTheCard()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TrackedFakeTimeProvider clock = new();
        Dictionary<string, string?> config = new(StringComparer.Ordinal) { ["Team:Acp:UserInputTimeoutSeconds"] = "30" };
        await using Scenario scenario = await Scenario.StartAsync(AskUserQuestion, leaveOpen: false, ct, config, clock);
        await scenario.PostHumanAsync("pick a database", ct);
        _ = await scenario.NextCardAsync(ct);

        clock.Advance(TimeSpan.FromSeconds(29));
        Assert.False(scenario.Script.Wire.Task.IsCompleted);
        Assert.Single(scenario.Store.Get(scenario.RoomId));

        clock.Advance(TimeSpan.FromSeconds(1));
        JsonObject wire = await scenario.WireAsync(ct);

        Assert.Equal("""{"action":"cancel"}""", wire.ToJsonString());
        Assert.Empty(scenario.Store.Get(scenario.RoomId));
    }

    /// <summary>
    /// The agent ends its Turn while a form is still open (it stopped waiting): the Room Session cancels the
    /// request itself, so the card goes and the request is answered <c>cancel</c> instead of lingering for a
    /// Turn that no longer exists.
    /// </summary>
    [Fact]
    public async Task TurnEndsWithCardOpen_AnswersCancel_RemovesTheCard()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Scenario scenario = await Scenario.StartAsync(AskUserQuestion, leaveOpen: true, ct);
        await scenario.PostHumanAsync("pick a database", ct);
        _ = await scenario.NextCardAsync(ct);

        scenario.Script.EndTurn.TrySetResult();
        JsonObject wire = await scenario.WireAsync(ct);

        Assert.Equal("""{"action":"cancel"}""", wire.ToJsonString());
        Assert.Empty(scenario.Store.Get(scenario.RoomId));
    }

    /// <summary>
    /// A card open for longer than <c>Acp:TurnIdleTimeoutSeconds</c> does not get the Turn killed (gate G1):
    /// three bounds of silence pass while the Human is being asked, nothing is cancelled at the Adapter, and
    /// once the Human answers the Turn completes and posts its reply as normal.
    /// </summary>
    [Fact]
    public async Task WatchdogPaused_WhileACardIsOpen_TurnCompletesAfterTheAnswer()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TrackedFakeTimeProvider clock = new();
        Dictionary<string, string?> config = new(StringComparer.Ordinal) { ["Team:Acp:TurnIdleTimeoutSeconds"] = "5" };
        await using Scenario scenario = await Scenario.StartAsync(AskUserQuestion, leaveOpen: false, ct, config, clock);
        await scenario.PostHumanAsync("pick a database", ct);
        PendingElicitation card = await scenario.NextCardAsync(ct);

        clock.Advance(TimeSpan.FromSeconds(5));
        clock.Advance(TimeSpan.FromSeconds(5));
        clock.Advance(TimeSpan.FromSeconds(5));
        _ = await scenario.Service.AnswerAsync(scenario.RoomId, card.Id, Values(Pair("question_0", "Postgres")), ct);
        JsonObject wire = await scenario.WireAsync(ct);
        await scenario.WaitForAgentRepliesAsync(1, ct);

        Assert.Equal("""{"action":"accept","content":{"question_0":"Postgres"}}""", wire.ToJsonString());
        Assert.DoesNotContain(scenario.Fixture.Agent.Received, message => string.Equals((string?)message["method"], "session/cancel", StringComparison.Ordinal));
        Assert.Equal(
            ["Human: pick a database", "Human: > Which database?\n\nPostgres", "Agent: noted"],
            await scenario.TranscriptAsync(ct));
    }

    /// <summary>
    /// The refusal-fallback dialog (no tool call id, one <c>choice</c> field) round-trips: whichever of its two
    /// results the Human picks goes back as <c>{choice:...}</c> accepted, and the Transcript quotes the field
    /// and the option's label.
    /// </summary>
    /// <param name="choice">The result constant picked.</param>
    /// <param name="label">That option's label, which the Transcript shows.</param>
    [Theory]
    [InlineData("retry_fallback", "Retry with Opus")]
    [InlineData("cancelled", "Keep the refusal")]
    public async Task RefusalDialog_RoundTripsTheChoice(string choice, string label)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Scenario scenario = await Scenario.StartAsync(RefusalDialog, leaveOpen: false, ct);
        await scenario.PostHumanAsync("do the thing", ct);
        PendingElicitation card = await scenario.NextCardAsync(ct);

        string? posted = await scenario.Service.AnswerAsync(scenario.RoomId, card.Id, Values(Pair("choice", choice)), ct);
        JsonObject wire = await scenario.WireAsync(ct);

        Assert.Equal("{\"action\":\"accept\",\"content\":{\"choice\":\"" + choice + "\"}}", wire.ToJsonString());
        Assert.Equal("> choice\n\n" + label, posted);
        Assert.Equal(RefusalMessage, card.Form.Message);
    }

    /// <summary>
    /// An MCP-style typed form (a date, an integer, a boolean, an enum) round-trips with typed wire content: the
    /// integer is a JSON number and the boolean a JSON boolean, never the text the Human typed, because a server
    /// rejects a string where it asked for a number.
    /// </summary>
    [Fact]
    public async Task McpTypedForm_RoundTripsTypedContent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Scenario scenario = await Scenario.StartAsync(McpTypedForm, leaveOpen: false, ct);
        await scenario.PostHumanAsync("make the report", ct);
        PendingElicitation card = await scenario.NextCardAsync(ct);

        string? posted = await scenario.Service.AnswerAsync(
            scenario.RoomId,
            card.Id,
            Values(Pair("due", "2026-10-01"), Pair("count", "3"), Pair("agree", "true"), Pair("color", "green")),
            ct);
        JsonObject wire = await scenario.WireAsync(ct);

        Assert.Equal("""{"action":"accept","content":{"due":"2026-10-01","count":3,"agree":true,"color":"green"}}""", wire.ToJsonString());
        Assert.Equal("> Due\n\n2026-10-01\n\n> Count\n\n3\n\n> I agree\n\nYes\n\n> Color\n\ngreen", posted);
    }

    /// <summary>
    /// A schema the bridge cannot show faithfully is declined at once: the agent gets <c>decline</c>, the Room
    /// never raises a card change, and nothing waits in the store.
    /// </summary>
    [Fact]
    public async Task UnsupportedSchema_AnswersDecline_AndNeverShowsACard()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Scenario scenario = await Scenario.StartAsync(UnsupportedForm, leaveOpen: false, ct);
        int cardChanges = 0;
        void OnElicitationsChanged(string changedRoomId)
        {
            _ = Interlocked.Increment(ref cardChanges);
        }

        scenario.Events.ElicitationsChanged += OnElicitationsChanged;
        try
        {
            await scenario.PostHumanAsync("pick one", ct);
            JsonObject wire = await scenario.WireAsync(ct);
            await scenario.WaitForAgentRepliesAsync(1, ct);

            Assert.Equal("""{"action":"decline"}""", wire.ToJsonString());
            Assert.Equal(0, Volatile.Read(ref cardChanges));
            Assert.Empty(scenario.Store.Get(scenario.RoomId));
        }
        finally
        {
            scenario.Events.ElicitationsChanged -= OnElicitationsChanged;
        }
    }

    /// <summary>A request that names a session the host never opened is answered <c>cancel</c>: there is no Room to ask in, and the Adapter must never be left waiting.</summary>
    [Fact]
    public async Task UnknownSession_AnswersCancel()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Dictionary<string, string?> config = new(StringComparer.Ordinal) { [AdvertiseKey] = "true" };
        await using MockAdapterFixture fixture = await MockAdapterFixture.StartAsync(new Persona("nova", "You are Nova."), config, cancellationToken: ct);
        JsonObject parameters = new()
        {
            ["sessionId"] = "no-such-session",
            ["mode"] = "form",
            ["message"] = "Which one?",
            ["requestedSchema"] = JsonNode.Parse(SingleQuestionSchema),
        };

        JsonObject result = await fixture.Agent.SendRequestAsync("elicitation/create", parameters).WaitAsync(Bound, ct);

        Assert.Equal("""{"action":"cancel"}""", result.ToJsonString());
    }

    /// <summary>One form the scripted agent asks for: its prompt text, its schema as JSON text and the tool call it says triggered it.</summary>
    /// <param name="Message">The prompt text.</param>
    /// <param name="Schema">The form's JSON Schema.</param>
    /// <param name="ToolCallId">The triggering tool call id, or null for none.</param>
    private sealed record FormAsk(string Message, string Schema, string? ToolCallId);

    /// <summary>
    /// What the scripted agent does on each <c>session/prompt</c>: the first one asks <see cref="FormAsk"/> and
    /// the rest simply reply. It either waits for the answer and replies (<c>noted</c>), or, when it leaves the
    /// request open, ends its Turn only when <see cref="EndTurn"/> is completed.
    /// </summary>
    /// <param name="ask">The form the first prompt asks for, or null to ask nothing.</param>
    /// <param name="leaveOpen">Whether the first prompt ends without waiting for the answer.</param>
    private sealed class Script(FormAsk? ask, bool leaveOpen)
    {
        private int promptCalls;

        /// <summary>Completed with what the client answered the form on the wire, or faulted when the request failed.</summary>
        internal TaskCompletionSource<JsonObject> Wire { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Completed by a test to let a prompt that left its request open end its Turn.</summary>
        internal TaskCompletionSource EndTurn { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Runs one <c>session/prompt</c>.</summary>
        /// <param name="context">The prompt in flight.</param>
        internal async Task<string> RunAsync(PromptContext context)
        {
            int call = Interlocked.Increment(ref this.promptCalls);
            if (call > 1 || ask is null)
            {
                await context.SendTextChunkAsync("ok");
                return "end_turn";
            }

            try
            {
                JsonObject schema = JsonNode.Parse(ask.Schema) as JsonObject
                    ?? throw new InvalidOperationException("The scripted schema is not a JSON object.");
                Task<JsonObject> asking = context.RequestElicitationAsync(ask.Message, schema, ask.ToolCallId);
                if (leaveOpen)
                {
                    _ = this.CaptureAsync(asking);
                    await this.EndTurn.Task;
                    return "end_turn";
                }

                _ = this.Wire.TrySetResult(await asking);
                await context.SendTextChunkAsync("noted");
                return "end_turn";
            }
            catch (Exception ex)
            {
                // The fake runs this on a thread-pool task that swallows anything but its own RPC error,
                // so the failure goes to the test, which is waiting on Wire.
                _ = this.Wire.TrySetException(ex);
                return "end_turn";
            }
        }

        private async Task CaptureAsync(Task<JsonObject> asking)
        {
            try
            {
                _ = this.Wire.TrySetResult(await asking);
            }
            catch (Exception ex)
            {
                _ = this.Wire.TrySetException(ex);
            }
        }
    }

    /// <summary>
    /// A started <see cref="MockAdapterFixture"/> with the Persona's own Agent id and direct Room resolved and
    /// the services a test reaches for, plus a log of every Message posted anywhere.
    /// </summary>
    private sealed class Scenario : IAsyncDisposable
    {
        private readonly Lock gate = new();
        private readonly List<ChatMessage> posted = [];

        private Scenario(MockAdapterFixture fixture, Script script, string agentId, string roomId)
        {
            this.Fixture = fixture;
            this.Script = script;
            this.AgentId = agentId;
            this.RoomId = roomId;
            this.Events = fixture.Services.GetRequiredService<RoomEvents>();
            this.Events.MessagePosted += this.OnMessagePosted;
        }

        internal MockAdapterFixture Fixture { get; }

        internal Script Script { get; }

        internal string AgentId { get; }

        internal string RoomId { get; }

        internal RoomEvents Events { get; }

        internal ChatService Chat => this.Fixture.Services.GetRequiredService<ChatService>();

        internal ElicitationStore Store => this.Fixture.Services.GetRequiredService<ElicitationStore>();

        internal ElicitationService Service => this.Fixture.Services.GetRequiredService<ElicitationService>();

        internal IAgentGateway Gateway => this.Fixture.Services.GetRequiredService<IAgentGateway>();

        /// <summary>Starts a fixture that advertises elicitation, scripted to ask <paramref name="ask"/> on its first prompt.</summary>
        /// <param name="ask">The form the first prompt asks for.</param>
        /// <param name="leaveOpen">Whether the first prompt ends without waiting for the answer.</param>
        /// <param name="ct">Cancels startup.</param>
        /// <param name="extraConfig">Configuration applied after the fixture's own, or null for none.</param>
        /// <param name="clock">The clock the Persona's runner uses, or null for the real one.</param>
        internal static async Task<Scenario> StartAsync(
            FormAsk ask,
            bool leaveOpen,
            CancellationToken ct,
            IReadOnlyDictionary<string, string?>? extraConfig = null,
            TimeProvider? clock = null)
        {
            Script script = new(ask, leaveOpen);
            Dictionary<string, string?> config = new(StringComparer.Ordinal) { [AdvertiseKey] = "true" };
            if (extraConfig is not null)
            {
                foreach (KeyValuePair<string, string?> pair in extraConfig)
                {
                    config[pair.Key] = pair.Value;
                }
            }

            MockAdapterFixture fixture = await MockAdapterFixture.StartAsync(
                new Persona("nova", "You are Nova."),
                config,
                clock,
                agent => agent.OnPrompt = script.RunAsync,
                ct);
            try
            {
                ITeamDirectory directory = fixture.Services.GetRequiredService<ITeamDirectory>();
                User? nova = await directory.FindUserByNameAsync("nova", ct);
                Assert.NotNull(nova);
                Room? room = await directory.FindRoomWithExactMembersAsync(KnownIds.Human, nova.Id, ct);
                Assert.NotNull(room);
                return new Scenario(fixture, script, nova.Id, room.Id);
            }
            catch
            {
                await fixture.DisposeAsync();
                throw;
            }
        }

        /// <summary>Posts <paramref name="text"/> into the Room as the Human.</summary>
        /// <param name="text">The Message text.</param>
        /// <param name="ct">Cancels the post.</param>
        internal async Task PostHumanAsync(string text, CancellationToken ct)
        {
            _ = await this.Chat.PostAsync(this.RoomId, KnownIds.Human, text, ct: ct);
        }

        /// <summary>Waits until the Room shows a card, then returns the newest one.</summary>
        /// <param name="ct">Cancels the wait.</param>
        internal async Task<PendingElicitation> NextCardAsync(CancellationToken ct)
        {
            await WaitUntilAsync(() => this.Store.Get(this.RoomId).Count > 0, "a card to appear in the Room", ct);
            return this.Store.Get(this.RoomId)[^1];
        }

        /// <summary>Waits for what the agent received as the answer to its form.</summary>
        /// <param name="ct">Cancels the wait.</param>
        internal async Task<JsonObject> WireAsync(CancellationToken ct)
        {
            try
            {
                return await this.Script.Wire.Task.WaitAsync(Bound, ct);
            }
            catch (TimeoutException)
            {
                Assert.Fail("The agent got no answer to its form within five seconds.");
                throw;
            }
        }

        /// <summary>Waits until the Persona's own Agent has posted <paramref name="count"/> Messages in total.</summary>
        /// <param name="count">How many Agent Messages to wait for.</param>
        /// <param name="ct">Cancels the wait.</param>
        internal async Task WaitForAgentRepliesAsync(int count, CancellationToken ct)
        {
            await WaitUntilAsync(() => this.AgentPosts() >= count, $"{count} Agent Message(s) in the Room", ct);
        }

        /// <summary>The Room's Transcript as one line per Message, <c>Human: text</c> or <c>Agent: text</c>.</summary>
        /// <param name="ct">Cancels the read.</param>
        internal async Task<IReadOnlyList<string>> TranscriptAsync(CancellationToken ct)
        {
            IChatStore store = this.Fixture.Services.GetRequiredService<IChatStore>();
            IReadOnlyList<ChatMessage> history = await store.ReadAllAsync(this.RoomId, ct);
            return [.. history.Select(message => (string.Equals(message.SenderId, KnownIds.Human, StringComparison.Ordinal) ? "Human: " : "Agent: ") + message.Text)];
        }

        /// <summary>The text of every <c>session/prompt</c> the fake agent has received, in arrival order.</summary>
        internal IReadOnlyList<string> PromptTexts()
        {
            return [.. this.Fixture.Agent.Received
                .Where(message => string.Equals((string?)message["method"], "session/prompt", StringComparison.Ordinal))
                .Select(PromptText)];
        }

        public async ValueTask DisposeAsync()
        {
            this.Events.MessagePosted -= this.OnMessagePosted;

            // A prompt that left its request open is held by this gate; a test that failed before it
            // opened the gate must not leave the Turn, and so the teardown, waiting on it for ever.
            _ = this.Script.EndTurn.TrySetResult();
            await this.Fixture.DisposeAsync();
        }

        private static string PromptText(JsonObject message)
        {
            JsonArray? blocks = (message["params"] as JsonObject)?["prompt"] as JsonArray;
            return blocks is null ? string.Empty : string.Concat(blocks.Select(block => (string?)block?["text"]));
        }

        private int AgentPosts()
        {
            lock (this.gate)
            {
                return this.posted.Count(message => string.Equals(message.SenderId, this.AgentId, StringComparison.Ordinal));
            }
        }

        private void OnMessagePosted(MessagePostedEvent messagePosted)
        {
            lock (this.gate)
            {
                this.posted.Add(messagePosted.Message);
            }
        }
    }

    /// <summary>Polls <paramref name="condition"/> until it holds, or fails the test after five seconds.</summary>
    /// <param name="condition">What the test is waiting for.</param>
    /// <param name="what">Names what was awaited, for the failure message.</param>
    /// <param name="ct">Cancels the wait.</param>
    private static async Task WaitUntilAsync(Func<bool> condition, string what, CancellationToken ct)
    {
        DateTime deadline = DateTime.UtcNow + Bound;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail($"Timed out after five seconds waiting for {what}.");
            }

            await Task.Delay(10, ct);
        }
    }
}

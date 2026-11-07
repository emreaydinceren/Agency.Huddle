using System.Text.Json;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Elicitation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// The connector approval prompt (Agent Connectors spec 0, 4, 8.4; T-12, T-13): every call to a claude.ai connector tool stops
/// and asks the Human, and anything but an accepted "allow once" refuses. A false negative sends mail with nobody asked, so each
/// way of not being answered has its own test.
/// </summary>
public sealed class ConnectorApprovalTests
{
    private const string Connector = "mcp__claude_ai_ms365__outlook_send_mail";

    private static readonly IReadOnlyList<PermissionOptionInfo> Offered =
    [
        new PermissionOptionInfo("allow-1", "Allow once", PermissionOptionKind.AllowOnce),
        new PermissionOptionInfo("always-1", "Always allow", PermissionOptionKind.AllowAlways),
        new PermissionOptionInfo("reject-1", "Reject once", PermissionOptionKind.RejectOnce),
    ];

    private static ToolCallInfo Call(string? title, ToolKind kind = ToolKind.Other, string? raw = null, string? toolName = null, string? server = null, string? source = null) =>
        new("call-1", title, kind, ToolCallStatus.Pending, raw, toolName, server, source);

    private static PermissionRequestContext Context(ToolCallInfo call, IElicitationScope? scope, IReadOnlyList<PermissionOptionInfo>? options = null) =>
        new("session-1", call, options ?? Offered, scope);

    private static WorkDirPermissionHandler Handler(string? protectedDirectory = null) =>
        new(NullLogger<WorkDirPermissionHandler>.Instance, [new ProtectedDirectoryGuard(protectedDirectory ?? Path.Combine(Path.GetTempPath(), "huddle-protected"))], personaName: "Nova");

    private static ElicitationAccepted Answer(object value) =>
        new(new Dictionary<string, object> { [ConnectorApprovalForm.DecisionKey] = value });

    /// <summary>A scope whose answer the test controls.</summary>
    private sealed class FakeScope : IElicitationScope, IConnectorTurnLatch
    {
        public List<ElicitationRequest> Asked { get; } = [];

        public TaskCompletionSource<ElicitationResult> Reply { get; set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool ApprovalTimedOut { get; set; }

        public string? ActiveRoomId { get; set; }

        public Exception? Throws { get; set; }

        public async Task<ElicitationResult> ElicitAsync(ElicitationRequest request, CancellationToken cancellationToken)
        {
            this.Asked.Add(request);
            if (this.Throws is not null)
            {
                throw this.Throws;
            }

            using CancellationTokenRegistration registration = cancellationToken.Register(() => this.Reply.TrySetCanceled(cancellationToken));
            return await this.Reply.Task;
        }
    }

    /// <summary>A connector tool is recognised by its name, by its server, or by its title alone.</summary>
    /// <param name="title">The title.</param>
    /// <param name="toolName">The tool name, or null.</param>
    /// <param name="server">The server name, or null.</param>
    [Theory]
    [InlineData("Send mail", Connector, null)]
    [InlineData("Send mail", null, "claude_ai_Gmail")]
    [InlineData("mcp__claude_ai_Gmail__send_message", null, null)]
    [InlineData(null, "MCP__CLAUDE_AI_Gmail__x", null)]
    public void Classifier_Connector_Asks(string? title, string? toolName, string? server) =>
        Assert.True(ConnectorCallClassifier.AsksHuman(Call(title, ToolKind.Other, null, toolName, server)));

    /// <summary>
    /// F3: the Adapter's own titles for a tool that is not a connector (reporters in claude-agent-acp 0.100.0,
    /// <c>dist/tool-calls/reporters</c>: <c>SkillReporter</c> "Load skill[: name]", <c>NotebookEditReporter</c> "NotebookEdit",
    /// <c>AskUserQuestionReporter</c> "Asking for your input", <c>AgentControlReporter</c> "SendMessage", "TaskStop", "ListAgents",
    /// "Monitor") do not ask, though they carry no <c>_meta</c> and are kind Other.
    /// </summary>
    /// <param name="title">The Adapter's title.</param>
    [Theory]
    [InlineData("Load skill")]
    [InlineData("Load skill: pdf")]
    [InlineData("NotebookEdit")]
    [InlineData("Asking for your input")]
    [InlineData("SendMessage")]
    [InlineData("TaskStop")]
    [InlineData("ListAgents")]
    [InlineData("Monitor")]
    public void Classifier_NamelessOther_KnownBuiltInTitle_DoesNotAsk(string title) =>
        Assert.False(ConnectorCallClassifier.AsksHuman(Call(title, ToolKind.Other)));

    /// <summary>F3: an unknown title, an <c>mcp__</c>-shaped one, a look-alike of a built-in title and no title at all still ask (fail safe).</summary>
    /// <param name="title">The Adapter's title, or null.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("Unknown Tool")]
    [InlineData("ToolSearch")]
    [InlineData("mcp__claude_ai_ms365__outlook_send_mail")]
    [InlineData("mcp__workdir__thing")]
    [InlineData("Load skills")]
    [InlineData("NotebookEdit2")]
    [InlineData("ReadMcpResourceTool")]
    public void Classifier_NamelessOther_UnknownTitle_Asks(string? title) =>
        Assert.True(ConnectorCallClassifier.AsksHuman(Call(title, ToolKind.Other)));

    /// <summary>F7: a named tool asks when the server's source is claude.ai, whatever its name; any other source keeps the name rule.</summary>
    [Fact]
    public void Classifier_ClaudeAiSource_AsksWhateverTheName()
    {
        Assert.True(ConnectorCallClassifier.AsksHuman(Call("send", toolName: "mcp__gmail__send_message", server: "gmail", source: "claudeai")));
        Assert.True(ConnectorCallClassifier.AsksHuman(Call("send", toolName: "mcp__gmail__send_message", server: "gmail", source: "ClaudeAI")));
        Assert.False(ConnectorCallClassifier.AsksHuman(Call("send", toolName: "mcp__team__send_message", server: "team", source: "dynamic")));
        Assert.False(ConnectorCallClassifier.AsksHuman(Call("send", toolName: "mcp__workdir__tool", server: "workdir", source: "project")));
    }

    /// <summary>F7: the MCP resource tools take the server in their arguments; one aimed at a claude.ai server asks, one aimed elsewhere does not.</summary>
    [Fact]
    public void Classifier_McpResourceTool_AsksOnlyForAConnectorServer()
    {
        Assert.True(ConnectorCallClassifier.AsksHuman(Call("ReadMcpResourceTool", toolName: "ReadMcpResourceTool", raw: """{"server":"claude_ai_Gmail","uri":"x://1"}""")));
        Assert.True(ConnectorCallClassifier.AsksHuman(Call("ListMcpResourcesTool", toolName: "ListMcpResourcesTool", raw: """{"server":"claude_ai_Gmail"}""")));
        Assert.False(ConnectorCallClassifier.AsksHuman(Call("ReadMcpResourceTool", toolName: "ReadMcpResourceTool", raw: """{"server":"workdir","uri":"x://1"}""")));
    }

    /// <summary>A call with no name and kind Other fails safe and asks; a known tool, a Team's own tool and a Work Dir server do not.</summary>
    [Fact]
    public void Classifier_NamelessOther_Asks_AndTheRestDoNot()
    {
        Assert.True(ConnectorCallClassifier.AsksHuman(Call(null, ToolKind.Other)));
        Assert.True(ConnectorCallClassifier.AsksHuman(Call("Tool", ToolKind.Other, """{"to":"a@example.com","via":"claude_ai_Gmail"}""")));
        Assert.False(ConnectorCallClassifier.AsksHuman(Call("Bash", ToolKind.Execute, toolName: "Bash")));
        Assert.False(ConnectorCallClassifier.AsksHuman(Call("echo claude_ai_", ToolKind.Execute, """{"command":"echo claude_ai_"}""")));
        Assert.False(ConnectorCallClassifier.AsksHuman(Call("Bash", ToolKind.Execute, """{"command":"echo claude_ai_"}""", toolName: "Bash")));
        Assert.False(ConnectorCallClassifier.AsksHuman(Call("send", ToolKind.Other, toolName: "mcp__team__send_message")));
        Assert.False(ConnectorCallClassifier.AsksHuman(Call("tool", ToolKind.Other, toolName: "mcp__workdir__tool", server: "workdir")));
    }

    /// <summary>The form is one required choice with no default, naming the Agent and a clean tool name, and the reader shows it.</summary>
    [Fact]
    public void Form_IsAnEnumWithNoDefault_NamingTheAgentAndTheTool()
    {
        ToolCallInfo call = Call("Send", toolName: "mcp__claude_ai_ms365__send‮evil\nmail", raw: """{"to":"a@example.com"}""");

        ElicitationRequest request = ConnectorApprovalForm.Build("Nova", Context(call, null));

        using JsonDocument schema = JsonDocument.Parse(request.RequestedSchemaJson);
        JsonElement field = schema.RootElement.GetProperty("properties").GetProperty(ConnectorApprovalForm.DecisionKey);
        Assert.False(field.TryGetProperty("default", out _));
        Assert.Equal(["allow_once", "refuse"], field.GetProperty("oneOf").EnumerateArray().Select(option => option.GetProperty("const").GetString()).ToArray());
        Assert.Equal(ConnectorApprovalForm.DecisionKey, Assert.Single(schema.RootElement.GetProperty("required").EnumerateArray()).GetString());
        Assert.Equal("Nova wants to use the connector tool mcp__claude_ai_ms365__sendevilmail. Allow this one call?", request.Message);
        Assert.True(ElicitationSchemaReader.TryRead(request, out ElicitationForm? form, out _));
        Assert.Equal(request.UntrustedDetail, form.UntrustedDetail);
        Assert.Equal("to: a@example.com", form.UntrustedDetail);
    }

    /// <summary>The arguments lose control and bidi characters, are capped at 4 KB, and say how much is hidden.</summary>
    [Fact]
    public void Form_HostileArguments_AreSanitisedCappedAndCounted()
    {
        string raw = "{\"body\":\"" + "‮⁦" + "a\u0007b\r" + new string('x', 5000) + "\"}";

        string? detail = ConnectorApprovalForm.Build("Nova", Context(Call("t", raw: raw, toolName: Connector), null)).UntrustedDetail;

        // The raw control characters make this invalid JSON, so the text is shown as it came, stripped and cut at 4 KB.
        Assert.Equal("{\"body\":\"ab" + new string('x', 4085) + "\n... 917 more characters not shown.", detail);
    }

    /// <summary>The hidden count is exact, and a call with no arguments says so.</summary>
    [Fact]
    public void Form_Detail_ExactWordingForCapAndEmpty()
    {
        ElicitationRequest empty = ConnectorApprovalForm.Build("Nova", Context(Call("t", toolName: Connector), null));
        ElicitationRequest capped = ConnectorApprovalForm.Build("Nova", Context(Call("t", raw: new string('x', 5000), toolName: Connector), null));

        Assert.Equal("(no arguments)", empty.UntrustedDetail);
        Assert.Equal(new string('x', 4096) + "\n... 904 more characters not shown.", capped.UntrustedDetail);
    }

    private static string[] Choices(ElicitationRequest request)
    {
        using JsonDocument schema = JsonDocument.Parse(request.RequestedSchemaJson);
        return [.. schema.RootElement.GetProperty("properties").GetProperty(ConnectorApprovalForm.DecisionKey).GetProperty("oneOf")
            .EnumerateArray().Select(option => option.GetProperty("const").GetString() ?? string.Empty)];
    }

    private static string RefuseOnlyMessage => $"Nova wants to use the connector tool {Connector}, but its arguments cannot be shown in full, so this call can only be refused.";

    private static ElicitationRequest FormFor(string raw) => ConnectorApprovalForm.Build("Nova", Context(Call("t", raw: raw, toolName: Connector), null));

    /// <summary>
    /// F6: a long body ahead of the destination must not push the destination out of view. Each top-level field is shown on its own,
    /// the shortest first, a long text is cut with a count, and Allow is still offered because every field is on the card.
    /// </summary>
    [Fact]
    public void Form_LongBodyBeforeTheDestination_ShowsTheDestinationFirst()
    {
        ElicitationRequest request = FormFor("{\"body\":\"" + new string('x', 5000) + "\",\"to\":\"evil@x\"}");

        string detail = request.UntrustedDetail ?? string.Empty;

        Assert.Equal("to: evil@x\nbody: " + new string('x', 1000) + "\n    ... 4000 more characters not shown.", detail);
        Assert.Equal(["allow_once", "refuse"], Choices(request));
    }

    /// <summary>F6: a line break inside a value cannot pass for another field: continuation lines are indented.</summary>
    [Fact]
    public void Form_NewlineInAValue_CannotFakeAField()
    {
        string detail = FormFor("""{"body":"hi\nto: friend@x","to":"evil@x"}""").UntrustedDetail ?? string.Empty;

        Assert.Equal("to: evil@x\nbody: hi\n    to: friend@x", detail);
    }

    /// <summary>F6: when the fields do not all fit, the form offers Refuse only, says how many fields are hidden, and the message says why.</summary>
    [Fact]
    public void Form_TooManyFields_OffersRefuseOnly()
    {
        string raw = "{" + string.Join(",", Enumerable.Range(0, 100).Select(i => $"\"field{i}\":\"{new string('v', 100)}\"")) + "}";

        ElicitationRequest request = FormFor(raw);

        Assert.Equal(["refuse"], Choices(request));
        Assert.True(ElicitationSchemaReader.TryRead(request, out _, out _));
        Assert.Equal(RefuseOnlyMessage, request.Message);
        string[] lines = (request.UntrustedDetail ?? string.Empty).Split('\n');
        int shown = lines.Length - 1;
        Assert.InRange(shown, 1, 99);
        Assert.Equal($"... {100 - shown} more fields not shown.", lines[^1]);
        Assert.Equal("field0: " + new string('v', 100), lines[0]);
    }

    /// <summary>F6: a small nested object is shown whole and allowed; a nested value that would be cut hides fields inside it, so it is Refuse only.</summary>
    [Fact]
    public void Form_NestedObjects_AreShownWholeOrRefuseOnly()
    {
        ElicitationRequest small = FormFor("""{"message":{"to":"a@x","cc":"b@x"}}""");
        string options = "{\"cc\":\"evil@x\",\"body\":\"" + new string('y', 5000) + "\"}";
        ElicitationRequest big = FormFor("{\"options\":" + options + ",\"to\":\"a@x\"}");

        Assert.Equal(["allow_once", "refuse"], Choices(small));
        Assert.Equal("message: {\"to\":\"a@x\",\"cc\":\"b@x\"}", small.UntrustedDetail);
        Assert.Equal(["refuse"], Choices(big));
        Assert.Equal("to: a@x\noptions: " + options[..1000] + $"\n    ... {options.Length - 1000} more characters not shown.", big.UntrustedDetail);
    }

    /// <summary>F6: arguments that are not a JSON object cannot be split into fields, so the text is shown as it came and only Refuse is offered.</summary>
    /// <param name="raw">The raw arguments.</param>
    [Theory]
    [InlineData("not json {to: evil@x")]
    [InlineData("[1,2,3]")]
    [InlineData("\"just a string\"")]
    public void Form_ArgumentsThatAreNotAnObject_OfferRefuseOnly(string raw)
    {
        ElicitationRequest request = FormFor(raw);

        Assert.Equal(["refuse"], Choices(request));
        Assert.Equal(raw, request.UntrustedDetail);
        Assert.Equal(RefuseOnlyMessage, request.Message);
    }

    /// <summary>F6: no arguments and an empty object read as "(no arguments)" and may be allowed.</summary>
    [Fact]
    public void Form_EmptyObject_IsAllowed()
    {
        ElicitationRequest request = FormFor("{}");

        Assert.Equal("(no arguments)", request.UntrustedDetail);
        Assert.Equal(["allow_once", "refuse"], Choices(request));
    }

    /// <summary>F8: format characters outside the Basic Multilingual Plane (tag characters) are stripped from the tool name and the arguments.</summary>
    [Fact]
    public void Form_AstralFormatCharacters_AreStripped()
    {
        string tag = char.ConvertFromUtf32(0xE0041) + char.ConvertFromUtf32(0xE0001);
        ToolCallInfo call = Call("t", raw: "{\"to\":\"a" + tag + "@x\"}", toolName: "mcp__claude_ai_x__se" + tag + "nd");

        ElicitationRequest request = ConnectorApprovalForm.Build("Nova", Context(call, null));

        Assert.Equal("Nova wants to use the connector tool mcp__claude_ai_x__send. Allow this one call?", request.Message);
        Assert.Equal("to: a@x", request.UntrustedDetail);
    }

    /// <summary>F8: every connector decision is logged at Information with the Agent, tool, Room and answer, and never the arguments.</summary>
    [Fact]
    public async Task Handler_LogsEveryDecision_WithoutArguments()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        CapturingLogger log = new();
        WorkDirPermissionHandler handler = new(log, [new ProtectedDirectoryGuard(Path.Combine(Path.GetTempPath(), "huddle-protected"))], personaName: "Nova");
        FakeScope allowed = new() { ActiveRoomId = "room-7" };
        FakeScope refused = new() { ActiveRoomId = "room-7" };
        allowed.Reply.SetResult(Answer(ConnectorApprovalForm.AllowOnce));
        refused.Reply.SetResult(Answer(ConnectorApprovalForm.Refuse));
        ToolCallInfo call = Call("send", toolName: Connector, raw: """{"to":"secret@example.com"}""");

        _ = await handler.DecideAsync(Context(call, allowed), ct);
        _ = await handler.DecideAsync(Context(call, refused), ct);
        _ = await handler.DecideAsync(Context(call, null), ct);

        List<string> decisions = log.Informations.Where(line => line.StartsWith("Connector call decision", StringComparison.Ordinal)).ToList();
        Assert.Equal(
            [
                $"Connector call decision for 'Nova' in room room-7: tool {Connector}, allowed once.",
                $"Connector call decision for 'Nova' in room room-7: tool {Connector}, refused by the Human.",
                $"Connector call decision for 'Nova' in room (none): tool {Connector}, refused: no Room to ask in.",
            ],
            decisions);
        Assert.DoesNotContain(log.Informations, line => line.Contains("secret@example.com", StringComparison.Ordinal));
    }

    /// <summary>
    /// A request that asks is logged before the wait with ToolName, McpServerName, McpServerSource, Kind and Title, so a call that is
    /// never answered still leaves what the wire carried (AGCONN-13). The arguments are not in the line.
    /// </summary>
    [Fact]
    public async Task Handler_LogsTheWireFacts_BeforeTheQuestionIsAnswered()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        CapturingLogger log = new();
        WorkDirPermissionHandler handler = new(log, [new ProtectedDirectoryGuard(Path.Combine(Path.GetTempPath(), "huddle-protected"))], personaName: "Nova");
        FakeScope scope = new() { ActiveRoomId = "room-7" };
        ToolCallInfo call = Call("Probe echo", ToolKind.Other, raw: """{"to":"secret@example.com"}""", toolName: Connector, server: "claude_ai_probe", source: "project");

        Task<PermissionDecision> pending = handler.DecideAsync(Context(call, scope), ct);

        Assert.False(pending.IsCompleted);
        Assert.Equal(
            [$"Connector call asks 'Nova' in room room-7: ToolName {Connector}, McpServerName claude_ai_probe, McpServerSource project, Kind Other, Title Probe echo."],
            log.Informations);
        scope.Reply.SetResult(Answer(ConnectorApprovalForm.Refuse));
        _ = await pending;
    }

    /// <summary>Keeps the rendered Information lines.</summary>
    private sealed class CapturingLogger : ILogger<WorkDirPermissionHandler>
    {
        public List<string> Informations { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Information)
            {
                this.Informations.Add(formatter(state, exception));
            }
        }
    }

    /// <summary>T-13: the handler is pending until the Human answers, and allow-once picks the one-time allow, never always.</summary>
    [Fact]
    public async Task Handler_ConnectorCall_IsPendingUntilAnswered_ThenAllowsOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeScope scope = new();

        Task<PermissionDecision> pending = Handler().DecideAsync(Context(Call("send", toolName: Connector), scope), ct);

        Assert.False(pending.IsCompleted);
        Assert.Single(scope.Asked);
        scope.Reply.SetResult(Answer(ConnectorApprovalForm.AllowOnce));
        Assert.Equal("allow-1", Assert.IsType<SelectedDecision>(await pending.WaitAsync(TimeSpan.FromSeconds(5), ct)).OptionId);
    }

    /// <summary>Every answer that is not an accepted allow-once refuses with the one-time reject option.</summary>
    /// <param name="kind">Which non-answer.</param>
    [Theory]
    [InlineData("refuse")]
    [InlineData("declined")]
    [InlineData("cancelled")]
    [InlineData("missing")]
    [InlineData("number")]
    [InlineData("unknown")]
    [InlineData("case")]
    [InlineData("empty")]
    public async Task Handler_AnythingButAllowOnce_Refuses(string kind)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeScope scope = new();
        scope.Reply.SetResult(kind switch
        {
            "refuse" => Answer(ConnectorApprovalForm.Refuse),
            "declined" => new ElicitationDeclined(),
            "cancelled" => new ElicitationCancelled(),
            "missing" => new ElicitationAccepted(new Dictionary<string, object>()),
            "number" => Answer(1L),
            "unknown" => Answer("always"),
            "case" => Answer("ALLOW_ONCE"),
            _ => Answer(string.Empty),
        });

        PermissionDecision decision = await Handler().DecideAsync(Context(Call("send", toolName: Connector), scope), ct);

        Assert.Equal("reject-1", Assert.IsType<SelectedDecision>(decision).OptionId);
    }

    /// <summary>T-12 and T-13: no scope, no latch passed, a scope that throws, and a timed-out Turn all refuse; the last asks nothing.</summary>
    [Fact]
    public async Task Handler_NoScope_ThrowingScope_TimedOutTurn_AllRefuse()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        ToolCallInfo call = Call("mcp__claude_ai_ms365__outlook_send_mail");
        FakeScope throwing = new() { Throws = new InvalidOperationException("boom") };
        FakeScope timedOut = new() { ApprovalTimedOut = true };

        PermissionDecision noScope = await Handler().DecideAsync(Context(call, null), ct);
        PermissionDecision thrown = await Handler().DecideAsync(Context(call, throwing), ct);
        PermissionDecision afterTimeout = await Handler().DecideAsync(Context(call, timedOut), ct);

        Assert.Equal("reject-1", Assert.IsType<SelectedDecision>(noScope).OptionId);
        Assert.Equal("reject-1", Assert.IsType<SelectedDecision>(thrown).OptionId);
        Assert.Equal("reject-1", Assert.IsType<SelectedDecision>(afterTimeout).OptionId);
        Assert.Empty(timedOut.Asked);
    }

    /// <summary>When only the standing allow is offered, an accepted allow-once still never selects it: the call is refused.</summary>
    [Fact]
    public async Task Handler_OnlyAllowAlwaysOffered_Refuses()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeScope scope = new();
        scope.Reply.SetResult(Answer(ConnectorApprovalForm.AllowOnce));
        IReadOnlyList<PermissionOptionInfo> onlyAlways = [new PermissionOptionInfo("always-1", "Always", PermissionOptionKind.AllowAlways)];

        PermissionDecision decision = await Handler().DecideAsync(Context(Call("send", toolName: Connector), scope, onlyAlways), ct);

        Assert.Equal(PermissionDecision.Cancelled, decision);
    }

    /// <summary>The host disposing (the token cancelled) unblocks a waiting handler, and the call is refused.</summary>
    [Fact]
    public async Task Handler_TokenCancelled_UnblocksAndRefuses()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeScope scope = new();
        using CancellationTokenSource host = CancellationTokenSource.CreateLinkedTokenSource(ct);

        Task<PermissionDecision> pending = Handler().DecideAsync(Context(Call("send", toolName: Connector), scope), host.Token);
        await host.CancelAsync();
        PermissionDecision decision = await pending.WaitAsync(TimeSpan.FromSeconds(5), ct);

        Assert.Equal("reject-1", Assert.IsType<SelectedDecision>(decision).OptionId);
    }

    /// <summary>Two calls waiting at once are answered independently.</summary>
    [Fact]
    public async Task Handler_TwoCalls_ResolveIndependently()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeScope first = new();
        FakeScope second = new();
        WorkDirPermissionHandler handler = Handler();

        Task<PermissionDecision> one = handler.DecideAsync(Context(Call("a", toolName: Connector), first), ct);
        Task<PermissionDecision> two = handler.DecideAsync(Context(Call("b", toolName: Connector), second), ct);
        second.Reply.SetResult(Answer(ConnectorApprovalForm.Refuse));

        Assert.Equal("reject-1", Assert.IsType<SelectedDecision>(await two.WaitAsync(TimeSpan.FromSeconds(5), ct)).OptionId);
        Assert.False(one.IsCompleted);
        first.Reply.SetResult(Answer(ConnectorApprovalForm.AllowOnce));
        Assert.Equal("allow-1", Assert.IsType<SelectedDecision>(await one.WaitAsync(TimeSpan.FromSeconds(5), ct)).OptionId);
    }

    /// <summary>A guard refuses first: a connector-named call that writes into the protected directory posts no card.</summary>
    [Fact]
    public async Task Handler_GuardRefusal_PostsNoCard()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string root = Path.Combine(Path.GetTempPath(), "huddle-protected");
        FakeScope scope = new();
        string raw = JsonSerializer.Serialize(new { file_path = Path.Combine(root, "memory", "x.md") });

        PermissionDecision decision = await Handler(root).DecideAsync(Context(Call("write", ToolKind.Other, raw, Connector), scope), ct);

        Assert.Equal("reject-1", Assert.IsType<SelectedDecision>(decision).OptionId);
        Assert.Empty(scope.Asked);
    }

    /// <summary>Calls that are not connector calls are approved as before and ask nothing.</summary>
    [Fact]
    public async Task Handler_NonConnectorCalls_AreApproved_WithoutAsking()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeScope scope = new();

        PermissionDecision bash = await Handler().DecideAsync(Context(Call("echo claude_ai_", ToolKind.Execute, """{"command":"echo claude_ai_"}""", "Bash"), scope), ct);
        PermissionDecision team = await Handler().DecideAsync(Context(Call("send", toolName: "mcp__team__post", server: "team"), scope), ct);

        Assert.Equal("allow-1", Assert.IsType<SelectedDecision>(bash).OptionId);
        Assert.Equal("allow-1", Assert.IsType<SelectedDecision>(team).OptionId);
        Assert.Empty(scope.Asked);
    }
}

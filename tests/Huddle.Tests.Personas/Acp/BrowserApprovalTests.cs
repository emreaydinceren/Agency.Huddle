using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Acp;

/// <summary>The approval card and the handler's answer for a Playwright browser call (issue #551, decision 4).</summary>
public sealed class BrowserApprovalTests
{
    private static readonly IReadOnlyList<PermissionOptionInfo> Offered =
    [
        new PermissionOptionInfo("allow-1", "Allow once", PermissionOptionKind.AllowOnce),
        new PermissionOptionInfo("reject-1", "Reject once", PermissionOptionKind.RejectOnce),
    ];

    private static ToolCallInfo Call(string tool, string? raw) =>
        new("call-1", tool, ToolKind.Other, ToolCallStatus.Pending, raw, "mcp__playwright__" + tool, "playwright", "dynamic");

    private static PermissionRequestContext Context(ToolCallInfo call, IElicitationScope? scope) =>
        new("session-1", call, Offered, scope);

    private static WorkDirPermissionHandler Handler() =>
        new(NullLogger<WorkDirPermissionHandler>.Instance, [], personaName: "Nova");

    private static ElicitationAccepted Answer(string value) =>
        new(new Dictionary<string, object> { [ConnectorApprovalForm.DecisionKey] = value });

    /// <summary>A scope whose answer the test controls.</summary>
    private sealed class FakeScope : IElicitationScope, IConnectorTurnLatch
    {
        public List<ElicitationRequest> Asked { get; } = [];

        public TaskCompletionSource<ElicitationResult> Reply { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool ApprovalTimedOut { get; set; }

        public string? ActiveRoomId { get; set; }

        public async Task<ElicitationResult> ElicitAsync(ElicitationRequest request, CancellationToken cancellationToken)
        {
            this.Asked.Add(request);
            using CancellationTokenRegistration registration = cancellationToken.Register(() => this.Reply.TrySetCanceled(cancellationToken));
            return await this.Reply.Task;
        }
    }

    /// <summary>A click posts a card in browser wording that shows its arguments, and allow-once selects the one-time allow.</summary>
    [Fact]
    public async Task BrowserCall_PostsBrowserWording_AllowOnceRuns()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeScope scope = new();
        scope.Reply.SetResult(Answer(ConnectorApprovalForm.AllowOnce));

        PermissionDecision decision = await Handler().DecideAsync(
            Context(Call("browser_click", """{"element":"Buy now","target":"e12"}"""), scope), ct);

        ElicitationRequest card = Assert.Single(scope.Asked);
        Assert.Equal("Nova wants to click in its browser. Allow this one call?", card.Message);
        Assert.Contains("element: Buy now", card.UntrustedDetail, StringComparison.Ordinal);
        Assert.Contains("target: e12", card.UntrustedDetail, StringComparison.Ordinal);
        Assert.Equal("allow-1", Assert.IsType<SelectedDecision>(decision).OptionId);
    }

    /// <summary>The text an Agent types is shown on the card exactly.</summary>
    [Fact]
    public void BrowserType_CardShowsExactText()
    {
        ElicitationRequest card = ConnectorApprovalForm.Build("Nova", Context(Call("browser_type", """{"element":"Password","text":"hunter2 & more"}"""), null));

        Assert.Equal("Nova wants to type text in its browser. Allow this one call?", card.Message);
        Assert.Contains("text: hunter2 & more", card.UntrustedDetail, StringComparison.Ordinal);
    }

    /// <summary>The verbs of the remaining acting tools, and the generic wording for a tool without its own.</summary>
    [Fact]
    public void BrowserCard_Verbs()
    {
        Assert.Equal("Nova wants to open a page in its browser. Allow this one call?", ConnectorApprovalForm.Build("Nova", Context(Call("browser_navigate", """{"url":"file:///x"}"""), null)).Message);
        Assert.Equal("Nova wants to save a screenshot in its browser. Allow this one call?", ConnectorApprovalForm.Build("Nova", Context(Call("browser_take_screenshot", """{"filename":"a.png"}"""), null)).Message);
        Assert.Equal("Nova wants to use the browser tool browser_new_thing. Allow this one call?", ConnectorApprovalForm.Build("Nova", Context(Call("browser_new_thing", "{}"), null)).Message);
    }

    /// <summary>Arguments that cannot be shown whole make the card refuse-only, in browser wording.</summary>
    [Fact]
    public void BrowserCard_IncompleteArguments_RefuseOnly()
    {
        ElicitationRequest card = ConnectorApprovalForm.Build("Nova", Context(Call("browser_click", "not json"), null));

        Assert.Equal("Nova wants to click in its browser, but its arguments cannot be shown in full, so this call can only be refused.", card.Message);
        Assert.DoesNotContain("allow_once", card.RequestedSchemaJson, StringComparison.Ordinal);
    }

    /// <summary>A refusal rejects the call.</summary>
    [Fact]
    public async Task BrowserCall_Refused_Rejects()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeScope scope = new();
        scope.Reply.SetResult(Answer(ConnectorApprovalForm.Refuse));

        PermissionDecision decision = await Handler().DecideAsync(Context(Call("browser_click", """{"element":"x"}"""), scope), ct);

        Assert.Equal("reject-1", Assert.IsType<SelectedDecision>(decision).OptionId);
    }

    /// <summary>After an earlier question on the Turn went unanswered, a browser call is refused with no new card.</summary>
    [Fact]
    public async Task BrowserCall_Timeout_Rejects()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeScope scope = new() { ApprovalTimedOut = true };

        PermissionDecision decision = await Handler().DecideAsync(Context(Call("browser_click", """{"element":"x"}"""), scope), ct);

        Assert.Equal("reject-1", Assert.IsType<SelectedDecision>(decision).OptionId);
        Assert.Empty(scope.Asked);
    }

    /// <summary>A read of an https page runs with no card.</summary>
    [Fact]
    public async Task BrowserRead_NoCard()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeScope scope = new();

        PermissionDecision decision = await Handler().DecideAsync(Context(Call("browser_navigate", """{"url":"https://example.com"}"""), scope), ct);

        Assert.Equal("allow-1", Assert.IsType<SelectedDecision>(decision).OptionId);
        Assert.Empty(scope.Asked);
    }

    /// <summary>A call with no Room to ask in (a Job) is refused at once.</summary>
    [Fact]
    public async Task BrowserCall_NoScope_Refuses()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        PermissionDecision decision = await Handler().DecideAsync(Context(Call("browser_click", """{"element":"x"}"""), null), ct);

        Assert.Equal("reject-1", Assert.IsType<SelectedDecision>(decision).OptionId);
    }
}

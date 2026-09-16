namespace Agency.Huddle.Tests.Acp;

using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;

/// <summary>
/// Covers the one refusal this application makes on an Agent's behalf: a tool call writing into the
/// human's own agent configuration directory. Everything else is approved exactly as
/// <see cref="AutoApprovePermissionHandler"/> approves it, so both halves are pinned — a handler that
/// refused too much would break ordinary Turns just as surely.
/// </summary>
public sealed class WorkDirPermissionHandlerTests
{
    private static readonly IReadOnlyList<PermissionOptionInfo> BothKinds =
    [
        new PermissionOptionInfo("allow-1", "Allow once", PermissionOptionKind.AllowOnce),
        new PermissionOptionInfo("reject-1", "Reject once", PermissionOptionKind.RejectOnce),
    ];

    /// <summary>Builds a permission request for a tool call carrying one path argument.</summary>
    /// <param name="rawInputJson">The tool call's raw input, or null for none.</param>
    /// <returns>A request with both an allow and a reject option offered.</returns>
    private static PermissionRequestContext Request(string? rawInputJson) =>
        new("session-1", new ToolCallInfo("call-1", "Write", ToolKind.Edit, ToolCallStatus.Pending, rawInputJson), BothKinds);

    /// <summary>Creates the handler under test, protecting a directory that does not have to exist.</summary>
    /// <param name="protectedDirectory">The directory to refuse writes into.</param>
    /// <returns>The handler.</returns>
    private static WorkDirPermissionHandler Handler(string protectedDirectory) =>
        new(protectedDirectory, NullLogger<WorkDirPermissionHandler>.Instance);

    /// <summary>A write into the protected directory is refused, not approved.</summary>
    [Fact]
    public async Task Write_InsideTheProtectedDirectory_IsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        var root = Path.Combine(Path.GetTempPath(), "huddle-protected");
        var target = Path.Combine(root, "projects", "repo", "memory", "remembered_word.md");

        var decision = await Handler(root).DecideAsync(Request($$"""{"file_path":{{System.Text.Json.JsonSerializer.Serialize(target)}}}"""), ct);

        var selected = Assert.IsType<SelectedDecision>(decision);
        Assert.Equal("reject-1", selected.OptionId);
    }

    /// <summary>
    /// A sibling whose name merely starts with the protected directory's name is outside it. Without
    /// the separator in the comparison this passes as "inside" and ordinary work gets refused.
    /// </summary>
    [Fact]
    public async Task Write_ToASiblingWithASharedNamePrefix_IsApproved()
    {
        var ct = TestContext.Current.CancellationToken;
        var root = Path.Combine(Path.GetTempPath(), "huddle-protected");
        var target = Path.Combine(Path.GetTempPath(), "huddle-protectedx", "notes.md");

        var decision = await Handler(root).DecideAsync(Request($$"""{"file_path":{{System.Text.Json.JsonSerializer.Serialize(target)}}}"""), ct);

        var selected = Assert.IsType<SelectedDecision>(decision);
        Assert.Equal("allow-1", selected.OptionId);
    }

    /// <summary>An ordinary write elsewhere is approved, preferring the one-time grant.</summary>
    [Fact]
    public async Task Write_OutsideTheProtectedDirectory_IsApprovedOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        var root = Path.Combine(Path.GetTempPath(), "huddle-protected");
        var target = Path.Combine(Path.GetTempPath(), "huddle-work", "nova", "scratch.md");

        var decision = await Handler(root).DecideAsync(Request($$"""{"file_path":{{System.Text.Json.JsonSerializer.Serialize(target)}}}"""), ct);

        var selected = Assert.IsType<SelectedDecision>(decision);
        Assert.Equal("allow-1", selected.OptionId);
    }

    /// <summary>
    /// A tool call this type cannot parse is approved rather than refused. Refusing the unparseable
    /// would block every tool whose arguments it does not recognise, which is a far larger change than
    /// the one boundary it exists to draw.
    /// </summary>
    /// <param name="rawInputJson">Input that carries no usable path.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json at all")]
    [InlineData("[1,2,3]")]
    [InlineData("""{"command":"echo hi"}""")]
    public async Task ToolCall_WithNoRecognisablePath_IsApproved(string? rawInputJson)
    {
        var ct = TestContext.Current.CancellationToken;
        var root = Path.Combine(Path.GetTempPath(), "huddle-protected");

        var decision = await Handler(root).DecideAsync(Request(rawInputJson), ct);

        var selected = Assert.IsType<SelectedDecision>(decision);
        Assert.Equal("allow-1", selected.OptionId);
    }

    /// <summary>With no refusal option offered, a refused call is cancelled rather than approved.</summary>
    [Fact]
    public async Task Write_InsideTheProtectedDirectory_WithNoRejectOption_IsCancelled()
    {
        var ct = TestContext.Current.CancellationToken;
        var root = Path.Combine(Path.GetTempPath(), "huddle-protected");
        var target = Path.Combine(root, "settings.json");
        IReadOnlyList<PermissionOptionInfo> allowOnly = [new PermissionOptionInfo("allow-1", "Allow once", PermissionOptionKind.AllowOnce)];
        var request = new PermissionRequestContext(
            "session-1",
            new ToolCallInfo("call-1", "Write", ToolKind.Edit, ToolCallStatus.Pending, $$"""{"file_path":{{System.Text.Json.JsonSerializer.Serialize(target)}}}"""),
            allowOnly);

        var decision = await Handler(root).DecideAsync(request, ct);

        Assert.IsType<CancelledDecision>(decision);
    }
}

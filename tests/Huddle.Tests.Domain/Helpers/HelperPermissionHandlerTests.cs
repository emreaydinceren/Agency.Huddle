using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Helpers;

namespace Agency.Huddle.Tests.Helpers;

/// <summary>
/// <see cref="HelperPermissionHandler"/> refuses every request a Helper session makes and counts them
/// (Helpers Spec §6.2, plan HL-4).
/// </summary>
public sealed class HelperPermissionHandlerTests
{
    /// <summary>Builds a request offering the given options.</summary>
    /// <param name="options">The options the adapter offers.</param>
    private static PermissionRequestContext Request(params PermissionOptionInfo[] options) =>
        new("s", new ToolCallInfo("t1", "Read", ToolKind.Read, ToolCallStatus.Pending, null), options);

    /// <summary>A one-time refusal is chosen over allow and over a permanent refusal.</summary>
    [Fact]
    public async Task Decide_PrefersRejectOnce()
    {
        HelperPermissionHandler handler = new();

        PermissionDecision decision = await handler.DecideAsync(
            Request(
                new PermissionOptionInfo("allow", "Allow", PermissionOptionKind.AllowOnce),
                new PermissionOptionInfo("never", "Never", PermissionOptionKind.RejectAlways),
                new PermissionOptionInfo("no", "No", PermissionOptionKind.RejectOnce)),
            TestContext.Current.CancellationToken);

        Assert.Equal("no", Assert.IsType<SelectedDecision>(decision).OptionId);
    }

    /// <summary>With only a permanent refusal on offer, that one is chosen.</summary>
    [Fact]
    public async Task Decide_FallsBackToRejectAlways()
    {
        HelperPermissionHandler handler = new();

        PermissionDecision decision = await handler.DecideAsync(
            Request(
                new PermissionOptionInfo("allow", "Allow", PermissionOptionKind.AllowAlways),
                new PermissionOptionInfo("never", "Never", PermissionOptionKind.RejectAlways)),
            TestContext.Current.CancellationToken);

        Assert.Equal("never", Assert.IsType<SelectedDecision>(decision).OptionId);
    }

    /// <summary>With no refusal on offer the request is cancelled, never allowed.</summary>
    [Fact]
    public async Task Decide_NoRefusalOffered_Cancels()
    {
        HelperPermissionHandler handler = new();

        PermissionDecision decision = await handler.DecideAsync(
            Request(new PermissionOptionInfo("allow", "Allow", PermissionOptionKind.AllowOnce)),
            TestContext.Current.CancellationToken);

        Assert.Equal(PermissionDecision.Cancelled, decision);
    }

    /// <summary>Every request is counted, whatever the decision.</summary>
    [Fact]
    public async Task Refused_CountsEveryRequest()
    {
        HelperPermissionHandler handler = new();
        CancellationToken ct = TestContext.Current.CancellationToken;

        _ = await handler.DecideAsync(Request(new PermissionOptionInfo("no", "No", PermissionOptionKind.RejectOnce)), ct);
        _ = await handler.DecideAsync(Request(new PermissionOptionInfo("allow", "Allow", PermissionOptionKind.AllowOnce)), ct);

        Assert.Equal(2, handler.Refused);
    }
}

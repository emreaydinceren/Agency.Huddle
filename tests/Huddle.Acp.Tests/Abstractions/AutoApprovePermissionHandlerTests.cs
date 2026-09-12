namespace Agency.Huddle.Acp.Tests.Abstractions;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Agency.Huddle.Acp.Abstractions;
using Xunit;

public sealed class AutoApprovePermissionHandlerTests
{
    [Fact]
    public async Task Prefers_AllowOnce()
    {
        ToolCallInfo toolCall = new ToolCallInfo("call-1", "Test Tool", ToolKind.Execute, ToolCallStatus.Pending, null);
        List<PermissionOptionInfo> options = new List<PermissionOptionInfo>
        {
            new PermissionOptionInfo("opt-allow-always", "Allow Always", PermissionOptionKind.AllowAlways),
            new PermissionOptionInfo("opt-allow-once", "Allow Once", PermissionOptionKind.AllowOnce),
            new PermissionOptionInfo("opt-reject-once", "Reject Once", PermissionOptionKind.RejectOnce),
        };
        PermissionRequestContext context = new PermissionRequestContext("session-1", toolCall, options);
        AutoApprovePermissionHandler handler = new AutoApprovePermissionHandler();

        PermissionDecision decision = await handler.DecideAsync(context, CancellationToken.None);

        SelectedDecision selected = Assert.IsType<SelectedDecision>(decision);
        Assert.Equal("opt-allow-once", selected.OptionId);
    }

    [Fact]
    public async Task FallsBack_To_AllowAlways()
    {
        ToolCallInfo toolCall = new ToolCallInfo("call-2", "Test Tool", ToolKind.Execute, ToolCallStatus.Pending, null);
        List<PermissionOptionInfo> options = new List<PermissionOptionInfo>
        {
            new PermissionOptionInfo("opt-reject-once", "Reject Once", PermissionOptionKind.RejectOnce),
            new PermissionOptionInfo("opt-allow-always", "Allow Always", PermissionOptionKind.AllowAlways),
        };
        PermissionRequestContext context = new PermissionRequestContext("session-2", toolCall, options);
        AutoApprovePermissionHandler handler = new AutoApprovePermissionHandler();

        PermissionDecision decision = await handler.DecideAsync(context, CancellationToken.None);

        SelectedDecision selected = Assert.IsType<SelectedDecision>(decision);
        Assert.Equal("opt-allow-always", selected.OptionId);
    }

    [Fact]
    public async Task NoAllowOption_ReturnsCancelled()
    {
        ToolCallInfo toolCall = new ToolCallInfo("call-3", "Test Tool", ToolKind.Execute, ToolCallStatus.Pending, null);
        List<PermissionOptionInfo> options = new List<PermissionOptionInfo>
        {
            new PermissionOptionInfo("opt-reject-once", "Reject Once", PermissionOptionKind.RejectOnce),
            new PermissionOptionInfo("opt-reject-always", "Reject Always", PermissionOptionKind.RejectAlways),
        };
        PermissionRequestContext context = new PermissionRequestContext("session-3", toolCall, options);
        AutoApprovePermissionHandler handler = new AutoApprovePermissionHandler();

        PermissionDecision decision = await handler.DecideAsync(context, CancellationToken.None);

        Assert.Same(PermissionDecision.Cancelled, decision);
    }
}

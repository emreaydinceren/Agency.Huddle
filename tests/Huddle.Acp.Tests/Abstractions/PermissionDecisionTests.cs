namespace Agency.Huddle.Acp.Tests.Abstractions;

using System;
using Agency.Huddle.Acp.Abstractions;
using Xunit;

public sealed class PermissionDecisionTests
{
    [Fact]
    public void Selected_EmptyOptionId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new SelectedDecision(""));
        Assert.Throws<ArgumentException>(() => new SelectedDecision("  "));
    }

    [Fact]
    public void Cancelled_IsSingleton()
    {
        PermissionDecision first = PermissionDecision.Cancelled;
        PermissionDecision second = PermissionDecision.Cancelled;

        Assert.Same(first, second);
        Assert.IsType<CancelledDecision>(first);
    }
}

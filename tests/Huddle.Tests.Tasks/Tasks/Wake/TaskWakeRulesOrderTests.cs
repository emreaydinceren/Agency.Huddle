using Agency.Huddle.App.Tasks.Wake;

namespace Agency.Huddle.Tests.Tasks.Wake;

/// <summary>The order the service runs its wake rules in (S-B4), which decides who is woken first and who is already woken when a later rule looks.</summary>
public sealed class TaskWakeRulesOrderTests
{
    /// <summary>The production order is the assignee, the manager, then the requesting manager.</summary>
    [Fact]
    public void ProductionOrder_IsAssigneeManagerRequestingManager()
    {
        Assert.Equal(["Assignee", "Manager", "RequestingManager"], TaskWakeRules.ProductionOrder);
    }

    /// <summary>Each rule reports the name the order lists it under, so a log line and the order agree.</summary>
    [Fact]
    public void Names_MatchProductionOrder()
    {
        using WakeRuleKit kit = new();
        ITaskWakeRule[] rules = [new AssigneeWakeRule(), kit.ManagerRule(),kit.RequestingManagerRule()];

        Assert.Equal(TaskWakeRules.ProductionOrder, rules.Select(static r => r.Name).ToArray());
    }
}

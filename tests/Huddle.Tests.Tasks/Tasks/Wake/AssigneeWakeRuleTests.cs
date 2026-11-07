using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Wake;

namespace Agency.Huddle.Tests.Tasks.Wake;

/// <summary>
/// <see cref="AssigneeWakeRule"/> turns the assignee's <see cref="WakePreview"/> into a choice, as
/// <c>TaskTriggerService.WakeAssigneeAsync</c> did (TaskTriggerService.cs:927-946, Spec §10.2). The rule is constructed with no arguments:
/// the guards were already applied by <c>Preview</c>, which the round carries.
/// </summary>
public sealed class AssigneeWakeRuleTests
{
    private static TaskWakeChoice Choose(WakePreview preview)
    {
        TaskWakeRound round = WakeRuleKit.Round(TestTasks.Make(assignee: "Nova")) with { AssigneePreview = preview };
        return new AssigneeWakeRule().Choose(round);
    }

    /// <summary>A clear preview naming an assignee wakes that assignee (TaskTriggerService.cs:945).</summary>
    [Fact]
    public void Choose_PreviewClear_WakesAssignee()
    {
        TaskWakeChoice choice = Choose(new WakePreview("Nova", null, WakeBlock.None));

        Assert.Equal(new TaskWakeChoice.Wake("Nova"), choice);
    }

    /// <summary>A spent wake budget with a named assignee holds the wake for the budget instead of dropping it (TaskTriggerService.cs:931-936).</summary>
    [Fact]
    public void Choose_BudgetPausedWithAssignee_HoldsForBudget()
    {
        TaskWakeChoice choice = Choose(new WakePreview("Nova", null, WakeBlock.BudgetPaused));

        Assert.Equal(new TaskWakeChoice.HoldForBudget("Nova"), choice);
    }

    /// <summary>BudgetPaused with no assignee name cannot be held, so it is skipped with its block (TaskTriggerService.cs:931 falls through to :939-942).</summary>
    [Fact]
    public void Choose_BudgetPausedWithoutAssignee_SkipsWithBlock()
    {
        TaskWakeChoice choice = Choose(new WakePreview(null, null, WakeBlock.BudgetPaused));

        Assert.Equal(new TaskWakeChoice.Skip(WakeBlock.BudgetPaused), choice);
    }

    /// <summary>Every other block skips and reports that block (TaskTriggerService.cs:939-942).</summary>
    /// <param name="block">The block the preview carries.</param>
    [Theory]
    [InlineData(WakeBlock.NoAssignee)]
    [InlineData(WakeBlock.AssigneeIsHuman)]
    [InlineData(WakeBlock.AssigneeIsActor)]
    [InlineData(WakeBlock.Disabled)]
    [InlineData(WakeBlock.RoomBudgetSpent)]
    public void Choose_OtherBlock_SkipsWithThatBlock(WakeBlock block)
    {
        TaskWakeChoice choice = Choose(new WakePreview("Nova", null, block));

        Assert.Equal(new TaskWakeChoice.Skip(block), choice);
    }

    /// <summary>No block but no assignee name still skips, reporting <see cref="WakeBlock.None"/> (TaskTriggerService.cs:939 second operand).</summary>
    [Fact]
    public void Choose_NoBlockButNoName_SkipsWithNone()
    {
        TaskWakeChoice choice = Choose(new WakePreview(null, null, WakeBlock.None));

        Assert.Equal(new TaskWakeChoice.Skip(WakeBlock.None), choice);
    }
}

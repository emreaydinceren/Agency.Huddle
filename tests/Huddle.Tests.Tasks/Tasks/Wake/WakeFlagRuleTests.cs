using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Wake;

namespace Agency.Huddle.Tests.Tasks.Wake;

/// <summary>
/// Smart wakes spec 5.3 and correction 7: a <see cref="TaskWakeRound"/> says whether its batch wakes the assignee and whether it wakes the
/// manager, and <see cref="AssigneeWakeRule"/> and <see cref="ManagerWakeRule"/> skip with no block when it does not. The flags default to
/// true, so a round built the old way (and every existing rule test) still wakes.
/// </summary>
public sealed class WakeFlagRuleTests
{
    private static readonly TaskWakeChoice NoOne = new TaskWakeChoice.Skip(null);

    /// <summary>A kit with Personas Nova and Kai and Platform managed by Nova.</summary>
    private static WakeRuleKit Arranged()
    {
        WakeRuleKit kit = new();
        kit.AddPersonas("Nova", "Kai");
        kit.SetTeams(("Platform", "Nova", ["Nova", "Kai"]));
        return kit;
    }

    /// <summary>A round built with the four positional arguments carries both flags as true and no Mentioned names (correction 7).</summary>
    [Fact]
    public void TaskWakeRound_FlagsAndMentionedOmitted_DefaultToWakingBothAndNoMentions()
    {
        TaskWakeRound round = WakeRuleKit.Round(TestTasks.Make(assignee: "Kai"));

        Assert.Equal([true, true], [round.WakesAssignee, round.WakesManager]);
        Assert.Null(round.Mentioned);
    }

    /// <summary>A batch that does not wake the assignee skips the assignee rule with no block, whatever the preview says, and a batch that does wakes them.</summary>
    [Fact]
    public void AssigneeRule_RoundDoesNotWakeTheAssignee_SkipsWithNoBlock()
    {
        TaskWakeRound round = WakeRuleKit.Round(TestTasks.Make(assignee: "Nova")) with { AssigneePreview = new WakePreview("Nova", null, WakeBlock.None) };

        TaskWakeChoice[] choices =
        [
            new AssigneeWakeRule().Choose(round with { WakesAssignee = false }),
            new AssigneeWakeRule().Choose(round with { WakesAssignee = true }),
        ];

        Assert.Equal([NoOne, new TaskWakeChoice.Wake("Nova")], choices);
    }

    /// <summary>A batch that does not wake the assignee does not hold the assignee's wake for a spent budget either: there is nothing to hold.</summary>
    [Fact]
    public void AssigneeRule_RoundDoesNotWakeTheAssigneeAndBudgetIsSpent_SkipsInsteadOfHolding()
    {
        TaskWakeRound round = WakeRuleKit.Round(TestTasks.Make(assignee: "Nova")) with
        {
            AssigneePreview = new WakePreview("Nova", null, WakeBlock.BudgetPaused),
            WakesAssignee = false,
        };

        Assert.Equal(NoOne, new AssigneeWakeRule().Choose(round));
    }

    /// <summary>A batch that does not wake the manager skips the manager rule, and a batch that does wakes them: the two flags are separate, so a To Do to In Progress move wakes the assignee only.</summary>
    [Fact]
    public void ManagerRule_RoundDoesNotWakeTheManager_Skips()
    {
        using WakeRuleKit kit = Arranged();
        TaskWakeRound round = WakeRuleKit.Round(TestTasks.Make(assignee: "Kai"));

        TaskWakeChoice[] choices =
        [
            kit.ManagerRule().Choose(round with { WakesManager = false }),
            kit.ManagerRule().Choose(round with { WakesAssignee = false, WakesManager = true }),
        ];

        Assert.Equal([NoOne, new TaskWakeChoice.Wake("Nova")], choices);
    }
}

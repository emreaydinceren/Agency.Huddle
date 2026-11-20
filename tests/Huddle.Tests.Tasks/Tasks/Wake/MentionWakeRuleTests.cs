using Agency.Huddle.App.Tasks.Wake;

namespace Agency.Huddle.Tests.Tasks.Wake;

/// <summary>
/// <see cref="MentionWakeRule"/> (smart wakes spec 5.3, SW-T4): wakes each Agent a batch's comments Mention, after the wake-up switches, minus the
/// actor, the Human and anyone who is no known Persona. Built as <c>new MentionWakeRule(human, personas, options)</c>; its choice is
/// <see cref="TaskWakeChoice.WakeEach"/>, whose list the tests read through <c>Names</c> because a record holding a list does not compare by content.
/// </summary>
public sealed class MentionWakeRuleTests
{
    private static readonly TaskWakeChoice NoOne = new TaskWakeChoice.Skip(null);

    /// <summary>A kit with Personas Nova, Kai, Rex and "You" (so the Human is a known Persona and only the Human check can exclude it).</summary>
    private static WakeRuleKit Arranged()
    {
        WakeRuleKit kit = new();
        kit.AddPersonas("Nova", "Kai", "Rex", "You");
        return kit;
    }

    /// <summary>The names a <see cref="TaskWakeChoice.WakeEach"/> carries.</summary>
    private static IReadOnlyList<string> NamesOf(TaskWakeChoice choice) => Assert.IsType<TaskWakeChoice.WakeEach>(choice).Names;

    /// <summary>The control every "wakes nobody" test runs first, so it cannot pass against a rule that wakes nobody ever: a known, Mentioned Agent other than the actor is chosen.</summary>
    private static void AssertRuleIsLive(WakeRuleKit kit) =>
        Assert.Equal(["Rex"], NamesOf(kit.MentionRule().Choose(WakeRuleKit.MentionRound(TestTasks.Make(assignee: "Nova"), "Kai", "Rex"))));

    /// <summary>Every Mentioned Agent is chosen, in the round's order, whatever the assignee and manager flags say.</summary>
    [Fact]
    public void Choose_MentionedKnownPersonas_WakesEachInOrder()
    {
        using WakeRuleKit kit = Arranged();

        TaskWakeChoice choice = kit.MentionRule().Choose(WakeRuleKit.MentionRound(TestTasks.Make(assignee: "Nova"), "Kai", "Rex", "Nova"));

        Assert.Equal(["Rex", "Nova"], NamesOf(choice));
    }

    /// <summary>A round whose Mentioned list was never filled (<see langword="null"/>) wakes nobody and logs nothing.</summary>
    [Fact]
    public void Choose_MentionedIsNull_Skips()
    {
        using WakeRuleKit kit = Arranged();
        AssertRuleIsLive(kit);

        Assert.Equal(NoOne, kit.MentionRule().Choose(WakeRuleKit.Round(TestTasks.Make(assignee: "Nova"), "Kai")));
    }

    /// <summary>A round whose Mentioned list is empty wakes nobody and logs nothing.</summary>
    [Fact]
    public void Choose_MentionedIsEmpty_Skips()
    {
        using WakeRuleKit kit = Arranged();
        AssertRuleIsLive(kit);

        Assert.Equal(NoOne, kit.MentionRule().Choose(WakeRuleKit.MentionRound(TestTasks.Make(assignee: "Nova"), "Kai")));
    }

    /// <summary>The actor is never woken for their own Mention, compared ignoring case, and the others are.</summary>
    [Fact]
    public void Choose_MentionsTheActor_LeavesTheActorOut()
    {
        using WakeRuleKit kit = Arranged();

        TaskWakeChoice choice = kit.MentionRule().Choose(WakeRuleKit.MentionRound(TestTasks.Make(assignee: "Nova"), "Kai", "kai", "Rex"));

        Assert.Equal(["Rex"], NamesOf(choice));
    }

    /// <summary>The Human is never woken, even when a Persona of the same Name exists.</summary>
    [Fact]
    public void Choose_MentionsTheHuman_LeavesTheHumanOut()
    {
        using WakeRuleKit kit = Arranged();

        TaskWakeChoice choice = kit.MentionRule().Choose(WakeRuleKit.MentionRound(TestTasks.Make(assignee: "Nova"), "Kai", "You", "Rex"));

        Assert.Equal(["Rex"], NamesOf(choice));
    }

    /// <summary>A name that is no known Persona is left out.</summary>
    [Fact]
    public void Choose_MentionsAnUnknownName_LeavesItOut()
    {
        using WakeRuleKit kit = Arranged();

        TaskWakeChoice choice = kit.MentionRule().Choose(WakeRuleKit.MentionRound(TestTasks.Make(assignee: "Nova"), "Kai", "Zed", "Rex"));

        Assert.Equal(["Rex"], NamesOf(choice));
    }

    /// <summary>When every Mentioned name is excluded the rule wakes nobody: <c>Skip(null)</c>, not an empty <c>WakeEach</c>.</summary>
    [Fact]
    public void Choose_EveryMentionExcluded_Skips()
    {
        using WakeRuleKit kit = Arranged();
        AssertRuleIsLive(kit);

        TaskWakeChoice choice = kit.MentionRule().Choose(WakeRuleKit.MentionRound(TestTasks.Make(assignee: "Nova"), "Kai", "Kai", "You", "Zed"));

        Assert.Equal(NoOne, choice);
    }

    /// <summary>With <c>Tasks.WakeEnabled</c> off the rule wakes nobody.</summary>
    [Fact]
    public void Choose_WakeUpsOff_Skips()
    {
        using WakeRuleKit kit = Arranged();
        AssertRuleIsLive(kit);
        kit.Options.Tasks.WakeEnabled = false;

        Assert.Equal(NoOne, kit.MentionRule().Choose(WakeRuleKit.MentionRound(TestTasks.Make(assignee: "Nova"), "Kai", "Rex")));
    }

    /// <summary>With <c>Tasks.Enabled</c> off the rule wakes nobody.</summary>
    [Fact]
    public void Choose_TasksOff_Skips()
    {
        using WakeRuleKit kit = Arranged();
        AssertRuleIsLive(kit);
        kit.Options.Tasks.Enabled = false;

        Assert.Equal(NoOne, kit.MentionRule().Choose(WakeRuleKit.MentionRound(TestTasks.Make(assignee: "Nova"), "Kai", "Rex")));
    }
}

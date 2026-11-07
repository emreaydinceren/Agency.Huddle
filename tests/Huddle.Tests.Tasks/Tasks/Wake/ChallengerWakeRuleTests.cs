using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Wake;

namespace Agency.Huddle.Tests.Tasks.Wake;

/// <summary>
/// <see cref="ChallengerWakeRule"/> (#403) chooses who reviews a Task that entered Review as
/// <c>TaskTriggerService.WakeChallengerAsync</c> did (TaskTriggerService.cs:989-1028). Built as
/// <c>new ChallengerWakeRule(teams, human, personas, confirmers, options)</c>.
/// </summary>
public sealed class ChallengerWakeRuleTests
{
    private static readonly TaskWakeChoice NoOne = new TaskWakeChoice.Skip(null);

    /// <summary>A kit with Personas Kai (builder), Mia, Nova and Zed; Platform has no manager; Mia and Nova can review; members in that order.</summary>
    private static WakeRuleKit Arranged(string? manager = null, string[]? members = null)
    {
        WakeRuleKit kit = new();
        kit.AddPersonas("Kai", "Mia", "Nova", "Zed");
        kit.SetTeams(("Platform", manager, members ?? ["Kai", "Mia", "Nova"]));
        kit.Challengers.Add("Platform", "Mia");
        kit.Challengers.Add("Platform", "Nova");
        return kit;
    }

    private static TaskItem InReview(string? assignee = "Kai") => TestTasks.Make(status: TaskState.Review, assignee: assignee);

    private static TaskWakeRound Entered(TaskItem task, string actorName = "You") => WakeRuleKit.Round(task, actorName, movedToReview: true);

    /// <summary>The first member who can review is woken as challenger (TaskTriggerService.cs:1016-1026).</summary>
    [Fact]
    public void Choose_EnteredReview_WakesFirstChallengerAsChallenger()
    {
        using WakeRuleKit kit = Arranged();

        Assert.Equal(new TaskWakeChoice.Wake("Mia", AsChallenger: true), kit.ChallengerRule().Choose(Entered(InReview())));
    }

    /// <summary>A Task that did not move to Review in this batch wakes nobody (TaskTriggerService.cs:991).</summary>
    [Fact]
    public void Choose_DidNotMoveToReview_Skips()
    {
        using WakeRuleKit kit = Arranged();

        Assert.Equal(NoOne, kit.ChallengerRule().Choose(WakeRuleKit.Round(InReview(), movedToReview: false)));
    }

    /// <summary>A Task no longer in Review when the fire runs wakes nobody (TaskTriggerService.cs:992).</summary>
    [Fact]
    public void Choose_StatusNotReview_Skips()
    {
        using WakeRuleKit kit = Arranged();
        TaskItem task = TestTasks.Make(status: TaskState.InProgress, assignee: "Kai");

        Assert.Equal(NoOne, kit.ChallengerRule().Choose(Entered(task)));
    }

    /// <summary>With no confirmers wired, nobody is woken (TaskTriggerService.cs:993).</summary>
    [Fact]
    public void Choose_NoConfirmers_Skips()
    {
        using WakeRuleKit kit = Arranged();

        Assert.Equal(NoOne, kit.ChallengerRule(withConfirmers: false).Choose(Entered(InReview())));
    }

    /// <summary>Tasks turned off wakes nobody (TaskTriggerService.cs:994).</summary>
    [Fact]
    public void Choose_TasksDisabled_Skips()
    {
        using WakeRuleKit kit = Arranged();
        kit.Options.Tasks.Enabled = false;

        Assert.Equal(NoOne, kit.ChallengerRule().Choose(Entered(InReview())));
    }

    /// <summary>Wake-ups turned off wakes nobody (TaskTriggerService.cs:995).</summary>
    [Fact]
    public void Choose_WakeDisabled_Skips()
    {
        using WakeRuleKit kit = Arranged();
        kit.Options.Tasks.WakeEnabled = false;

        Assert.Equal(NoOne, kit.ChallengerRule().Choose(Entered(InReview())));
    }

    /// <summary>An assignee who can review was woken as the assignee, so nobody else is (TaskTriggerService.cs:1002-1005).</summary>
    [Fact]
    public void Choose_AssigneeIsChallenger_Skips()
    {
        using WakeRuleKit kit = Arranged();

        Assert.Equal(NoOne, kit.ChallengerRule().Choose(Entered(InReview(assignee: "Nova"))));
    }

    /// <summary>A manager who did not make the change and is not the assignee was woken for this entry, so nobody else is (TaskTriggerService.cs:1007-1013).</summary>
    [Fact]
    public void Choose_ManagerWasWoken_Skips()
    {
        using WakeRuleKit kit = Arranged(manager: "Zed");

        Assert.Equal(NoOne, kit.ChallengerRule().Choose(Entered(InReview())));
    }

    /// <summary>A manager who made the change was not woken, so the challenger is (TaskTriggerService.cs:1008).</summary>
    [Fact]
    public void Choose_ManagerIsActor_StillWakesChallenger()
    {
        using WakeRuleKit kit = Arranged(manager: "Zed");

        Assert.Equal(new TaskWakeChoice.Wake("Mia", AsChallenger: true), kit.ChallengerRule().Choose(Entered(InReview(), actorName: "Zed")));
    }

    /// <summary>A manager who is the assignee was woken as the assignee, not as a manager, so the challenger is woken (TaskTriggerService.cs:1009).</summary>
    [Fact]
    public void Choose_ManagerIsAssignee_StillWakesChallenger()
    {
        using WakeRuleKit kit = Arranged(manager: "Kai");

        Assert.Equal(new TaskWakeChoice.Wake("Mia", AsChallenger: true), kit.ChallengerRule().Choose(Entered(InReview())));
    }

    /// <summary>An assignee-less Task still gets a challenger (TaskTriggerService.cs:1002, <c>Length: &gt; 0</c>).</summary>
    [Fact]
    public void Choose_NoAssignee_WakesChallenger()
    {
        using WakeRuleKit kit = Arranged();

        Assert.Equal(new TaskWakeChoice.Wake("Mia", AsChallenger: true), kit.ChallengerRule().Choose(Entered(InReview(assignee: null))));
    }

    /// <summary>A challenger who made the change is passed over for the next one (TaskTriggerService.cs:1018).</summary>
    [Fact]
    public void Choose_FirstChallengerIsActor_WakesNext()
    {
        using WakeRuleKit kit = Arranged();

        Assert.Equal(new TaskWakeChoice.Wake("Nova", AsChallenger: true), kit.ChallengerRule().Choose(Entered(InReview(), actorName: "mia")));
    }

    /// <summary>A member who is the Human is passed over (TaskTriggerService.cs:1021).</summary>
    [Fact]
    public void Choose_FirstChallengerIsHuman_WakesNext()
    {
        using WakeRuleKit kit = Arranged(members: ["Kai", "You", "Nova"]);
        kit.Challengers.Add("Platform", "You");

        Assert.Equal(new TaskWakeChoice.Wake("Nova", AsChallenger: true), kit.ChallengerRule().Choose(Entered(InReview())));
    }

    /// <summary>A member who is no known Persona is passed over (TaskTriggerService.cs:1022).</summary>
    [Fact]
    public void Choose_FirstChallengerIsNoPersona_WakesNext()
    {
        using WakeRuleKit kit = Arranged(members: ["Kai", "Ghost", "Nova"]);
        kit.Challengers.Add("Platform", "Ghost");

        Assert.Equal(new TaskWakeChoice.Wake("Nova", AsChallenger: true), kit.ChallengerRule().Choose(Entered(InReview())));
    }

    /// <summary>A member who cannot review is never chosen, so a Team with nobody who can is left as before (TaskTriggerService.cs:1015-1017, :1023).</summary>
    [Fact]
    public void Choose_NobodyCanReview_Skips()
    {
        using WakeRuleKit kit = Arranged(members: ["Kai", "Zed"]);

        Assert.Equal(NoOne, kit.ChallengerRule().Choose(Entered(InReview())));
    }

    /// <summary>A Team the catalog does not know has no members to wake (TaskTriggerService.cs:1015, <c>?? []</c>).</summary>
    [Fact]
    public void Choose_TeamUnknown_Skips()
    {
        using WakeRuleKit kit = Arranged();
        TaskItem task = TestTasks.Make(status: TaskState.Review, assignee: "Kai", location: new TaskLocation("Elsewhere", null, false));

        Assert.Equal(NoOne, kit.ChallengerRule().Choose(Entered(task)));
    }
}

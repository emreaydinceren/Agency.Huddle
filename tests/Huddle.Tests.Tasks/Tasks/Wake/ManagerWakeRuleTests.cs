using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Wake;

namespace Agency.Huddle.Tests.Tasks.Wake;

/// <summary>
/// <see cref="ManagerWakeRule"/> (OR-2) chooses the Team's manager as <c>TaskTriggerService.WakeManagerAsync</c> did
/// (TaskTriggerService.cs:959-977). Built as <c>new ManagerWakeRule(teams, human, personas, options)</c>.
/// </summary>
public sealed class ManagerWakeRuleTests
{
    private static readonly TaskWakeChoice NoOne = new TaskWakeChoice.Skip(null);

    /// <summary>A kit with Persona Nova and Kai, and Platform managed by Nova.</summary>
    private static WakeRuleKit Arranged(string? manager = "Nova")
    {
        WakeRuleKit kit = new();
        kit.AddPersonas("Nova", "Kai");
        kit.SetTeams(("Platform", manager, ["Nova", "Kai"]));
        return kit;
    }

    /// <summary>The Team's manager is woken when nothing excludes them (TaskTriggerService.cs:976).</summary>
    [Fact]
    public void Choose_ManagerIsOtherPersona_WakesManager()
    {
        using WakeRuleKit kit = Arranged();

        TaskWakeChoice choice = kit.ManagerRule().Choose(WakeRuleKit.Round(TestTasks.Make(assignee: "Kai")));

        Assert.Equal(new TaskWakeChoice.Wake("Nova"), choice);
    }

    /// <summary>A Team with no manager wakes nobody (TaskTriggerService.cs:961).</summary>
    [Fact]
    public void Choose_TeamHasNoManager_Skips()
    {
        using WakeRuleKit kit = Arranged(manager: null);

        Assert.Equal(NoOne, kit.ManagerRule().Choose(WakeRuleKit.Round(TestTasks.Make(assignee: "Kai"))));
    }

    /// <summary>An empty manager Name is no manager (TaskTriggerService.cs:961, <c>Length: &gt; 0</c>).</summary>
    [Fact]
    public void Choose_ManagerNameEmpty_Skips()
    {
        using WakeRuleKit kit = Arranged(manager: "");

        Assert.Equal(NoOne, kit.ManagerRule().Choose(WakeRuleKit.Round(TestTasks.Make(assignee: "Kai"))));
    }

    /// <summary>No catalog at all behaves as before: nobody (TaskTriggerService.cs:961, <c>teams?.Find</c>).</summary>
    [Fact]
    public void Choose_NoCatalog_Skips()
    {
        using WakeRuleKit kit = Arranged();

        Assert.Equal(NoOne, kit.ManagerRule(withCatalog: false).Choose(WakeRuleKit.Round(TestTasks.Make(assignee: "Kai"))));
    }

    /// <summary>A Task in a Team the catalog does not know wakes nobody (TaskTriggerService.cs:961).</summary>
    [Fact]
    public void Choose_TeamUnknown_Skips()
    {
        using WakeRuleKit kit = Arranged();
        TaskItem task = TestTasks.Make(assignee: "Kai", location: new TaskLocation("Elsewhere", null, false));

        Assert.Equal(NoOne, kit.ManagerRule().Choose(WakeRuleKit.Round(task)));
    }

    /// <summary>Tasks turned off wakes nobody (TaskTriggerService.cs:962).</summary>
    [Fact]
    public void Choose_TasksDisabled_Skips()
    {
        using WakeRuleKit kit = Arranged();
        kit.Options.Tasks.Enabled = false;

        Assert.Equal(NoOne, kit.ManagerRule().Choose(WakeRuleKit.Round(TestTasks.Make(assignee: "Kai"))));
    }

    /// <summary>Wake-ups turned off wakes nobody (TaskTriggerService.cs:963).</summary>
    [Fact]
    public void Choose_WakeDisabled_Skips()
    {
        using WakeRuleKit kit = Arranged();
        kit.Options.Tasks.WakeEnabled = false;

        Assert.Equal(NoOne, kit.ManagerRule().Choose(WakeRuleKit.Round(TestTasks.Make(assignee: "Kai"))));
    }

    /// <summary>The manager who made the change is not woken, ignoring case (TaskTriggerService.cs:968).</summary>
    [Fact]
    public void Choose_ManagerIsActor_Skips()
    {
        using WakeRuleKit kit = Arranged();

        Assert.Equal(NoOne, kit.ManagerRule().Choose(WakeRuleKit.Round(TestTasks.Make(assignee: "Kai"), actorName: "NOVA")));
    }

    /// <summary>A manager who is the assignee was woken as the assignee, so is not woken twice, ignoring case (TaskTriggerService.cs:969).</summary>
    [Fact]
    public void Choose_ManagerIsAssignee_Skips()
    {
        using WakeRuleKit kit = Arranged();

        Assert.Equal(NoOne, kit.ManagerRule().Choose(WakeRuleKit.Round(TestTasks.Make(assignee: "nova"))));
    }

    /// <summary>A manager who is the Human is never woken (TaskTriggerService.cs:970).</summary>
    [Fact]
    public void Choose_ManagerIsHuman_Skips()
    {
        using WakeRuleKit kit = Arranged(manager: "You");

        Assert.Equal(NoOne, kit.ManagerRule().Choose(WakeRuleKit.Round(TestTasks.Make(assignee: "Kai"), actorName: "Kai")));
    }

    /// <summary>A manager who is no known Persona is not woken (TaskTriggerService.cs:971).</summary>
    [Fact]
    public void Choose_ManagerIsNoPersona_Skips()
    {
        using WakeRuleKit kit = Arranged(manager: "Ghost");

        Assert.Equal(NoOne, kit.ManagerRule().Choose(WakeRuleKit.Round(TestTasks.Make(assignee: "Kai"))));
    }
}

using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Wake;

namespace Agency.Huddle.Tests.Tasks.Wake;

/// <summary>
/// <see cref="RequestingManagerWakeRule"/> (RQ-4) chooses the manager of the Team that filed a Request as
/// <c>TaskTriggerService.WakeRequestingManagerAsync</c> did (TaskTriggerService.cs:810-845). Built as
/// <c>new RequestingManagerWakeRule(teams, human, personas, options)</c>.
/// </summary>
public sealed class RequestingManagerWakeRuleTests
{
    private static readonly TaskWakeChoice NoOne = new TaskWakeChoice.Skip(null);

    /// <summary>A kit where Design (manager Mia) filed to Platform (manager Nova); Personas Kai, Mia, Nova.</summary>
    private static WakeRuleKit Arranged(string? designManager = "Mia", string? platformManager = "Nova")
    {
        WakeRuleKit kit = new();
        kit.AddPersonas("Kai", "Mia", "Nova");
        kit.SetTeams(("Platform", platformManager, ["Nova", "Kai"]), ("Design", designManager, ["Mia"]));
        return kit;
    }

    /// <summary>A Request filed by <paramref name="fromTeam"/> and assigned to Kai, or an ordinary Task when <paramref name="isRequest"/> is false.</summary>
    private static TaskItem Request(string fromTeam = "Design", bool isRequest = true, string? assignee = "Kai") =>
        TestTasks.Make(
            assignee: assignee,
            unknownFields: isRequest ? [.. new RequestFields(RequestKind.Bug, fromTeam, "Mia", TriageState.None, string.Empty, string.Empty).ToFrontmatter()] : []);

    private static TaskWakeRound Changed(TaskItem task, string actorName = "You", bool statusChanged = true) =>
        WakeRuleKit.Round(task, actorName, statusChanged: statusChanged);

    /// <summary>The requesting Team's manager is woken on a status change of a Request (TaskTriggerService.cs:844).</summary>
    [Fact]
    public void Choose_RequestStatusChanged_WakesRequestingManager()
    {
        using WakeRuleKit kit = Arranged();

        Assert.Equal(new TaskWakeChoice.Wake("Mia"), kit.RequestingManagerRule().Choose(Changed(Request())));
    }

    /// <summary>A batch that changed no status wakes nobody (TaskTriggerService.cs:812).</summary>
    [Fact]
    public void Choose_NoStatusChange_Skips()
    {
        using WakeRuleKit kit = Arranged();

        Assert.Equal(NoOne, kit.RequestingManagerRule().Choose(Changed(Request(), statusChanged: false)));
    }

    /// <summary>Tasks turned off wakes nobody (TaskTriggerService.cs:812).</summary>
    [Fact]
    public void Choose_TasksDisabled_Skips()
    {
        using WakeRuleKit kit = Arranged();
        kit.Options.Tasks.Enabled = false;

        Assert.Equal(NoOne, kit.RequestingManagerRule().Choose(Changed(Request())));
    }

    /// <summary>Wake-ups turned off wakes nobody (TaskTriggerService.cs:812).</summary>
    [Fact]
    public void Choose_WakeDisabled_Skips()
    {
        using WakeRuleKit kit = Arranged();
        kit.Options.Tasks.WakeEnabled = false;

        Assert.Equal(NoOne, kit.RequestingManagerRule().Choose(Changed(Request())));
    }

    /// <summary>An ordinary Task, not a Request, wakes nobody (TaskTriggerService.cs:823-826).</summary>
    [Fact]
    public void Choose_NotARequest_Skips()
    {
        using WakeRuleKit kit = Arranged();

        Assert.Equal(NoOne, kit.RequestingManagerRule().Choose(Changed(Request(isRequest: false))));
    }

    /// <summary>A requesting Team with no manager wakes nobody (TaskTriggerService.cs:828-833).</summary>
    [Fact]
    public void Choose_RequestingTeamHasNoManager_Skips()
    {
        using WakeRuleKit kit = Arranged(designManager: null);

        Assert.Equal(NoOne, kit.RequestingManagerRule().Choose(Changed(Request())));
    }

    /// <summary>A requesting Team the catalog does not know has no manager, so wakes nobody (TaskTriggerService.cs:828-833).</summary>
    [Fact]
    public void Choose_RequestingTeamUnknown_Skips()
    {
        using WakeRuleKit kit = Arranged();

        Assert.Equal(NoOne, kit.RequestingManagerRule().Choose(Changed(Request(fromTeam: "Elsewhere"))));
    }

    /// <summary>A manager who made the change is not woken, ignoring case (TaskTriggerService.cs:835).</summary>
    [Fact]
    public void Choose_ManagerIsActor_Skips()
    {
        using WakeRuleKit kit = Arranged();

        Assert.Equal(NoOne, kit.RequestingManagerRule().Choose(Changed(Request(), actorName: "MIA")));
    }

    /// <summary>A manager who is the assignee was woken as the assignee (TaskTriggerService.cs:836).</summary>
    [Fact]
    public void Choose_ManagerIsAssignee_Skips()
    {
        using WakeRuleKit kit = Arranged();

        Assert.Equal(NoOne, kit.RequestingManagerRule().Choose(Changed(Request(assignee: "mia"))));
    }

    /// <summary>A manager who is also the receiving Team's manager was woken by the manager rule (TaskTriggerService.cs:837).</summary>
    [Fact]
    public void Choose_ManagerIsReceivingManager_Skips()
    {
        using WakeRuleKit kit = Arranged(platformManager: "Mia");

        Assert.Equal(NoOne, kit.RequestingManagerRule().Choose(Changed(Request())));
    }

    /// <summary>A manager who is the Human is never woken (TaskTriggerService.cs:838).</summary>
    [Fact]
    public void Choose_ManagerIsHuman_Skips()
    {
        using WakeRuleKit kit = Arranged(designManager: "You");

        Assert.Equal(NoOne, kit.RequestingManagerRule().Choose(Changed(Request(), actorName: "Kai")));
    }

    /// <summary>A manager who is no known Persona is not woken (TaskTriggerService.cs:839).</summary>
    [Fact]
    public void Choose_ManagerIsNoPersona_Skips()
    {
        using WakeRuleKit kit = Arranged(designManager: "Ghost");

        Assert.Equal(NoOne, kit.RequestingManagerRule().Choose(Changed(Request())));
    }
}

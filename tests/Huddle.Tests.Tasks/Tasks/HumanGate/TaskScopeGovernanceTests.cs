using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// The scope that governs a Task's Needs You level is the one Huddle placed it in (where it was created, or where the Human last moved it)
/// and never a laxer one the Task reached by an Agent's <c>update_task</c> or a shell move (issue #602). Team Platform has Project <c>Site</c>.
/// </summary>
public sealed class TaskScopeGovernanceTests
{
    private static TaskPatch ToProject(string? project) => new() { Project = Optional<string?>.Set(project) };

    /// <summary>
    /// A Task of the Team's own scope (Ask me) that an Agent moves into Project Site (Run inside the Team) stays under Ask me: its ready Proof
    /// is not released in Review but goes to Needs You with the merge Action.
    /// </summary>
    [Fact]
    public void AgentMove_IntoRunInsideProject_IsNotReleased()
    {
        using NeedsYouLevelRig rig = new(projectLevel: NeedsYouLevel.RunInsideTheTeam);
        TaskItem task = rig.CreateInReview();

        _ = Assert.IsType<TaskResult.Saved>(rig.Service.Update(task.Id, ToProject(NeedsYouLevelRig.Project), null, NeedsYouLevelRig.Nova));
        _ = rig.AddEvidence(task);

        Assert.Equal(NeedsYouLevelRig.Project, rig.Read(task).Location.Project);
        Assert.Equal(NeedsYouLevel.AskMe, rig.Levels.ForTask(rig.Read(task)).Level);
        Assert.Empty(rig.Releases(task));
        Assert.Equal(TaskState.NeedsYou, rig.Read(task).Status);
    }

    /// <summary>A Task created in a Run inside project that an Agent moves to the Team root (Ask me) becomes Ask me: the stricter scope wins.</summary>
    [Fact]
    public void AgentMove_IntoAskMeScope_MakesTheTaskAskMe()
    {
        using NeedsYouLevelRig rig = new(projectLevel: NeedsYouLevel.RunInsideTheTeam);
        TaskItem task = rig.CreateInReview(NeedsYouLevelRig.Project);
        Assert.Equal(NeedsYouLevel.RunInsideTheTeam, rig.Levels.ForTask(rig.Read(task)).Level);

        _ = Assert.IsType<TaskResult.Saved>(rig.Service.Update(task.Id, ToProject(null), null, NeedsYouLevelRig.Nova));

        Assert.Equal(NeedsYouLevel.AskMe, rig.Levels.ForTask(rig.Read(task)).Level);
    }

    /// <summary>The Human moving a Task into a Run inside project re-places it: from then on it runs inside the Team.</summary>
    [Fact]
    public void HumanMove_IntoRunInsideProject_MakesTheTaskRunInside()
    {
        using NeedsYouLevelRig rig = new(projectLevel: NeedsYouLevel.RunInsideTheTeam);
        TaskItem task = rig.CreateInReview();
        Assert.Equal(NeedsYouLevel.AskMe, rig.Levels.ForTask(rig.Read(task)).Level);

        _ = Assert.IsType<TaskResult.Saved>(rig.Service.Update(task.Id, ToProject(NeedsYouLevelRig.Project), null, rig.Human));
        _ = rig.AddEvidence(task);

        EffectiveLevel level = rig.Levels.ForTask(rig.Read(task));
        Assert.Equal((NeedsYouLevel.RunInsideTheTeam, NeedsYouScope.Project), (level.Level, level.Scope));
        _ = Assert.Single(rig.Releases(task));
    }

    /// <summary>A Task Huddle never placed (no record, as a file written by a shell) is under Ask me even inside a Run inside project.</summary>
    [Fact]
    public void Task_WithNoRecord_IsAskMe()
    {
        using NeedsYouLevelRig rig = new(projectLevel: NeedsYouLevel.RunInsideTheTeam);
        TaskItem placed = rig.CreateInReview(NeedsYouLevelRig.Project);
        TaskItem unplaced = rig.Read(placed) with { Id = new TaskId(placed.Id.Prefix, 900) };

        Assert.Equal(NeedsYouLevel.RunInsideTheTeam, rig.Levels.ForTask(rig.Read(placed)).Level);
        Assert.Equal(NeedsYouLevel.AskMe, rig.Levels.ForTask(unplaced).Level);
    }

    /// <summary>A shell move of the Task file into a Run inside project, which no service saw, keeps the recorded Ask me.</summary>
    [Fact]
    public void ShellMove_IntoRunInsideProject_KeepsTheRecordedLevel()
    {
        using NeedsYouLevelRig rig = new(projectLevel: NeedsYouLevel.RunInsideTheTeam);
        TaskItem task = rig.CreateInReview();
        string text = rig.Harness.Store.ReadText(task.Id) ?? throw new InvalidOperationException("The Task file is missing.");

        TaskItem? moved = rig.Harness.Store.Move(task, task.Version, task.Location with { Project = NeedsYouLevelRig.Project }, text);

        Assert.Equal(NeedsYouLevelRig.Project, moved?.Location.Project);
        Assert.Equal(NeedsYouLevel.AskMe, rig.Levels.ForTask(rig.Read(task)).Level);
    }
}

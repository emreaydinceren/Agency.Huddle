using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.HumanGate;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// What <see cref="TaskService"/> does with a ready ungated Task of a Team with <c>accept-before-merge</c> under each Needs You level
/// (ADR-0088): Ask me names the merge and moves it to Needs You (#416); Run inside the Team releases it instead, in Review, with no Action.
/// </summary>
public sealed class TaskServiceNeedsYouLevelTests
{
    private const string RunInside = "run-inside-team";

    private static string ReleaseText(string scope) => $"proceeded under {scope} level Run inside the Team: its merge into main needs no Accept";

    /// <summary>A project at Run inside the Team: the ready Task is released in Review with no Action, naming the project scope.</summary>
    [Fact]
    public void ReadyTask_ProjectRunInside_IsReleasedNotMoved()
    {
        using NeedsYouLevelRig rig = new(projectLevel: NeedsYouLevel.RunInsideTheTeam);
        TaskItem task = rig.CreateInReview(NeedsYouLevelRig.Project);

        _ = Assert.IsType<TaskResult.Saved>(rig.AddEvidence(task));

        TaskItem stored = rig.Read(task);
        Assert.Equal(TaskState.InProgress, stored.Status);
        Assert.Null(stored.Action);
        Assert.Equal(ReleaseText("project"), Assert.Single(rig.Releases(task)).Summary);
        Assert.Equal("Huddle", Assert.Single(rig.Releases(task)).Actor);
    }

    /// <summary>A Team at Run inside the Team releases a ready Task of the Team, naming the Team scope.</summary>
    [Fact]
    public void ReadyTask_TeamRunInside_IsReleasedNamingTheTeamScope()
    {
        using NeedsYouLevelRig rig = new(teamLevel: NeedsYouLevel.RunInsideTheTeam);
        TaskItem task = rig.CreateInReview();

        _ = rig.AddEvidence(task);

        Assert.Equal(TaskState.InProgress, rig.Read(task).Status);
        Assert.Equal(ReleaseText("Team"), Assert.Single(rig.Releases(task)).Summary);
    }

    /// <summary>The configured global level releases a ready Task, naming the global scope.</summary>
    [Fact]
    public void ReadyTask_GlobalRunInside_IsReleasedNamingTheGlobalScope()
    {
        using NeedsYouLevelRig rig = new(globalLevel: RunInside);
        TaskItem task = rig.CreateInReview();

        _ = rig.AddEvidence(task);

        Assert.Equal(ReleaseText("global"), Assert.Single(rig.Releases(task)).Summary);
    }

    /// <summary>A release raises one change by Huddle, so the wake rules reach the assignee, and posts one Activity item to the Feed.</summary>
    [Fact]
    public void Release_RaisesTheChangeByHuddleAndPostsOneFeedActivity()
    {
        using NeedsYouLevelRig rig = new(teamLevel: NeedsYouLevel.RunInsideTheTeam);
        List<TaskChange> raised = [];
        rig.Harness.Events.TaskChanged += raised.Add;
        TaskItem task = rig.CreateInReview();
        raised.Clear();

        _ = rig.AddEvidence(task);

        TaskChange release = Assert.Single(raised, static change => NeedsYouRelease.IsRelease(change.Entry.Summary));
        Assert.Equal(TaskActors.Huddle, release.Actor);
        FeedMessage post = Assert.Single(rig.Feed.Posts, static message => message.SourceType == FeedSourceType.TaskProceeded);
        Assert.False(post.Notify);
        TaskProceededPayload payload = Assert.IsType<TaskProceededPayload>(post.Payload);
        Assert.Equal((task.Id.ToString(), "Platform", "Team"), (payload.Id, payload.Team, payload.Scope));
    }

    /// <summary>Under Ask me the ready Task still gets the merge as its Action and moves to Needs You, exactly as before (#416).</summary>
    [Fact]
    public void ReadyTask_AskMe_NamesTheMergeAndMovesToNeedsYou()
    {
        using NeedsYouLevelRig rig = new();
        TaskItem task = rig.CreateInReview();

        _ = rig.AddEvidence(task);

        TaskItem stored = rig.Read(task);
        Assert.Equal(TaskState.NeedsYou, stored.Status);
        Assert.Equal(HumanGateRules.MergeAction, stored.Action);
        Assert.Empty(rig.Releases(task));
    }

    /// <summary>A project at Ask me beats a Team at Run inside the Team: that project's Task still goes to Needs You.</summary>
    [Fact]
    public void ReadyTask_ProjectAskMeUnderTeamRunInside_GoesToNeedsYou()
    {
        using NeedsYouLevelRig rig = new(teamLevel: NeedsYouLevel.RunInsideTheTeam, projectLevel: NeedsYouLevel.AskMe);
        TaskItem task = rig.CreateInReview(NeedsYouLevelRig.Project);

        _ = rig.AddEvidence(task);

        Assert.Equal(TaskState.NeedsYou, rig.Read(task).Status);
        Assert.Empty(rig.Releases(task));
    }

    /// <summary>A gated Task is not released by the level: it goes to Needs You as always.</summary>
    [Fact]
    public void ReadyTask_GatedUnderRunInside_StillGoesToNeedsYou()
    {
        using NeedsYouLevelRig rig = new(teamLevel: NeedsYouLevel.RunInsideTheTeam);
        TaskDraft draft = new("Quote calculator", NeedsYouLevelRig.Team, null, TaskState.InProgress, Assignee: "Nova", Description: "Body") { Action = "Merge it" };
        TaskItem task = Assert.IsType<TaskResult.Saved>(rig.Service.Create(draft, rig.Human)).Task;

        _ = rig.AddEvidence(task);

        TaskItem stored = rig.Read(task);
        Assert.Equal(TaskState.NeedsYou, stored.Status);
        Assert.Empty(rig.Releases(task));
    }

    /// <summary>A Task whose gate the Human turned off is neither moved nor released.</summary>
    [Fact]
    public void ReadyTask_HumanTurnedGateOff_IsNeitherMovedNorReleased()
    {
        using NeedsYouLevelRig rig = new(teamLevel: NeedsYouLevel.RunInsideTheTeam);
        TaskDraft draft = new("Quote calculator", NeedsYouLevelRig.Team, null, TaskState.InProgress, Assignee: "Nova", Description: "Body") { HumanGate = true };
        TaskItem task = Assert.IsType<TaskResult.Saved>(rig.Service.Create(draft, rig.Human)).Task;
        _ = Assert.IsType<TaskResult.Saved>(rig.Service.Update(task.Id, new TaskPatch { HumanGate = Optional<bool>.Set(false) }, null, rig.Human));

        _ = rig.AddEvidence(task);

        Assert.Equal(TaskState.InProgress, rig.Read(task).Status);
        Assert.Empty(rig.Releases(task));
    }

    /// <summary>A second ready write within 24 hours does not write a second release; one after 24 hours does.</summary>
    [Fact]
    public void Release_IsWrittenOncePerDay()
    {
        using NeedsYouLevelRig rig = new(teamLevel: NeedsYouLevel.RunInsideTheTeam);
        TaskItem task = rig.CreateInReview();
        _ = rig.AddEvidence(task);

        rig.Clock.Advance(TimeSpan.FromHours(23));
        _ = rig.AddEvidence(task);
        Assert.Single(rig.Releases(task));

        rig.Clock.Advance(TimeSpan.FromHours(2));
        _ = rig.AddEvidence(task);
        Assert.Equal(2, rig.Releases(task).Count);
    }

    /// <summary>A Team without <c>accept-before-merge</c> has nothing to release: the Task stays put.</summary>
    [Fact]
    public void ReadyTask_NoAcceptGate_IsNotReleased()
    {
        using NeedsYouLevelRig rig = new(teamLevel: NeedsYouLevel.RunInsideTheTeam, gates: "'qa-evidence-before-done'");
        TaskItem task = rig.CreateInReview();

        _ = rig.AddEvidence(task);

        Assert.Equal(TaskState.InProgress, rig.Read(task).Status);
        Assert.Empty(rig.Releases(task));
    }
}

using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.HumanGate;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Tasks;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// <see cref="ProcessMergeGate"/> under the Needs You levels (ADR-0088): a released Task passes a local merge into main and never a push;
/// Ask me keeps the texts it always had.
/// </summary>
public sealed class NeedsYouLevelMergeGateTests
{
    private const string FeatureMerge = "git merge feat/x";
    private const string ReleasedMerge = "git merge feat/PLAT-0001-x";
    private const string ChecksFirst = "Gate checks-before-merge: an Evidence line with exit code 0 for 'dotnet test' is needed before merging or pushing.";
    private const string PushText = "Cannot push: a push leaves the Team's repository and waits for the Human's Accept at every Needs You level. Put the push in Task PLAT-0001's Action with update_task, for example 'Push feat/DEV-0012-login to origin'. Huddle moves the Task to Needs You when its Proof is complete; push after the Human accepts it. Do not ask for the Accept in chat.";
    private const string NothingReadyText = "Cannot merge yet: under Run inside the Team a merge into main runs only for a branch named after a Task's id (for example feat/DEV-0012-login), once that Task's Proof is complete, and no Task of your Team is ready. Finish review and QA Evidence first; do not set a merge Action.";
    private const string AskMeText = "Cannot merge: a merge into main waits for the Human's Accept, and no Task of your Team has one. Put the merge in Task PLAT-0001's Action with update_task, for example 'Merge feat/x into main'. Huddle moves the Task to Needs You when its Proof is complete; merge after the Human accepts it. Do not ask for the Accept in chat.";

    private static NeedsYouLevelRig RunInsideRig(string gates = "'accept-before-merge'") => new(teamLevel: NeedsYouLevel.RunInsideTheTeam, gates: gates);

    private static TaskItem Release(NeedsYouLevelRig rig)
    {
        TaskItem task = rig.CreateInReview();
        _ = rig.AddEvidence(task);
        Assert.Single(rig.Releases(task));
        return task;
    }

    /// <summary>A released Task lets a local merge of a feature branch into main through, and refuses every push with the push text.</summary>
    [Fact]
    public void RefusalFor_ReleasedTask_AllowsLocalMergeAndRefusesPushes()
    {
        using NeedsYouLevelRig rig = RunInsideRig();
        _ = Release(rig);

        Assert.Null(rig.Gate.RefusalFor("Nova", ReleasedMerge));
        Assert.Null(rig.Gate.RefusalFor("Nova", "git pull origin feat/PLAT-0001-x"));
        Assert.Equal(PushText, rig.Gate.RefusalFor("Nova", "git push origin main"));
        Assert.Equal(PushText, rig.Gate.RefusalFor("Nova", "git push ../bare main"));
        Assert.Equal(PushText, rig.Gate.RefusalFor("Nova", "git push"));
        Assert.Equal(PushText, rig.Gate.RefusalFor("Nova", "git merge feat/x && git push"));
    }

    /// <summary>A release stops carrying a merge as soon as the Human sets the scope back to Ask me: the gate reads the level again.</summary>
    [Fact]
    public void RefusalFor_ReleasedThenSetBackToAskMe_RefusesTheMerge()
    {
        using NeedsYouLevelRig rig = new(globalLevel: "run-inside-team");
        _ = Release(rig);

        rig.Levels.SetGlobal(NeedsYouLevel.AskMe, null);

        Assert.Equal(AskMeText, rig.Gate.RefusalFor("Nova", ReleasedMerge));
    }

    /// <summary>With nothing released, a local merge is refused with the Run inside the Team text, which tells the Agent not to set an Action.</summary>
    [Fact]
    public void RefusalFor_RunInsideNothingReleased_RefusesWithTheRunInsideText()
    {
        using NeedsYouLevelRig rig = RunInsideRig();
        _ = rig.CreateInReview();

        Assert.Equal(NothingReadyText, rig.Gate.RefusalFor("Nova", FeatureMerge));
    }

    /// <summary>A release older than 24 hours no longer carries a merge.</summary>
    [Fact]
    public void RefusalFor_ReleaseOlderThanADay_IsRefused()
    {
        using NeedsYouLevelRig rig = RunInsideRig();
        _ = Release(rig);

        rig.Clock.Advance(TimeSpan.FromHours(24));
        Assert.Null(rig.Gate.RefusalFor("Nova", ReleasedMerge));

        rig.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(NothingReadyText, rig.Gate.RefusalFor("Nova", ReleasedMerge));
    }

    /// <summary>A released Task that reached Done carries a merge for an hour and not after.</summary>
    [Fact]
    public void RefusalFor_ReleasedTaskDone_AllowsMergeWithinTheHourOnly()
    {
        using NeedsYouLevelRig rig = RunInsideRig();
        TaskItem task = Release(rig);
        _ = Assert.IsType<TaskResult.Saved>(rig.Service.Update(task.Id, new TaskPatch { Status = TaskState.Done }, null, NeedsYouLevelRig.Nova));

        rig.Clock.Advance(TimeSpan.FromHours(1));
        Assert.Null(rig.Gate.RefusalFor("Nova", ReleasedMerge));

        rig.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.NotNull(rig.Gate.RefusalFor("Nova", ReleasedMerge));
    }

    /// <summary>Under Ask me nothing changes: the merge and the push keep the texts they always had.</summary>
    [Fact]
    public void RefusalFor_AskMe_KeepsTheAcceptTexts()
    {
        using NeedsYouLevelRig rig = new();
        _ = rig.CreateInReview();

        Assert.Equal(AskMeText, rig.Gate.RefusalFor("Nova", FeatureMerge));
        Assert.Equal(PushText, rig.Gate.RefusalFor("Nova", "git push origin main"));
    }

    /// <summary>Under both levels, with no Accept that names a push, every push is refused whatever its branch, refspec, remote, flag or alias (issue #602).</summary>
    /// <param name="runInside">Whether the Team runs under Run inside the Team with a released Task, or under Ask me.</param>
    /// <param name="command">The shell line.</param>
    [Theory]
    [InlineData(false, "git push origin feat/x")]
    [InlineData(true, "git push origin feat/x")]
    [InlineData(false, "git push https://host/r.git feat/x")]
    [InlineData(true, "git push https://host/r.git feat/x")]
    [InlineData(false, "git push origin main:deploy")]
    [InlineData(true, "git push origin main:deploy")]
    [InlineData(false, "git push --all")]
    [InlineData(true, "git push --all")]
    [InlineData(false, "git push --mirror origin")]
    [InlineData(true, "git push --mirror origin")]
    [InlineData(false, "git -C ../repo push origin feat/x")]
    [InlineData(true, "git -C ../repo push origin feat/x")]
    [InlineData(false, "git -c alias.p=push p origin feat/x")]
    [InlineData(true, "git -c alias.p=push p origin feat/x")]
    [InlineData(false, "git config alias.p push")]
    [InlineData(true, "git config alias.p push")]
    public void RefusalFor_AnyPushWithoutAnAccept_IsRefusedAtBothLevels(bool runInside, string command)
    {
        using NeedsYouLevelRig rig = runInside ? RunInsideRig() : new NeedsYouLevelRig();
        _ = runInside ? Release(rig) : rig.CreateInReview();

        Assert.Equal(PushText, rig.Gate.RefusalFor("Nova", command));
        Assert.True(GitCommandLine.NeedsGate(command));
    }

    /// <summary>A Team whose gates line lacks <c>accept-before-merge</c> still has a merge into main and a push to main refused with no Accept, at both levels.</summary>
    /// <param name="runInside">Whether the Team runs under Run inside the Team, or under Ask me.</param>
    /// <param name="gates">The gates line.</param>
    [Theory]
    [InlineData(false, "'qa-evidence-before-done'")]
    [InlineData(false, "")]
    [InlineData(true, "'qa-evidence-before-done'")]
    [InlineData(true, "")]
    public void RefusalFor_GatesLineWithoutAcceptBeforeMerge_StillRefusesMergeAndPushToMain(bool runInside, string gates)
    {
        using NeedsYouLevelRig rig = new(teamLevel: runInside ? NeedsYouLevel.RunInsideTheTeam : null, gates: gates);
        _ = rig.CreateInReview();

        Assert.Equal(AskMeText, rig.Gate.RefusalFor("Nova", FeatureMerge));
        Assert.Equal(PushText, rig.Gate.RefusalFor("Nova", "git push origin main"));
    }

    /// <summary>
    /// A Team at Run inside the Team whose gates line is empty never has a merge released or named by Huddle, so its refusal says to put the
    /// merge in a Task's Action (not the Run inside text, which forbids that), and an Accept on a Task with a merge Action lets the merge through.
    /// </summary>
    [Fact]
    public void RefusalFor_RunInsideWithoutAcceptBeforeMerge_TellsTheAgentToPutTheMergeInAnAction()
    {
        using NeedsYouLevelRig rig = new(teamLevel: NeedsYouLevel.RunInsideTheTeam, gates: string.Empty);
        TaskItem task = Assert.IsType<TaskResult.Saved>(rig.Service.Create(
            new TaskDraft("Quote calculator", NeedsYouLevelRig.Team, null, Assignee: "Nova") { HumanGate = true, Action = "Merge feat/x into main" },
            rig.Human)).Task;
        _ = rig.AddEvidence(task);

        Assert.Equal(AskMeText, rig.Gate.RefusalFor("Nova", FeatureMerge));
        Assert.Equal(AskMeText, rig.Gate.RefusalFor("Nova", ReleasedMerge));

        task = Assert.IsType<TaskResult.Saved>(rig.Service.Update(task.Id, new TaskPatch { Status = TaskState.NeedsYou }, null, NeedsYouLevelRig.Nova)).Task;
        _ = Assert.IsType<TaskResult.Saved>(rig.Service.Decide(task.Id, TaskDecision.Accept, null, task.Version, rig.Human));

        Assert.Null(rig.Gate.RefusalFor("Nova", FeatureMerge));
    }

    /// <summary>The Task id as <see cref="TaskId.ToString"/> writes it, zero-padded, names a branch that carries it; the unpadded form does not.</summary>
    [Fact]
    public void NamesTask_ZeroPaddedId_MatchesBranchCarryingIt()
    {
        Assert.True(TaskId.TryParse("DEV-0012", out TaskId id));

        Assert.True(ProcessMergeGate.NamesTask("feat/DEV-0012-login", id));
        Assert.False(ProcessMergeGate.NamesTask("feat/DEV-12-login", id));
        Assert.False(ProcessMergeGate.NamesTask("feat/DEV-00123-login", id));
    }

    /// <summary>Evidence on a released Task that reached Done less than an hour ago still counts for <c>checks-before-merge</c>.</summary>
    [Fact]
    public void RefusalFor_ChecksGate_CountsReleasedDoneEvidenceWithinTheHour()
    {
        using NeedsYouLevelRig rig = RunInsideRig("'checks-before-merge', 'accept-before-merge'");
        TaskItem task = Release(rig);
        _ = Assert.IsType<TaskResult.Saved>(rig.Service.Update(task.Id, new TaskPatch { Status = TaskState.Done }, null, NeedsYouLevelRig.Nova));

        Assert.Null(rig.Gate.RefusalFor("Nova", ReleasedMerge));

        rig.Clock.Advance(TimeSpan.FromHours(1) + TimeSpan.FromSeconds(1));
        Assert.Equal(ChecksFirst, rig.Gate.RefusalFor("Nova", ReleasedMerge));
    }

    /// <summary>A line that merges a branch names the released Task only when its id is a whole word of the branch, ignoring case; both directions of the boundary hold.</summary>
    [Fact]
    public void RefusalFor_ReleasedTask_PassesOnlyABranchNamingItsId()
    {
        using NeedsYouLevelRig rig = RunInsideRig();
        _ = Release(rig);

        Assert.Null(rig.Gate.RefusalFor("Nova", "git merge feat/PLAT-0001-site"));
        Assert.Null(rig.Gate.RefusalFor("Nova", "git merge plat-0001"));
        Assert.Null(rig.Gate.RefusalFor("Nova", "git merge fix/plat-0001"));
        Assert.Contains("PLAT-0001", rig.Gate.RefusalFor("Nova", "git merge feat/other"));
        Assert.NotNull(rig.Gate.RefusalFor("Nova", "git merge feat/PLAT-00012"));
        Assert.NotNull(rig.Gate.RefusalFor("Nova", "git merge feat/PLAT-0001x"));
        Assert.NotNull(rig.Gate.RefusalFor("Nova", "git merge feat/XPLAT-0001"));
        Assert.NotNull(rig.Gate.RefusalFor("Nova", "git merge -"));
        Assert.NotNull(rig.Gate.RefusalFor("Nova", "git merge 3f2a9c1"));
    }

    /// <summary>A line merging two branches passes only when each names a released Task; one stray branch refuses the whole line.</summary>
    [Fact]
    public void RefusalFor_TwoBranches_NeedBothToNameAReleasedTask()
    {
        using NeedsYouLevelRig rig = RunInsideRig();
        _ = Release(rig);

        Assert.Null(rig.Gate.RefusalFor("Nova", "git merge feat/PLAT-0001-a feat/PLAT-0001-b"));
        Assert.NotNull(rig.Gate.RefusalFor("Nova", "git merge feat/PLAT-0001-a feat/other"));
        Assert.NotNull(rig.Gate.RefusalFor("Nova", "git merge feat/PLAT-0001-a && git merge feat/other"));
    }

    /// <summary>A pull passes by its branch the same way, and the refusal for a stray branch says to name the branch after the released Task's id.</summary>
    [Fact]
    public void RefusalFor_Pull_NeedsTheBranchToNameTheReleasedTask()
    {
        using NeedsYouLevelRig rig = RunInsideRig();
        _ = Release(rig);

        Assert.Null(rig.Gate.RefusalFor("Nova", "git pull origin feat/PLAT-0001-site"));
        string? refusal = rig.Gate.RefusalFor("Nova", "git pull origin feat/other");
        Assert.NotNull(refusal);
        Assert.Contains("only for a branch named after a released Task's id", refusal, StringComparison.Ordinal);
        Assert.Contains("PLAT-0001", refusal, StringComparison.Ordinal);
    }

    /// <summary>Setting the Team's level back to Ask me refuses the very merge that passed by the released Task's branch.</summary>
    [Fact]
    public void RefusalFor_LevelSetBackToAskMe_RefusesTheBranchOfTheReleasedTask()
    {
        using NeedsYouLevelRig rig = RunInsideRig();
        _ = Release(rig);
        Assert.Null(rig.Gate.RefusalFor("Nova", ReleasedMerge));

        _ = rig.Levels.SetTeam(NeedsYouLevelRig.Team, NeedsYouLevel.AskMe, null);

        Assert.Equal(AskMeText, rig.Gate.RefusalFor("Nova", ReleasedMerge));
    }
}

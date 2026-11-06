using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.HumanGate;
using Agency.Huddle.Tests.Tasks.HumanGate;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Agents;
using Agency.Huddle.Tests.Ui.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// <see cref="ProcessMergeGate"/> maps a Persona's merge or push to its Team's <c>checks-before-merge</c>
/// gate, reading Evidence from the Team's Task files (PR-7, Spec §Gates).
/// </summary>
public sealed class ProcessMergeGateTests
{
    private const string Team = "Platform";
    private const string ChecksFirst = "Gate checks-before-merge: an Evidence line with exit code 0 for 'dotnet test' is needed before merging or pushing.";

    private const string OldTest = "dotnet test (repo root C:\\HuddleZero\\repos\\bakery-quote)";

    private static readonly DateTimeOffset Noon = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A Persona whose Team has the gate and no passing check Evidence is refused with the gate message.</summary>
    [Fact]
    public void RefusalFor_GateOnNoEvidence_ReturnsGateMessage()
    {
        using TaskToolHarness harness = NewHarness();
        using TempDataDir dir = new();
        ProcessMergeGate gate = Build(harness, dir, "'checks-before-merge'");

        Assert.Equal(ChecksFirst, gate.RefusalFor("Nova", "git push"));
    }

    /// <summary>A passing check recorded on any Task of the Persona's Team lets the merge through.</summary>
    [Fact]
    public void RefusalFor_PassingEvidenceOnATeamTask_IsAllowed()
    {
        using TaskToolHarness harness = NewHarness();
        using TempDataDir dir = new();
        AddEvidence(harness, "dotnet test", 0);
        ProcessMergeGate gate = Build(harness, dir, "'checks-before-merge'");

        Assert.Null(gate.RefusalFor("Nova", "git merge main"));
    }

    /// <summary>A failing run is not a pass, so the merge stays refused.</summary>
    [Fact]
    public void RefusalFor_FailingEvidence_IsRefused()
    {
        using TaskToolHarness harness = NewHarness();
        using TempDataDir dir = new();
        AddEvidence(harness, "dotnet test", 1);
        ProcessMergeGate gate = Build(harness, dir, "'checks-before-merge'");

        Assert.Equal(ChecksFirst, gate.RefusalFor("Nova", "git push"));
    }

    /// <summary>A Team whose process does not enable the gate is never refused.</summary>
    [Fact]
    public void RefusalFor_GateNotEnabled_IsAllowed()
    {
        using TaskToolHarness harness = NewHarness();
        using TempDataDir dir = new();
        ProcessMergeGate gate = Build(harness, dir, "'qa-evidence-before-done'");

        Assert.Null(gate.RefusalFor("Nova", "git push"));
    }

    /// <summary>A Persona in no Team is not refused; a Persona that cannot be resolved fails closed (review fix item 5).</summary>
    [Fact]
    public void RefusalFor_PersonaInNoTeamAllowedUnknownRefused()
    {
        using TaskToolHarness harness = NewHarness();
        using TempDataDir dir = new();
        ProcessMergeGate gate = Build(harness, dir, "'checks-before-merge'");

        Assert.Null(gate.RefusalFor("Kai", "git push"));
        Assert.Equal("Cannot check the merge gate: the Persona 'Ghost' could not be resolved.", gate.RefusalFor("Ghost", "git push"));
    }

    /// <summary>Passing Evidence on a Task that is already Done is stale and does not satisfy the merge gate (review fix item 6).</summary>
    [Fact]
    public void RefusalFor_EvidenceOnlyOnDoneTask_IsRefused()
    {
        using TaskToolHarness harness = NewHarness();
        using TempDataDir dir = new();
        AddEvidence(harness, "dotnet test", 0);
        TaskItem task = Assert.Single(harness.Store.All);
        TaskResult moved = harness.Service.Update(task.Id, new TaskPatch { Status = TaskState.Done }, null, TaskActors.Human(harness.Options.Value));
        Assert.IsType<TaskResult.Saved>(moved);
        ProcessMergeGate gate = Build(harness, dir, "'checks-before-merge'");

        Assert.Equal(ChecksFirst, gate.RefusalFor("Nova", "git push"));
    }

    /// <summary>A passing run recorded before #340, with its note folded into the command, lets the merge through.</summary>
    [Fact]
    public void RefusalFor_OldLineWithNoteInCommand_ExitZero_IsAllowed()
    {
        using TaskToolHarness harness = NewHarness();
        using TempDataDir dir = new();
        AddEvidence(harness, OldTest, 0);
        ProcessMergeGate gate = Build(harness, dir, "'checks-before-merge'");

        Assert.Null(gate.RefusalFor("Nova", "git merge main"));
    }

    /// <summary>A failing run recorded before #340 keeps the merge refused.</summary>
    [Fact]
    public void RefusalFor_OldLineWithNoteInCommand_ExitOne_IsRefused()
    {
        using TaskToolHarness harness = NewHarness();
        using TempDataDir dir = new();
        AddEvidence(harness, OldTest, 1);
        ProcessMergeGate gate = Build(harness, dir, "'checks-before-merge'");

        Assert.Equal(ChecksFirst, gate.RefusalFor("Nova", "git push"));
    }

    /// <summary>An old passing line followed by a new failing run is refused: the latest run wins.</summary>
    [Fact]
    public void RefusalFor_OldPassThenNewFailure_IsRefused()
    {
        using TaskToolHarness harness = NewHarness();
        using TempDataDir dir = new();
        AddEvidence(harness, OldTest, 0);
        AddEvidence(harness, "dotnet test", 1, minutes: 5);
        ProcessMergeGate gate = Build(harness, dir, "'checks-before-merge'");

        Assert.Equal(ChecksFirst, gate.RefusalFor("Nova", "git push"));
    }

    /// <summary>The assignee's merge of a feature branch is refused while its gated Task has no Accept, and the refusal names the Task.</summary>
    [Fact]
    public void RefusalFor_GatedUnacceptedTaskOfAssignee_RefusesMerge()
    {
        using TaskToolHarness harness = NewHarness();
        using TempDataDir dir = new();
        TaskItem task = AddGatedTask(harness, "Nova", gated: true, accepted: false, TaskState.Review);
        ProcessMergeGate gate = Build(harness, dir, "'qa-evidence-before-done'");

        string? refusal = gate.RefusalFor("Nova", "git merge --no-ff feat/quote-calculator");

        Assert.Equal(
            $"Cannot merge: Task {task.Id} waits for the Human's Accept in Needs You, and this merge waits for it. Huddle moves the Task there when its Proof is complete. Do not ask for the Accept in chat.",
            refusal);
    }

    /// <summary>A push naming main, or naming no branch, is refused under the same rule; a push of a feature branch is not.</summary>
    [Fact]
    public void RefusalFor_GatedUnacceptedTask_PushRefusedOnlyForMainOrNoBranch()
    {
        using TaskToolHarness harness = NewHarness();
        using TempDataDir dir = new();
        AddGatedTask(harness, "Nova", gated: true, accepted: false, TaskState.Review);
        ProcessMergeGate gate = Build(harness, dir, "'qa-evidence-before-done'");

        Assert.NotNull(gate.RefusalFor("Nova", "git push origin main"));
        Assert.NotNull(gate.RefusalFor("Nova", "git push"));
        Assert.NotNull(gate.RefusalFor("Nova", "git push origin HEAD:refs/heads/master"));
        Assert.Null(gate.RefusalFor("Nova", "git push -u origin feat/quote-calculator"));
    }

    /// <summary>A valid Accept lets the assignee merge.</summary>
    [Fact]
    public void RefusalFor_GatedTaskWithValidAccept_AllowsMerge()
    {
        using TaskToolHarness harness = NewHarness();
        using TempDataDir dir = new();
        AddGatedTask(harness, "Nova", gated: true, accepted: true, TaskState.NeedsYou);
        ProcessMergeGate gate = Build(harness, dir, "'qa-evidence-before-done'");

        Assert.Null(gate.RefusalFor("Nova", "git merge --no-ff feat/quote-calculator"));
    }

    /// <summary>Another Persona's Task, or an ungated Task, does not hold the assignee's merge.</summary>
    [Fact]
    public void RefusalFor_NotAssigneeOrNotGated_AllowsMerge()
    {
        using TaskToolHarness harness = NewHarness();
        using TempDataDir dir = new();
        AddGatedTask(harness, "Kai", gated: true, accepted: false, TaskState.Review);
        AddGatedTask(harness, "Nova", gated: false, accepted: false, TaskState.Review);
        ProcessMergeGate gate = Build(harness, dir, "'qa-evidence-before-done'");

        Assert.Null(gate.RefusalFor("Nova", "git merge feat/quote-calculator"));
    }

    /// <summary>A Cancelled gated Task no longer holds the merge.</summary>
    [Fact]
    public void RefusalFor_ClosedGatedTask_AllowsMerge()
    {
        using TaskToolHarness harness = NewHarness();
        using TempDataDir dir = new();
        AddGatedTask(harness, "Nova", gated: true, accepted: false, TaskState.Cancelled);
        ProcessMergeGate gate = Build(harness, dir, "'qa-evidence-before-done'");

        Assert.Null(gate.RefusalFor("Nova", "git merge feat/quote-calculator"));
    }

    /// <summary>Merging main into a feature branch is routine work and stays allowed, however main is spelled.</summary>
    [Theory]
    [InlineData("git merge main")]
    [InlineData("git merge origin/main")]
    [InlineData("git merge --no-edit origin/main")]
    [InlineData("git -C repo merge main")]
    public void RefusalFor_GatedUnacceptedTask_MergeOfMainIsAllowed(string command)
    {
        using TaskToolHarness harness = NewHarness();
        using TempDataDir dir = new();
        AddGatedTask(harness, "Nova", gated: true, accepted: false, TaskState.Review);
        ProcessMergeGate gate = Build(harness, dir, "'qa-evidence-before-done'");

        Assert.Null(gate.RefusalFor("Nova", command));
    }

    /// <summary>With both rules on, the checks gate still speaks first, and passing checks do not lift the Accept rule.</summary>
    [Fact]
    public void RefusalFor_BothRulesOn_ChecksFirstThenAccept()
    {
        using TaskToolHarness harness = NewHarness();
        using TempDataDir dir = new();
        AddGatedTask(harness, "Nova", gated: true, accepted: false, TaskState.Review);
        ProcessMergeGate gate = Build(harness, dir, "'checks-before-merge'");

        Assert.Equal(ChecksFirst, gate.RefusalFor("Nova", "git merge feat/quote-calculator"));

        AddEvidence(harness, "dotnet test", 0);

        string? refusal = gate.RefusalFor("Nova", "git merge feat/quote-calculator");
        Assert.StartsWith("Cannot merge: Task ", refusal, StringComparison.Ordinal); // contains-ok: the full wording is pinned in RefusalFor_GatedUnacceptedTaskOfAssignee_RefusesMerge.
    }

    private static TaskItem AddGatedTask(TaskToolHarness harness, string assignee, bool gated, bool accepted, TaskState status)
    {
        TaskDraft draft = new("Quote calculator", Team, null, Assignee: assignee, Description: "Body") { HumanGate = gated, Action = gated ? "Merge it" : null };
        TaskActor human = TaskActors.Human(harness.Options.Value);
        TaskItem task = Assert.IsType<TaskResult.Saved>(harness.Service.Create(draft, human)).Task;
        if (accepted)
        {
            task = Assert.IsType<TaskResult.Saved>(harness.Service.Update(task.Id, new TaskPatch { Status = TaskState.NeedsYou }, null, new TaskActor(TaskActorKind.Agent, assignee, "id"))).Task;
            task = Assert.IsType<TaskResult.Saved>(harness.Service.Decide(task.Id, TaskDecision.Accept, null, task.Version, human)).Task;
        }
        else if (status == TaskState.Cancelled)
        {
            task = Assert.IsType<TaskResult.Saved>(harness.Service.Update(task.Id, new TaskPatch { Status = TaskState.Cancelled }, null, human)).Task;
        }

        return task;
    }

    private static TaskToolHarness NewHarness() => new(clock: new ManualTimeProvider(Noon), proof: new FixedProofSource(FixedProofSource.Complete));

    private static void AddEvidence(TaskToolHarness harness, string command, int exitCode, int minutes = 0)
    {
        TaskResult created = harness.Service.Create(new TaskDraft("Fix login", Team, null), TaskActors.Human(harness.Options.Value));
        TaskItem task = Assert.IsType<TaskResult.Saved>(created).Task;
        EvidenceLine line = new(Noon.AddMinutes(minutes), "Nova", command, exitCode, null, null, string.Empty);
        TaskResult added = harness.Service.AddEvidence(task.Id, line, new TaskActor(TaskActorKind.Agent, "Nova", "nova-id"));
        Assert.IsType<TaskResult.Saved>(added);
    }

    private static ProcessMergeGate Build(TaskToolHarness harness, TempDataDir dir, string gates)
    {
        VersionedRef type = new("web", "1.2.0");
        LibraryRoot root = new("teams", "Teams", dir.Path, LibraryRootKind.Teams);
        Assert.True(TeamFile.TryParse($"---\nteamType: 'web@1.2.0'\ngates: [{gates}]\ncheckCommand: 'dotnet test'\n---\n", out TeamFile? teamFile));
        FakeTeamFolders folders = new()
        {
            TeamResult = LibraryResult<LibraryPath>.Ok(new LibraryPath(root, Team, dir.Path, LibraryNodeRole.TeamFolder)),
            ReadResult = teamFile,
        };
        FakeTeamCatalog catalog = new() { Teams = [new TeamSummary(Team, [], [], true) { TeamType = type }] };
        ProcessGateService service = new(catalog, folders, RoleFixtures.Types(dir));
        return new ProcessMergeGate(harness.Personas, harness.Store, service, catalog);
    }
}

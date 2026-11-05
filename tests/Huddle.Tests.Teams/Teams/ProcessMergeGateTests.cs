using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;
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
        using TaskToolHarness harness = new(new ManualTimeProvider(Noon));
        using TempDataDir dir = new();
        ProcessMergeGate gate = Build(harness, dir, "'checks-before-merge'");

        Assert.Equal(ChecksFirst, gate.RefusalFor("Nova", "git push"));
    }

    /// <summary>A passing check recorded on any Task of the Persona's Team lets the merge through.</summary>
    [Fact]
    public void RefusalFor_PassingEvidenceOnATeamTask_IsAllowed()
    {
        using TaskToolHarness harness = new(new ManualTimeProvider(Noon));
        using TempDataDir dir = new();
        AddEvidence(harness, "dotnet test", 0);
        ProcessMergeGate gate = Build(harness, dir, "'checks-before-merge'");

        Assert.Null(gate.RefusalFor("Nova", "git merge main"));
    }

    /// <summary>A failing run is not a pass, so the merge stays refused.</summary>
    [Fact]
    public void RefusalFor_FailingEvidence_IsRefused()
    {
        using TaskToolHarness harness = new(new ManualTimeProvider(Noon));
        using TempDataDir dir = new();
        AddEvidence(harness, "dotnet test", 1);
        ProcessMergeGate gate = Build(harness, dir, "'checks-before-merge'");

        Assert.Equal(ChecksFirst, gate.RefusalFor("Nova", "git push"));
    }

    /// <summary>A Team whose process does not enable the gate is never refused.</summary>
    [Fact]
    public void RefusalFor_GateNotEnabled_IsAllowed()
    {
        using TaskToolHarness harness = new(new ManualTimeProvider(Noon));
        using TempDataDir dir = new();
        ProcessMergeGate gate = Build(harness, dir, "'qa-evidence-before-done'");

        Assert.Null(gate.RefusalFor("Nova", "git push"));
    }

    /// <summary>A Persona in no Team is not refused; a Persona that cannot be resolved fails closed (review fix item 5).</summary>
    [Fact]
    public void RefusalFor_PersonaInNoTeamAllowedUnknownRefused()
    {
        using TaskToolHarness harness = new(new ManualTimeProvider(Noon));
        using TempDataDir dir = new();
        ProcessMergeGate gate = Build(harness, dir, "'checks-before-merge'");

        Assert.Null(gate.RefusalFor("Kai", "git push"));
        Assert.Equal("Cannot check the merge gate: the Persona 'Ghost' could not be resolved.", gate.RefusalFor("Ghost", "git push"));
    }

    /// <summary>Passing Evidence on a Task that is already Done is stale and does not satisfy the merge gate (review fix item 6).</summary>
    [Fact]
    public void RefusalFor_EvidenceOnlyOnDoneTask_IsRefused()
    {
        using TaskToolHarness harness = new(new ManualTimeProvider(Noon));
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
        using TaskToolHarness harness = new(new ManualTimeProvider(Noon));
        using TempDataDir dir = new();
        AddEvidence(harness, OldTest, 0);
        ProcessMergeGate gate = Build(harness, dir, "'checks-before-merge'");

        Assert.Null(gate.RefusalFor("Nova", "git merge main"));
    }

    /// <summary>A failing run recorded before #340 keeps the merge refused.</summary>
    [Fact]
    public void RefusalFor_OldLineWithNoteInCommand_ExitOne_IsRefused()
    {
        using TaskToolHarness harness = new(new ManualTimeProvider(Noon));
        using TempDataDir dir = new();
        AddEvidence(harness, OldTest, 1);
        ProcessMergeGate gate = Build(harness, dir, "'checks-before-merge'");

        Assert.Equal(ChecksFirst, gate.RefusalFor("Nova", "git push"));
    }

    /// <summary>An old passing line followed by a new failing run is refused: the latest run wins.</summary>
    [Fact]
    public void RefusalFor_OldPassThenNewFailure_IsRefused()
    {
        using TaskToolHarness harness = new(new ManualTimeProvider(Noon));
        using TempDataDir dir = new();
        AddEvidence(harness, OldTest, 0);
        AddEvidence(harness, "dotnet test", 1, minutes: 5);
        ProcessMergeGate gate = Build(harness, dir, "'checks-before-merge'");

        Assert.Equal(ChecksFirst, gate.RefusalFor("Nova", "git push"));
    }

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

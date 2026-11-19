using Agency.Huddle.App.Library;
using Agency.Huddle.App.Teams;
using Agency.Huddle.App.TeamTypes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Provisioning a process fills only the settings the Team's <c>_team.md</c> leaves unset, and leaves no half-done state behind.</summary>
public sealed class ProvisionKeepsSettingsTests
{
    private const string Team = "Platform";

    // Template development/processes/maintenance.md lines 4-7: gates qa/checks/failing-test, retro 10 done-tasks, wip 2, checkCommand 'dotnet test'.
    private const string ProcessId = "maintenance";

    private static ProcessProvisioner Build(TempDataDir dir, ITeamFolders folders) =>
        new(new TeamTypeCatalog(dir.Options(), NullLogger<TeamTypeCatalog>.Instance), folders);

    private static DiskTeamFolders Folders(TempDataDir dir, ProcessSettings existing) =>
        new(dir.Path)
        {
            File = new TeamFile(new VersionedRef("development", "1.0.0"), "Ada", null, "ships the platform", string.Empty) { Process = existing },
            TeamFilePath = Path.Combine(dir.Path, TeamPaths.TeamFileName),
        };

    /// <summary>Existing wip and checkCommand survive; the keys the Team leaves unset come from the template; manager and owns are untouched.</summary>
    [Fact]
    public void Provision_TeamHasSettings_KeepsThemAndFillsTheRest()
    {
        using TempDataDir dir = new();
        DiskTeamFolders folders = Folders(dir, new ProcessSettings([], 3, null, null, "make test", null));

        ProvisionResult result = Build(dir, folders).Provision(Team, ProcessId);

        // wip 3 and 'make test' are set on the Team, so the template's 2 and 'dotnet test' lose; gates and retro are unset, so they come from the template.
        ProcessSettings settings = folders.File!.Process;
        Assert.Equal(ProvisionResult.Created, result);
        Assert.Equal(3, settings.Wip);
        Assert.Equal("make test", settings.CheckCommand);
        Assert.Equal([GateId.QaEvidenceBeforeDone, GateId.ChecksBeforeMerge, GateId.FailingTestBeforeFix, GateId.AcceptBeforeMerge, GateId.BlockersDoneBeforeStart, GateId.OneInProgressPerAssignee], settings.Gates);
        Assert.Equal(new RetroCadence(10, CadenceUnit.DoneTasks), settings.RetroSchedule());
        Assert.StartsWith("maintenance", settings.Template, StringComparison.Ordinal);
        Assert.Equal("Ada", folders.File.Manager);
        Assert.Equal("ships the platform", folders.File.Owns);
    }

    /// <summary>A Team that already enables a gate id this build does not know keeps its gate list; the template's gates are not added.</summary>
    [Fact]
    public void Provision_TeamHasUnknownGate_KeepsTheGateList()
    {
        using TempDataDir dir = new();
        ProcessSettings existing = new([], null, null, null, null, null) { UnknownGates = ["future-gate"] };
        DiskTeamFolders folders = Folders(dir, existing);

        _ = Build(dir, folders).Provision(Team, ProcessId);

        Assert.Empty(folders.File!.Process.Gates);
        Assert.Equal(["future-gate"], folders.File.Process.UnknownGates);
    }

    /// <summary>The AlreadyExists path leaves _team.md byte-identical.</summary>
    [Fact]
    public void Provision_AgentsMdExists_LeavesTeamFileUnchanged()
    {
        using TempDataDir dir = new();
        DiskTeamFolders folders = Folders(dir, new ProcessSettings([], 3, null, null, "make test", null));
        File.WriteAllText(Path.Combine(dir.Path, TeamPaths.TeamFileName), folders.File!.Compose());
        File.WriteAllText(Path.Combine(dir.Path, TeamPaths.AgentsMdFileName), "mine\n");
        byte[] before = File.ReadAllBytes(Path.Combine(dir.Path, TeamPaths.TeamFileName));

        ProvisionResult result = Build(dir, folders).Provision(Team, ProcessId);

        Assert.Equal(ProvisionResult.AlreadyExists, result);
        Assert.Equal(before, File.ReadAllBytes(Path.Combine(dir.Path, TeamPaths.TeamFileName)));
    }

    /// <summary>When the _team.md update fails, the AGENTS.md this call created is removed so a retry can complete.</summary>
    [Fact]
    public void Provision_TeamFileUpdateFails_RemovesTheAgentsMdItCreated()
    {
        using TempDataDir dir = new();
        FailingUpdateFolders folders = new(Folders(dir, ProcessSettings.Empty));
        ProcessProvisioner provisioner = Build(dir, folders);

        ProvisionResult failed = provisioner.Provision(Team, ProcessId);
        string agents = Path.Combine(dir.Path, TeamPaths.AgentsMdFileName);
        bool leftover = File.Exists(agents);
        folders.Fail = false;
        ProvisionResult retry = provisioner.Provision(Team, ProcessId);

        Assert.Equal(ProvisionResult.Failed, failed);
        Assert.False(leftover);
        Assert.Equal(ProvisionResult.Created, retry);
    }

    /// <summary>Delegates to a disk-backed fake but can refuse <see cref="ITeamFolders.UpdateTeamFile"/>.</summary>
    private sealed class FailingUpdateFolders(DiskTeamFolders inner) : ITeamFolders
    {
        public bool Fail { get; set; } = true;

        public LibraryResult<LibraryPath> EnsureTeam(string name) => inner.EnsureTeam(name);

        public LibraryResult<LibraryPath> FindTeam(string name) => inner.FindTeam(name);

        public LibraryResult<LibraryPath> EnsureProjectIn(string team, string project) => inner.EnsureProjectIn(team, project);

        public TeamCreateResult CreateTeam(string team, TeamFile file, bool leaveExistingAlone = false) => inner.CreateTeam(team, file, leaveExistingAlone);

        public bool UpdateTeamFile(string team, Func<TeamFile, TeamFile> change) => !this.Fail && inner.UpdateTeamFile(team, change);

        public TeamFile? ReadTeamFile(string team) => inner.ReadTeamFile(team);
    }
}

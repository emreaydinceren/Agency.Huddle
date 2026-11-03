using Agency.Huddle.App.Teams;
using static Agency.Huddle.Tests.Teams.ProcessProvisionerFixtures;

namespace Agency.Huddle.Tests.Teams;

/// <summary>The chosen process template is split into the Team's <c>_team.md</c> settings and its <c>AGENTS.md</c> prose (PR-3, PR-6, Spec 6.4).</summary>
public sealed class ProcessProvisionerTests
{
    private const string Team = "Web Team";

    /// <summary>The template's body after its frontmatter (see <see cref="ProcessProvisionerFixtures.FlowText"/>).</summary>
    private const string FlowBody = "# Flow\n\n## When it applies\n";

    /// <summary>The template body is written to <c>AGENTS.md</c> and its gates go to <c>_team.md</c>.</summary>
    [Fact]
    public void Provision_KnownProcess_WritesProseAndSettings()
    {
        using TempDataDir dir = new();
        DiskTeamFolders folders = new(dir.Path);
        ProcessProvisioner provisioner = Build(dir, folders);

        ProvisionResult result = provisioner.Provision(Team, "flow");

        string text = File.ReadAllText(Path.Combine(dir.Path, TeamPaths.AgentsMdFileName));
        Assert.Equal(ProvisionResult.Created, result);
        Assert.Equal(FlowBody, text);
        Assert.Equal([GateId.QaEvidenceBeforeDone], folders.File?.Process.Gates);
    }

    /// <summary>The template id and version the settings were copied from are recorded in <c>_team.md</c>.</summary>
    [Fact]
    public void Provision_KnownProcess_RecordsTemplateIdAndVersion()
    {
        using TempDataDir dir = new();
        DiskTeamFolders folders = new(dir.Path);
        ProcessProvisioner provisioner = Build(dir, folders);

        _ = provisioner.Provision(Team, "flow");

        Assert.Equal("flow@1.0.0", folders.File?.Process.Template);
    }

    /// <summary>A second call reports <c>AlreadyExists</c> and leaves <c>AGENTS.md</c> as it was, even for another process.</summary>
    [Fact]
    public void Provision_FileExists_ReturnsAlreadyExistsAndKeepsFile()
    {
        using TempDataDir dir = new();
        ProcessProvisioner provisioner = Build(dir, new DiskTeamFolders(dir.Path));
        _ = provisioner.Provision(Team, "flow");
        string path = Path.Combine(dir.Path, TeamPaths.AgentsMdFileName);
        File.AppendAllText(path, "hand edit\n");

        ProvisionResult again = provisioner.Provision(Team, "flow");
        ProvisionResult other = provisioner.Provision(Team, "other");
        ProvisionResult blank = provisioner.Blank(Team);

        Assert.Equal(ProvisionResult.AlreadyExists, again);
        Assert.Equal(ProvisionResult.AlreadyExists, other);
        Assert.Equal(ProvisionResult.AlreadyExists, blank);
        Assert.Equal(FlowBody + "hand edit\n", File.ReadAllText(path));
    }

    /// <summary>The blank outline in <c>AGENTS.md</c> has all seven PR-6 headings and the Team's settings declare no gates.</summary>
    [Fact]
    public void Blank_NoProcessYet_WritesSevenHeadingsAndNoGates()
    {
        using TempDataDir dir = new();
        DiskTeamFolders folders = new(dir.Path);
        ProcessProvisioner provisioner = Build(dir, folders);

        ProvisionResult result = provisioner.Blank(Team);

        string text = File.ReadAllText(Path.Combine(dir.Path, TeamPaths.AgentsMdFileName));
        Assert.Equal(ProvisionResult.Created, result);
        Assert.True(ProcessDocument.TryParse(text, out ProcessDocument? document));
        Assert.Empty(document.MissingSections);
        Assert.Empty(folders.File?.Process.Gates ?? [GateId.QaEvidenceBeforeDone]);
        Assert.Null(folders.File?.Process.Template);
    }

    /// <summary>An unknown process id fails and writes nothing.</summary>
    [Fact]
    public void Provision_UnknownProcess_FailsAndWritesNothing()
    {
        using TempDataDir dir = new();
        ProcessProvisioner provisioner = Build(dir, new DiskTeamFolders(dir.Path));

        ProvisionResult result = provisioner.Provision(Team, "nope");

        Assert.Equal(ProvisionResult.UnknownProcess, result);
        Assert.False(File.Exists(Path.Combine(dir.Path, TeamPaths.AgentsMdFileName)));
    }

    /// <summary>A Team with no <c>_team.md</c> is refused and nothing is written.</summary>
    [Fact]
    public void Provision_TeamWithoutTeamFile_ReturnsUnknownTeam()
    {
        using TempDataDir dir = new();
        ProcessProvisioner provisioner = Build(dir, new DiskTeamFolders(dir.Path) { File = null });

        Assert.Equal(ProvisionResult.UnknownTeam, provisioner.Provision(Team, "flow"));
        Assert.Equal(ProvisionResult.UnknownTeam, provisioner.Blank(Team));
        Assert.False(File.Exists(Path.Combine(dir.Path, TeamPaths.AgentsMdFileName)));
    }

    /// <summary>The type's default process is provisioned when the Team has none, and left alone when it has one.</summary>
    [Fact]
    public void ProvisionDefault_NoProcessYet_UsesTheDefaultProcess()
    {
        using TempDataDir dir = new();
        ProcessProvisioner provisioner = Build(dir, new DiskTeamFolders(dir.Path));

        ProvisionResult first = provisioner.ProvisionDefault(Team);
        ProvisionResult second = provisioner.ProvisionDefault(Team);

        Assert.Equal(ProvisionResult.Created, first);
        Assert.Equal(ProvisionResult.AlreadyExists, second);
        Assert.Equal(FlowBody, File.ReadAllText(Path.Combine(dir.Path, TeamPaths.AgentsMdFileName)));
    }
}

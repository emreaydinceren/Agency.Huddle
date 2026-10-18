using Agency.Huddle.App.Library;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Teams;

/// <summary>The chosen process template is copied into the Team folder as <c>Process.md</c> (PR-3, PR-6, Spec §Process).</summary>
public sealed class ProcessProvisionerTests
{
    private const string Team = "Web Team";

    private const string Manifest = """
        { "schemaVersion": 1, "id": "web", "version": "1.2.0", "displayName": "Web", "order": 1,
          "workspace": { "kind": "none", "label": "None", "required": false },
          "processes": [
            { "id": "other", "file": "processes/other.md" },
            { "id": "flow", "file": "processes/flow.md", "default": true }
          ],
          "roles": [
            { "id": "lead", "title": "Lead", "candidate": "candidates/lead.md", "manager": true, "required": true }
          ] }
        """;

    private const string FlowText = "---\ntitle: 'Flow'\ntemplate: 'flow@1.0.0'\ngates: ['qa-evidence-before-done']\n---\n# Flow\n\n## When it applies\n";

    /// <summary>The template text is written to <c>Process.md</c>, parses, and <c>_team.md</c> names the file.</summary>
    [Fact]
    public void Provision_KnownProcess_WritesParsableFileAndSetsTeamFile()
    {
        using TempDataDir dir = new();
        DiskTeamFolders folders = new(dir.Path);
        ProcessProvisioner provisioner = Build(dir, folders);

        ProvisionResult result = provisioner.Provision(Team, "flow");

        string text = File.ReadAllText(Path.Combine(dir.Path, TeamPaths.ProcessFileName));
        Assert.Equal(ProvisionResult.Created, result);
        Assert.Equal(FlowText, text);
        Assert.True(ProcessDocument.TryParse(text, out ProcessDocument? document));
        Assert.Equal([GateId.QaEvidenceBeforeDone], document.Gates);
        Assert.Equal(TeamPaths.ProcessFileName, folders.File?.Process);
    }

    /// <summary>The template id and version the file was copied from are recorded in its frontmatter.</summary>
    [Fact]
    public void Provision_KnownProcess_RecordsTemplateIdAndVersion()
    {
        using TempDataDir dir = new();
        ProcessProvisioner provisioner = Build(dir, new DiskTeamFolders(dir.Path));

        _ = provisioner.Provision(Team, "flow");

        Assert.True(ProcessDocument.TryParse(File.ReadAllText(Path.Combine(dir.Path, TeamPaths.ProcessFileName)), out ProcessDocument? document));
        Assert.Equal(new VersionedRef("flow", "1.0.0"), document.Template);
    }

    /// <summary>A second call reports <c>AlreadyExists</c> and leaves the file as it was, even for another process.</summary>
    [Fact]
    public void Provision_FileExists_ReturnsAlreadyExistsAndKeepsFile()
    {
        using TempDataDir dir = new();
        ProcessProvisioner provisioner = Build(dir, new DiskTeamFolders(dir.Path));
        _ = provisioner.Provision(Team, "flow");
        string path = Path.Combine(dir.Path, TeamPaths.ProcessFileName);
        File.AppendAllText(path, "hand edit\n");

        ProvisionResult again = provisioner.Provision(Team, "flow");
        ProvisionResult other = provisioner.Provision(Team, "other");
        ProvisionResult blank = provisioner.Blank(Team);

        Assert.Equal(ProvisionResult.AlreadyExists, again);
        Assert.Equal(ProvisionResult.AlreadyExists, other);
        Assert.Equal(ProvisionResult.AlreadyExists, blank);
        Assert.Equal(FlowText + "hand edit\n", File.ReadAllText(path));
    }

    /// <summary>The blank outline has all seven PR-6 headings and declares no gates.</summary>
    [Fact]
    public void Blank_NoProcessYet_WritesSevenHeadingsAndNoGates()
    {
        using TempDataDir dir = new();
        DiskTeamFolders folders = new(dir.Path);
        ProcessProvisioner provisioner = Build(dir, folders);

        ProvisionResult result = provisioner.Blank(Team);

        string text = File.ReadAllText(Path.Combine(dir.Path, TeamPaths.ProcessFileName));
        Assert.Equal(ProvisionResult.Created, result);
        Assert.True(ProcessDocument.TryParse(text, out ProcessDocument? document));
        Assert.Empty(document.MissingSections);
        Assert.Empty(document.Gates);
        Assert.Null(document.Template);
        Assert.Equal(TeamPaths.ProcessFileName, folders.File?.Process);
    }

    /// <summary>An unknown process id fails and writes nothing.</summary>
    [Fact]
    public void Provision_UnknownProcess_FailsAndWritesNothing()
    {
        using TempDataDir dir = new();
        ProcessProvisioner provisioner = Build(dir, new DiskTeamFolders(dir.Path));

        ProvisionResult result = provisioner.Provision(Team, "nope");

        Assert.Equal(ProvisionResult.UnknownProcess, result);
        Assert.False(File.Exists(Path.Combine(dir.Path, TeamPaths.ProcessFileName)));
    }

    /// <summary>A Team with no <c>_team.md</c> is refused and nothing is written.</summary>
    [Fact]
    public void Provision_TeamWithoutTeamFile_ReturnsUnknownTeam()
    {
        using TempDataDir dir = new();
        ProcessProvisioner provisioner = Build(dir, new DiskTeamFolders(dir.Path) { File = null });

        Assert.Equal(ProvisionResult.UnknownTeam, provisioner.Provision(Team, "flow"));
        Assert.Equal(ProvisionResult.UnknownTeam, provisioner.Blank(Team));
        Assert.False(File.Exists(Path.Combine(dir.Path, TeamPaths.ProcessFileName)));
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
        Assert.Equal(FlowText, File.ReadAllText(Path.Combine(dir.Path, TeamPaths.ProcessFileName)));
    }

    internal static ProcessProvisioner Build(TempDataDir dir, ITeamFolders folders) =>
        new(Types(dir), folders);

    internal static TeamTypeCatalog Types(TempDataDir dir)
    {
        Dictionary<string, string> builtins = new(StringComparer.Ordinal)
        {
            ["web/team-type.json"] = Manifest,
            ["web/candidates/lead.md"] = "---\ntitle: Lead\nconsult_when: planning\n---\nYou lead.",
            ["web/processes/flow.md"] = FlowText,
            ["web/processes/other.md"] = "---\ntitle: 'Other'\n---\n# Other\n",
        };
        return new TeamTypeCatalog(dir.Options(), NullLogger<TeamTypeCatalog>.Instance, builtins);
    }
}

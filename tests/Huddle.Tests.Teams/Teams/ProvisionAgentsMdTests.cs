using Agency.Huddle.App.Teams;
using Agency.Huddle.App.TeamTypes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Spec 6.2 rule 5: provisioning a process writes the prose to <c>AGENTS.md</c> and the settings to <c>_team.md</c>, never <c>Process.md</c>.</summary>
public sealed class ProvisionAgentsMdTests
{
    private const string Team = "Finances";

    private const string ProcessId = "tax-season-basics";

    // Template tax-season-basics.md lines 1-6 are the frontmatter; the body starts at line 7 and ends at line 63.
    private const string BodyStart = "# Tax season basics\n\n## Identity\n";

    private const string BodyEnd = "- Suggest a starter Task, such as this year's document checklist.\n";

    private static ProcessProvisioner Build(TempDataDir dir, DiskTeamFolders folders) =>
        new(new TeamTypeCatalog(dir.Options(), NullLogger<TeamTypeCatalog>.Instance), folders);

    private static DiskTeamFolders Folders(TempDataDir dir) =>
        new(dir.Path)
        {
            File = new TeamFile(new VersionedRef("tax-season", "1.0.0"), null, null, null, string.Empty),
            TeamFilePath = Path.Combine(dir.Path, TeamPaths.TeamFileName),
        };

    private static string AgentsPath(TempDataDir dir) => Path.Combine(dir.Path, TeamPaths.AgentsMdFileName);

    private static TeamFile ReadTeamFile(TempDataDir dir)
    {
        Assert.True(TeamFile.TryParse(File.ReadAllText(Path.Combine(dir.Path, TeamPaths.TeamFileName)), out TeamFile? file));
        return file;
    }

    /// <summary>AGENTS.md is the template text after the closing fence of its frontmatter: no frontmatter, no leading blank line.</summary>
    [Fact]
    public void Provision_TaxSeasonBasics_WritesTheTemplateBodyWithoutFrontmatter()
    {
        using TempDataDir dir = new();
        ProcessProvisioner provisioner = Build(dir, Folders(dir));

        ProvisionResult result = provisioner.Provision(Team, ProcessId);

        // The body begins at template line 7 ("# Tax season basics") and ends with line 63; line 6 is the closing fence.
        string text = File.ReadAllText(AgentsPath(dir));
        Assert.Equal(ProvisionResult.Created, result);
        Assert.StartsWith(BodyStart, text, StringComparison.Ordinal);
        Assert.EndsWith(BodyEnd, text, StringComparison.Ordinal);
        Assert.DoesNotContain("template: ", text, StringComparison.Ordinal);
        Assert.DoesNotContain("gates: [", text, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', text);
    }

    /// <summary>_team.md holds the template's frontmatter keys under Process (gates, retro, template), read back through TeamFile.TryParse.</summary>
    [Fact]
    public void Provision_TaxSeasonBasics_WritesTheFrontmatterKeysToTeamFile()
    {
        using TempDataDir dir = new();
        ProcessProvisioner provisioner = Build(dir, Folders(dir));

        _ = provisioner.Provision(Team, ProcessId);

        // Template line 3: template 'tax-season-basics@1.1.0'; line 4: gates ['qa-evidence-before-done']; line 5: retro every 14 days.
        ProcessSettings settings = ReadTeamFile(dir).Process;
        Assert.Equal("tax-season-basics@1.1.0", settings.Template);
        Assert.Equal([GateId.QaEvidenceBeforeDone], settings.Gates);
        Assert.Equal(new RetroCadence(14, CadenceUnit.Days), settings.RetroSchedule());
        Assert.Null(settings.Wip);
        Assert.Null(settings.CheckCommand);
    }

    /// <summary>Provisioning leaves no Process.md in the Team folder.</summary>
    [Fact]
    public void Provision_TaxSeasonBasics_WritesNoProcessMd()
    {
        using TempDataDir dir = new();
        ProcessProvisioner provisioner = Build(dir, Folders(dir));

        _ = provisioner.Provision(Team, ProcessId);

        Assert.True(File.Exists(AgentsPath(dir)));
        Assert.Empty(Directory.EnumerateFiles(dir.Path, "Process.md", SearchOption.AllDirectories));
    }

    /// <summary>A blank process writes no Process.md and no settings: _team.md keeps an empty Process and AGENTS.md carries no frontmatter.</summary>
    [Fact]
    public void Blank_WritesNoProcessMdAndNoSettings()
    {
        using TempDataDir dir = new();
        DiskTeamFolders folders = Folders(dir);
        ProcessProvisioner provisioner = Build(dir, folders);

        ProvisionResult result = provisioner.Blank(Team);

        Assert.Equal(ProvisionResult.Created, result);
        Assert.Empty(Directory.EnumerateFiles(dir.Path, "Process.md", SearchOption.AllDirectories));
        Assert.Empty(folders.File?.Process.Gates ?? [GateId.QaEvidenceBeforeDone]);
        Assert.Null(folders.File?.Process.Template);
        Assert.False(File.ReadAllText(AgentsPath(dir)).StartsWith("---", StringComparison.Ordinal));
    }

    /// <summary>Provisioning again leaves an existing AGENTS.md byte-identical, hand edits included.</summary>
    [Fact]
    public void Provision_AgentsMdExists_LeavesItByteIdentical()
    {
        using TempDataDir dir = new();
        ProcessProvisioner provisioner = Build(dir, Folders(dir));
        _ = provisioner.Provision(Team, ProcessId);
        File.AppendAllText(AgentsPath(dir), "hand edit\n");
        byte[] before = File.ReadAllBytes(AgentsPath(dir));

        ProvisionResult again = provisioner.Provision(Team, ProcessId);

        Assert.Equal(ProvisionResult.AlreadyExists, again);
        Assert.Equal(before, File.ReadAllBytes(AgentsPath(dir)));
    }
}

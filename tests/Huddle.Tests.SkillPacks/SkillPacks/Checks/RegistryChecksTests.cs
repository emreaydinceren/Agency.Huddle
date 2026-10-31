using Agency.Huddle.SkillPacks.Checking;
using Agency.Huddle.SkillPacks.Resolving;
using Agency.Huddle.SkillPacks.Reading;

namespace Agency.Huddle.Tests.SkillPacks.Checks;

/// <summary>Pins the registry, row, Pack, catalog and Source checks of §6.3: each code raised once and not raised once.</summary>
public sealed class RegistryChecksTests
{
    internal const string Pinned = "https://github.com/o/r/tree/0123456789abcdef0123456789abcdef01234567/skills/";

    /// <summary>A healthy registry: one Pack with one downloaded Skill, a valid Source with notes.</summary>
    internal static RegistryFixture Healthy()
    {
        RegistryFixture fixture = new();
        string folder = fixture.CloneSkill("docx");
        fixture.SkillFolder(folder).Source()
            .Pack("documents", $$"""[{ "name": "docx", "path": "{{Json(folder)}}", "origin": "{{Pinned}}docx" }]""");
        return fixture;
    }

    /// <summary>A JSON string for a path.</summary>
    internal static string Json(string path)
    {
        return path.Replace("\\", "\\\\", StringComparison.Ordinal);
    }

    /// <summary>The codes in a result.</summary>
    internal static string[] Codes(CheckResult result)
    {
        return [.. result.Findings.Select(static finding => finding.Code)];
    }

    /// <summary>A healthy registry raises nothing and every item is usable.</summary>
    [Fact]
    public void Healthy_RaisesNothing()
    {
        CheckResult result = Healthy().Check();

        Assert.Empty(result.Findings);
        Assert.Equal(ItemCheckState.Usable, result.StateOf("pack:documents"));
        Assert.Equal(ItemCheckState.Usable, result.StateOf("skill:docx"));
        Assert.Equal(new CheckSummary(1, 1, 1, 0, 0), result.Summary);
    }

    /// <summary>An empty Local path is R003 and the row is Broken.</summary>
    [Fact]
    public void R003_EmptyPath_Raised()
    {
        RegistryFixture fixture = Healthy();
        fixture.PackRow(" ");

        CheckResult result = fixture.Check();

        Assert.Contains("R003", Codes(result));
        Assert.Equal(ItemCheckState.Broken, result.StateOf("row:pack:1"));
    }

    /// <summary>A row whose file is on a drive that is not there is R003 and Broken.</summary>
    [Fact]
    public void R003_FileOnMissingDrive_Broken()
    {
        RegistryFixture fixture = Healthy();
        fixture.PackRow(InMemoryFileReader.At("Unplugged", "x.skillpack.json"));

        CheckResult result = fixture.Check();

        Finding finding = Assert.Single(result.Findings);
        Assert.Equal("R003", finding.Code);
        Assert.Contains("Unplugged", finding.Message, StringComparison.Ordinal);
        Assert.Equal(ItemCheckState.Broken, result.StateOf("row:pack:1"));
    }

    /// <summary>Two rows pointing at one file: the later is R004 and Broken; the first keeps working.</summary>
    [Fact]
    public void R004_TwoRowsOneFile_LaterBroken()
    {
        RegistryFixture fixture = Healthy();
        fixture.PackRow("packs/documents.skillpack.json");

        CheckResult result = fixture.Check();

        Assert.Equal(["R004"], Codes(result));
        Assert.Equal(ItemCheckState.Usable, result.StateOf("pack:documents"));
        Assert.Equal(ItemCheckState.Broken, result.StateOf("row:pack:1"));
    }

    /// <summary>A Packs row pointing at a catalog file is R005.</summary>
    [Fact]
    public void R005_WrongKindOfFile_Raised()
    {
        RegistryFixture fixture = Healthy();
        fixture.Files.Add(Path.Combine(fixture.Dir, "catalogs", "c.skillcatalog.json"), """{ "schemaVersion": 1, "id": "c", "source": "local/test", "skills": [] }""");
        fixture.PackRow("catalogs/c.skillcatalog.json");

        Assert.Equal(["R005"], Codes(fixture.Check()));
    }

    /// <summary>A role that suggests a Pack nobody provides is R008; without suggestions it never is.</summary>
    [Fact]
    public void R008_SuggestedPackMissing_Raised()
    {
        RegistryFixture fixture = Healthy();
        fixture.Roles.Add(new RoleInfo("reviewer", "Reviewer", "Reviews.", ["documents", "code-review"]));

        Finding finding = Assert.Single(fixture.Check().Findings);

        Assert.Equal("R008", finding.Code);
        Assert.Contains("code-review", finding.Message, StringComparison.Ordinal);
    }

    /// <summary>With no suggested Packs in the context, R008 is never raised.</summary>
    [Fact]
    public void R008_NoSuggestedPacks_NeverRaised()
    {
        Assert.DoesNotContain("R008", Codes(Healthy().Check()));
    }

    /// <summary>A row with only another computer's entry is R009 and Not downloaded here.</summary>
    [Fact]
    public void R009_NoEntryForThisComputer_NotDownloaded()
    {
        RegistryFixture fixture = Healthy();
        fixture.PackRow("LAPTOP: C:/src/x.skillpack.json");

        CheckResult result = fixture.Check();

        Finding finding = Assert.Single(result.Findings);
        Assert.Equal("R009", finding.Code);
        Assert.Equal(Severity.Warning, finding.Severity);
        Assert.Equal(ItemCheckState.NotDownloaded, result.StateOf("row:pack:1"));
    }

    /// <summary>Two entries for one computer are R010 and Broken.</summary>
    [Fact]
    public void R010_DuplicateComputer_Broken()
    {
        RegistryFixture fixture = Healthy();
        fixture.PackRow("DESK-01: a.skillpack.json<br>desk-01: b.skillpack.json");

        CheckResult result = fixture.Check();

        Assert.Contains("R010", Codes(result));
        Assert.Equal(ItemCheckState.Broken, result.StateOf("row:pack:1"));
    }

    /// <summary>A Pack id used by an earlier row is P004 on the later Pack.</summary>
    [Fact]
    public void P004_DuplicatePackId_LaterBroken()
    {
        RegistryFixture fixture = Healthy();
        string json = fixture.Files.ReadAllText(Path.Combine(fixture.Dir, "packs", "documents.skillpack.json"));
        fixture.Files.Add(Path.Combine(fixture.Clone, "documents.skillpack.json"), json);
        fixture.PackRow(Path.Combine(fixture.Clone, "documents.skillpack.json"));

        CheckResult result = fixture.Check();

        Assert.Equal(["P004"], Codes(result));
        Assert.Equal(ItemCheckState.Usable, result.StateOf("pack:documents"));
        Assert.Equal(ItemCheckState.Broken, result.StateOf("row:pack:1"));
        Assert.Equal([ItemCheckState.Usable, ItemCheckState.Broken], result.Rows.Select(static row => row.State));
    }

    /// <summary>appliesTo naming an unknown role is P005; empty is P006; an empty consultWhenAdds is P007.</summary>
    [Fact]
    public void P005_P006_P007_Raised()
    {
        RegistryFixture unknownRole = new();
        unknownRole.Source().Pack("a", """[{ "name": "x", "path": "" }]""", appliesTo: """["accountant"]""");
        RegistryFixture noRoles = new();
        noRoles.Source().Pack("a", """[{ "name": "x", "path": "" }]""", appliesTo: "[]");

        string[] first = Codes(unknownRole.Check());
        string[] second = Codes(noRoles.Check());

        Assert.Contains("P005", first);
        Assert.Contains("P006", second);
        Assert.DoesNotContain("P005", second);
    }

    /// <summary>A Pack Skill that names a catalog without that Skill is P009 and the Pack is Broken.</summary>
    [Fact]
    public void P009_CatalogEntryMissing_Raised()
    {
        RegistryFixture fixture = new();
        fixture.Source()
            .Pack("documents", """[{ "name": "target-authoring", "catalog": "dotnet-skills" }]""")
            .Catalog("dotnet-skills", "[]");

        CheckResult result = fixture.Check();

        Finding finding = Assert.Single(result.Findings, static finding => finding.Code == "P009");
        Assert.Contains("\"dotnet-skills\" has no Skill of that name", finding.Message, StringComparison.Ordinal);
        Assert.Equal(ItemCheckState.Broken, result.StateOf("pack:documents"));
    }

    /// <summary>A Pack Skill with both a path and a catalog is P009 (from the parser) and the Pack is Broken.</summary>
    [Fact]
    public void P009_PathAndCatalog_Raised()
    {
        RegistryFixture fixture = new();
        fixture.Source().Pack("documents", """[{ "name": "docx", "path": "", "catalog": "c" }]""");

        CheckResult result = fixture.Check();

        Assert.Contains("P009", Codes(result));
        Assert.Equal(ItemCheckState.Broken, result.StateOf("pack:documents"));
    }

    /// <summary>A second catalog with an id already used is D005; the first keeps it.</summary>
    [Fact]
    public void D005_SecondCatalogSameId_FirstWins()
    {
        RegistryFixture fixture = Healthy();
        fixture.Catalog("shared", "[]");
        fixture.Files.Add(Path.Combine(fixture.Clone, "shared.skillcatalog.json"), """{ "schemaVersion": 1, "id": "shared", "source": "local/test", "skills": [] }""");
        fixture.CatalogRow(Path.Combine(fixture.Clone, "shared.skillcatalog.json"));

        CheckResult result = fixture.Check();

        Assert.Equal(["D005"], Codes(result));
        Assert.Equal(ItemCheckState.Usable, result.Rows.Single(static row => row.Index == 0 && row.Kind == RowKind.Catalog).State);
        Assert.Equal(ItemCheckState.Broken, result.Rows.Single(static row => row.Index == 1 && row.Kind == RowKind.Catalog).State);
    }

    /// <summary>An origin that names a branch is D004.</summary>
    [Fact]
    public void D004_BranchOrigin_Warns()
    {
        RegistryFixture fixture = new();
        fixture.Source().Catalog("c", """[{ "name": "docx", "description": "d", "origin": "https://github.com/o/r/tree/main/skills/docx" }]""");

        Finding finding = Assert.Single(fixture.Check().Findings);

        Assert.Equal("D004", finding.Code);
        Assert.Equal(Severity.Warning, finding.Severity);
    }

    /// <summary>A Source with no source.json is O001 and its Pack is Broken.</summary>
    [Fact]
    public void O001_MissingSourceJson_Raised()
    {
        RegistryFixture fixture = new();
        fixture.Pack("documents", """[{ "name": "docx", "path": "" }]""", source: "local/nowhere");

        CheckResult result = fixture.Check();

        Assert.Contains("O001", Codes(result));
        Assert.Equal(ItemCheckState.Broken, result.StateOf("pack:documents"));
    }

    /// <summary>A github Source without a commit is O002 only when one of its entries fails D004.</summary>
    [Fact]
    public void O002_SourceWithoutCommit_RaisedOnlyWhenAnEntryFailsD004()
    {
        const string github = ", \"url\": \"https://github.com/o/r\"";
        RegistryFixture pinned = new();
        pinned.Source("github.com/o/r", extra: github).Catalog("c", $$"""[{ "name": "docx", "description": "d", "origin": "{{Pinned}}docx" }]""", source: "github.com/o/r");
        RegistryFixture branch = new();
        branch.Source("github.com/o/r", extra: github).Catalog("c", """[{ "name": "docx", "description": "d", "origin": "https://github.com/o/r/tree/main/docx" }]""", source: "github.com/o/r");
        foreach (RegistryFixture fixture in new[] { pinned, branch })
        {
            string path = Path.Combine(SourceId.FolderOf(fixture.Dir, "github.com/o/r"), "source.json");
            fixture.Files.Add(path, fixture.Files.ReadAllText(path).Replace("\"kind\": \"local\"", "\"kind\": \"github\"", StringComparison.Ordinal));
        }

        Assert.DoesNotContain("O002", Codes(pinned.Check()));
        Assert.Contains("O002", Codes(branch.Check()));
    }

    /// <summary>A withdrawn Source without a reason is O003 and Broken.</summary>
    [Fact]
    public void O003_WithdrawnWithoutReason_Raised()
    {
        RegistryFixture fixture = Healthy();
        fixture.Source(status: "withdrawn");

        CheckResult result = fixture.Check();

        Assert.Equal(["O003"], Codes(result));
        Assert.Equal(ItemCheckState.Broken, result.StateOf("pack:documents"));
    }

    /// <summary>A Source without NOTES.md is O004; notes without a layout are O005.</summary>
    [Fact]
    public void O004_O005_Notes_Raised()
    {
        RegistryFixture noNotes = Healthy();
        noNotes.Files.Remove(Path.Combine(SourceId.FolderOf(noNotes.Dir, "local/test"), "NOTES.md"));
        RegistryFixture noLayout = Healthy();
        noLayout.Files.Add(Path.Combine(SourceId.FolderOf(noLayout.Dir, "local/test"), "NOTES.md"), "Just notes.");

        Assert.Equal(["O004"], Codes(noNotes.Check()));
        Assert.Equal(["O005"], Codes(noLayout.Check()));
    }

    /// <summary>A file the checks cannot read gives a finding, not an exception.</summary>
    [Fact]
    public void Run_FileCannotBeRead_FindingNotThrow()
    {
        RegistryFixture fixture = Healthy();
        fixture.Files.MakeUnreadable(Path.Combine(fixture.Dir, "packs", "documents.skillpack.json"));

        CheckResult result = fixture.Check();

        Finding finding = Assert.Single(result.Findings);
        Assert.Equal("P001", finding.Code);
        Assert.StartsWith("cannot read:", finding.Message, StringComparison.Ordinal);
    }

    /// <summary>A cancelled token throws and returns nothing.</summary>
    [Fact]
    public void Run_Cancelled_Throws()
    {
        RegistryFixture fixture = Healthy();
        ResolvedRegistry resolved = fixture.Resolve();
        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();

        Assert.Throws<OperationCanceledException>(() => { _ = Agency.Huddle.SkillPacks.Checking.Checks.Run(resolved, fixture.Context(), new CheckOptions(), fixture.Files, cancelled.Token); });
    }
}

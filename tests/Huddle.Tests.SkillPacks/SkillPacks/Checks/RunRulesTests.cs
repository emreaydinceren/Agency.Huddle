using Agency.Huddle.SkillPacks.Checking;
using Agency.Huddle.SkillPacks.Reading;
using Agency.Huddle.SkillPacks.Resolving;
using static Agency.Huddle.Tests.SkillPacks.Checks.RegistryChecksTests;

namespace Agency.Huddle.Tests.SkillPacks.Checks;

/// <summary>Pins rule 5 of §6.3: strict raises exactly R009 and S001 to error.</summary>
public sealed class StrictTests
{
    /// <summary>A registry with one R009 row and one S001 Skill.</summary>
    private static RegistryFixture WithR009AndS001()
    {
        RegistryFixture fixture = Healthy();
        fixture.PackRow("LAPTOP: C:/x.skillpack.json");
        fixture.Catalog("c", $$"""[{ "name": "pdf", "description": "d", "origin": "{{Pinned}}pdf", "path": "{{Json(fixture.CloneSkill("pdf"))}}" }]""");
        return fixture;
    }

    /// <summary>Without strict both are warnings; with strict both are errors and their states stay Not downloaded.</summary>
    [Fact]
    public void Strict_R009AndS001_BecomeErrors()
    {
        CheckResult loose = WithR009AndS001().Check();
        CheckResult strict = WithR009AndS001().Check(new CheckOptions(Strict: true));

        Assert.All(loose.Findings.Where(static finding => finding.Code is "R009" or "S001"), static finding => Assert.Equal(Severity.Warning, finding.Severity));
        Assert.Equal(2, strict.Findings.Count(static finding => finding.Code is "R009" or "S001" && finding.Severity == Severity.Error));
        Assert.Equal(ItemCheckState.NotDownloaded, strict.StateOf("skill:pdf"));
        Assert.Equal(2, strict.Summary.Errors);
    }

    /// <summary>Strict changes no other severity.</summary>
    [Fact]
    public void Strict_NothingElseChanges()
    {
        RegistryFixture loose = Healthy();
        loose.Roles.Add(new RoleInfo("reviewer", "Reviewer", "Reviews.", ["absent"]));
        RegistryFixture strict = Healthy();
        strict.Roles.Add(new RoleInfo("reviewer", "Reviewer", "Reviews.", ["absent"]));

        Assert.Equal(loose.Check().Findings, strict.Check(new CheckOptions(Strict: true)).Findings);
    }

    /// <summary>A catalog Skill whose path is empty raises nothing, even with strict.</summary>
    [Fact]
    public void EmptyCatalogPath_NoFinding_EvenStrict()
    {
        RegistryFixture fixture = Healthy();
        fixture.Catalog("c", $$"""[{ "name": "pdf", "description": "d", "origin": "{{Pinned}}pdf", "path": "" }, { "name": "xlsx", "description": "d", "origin": "{{Pinned}}xlsx", "path": { "*": "" } }]""");

        CheckResult result = fixture.Check(new CheckOptions(Strict: true));

        Assert.Empty(result.Findings);
        Assert.Equal(ItemCheckState.NotDownloaded, result.StateOf("skill:pdf"));
        Assert.Equal(ItemCheckState.Usable, result.StateOf("catalog:c"));
    }
}

/// <summary>Pins rules 1 and 2 of §6.3: deterministic order, and one finding per fact on the first row.</summary>
public sealed class OrderingTests
{
    /// <summary>Findings sort by row line (registry first), then file, code and message.</summary>
    [Fact]
    public void Findings_AreSortedByRowThenFileThenCode()
    {
        RegistryFixture fixture = Healthy();
        fixture.Roles.Add(new RoleInfo("reviewer", "Reviewer", "Reviews.", ["absent"]));
        fixture.PackRow(InMemoryFileReader.At("gone.skillpack.json"));
        fixture.Catalog("c", """[{ "name": "pdf", "description": "d", "origin": "https://github.com/o/r/tree/main/pdf" }]""");

        CheckResult result = fixture.Check();

        Assert.Equal(["R008", "R003", "D004"], Codes(result));
    }

    /// <summary>Two runs over the same inputs give the same list, byte for byte.</summary>
    [Fact]
    public void SameInputs_GiveIdenticalOutput()
    {
        CheckResult first = StrictTestsHelper().Check();
        CheckResult second = StrictTestsHelper().Check();

        Assert.Equal(first.Findings, second.Findings);
        Assert.Equal(first.States.OrderBy(static pair => pair.Key, StringComparer.Ordinal), second.States.OrderBy(static pair => pair.Key, StringComparer.Ordinal));
    }

    /// <summary>A Skill reached by a Pack and by a catalog is checked once, on the first row.</summary>
    [Fact]
    public void SharedSkill_ReportedOnceOnFirstRow()
    {
        RegistryFixture fixture = new();
        string folder = fixture.CloneSkill("docx");
        fixture.SkillFolder(folder, frontmatterExtra: "tools: propose_sprint\n").Source()
            .Pack("documents", """[{ "name": "docx", "catalog": "c" }]""")
            .Catalog("c", $$"""[{ "name": "docx", "description": "d", "license": "MIT", "origin": "{{Pinned}}docx", "path": "{{Json(folder)}}" }]""");

        CheckResult result = fixture.Check();

        Assert.Single(result.Findings, static finding => finding.Code == "S011");
        RowCheck packRow = result.Rows.Single(static row => row.Kind == RowKind.Pack);
        Assert.Equal(1, packRow.Errors);
        Assert.Equal(0, result.Rows.Single(static row => row.Kind == RowKind.Catalog).Errors);
        Assert.Equal(ItemCheckState.Broken, result.StateOf("pack:documents"));
    }

    /// <summary>Each loading finding appears once in the result.</summary>
    [Fact]
    public void LoadingFindings_AppearOnce()
    {
        RegistryFixture fixture = Healthy();
        fixture.PackRow(InMemoryFileReader.At("gone.skillpack.json"));

        ResolvedRegistry resolved = fixture.Resolve();
        CheckResult result = fixture.Check();

        Finding loading = Assert.Single(resolved.Findings);
        Assert.Single(result.Findings, finding => finding == loading);
    }

    /// <summary>A registry with a little of everything.</summary>
    private static RegistryFixture StrictTestsHelper()
    {
        RegistryFixture fixture = Healthy();
        fixture.PackRow("LAPTOP: C:/x.skillpack.json");
        fixture.Catalog("c", """[{ "name": "pdf", "description": "d", "origin": "https://github.com/o/r/tree/main/pdf", "path": "missing" }]""");
        return fixture;
    }
}

/// <summary>Pins the context checks of §6.3: X002, R008 without suggestions, O002 and <c>CheckContext</c> use.</summary>
public sealed class ContextTests
{
    /// <summary>A caller version that differs from the context's Huddle version is X002.</summary>
    [Fact]
    public void X002_DifferentVersions_Warns()
    {
        Finding finding = Assert.Single(Healthy().Check(new CheckOptions(ToolVersion: "2.5.0")).Findings);

        Assert.Equal("X002", finding.Code);
        Assert.Contains("2.5.0", finding.Message, StringComparison.Ordinal);
        Assert.Contains("2.4.0", finding.Message, StringComparison.Ordinal);
    }

    /// <summary>Huddle passes no tool version, so X002 is skipped; the same version is silent too.</summary>
    [Fact]
    public void X002_NullToolVersion_Skipped()
    {
        Assert.Empty(Healthy().Check(new CheckOptions(ToolVersion: null)).Findings);
        Assert.Empty(Healthy().Check(new CheckOptions(ToolVersion: "2.4.0")).Findings);
    }

    /// <summary>OnlyPath checks only rows under it; registry findings stay and R008 is skipped.</summary>
    [Fact]
    public void OnlyPath_ChecksOnlyRowsUnderIt()
    {
        RegistryFixture fixture = Healthy();
        fixture.Roles.Add(new RoleInfo("reviewer", "Reviewer", "Reviews.", ["absent"]));
        fixture.PackRow(InMemoryFileReader.At("gone.skillpack.json"));

        CheckResult result = fixture.Check(new CheckOptions(OnlyPath: Path.Combine(fixture.Dir, "packs")));

        Assert.Empty(result.Findings);
        Assert.Equal(1, result.Summary.Rows);
    }
}

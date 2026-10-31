using System.Diagnostics;
using System.Text;
using Agency.Huddle.SkillPacks.Checking;
using Agency.Huddle.SkillPacks.Resolving;

namespace Agency.Huddle.Tests.SkillPacks.Resolving;

/// <summary>Pins §8.3: catalog references, first row wins, one Skill per name, the pointer and installed hashes, the
/// two-second grace, and that the resolver assigns no state.</summary>
public sealed class RegistryResolverTests
{
    private const string Origin = "https://github.com/o/r/tree/0123456789abcdef0123456789abcdef01234567/skills/docx";
    private const string OtherOrigin = "https://github.com/x/y/tree/0123456789abcdef0123456789abcdef01234567/skills/docx";

    /// <summary>The same name from two places with different origins: the first row keeps it, the later provider gets D003.</summary>
    [Fact]
    public void Resolve_SameNameDifferentOrigin_FirstRowWins_LaterIsBroken()
    {
        RegistryFixture fixture = new();
        string first = fixture.CloneSkill("docx");
        fixture.SkillFolder(first).Source()
            .Catalog("a", $$"""[{ "name": "docx", "description": "d", "origin": "{{Origin}}", "path": "{{Json(first)}}" }]""")
            .Catalog("b", $$"""[{ "name": "docx", "description": "d", "origin": "{{OtherOrigin}}", "path": "" }]""");

        ResolvedRegistry resolved = fixture.Resolve();
        CheckResult result = fixture.Check();

        SkillEntry skill = Assert.Single(resolved.Skills);
        Assert.Equal(Origin, skill.Origin);
        Assert.Equal(["catalog:a", "catalog:b"], skill.Providers);
        Finding d003 = Assert.Single(result.Findings, static finding => finding.Code == "D003");
        Assert.EndsWith("b.skillcatalog.json", d003.File, StringComparison.Ordinal);
        Assert.Equal(ItemCheckState.Broken, result.StateOf("catalog:b"));
        Assert.Equal(ItemCheckState.Usable, result.StateOf("skill:docx"));
    }

    /// <summary>A Pack Skill that names a catalog takes the entry's path, origin and licence.</summary>
    [Fact]
    public void Resolve_PackSkillByCatalog_TakesCatalogPathAndOrigin()
    {
        RegistryFixture fixture = new();
        string folder = fixture.CloneSkill("docx");
        fixture.SkillFolder(folder).Source()
            .Pack("documents", """[{ "name": "docx", "catalog": "shipped" }]""")
            .Catalog("shipped", $$"""[{ "name": "docx", "description": "Word", "license": "MIT", "origin": "{{Origin}}", "path": "{{Json(folder)}}" }]""");

        ResolvedRegistry resolved = fixture.Resolve();

        SkillEntry skill = Assert.Single(resolved.Skills);
        Assert.Equal(folder, skill.FolderPath);
        Assert.Equal(Origin, skill.Origin);
        Assert.Equal("MIT", skill.License);
        Assert.Equal(["pack:documents", "catalog:shipped"], skill.Providers);
        Assert.Equal("shipped", skill.Claims[0].ViaCatalog);
    }

    /// <summary>The library never produces the New or In use states: they are the app's overlays.</summary>
    [Fact]
    public void Resolve_LibraryNeverProducesNewOrInUse()
    {
        RegistryFixture fixture = new();
        string folder = fixture.CloneSkill("docx");
        fixture.SkillFolder(folder).Source().Pack("documents", $$"""[{ "name": "docx", "path": "{{Json(folder)}}", "origin": "{{Origin}}" }]""");

        CheckResult result = fixture.Check();

        Assert.NotEmpty(result.States);
        Assert.All(result.States.Values, static state => Assert.True(Enum.IsDefined(state)));
        Assert.DoesNotContain(Enum.GetNames<ItemCheckState>(), static name => name is "New" or "InUse");
    }

    /// <summary>The same input resolves to the same output, field by field.</summary>
    [Fact]
    public void Resolve_SameInputTwice_IsIdentical()
    {
        RegistryFixture fixture = new();
        string folder = fixture.CloneSkill("docx");
        fixture.SkillFolder(folder).Source().Pack("documents", $$"""[{ "name": "docx", "path": "{{Json(folder)}}", "origin": "{{Origin}}" }]""");

        ResolvedRegistry first = fixture.Resolve();
        ResolvedRegistry second = fixture.Resolve();

        Assert.Equal(Describe(first), Describe(second));
    }

    /// <summary>A pointer folder whose hash differs from the copy's recorded hash means an update is available.</summary>
    [Fact]
    public void Resolve_PointerHashDiffersFromInstalled_IsUpdateAvailable()
    {
        RegistryFixture fixture = new();
        string folder = fixture.CloneSkill("docx");
        fixture.SkillFolder(folder).Source().Copy("docx", Origin, "old-hash")
            .Pack("documents", $$"""[{ "name": "docx", "path": "{{Json(folder)}}", "origin": "{{Origin}}" }]""");

        SkillEntry skill = Assert.Single(fixture.Resolve().Skills);

        Assert.NotNull(skill.PointerSha256);
        Assert.Equal("old-hash", skill.InstalledSha256);
        Assert.True(skill.HasUpdate);
    }

    /// <summary>A copy from a different origin is not this Skill's copy: no installed hash.</summary>
    [Fact]
    public void Resolve_CopyFromOtherOrigin_HasNoInstalledHash()
    {
        RegistryFixture fixture = new();
        string folder = fixture.CloneSkill("docx");
        fixture.SkillFolder(folder).Source().Copy("docx", OtherOrigin, "old-hash")
            .Pack("documents", $$"""[{ "name": "docx", "path": "{{Json(folder)}}", "origin": "{{Origin}}" }]""");

        SkillEntry skill = Assert.Single(fixture.Resolve().Skills);

        Assert.Null(skill.InstalledSha256);
        Assert.False(skill.HasUpdate);
    }

    /// <summary>A Pack with one Skill whose folder is missing names it and is Not downloaded.</summary>
    [Fact]
    public void Resolve_PackWithOneMissingSkill_IsNotDownloaded()
    {
        RegistryFixture fixture = new();
        string present = fixture.CloneSkill("docx");
        string absent = fixture.CloneSkill("pdf");
        fixture.SkillFolder(present).Source()
            .Pack("documents", $$"""[{ "name": "docx", "path": "{{Json(present)}}", "origin": "{{Origin}}" }, { "name": "pdf", "path": "{{Json(absent)}}", "origin": "{{Origin}}x" }]""");

        ResolvedRegistry resolved = fixture.Resolve();
        CheckResult result = fixture.Check();

        Assert.Equal(["pdf"], Assert.Single(resolved.Packs).MissingSkills);
        Assert.Equal(ItemCheckState.NotDownloaded, result.StateOf("pack:documents"));
        Assert.Equal(SkillPathState.Missing, resolved.FindSkill("pdf")?.PathState);
    }

    /// <summary>A pack file that fails to parse within two seconds of its last write is not reported yet; it is listed for a retry.</summary>
    [Fact]
    public void Resolve_ParseFailureWithinTwoSeconds_NotReportedYet()
    {
        RegistryFixture fixture = new();
        string path = Path.Combine(fixture.Dir, "packs", "half.skillpack.json");
        fixture.Files.Add(path, """{ "schemaVersion": 1, "id": "ha""", fixture.Now.AddSeconds(-1));
        fixture.PackRow("packs/half.skillpack.json");

        ResolvedRegistry resolved = fixture.Resolve();
        CheckResult result = fixture.Check();

        Assert.Equal([path], resolved.Retry);
        Assert.DoesNotContain(result.Findings, static finding => finding.Code == "P001");
        Assert.Equal(ItemCheckState.NotDownloaded, result.StateOf("row:pack:0"));
    }

    /// <summary>The same failure three seconds after the write is reported and the row is Broken; Reload ignores the grace.</summary>
    [Fact]
    public void Resolve_ParseFailureAfterRetry_IsBroken()
    {
        RegistryFixture fixture = new();
        string path = Path.Combine(fixture.Dir, "packs", "half.skillpack.json");
        fixture.Files.Add(path, """{ "schemaVersion": 1, "id": "ha""", fixture.Now.AddSeconds(-3));
        fixture.PackRow("packs/half.skillpack.json");

        CheckResult result = fixture.Check();

        Assert.Contains(result.Findings, static finding => finding.Code == "P001");
        Assert.Equal(ItemCheckState.Broken, result.StateOf("row:pack:0"));

        fixture.Files.Add(path, """{ "schemaVersion": 1, "id": "ha""", fixture.Now);
        Assert.Empty(fixture.Resolve(new ResolveOptions(IgnoreGrace: true)).Retry);
    }

    /// <summary>A catalog of 5,000 entries resolves in under a second.</summary>
    [Fact]
    public void Read_FiveThousandEntryCatalog_UnderOneSecond()
    {
        RegistryFixture fixture = new();
        StringBuilder skills = new("[");
        for (int i = 0; i < 5000; i++)
        {
            skills.Append(i == 0 ? string.Empty : ",").Append(System.Globalization.CultureInfo.InvariantCulture, $$"""{ "name": "skill-{{i}}", "description": "d", "origin": "{{Origin}}{{i}}", "path": "" }""");
        }

        fixture.Source().Catalog("big", skills.Append(']').ToString());
        fixture.WriteSkillsMd();

        Stopwatch stopwatch = Stopwatch.StartNew();
        ResolvedRegistry resolved = fixture.Resolve();
        stopwatch.Stop();

        Assert.Equal(5000, resolved.Skills.Count);
        Assert.InRange(stopwatch.Elapsed, TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }

    /// <summary>A Source id with <c>..</c> is O001 and nothing is read for it.</summary>
    [Fact]
    public void SourceId_DotDot_O001AndNothingRead()
    {
        RegistryFixture fixture = new();
        fixture.Pack("documents", """[{ "name": "docx", "path": "" }]""", source: "local/../../etc");

        ResolvedSource source = Assert.Single(fixture.Resolve().Sources);

        Assert.Null(source.Folder);
        Assert.Equal("O001", Assert.Single(source.Findings).Code);
        Assert.False(SourceId.IsValid("local/../x"));
        Assert.True(SourceId.IsValid("github.com/anthropics/skills"));
        Assert.True(SourceId.IsValid("local/my-packs"));
    }

    /// <summary>A JSON string for a path.</summary>
    private static string Json(string path)
    {
        return path.Replace("\\", "\\\\", StringComparison.Ordinal);
    }

    /// <summary>A stable text form of a resolve, for comparing two runs.</summary>
    private static string Describe(ResolvedRegistry resolved)
    {
        StringBuilder text = new();
        foreach (PackEntry pack in resolved.Packs)
        {
            text.Append(pack.StateKey).Append(pack.FilePath).Append(string.Join(',', pack.SkillNames)).Append(string.Join(',', pack.Findings)).Append('|');
        }

        foreach (SkillEntry skill in resolved.Skills)
        {
            text.Append(skill.Name).Append(skill.FolderPath).Append(skill.PointerSha256).Append(skill.PathState).Append(string.Join(',', skill.Providers)).Append('|');
        }

        return text.Append(string.Join(',', resolved.Findings)).ToString();
    }
}

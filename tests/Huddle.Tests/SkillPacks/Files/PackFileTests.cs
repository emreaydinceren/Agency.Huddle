using Agency.Huddle.SkillPacks.Checking;
using Agency.Huddle.SkillPacks.Files;

namespace Agency.Huddle.Tests.SkillPacks.Files;

/// <summary>Pins the reading rules of §6.2 for <c>*.skillpack.json</c>: schema version, required fields, the id and
/// file name, and the per-Skill path-or-catalog rule.</summary>
public sealed class PackFileTests
{
    private static readonly string FilePath = InMemoryFileReader.At("SS", "packs", "msbuild-authoring.skillpack.json");

    /// <summary>The §7.2 example, as a template; <c>{0}</c> is replaced by the skills list.</summary>
    private static string Pack(string skills = """[{ "name": "target-authoring", "catalog": "dotnet-skills" }]""", int schema = 1)
    {
        return $$"""
            {
              "schemaVersion": {{schema}},
              "id": "msbuild-authoring",
              "version": "1.0.0",
              "title": "MSBuild Authoring",
              "source": "github.com/dotnet/skills",
              "appliesTo": ["developer"],
              "consultWhenAdds": ".csproj changes",
              "skills": {{skills}}
            }
            """;
    }

    /// <summary>A pack written for schema version 2 is P001 and has no value.</summary>
    [Fact]
    public void Parse_PackWithUnknownSchemaVersion_IsP001AndNull()
    {
        ParseResult<PackFile> result = PackFile.Parse(Pack(schema: 2), FilePath);

        Assert.Null(result.Value);
        Finding finding = Assert.Single(result.Findings);
        Assert.Equal("P001", finding.Code);
        Assert.Equal(FilePath, finding.File);
    }

    /// <summary><c>"path": ""</c> is a present path: no finding, and it resolves to nothing (Not downloaded).</summary>
    [Fact]
    public void Parse_PackSkillEmptyPath_IsValidAndNotDownloaded()
    {
        ParseResult<PackFile> result = PackFile.Parse(Pack("""[{ "name": "docx", "path": "", "origin": "https://github.com/o/r/tree/abc/docx" }]"""), FilePath);

        Assert.True(result.Ok);
        Assert.Empty(result.Findings);
        Assert.NotNull(result.Value);
        PackSkill skill = Assert.Single(result.Value.Skills);
        Assert.NotNull(skill.Path);
        Assert.Null(skill.Path.Resolve("PC"));
        Assert.Null(skill.Catalog);
    }

    /// <summary>A Skill with both path and catalog is P009 and is dropped; the Pack still loads.</summary>
    [Fact]
    public void Parse_PackSkillWithPathAndCatalog_IsP009()
    {
        ParseResult<PackFile> result = PackFile.Parse(Pack("""[{ "name": "a", "path": "x", "catalog": "c" }, { "name": "b", "catalog": "c" }]"""), FilePath);

        Assert.True(result.Ok);
        Assert.Equal("P009", Assert.Single(result.Findings).Code);
        Assert.NotNull(result.Value);
        Assert.Equal("b", Assert.Single(result.Value.Skills).Name);
    }

    /// <summary>A missing title is P002 and has no value.</summary>
    [Fact]
    public void Parse_MissingTitle_IsP002()
    {
        string json = Pack().Replace("\"title\": \"MSBuild Authoring\",", string.Empty, StringComparison.Ordinal);

        ParseResult<PackFile> result = PackFile.Parse(json, FilePath);

        Assert.Null(result.Value);
        Assert.Equal("P002", Assert.Single(result.Findings).Code);
        Assert.Contains("title", result.Findings[0].Message, StringComparison.Ordinal);
    }

    /// <summary>A file named for another id is P003.</summary>
    [Fact]
    public void Parse_FileNameDiffersFromId_IsP003()
    {
        ParseResult<PackFile> result = PackFile.Parse(Pack(), InMemoryFileReader.At("SS", "packs", "other.skillpack.json"));

        Assert.Null(result.Value);
        Assert.Equal("P003", Assert.Single(result.Findings).Code);
    }

    /// <summary>A Skill listed twice is P008.</summary>
    [Fact]
    public void Parse_DuplicateSkill_IsP008()
    {
        ParseResult<PackFile> result = PackFile.Parse(Pack("""[{ "name": "a", "path": "x" }, { "name": "a", "path": "y" }]"""), FilePath);

        Assert.Equal("P008", Assert.Single(result.Findings).Code);
    }

    /// <summary>Unknown keys, mixed-case keys and a BOM are accepted.</summary>
    [Fact]
    public void Parse_UnknownKeysCaseAndBom_Accepted()
    {
        string json = "\uFEFF" + Pack().Replace("\"title\"", "\"Title\"", StringComparison.Ordinal).Replace("\"id\":", "\"extra\": 1, \"id\":", StringComparison.Ordinal);

        ParseResult<PackFile> result = PackFile.Parse(json, FilePath);

        Assert.True(result.Ok);
        Assert.Empty(result.Findings);
        Assert.NotNull(result.Value);
        Assert.Equal("MSBuild Authoring", result.Value.Title);
    }

    /// <summary>Text that is not JSON is P001; trailing commas are not accepted.</summary>
    [Fact]
    public void Parse_NotJsonOrTrailingComma_IsP001()
    {
        Assert.Equal("P001", Assert.Single(PackFile.Parse("not json", FilePath).Findings).Code);
        Assert.Equal("P001", Assert.Single(PackFile.Parse("""{ "schemaVersion": 1, }""", FilePath).Findings).Code);
    }

    /// <summary>The §7.2 example with a per-computer path reads each computer's entry.</summary>
    [Fact]
    public void Parse_SpecExample_ReadsPerComputerPath()
    {
        const string skills = """
            [
              { "name": "target-authoring", "catalog": "dotnet-skills" },
              { "name": "incremental-build", "path": { "DESK-01": "E:\\Clones\\a", "LAPTOP": "C:\\src\\a" }, "origin": "https://github.com/dotnet/skills/tree/4c1e9a2/skills/incremental-build" }
            ]
            """;

        ParseResult<PackFile> result = PackFile.Parse(Pack(skills), FilePath);

        Assert.True(result.Ok);
        Assert.NotNull(result.Value);
        PackSkill skill = result.Value.Skills[1];
        Assert.NotNull(skill.Path);
        Assert.Equal(@"C:\src\a", skill.Path.Resolve("laptop"));
        Assert.Equal("dotnet-skills", result.Value.Skills[0].Catalog);
    }
}

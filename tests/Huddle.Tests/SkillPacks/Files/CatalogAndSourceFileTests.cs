using Agency.Huddle.SkillPacks.Files;

namespace Agency.Huddle.Tests.SkillPacks.Files;

/// <summary>Pins the reading rules of §6.2 for catalog files: a bad entry drops alone, and the id must match the file.</summary>
public sealed class CatalogFileTests
{
    private static readonly string FilePath = InMemoryFileReader.At("SS", "catalogs", "addyosmani-agent-skills.skillcatalog.json");

    /// <summary>One entry without an origin is D002 and is dropped; the other loads.</summary>
    [Fact]
    public void Parse_CatalogBadEntry_DropsOnlyThatEntry()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "id": "addyosmani-agent-skills",
              "source": "github.com/addyosmani/agent-skills",
              "skills": [
                { "name": "broken", "description": "No origin" },
                { "name": "code-review-and-quality", "description": "Reviews changes.", "origin": "https://github.com/addyosmani/agent-skills/tree/1401c8b8030e023baeebb31781a6653fe8e93026/skills/code-review-and-quality", "path": { "DESK-01": "E:\\a", "*": "" } }
              ]
            }
            """;

        ParseResult<CatalogFile> result = CatalogFile.Parse(json, FilePath);

        Assert.True(result.Ok);
        Assert.Equal("D002", Assert.Single(result.Findings).Code);
        Assert.NotNull(result.Value);
        CatalogEntry entry = Assert.Single(result.Value.Skills);
        Assert.Equal("code-review-and-quality", entry.Name);
        Assert.Equal("addyosmani-agent-skills", result.Value.Title);
        Assert.Null(entry.Path.Resolve("LAPTOP"));
        Assert.Equal(@"E:\a", entry.Path.Resolve("DESK-01"));
    }

    /// <summary>An id that does not match the file name is D001.</summary>
    [Fact]
    public void Parse_IdDiffersFromFileName_IsD001()
    {
        const string json = """{ "schemaVersion": 1, "id": "other", "source": "local/x", "skills": [] }""";

        ParseResult<CatalogFile> result = CatalogFile.Parse(json, FilePath);

        Assert.Null(result.Value);
        Assert.Equal("D001", Assert.Single(result.Findings).Code);
    }

    /// <summary>An empty skills list is valid.</summary>
    [Fact]
    public void Parse_EmptySkills_IsValid()
    {
        const string json = """{ "schemaVersion": 1, "id": "addyosmani-agent-skills", "source": "local/x", "skills": [] }""";

        ParseResult<CatalogFile> result = CatalogFile.Parse(json, FilePath);

        Assert.True(result.Ok);
        Assert.NotNull(result.Value);
        Assert.Empty(result.Value.Skills);
    }
}

/// <summary>Pins the reading rules of §6.2 for <c>source.json</c>: optional ref, commit and localPath, required fields,
/// and O003.</summary>
public sealed class SourceFileTests
{
    private static readonly string FilePath = InMemoryFileReader.At("SS", "sources", "github.com", "o", "r", "source.json");

    /// <summary>A Source with no ref, commit or localPath is valid.</summary>
    [Fact]
    public void Parse_SourceWithoutRefCommitLocalPath_IsValid()
    {
        const string json = """
            { "schemaVersion": 1, "kind": "github", "url": "https://github.com/o/r", "fetchedAt": "2026-10-17T16:40:00+02:00", "fetchedBy": "Claude Code", "signIn": "none", "status": "active" }
            """;

        ParseResult<SourceFile> result = SourceFile.Parse(json, FilePath);

        Assert.True(result.Ok);
        Assert.Empty(result.Findings);
        Assert.NotNull(result.Value);
        Assert.Null(result.Value.Commit);
        Assert.Equal(SourceKind.Github, result.Value.Kind);
        Assert.Equal(TimeSpan.FromHours(2), result.Value.FetchedAt.Offset);
    }

    /// <summary>A withdrawn Source without a reason is O003; the record still loads.</summary>
    [Fact]
    public void Parse_StatusWithdrawnWithoutReason_IsO003()
    {
        const string json = """
            { "schemaVersion": 1, "kind": "local", "fetchedAt": "2026-10-17T16:40:00Z", "fetchedBy": "Me", "signIn": "none", "status": "withdrawn" }
            """;

        ParseResult<SourceFile> result = SourceFile.Parse(json, FilePath);

        Assert.True(result.Ok);
        Assert.Equal("O003", Assert.Single(result.Findings).Code);
        Assert.NotNull(result.Value);
        Assert.Equal(SourceStatus.Withdrawn, result.Value.Status);
    }

    /// <summary>A github Source without a url, or with an unknown kind, is O001.</summary>
    [Fact]
    public void Parse_MissingUrlOrUnknownKind_IsO001()
    {
        const string noUrl = """{ "schemaVersion": 1, "kind": "github", "fetchedAt": "2026-10-17T16:40:00Z", "fetchedBy": "Me", "signIn": "none", "status": "active" }""";
        const string badKind = """{ "schemaVersion": 1, "kind": "svn", "fetchedAt": "2026-10-17T16:40:00Z", "fetchedBy": "Me", "signIn": "none", "status": "active" }""";

        ParseResult<SourceFile> first = SourceFile.Parse(noUrl, FilePath);
        ParseResult<SourceFile> second = SourceFile.Parse(badKind, FilePath);

        Assert.Equal("O001", Assert.Single(first.Findings).Code);
        Assert.Contains("url", first.Findings[0].Message, StringComparison.Ordinal);
        Assert.Equal("O001", Assert.Single(second.Findings).Code);
        Assert.Null(second.Value);
    }
}

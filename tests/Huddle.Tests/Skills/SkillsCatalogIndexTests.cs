using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Skills;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Skills;

/// <summary>
/// <see cref="SkillsCatalogIndex"/> (SK-1, SK-2): the embedded catalog, the user copy that replaces it,
/// the fallback on a malformed user copy and the lookups the picker and the import use.
/// </summary>
public sealed class SkillsCatalogIndexTests
{
    private const string UserCatalog = """
        [
          { "id": "acme/skills/alpha", "name": "alpha", "description": "Alpha.", "publisher": "Acme",
            "repo": "https://github.com/acme/skills", "path": "skills/alpha",
            "link": "https://github.com/acme/skills/tree/abc/skills/alpha", "commit": "abc", "license": "MIT",
            "category": "Cat", "subCategory": "Sub", "requires": ["node"] },
          { "id": "acme/skills/beta", "name": "beta", "description": "Beta.", "publisher": "Acme",
            "repo": "https://github.com/acme/skills", "path": "skills/beta",
            "link": "https://github.com/acme/skills/tree/abc/skills/beta", "commit": "abc", "license": "MIT",
            "category": "Cat", "subCategory": "Sub", "requires": [] }
        ]
        """;

    /// <summary>Builds the index over a data folder.</summary>
    private static SkillsCatalogIndex Create(TempDataDir dir) =>
        new(dir.Options(), NullLogger<SkillsCatalogIndex>.Instance);

    /// <summary>Writes the user copy of the catalog into the data folder.</summary>
    private static void WriteUserCopy(TempDataDir dir, string json) =>
        File.WriteAllText(Path.Combine(dir.Path, TeamPaths.SkillsCatalogFile), json);

    /// <summary>Without a user copy the index holds the embedded catalog, whose entries are all filled in.</summary>
    [Fact]
    public void All_WithoutUserCopy_LoadsTheEmbeddedCatalog()
    {
        using TempDataDir dir = new();

        SkillsCatalogIndex index = Create(dir);

        Assert.True(index.All.Count > 100);
        Assert.All(index.All, static entry =>
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.Id));
            Assert.False(string.IsNullOrWhiteSpace(entry.Name));
            Assert.False(string.IsNullOrWhiteSpace(entry.Repo));
            Assert.NotNull(entry.Path);
            Assert.False(string.IsNullOrWhiteSpace(entry.Commit));
        });
    }

    /// <summary>The embedded catalog answers lookups by id, name and sub category.</summary>
    [Fact]
    public void Lookups_OnTheEmbeddedCatalog_FindAnEntry()
    {
        using TempDataDir dir = new();
        SkillsCatalogIndex index = Create(dir);

        SkillsCatalogEntry? byId = index.FindById("addyosmani/agent-skills/api-and-interface-design");
        SkillsCatalogEntry? byName = index.FindByName("api-and-interface-design");

        Assert.NotNull(byId);
        Assert.Equal(byId, byName);
        Assert.Equal("API & Interface Design", index.SubCategoryOf("api-and-interface-design"));
    }

    /// <summary>A parsable user copy replaces the built-in catalog entirely.</summary>
    [Fact]
    public void All_WithUserCopy_ReplacesTheBuiltInCatalog()
    {
        using TempDataDir dir = new();
        WriteUserCopy(dir, UserCatalog);

        SkillsCatalogIndex index = Create(dir);

        Assert.Equal(["alpha", "beta"], index.All.Select(static entry => entry.Name).ToList());
        Assert.Equal(["node"], index.FindByName("alpha")?.Requires);
        Assert.Null(index.FindByName("api-and-interface-design"));
    }

    /// <summary>A malformed user copy falls back to the embedded catalog.</summary>
    [Fact]
    public void All_WithMalformedUserCopy_FallsBackToTheBuiltInCatalog()
    {
        using TempDataDir dir = new();
        WriteUserCopy(dir, "{ not json");

        SkillsCatalogIndex index = Create(dir);

        Assert.NotNull(index.FindByName("api-and-interface-design"));
        Assert.True(index.All.Count > 100);
    }

    /// <summary>Reload picks up a user copy written after construction.</summary>
    [Fact]
    public void Reload_AfterUserCopyAppears_UsesIt()
    {
        using TempDataDir dir = new();
        SkillsCatalogIndex index = Create(dir);
        WriteUserCopy(dir, UserCatalog);

        index.Reload();

        Assert.Equal(2, index.All.Count);
    }

    /// <summary>The repo and path match a GitHub URL after trimming a <c>.git</c> suffix and a trailing slash.</summary>
    /// <param name="repo">The repo URL as a user might paste it.</param>
    [Theory]
    [InlineData("https://github.com/acme/skills")]
    [InlineData("https://github.com/acme/skills.git")]
    [InlineData("https://github.com/acme/skills/")]
    [InlineData("https://github.com/acme/skills.git/")]
    public void FindByRepoPath_TrimsGitSuffixAndTrailingSlash(string repo)
    {
        using TempDataDir dir = new();
        WriteUserCopy(dir, UserCatalog);
        SkillsCatalogIndex index = Create(dir);

        SkillsCatalogEntry? entry = index.FindByRepoPath(repo, "skills/beta");

        Assert.Equal("beta", entry?.Name);
    }

    /// <summary>A path the catalog does not hold, or a different repo, finds nothing.</summary>
    [Fact]
    public void FindByRepoPath_UnknownPathOrRepo_IsNull()
    {
        using TempDataDir dir = new();
        WriteUserCopy(dir, UserCatalog);
        SkillsCatalogIndex index = Create(dir);

        Assert.Null(index.FindByRepoPath("https://github.com/acme/skills", "skills/gamma"));
        Assert.Null(index.FindByRepoPath("https://github.com/other/skills", "skills/beta"));
    }

    /// <summary>An unknown name or id has no entry and no sub category.</summary>
    [Fact]
    public void Lookups_UnknownName_AreNull()
    {
        using TempDataDir dir = new();
        SkillsCatalogIndex index = Create(dir);

        Assert.Null(index.FindByName("no-such-skill"));
        Assert.Null(index.FindById("no/such/skill"));
        Assert.Null(index.SubCategoryOf("no-such-skill"));
    }
}

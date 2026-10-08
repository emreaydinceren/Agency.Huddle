using Agency.Huddle.App.TeamTypes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.TeamTypes;

/// <summary>Tests for <see cref="SkillPackStore"/> (Spec §Pack, MS-4).</summary>
public sealed class SkillPackStoreTests
{
    private const string BuiltinMsbuild = """
        { "id": "msbuild-authoring", "version": "1.0.0", "title": "Built-in MSBuild", "appliesTo": ["developer", "release"],
          "consultWhenAdds": ".csproj changes",
          "skills": [{ "name": "target-authoring", "source": "dotnet/skills", "path": "plugins/dotnet-msbuild/skills/target-authoring" }] }
        """;

    private const string BuiltinTesting = """
        { "id": "testing", "version": "1.0.0", "title": "Testing", "appliesTo": ["tester"],
          "consultWhenAdds": "tests change", "skills": [{ "name": "tdd", "source": "builtin" }] }
        """;

    private static SkillPackStore Create(TempDataDir dir)
    {
        Dictionary<string, string> builtins = new(StringComparer.Ordinal)
        {
            ["msbuild-authoring.json"] = BuiltinMsbuild,
            ["testing.json"] = BuiltinTesting,
        };
        return new SkillPackStore(dir.Options(), NullLogger<SkillPackStore>.Instance, builtins);
    }

    private static void WriteUser(TempDataDir dir, string fileName, string text)
    {
        string folder = Path.Combine(dir.Path, "SkillPacks");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, fileName), text);
    }

    /// <summary>Built-in packs load with every Spec field.</summary>
    [Fact]
    public void All_Builtins_LoadWithFields()
    {
        using TempDataDir dir = new();

        SkillPackStore store = Create(dir);

        Assert.Equal(2, store.All.Count);
        SkillPack? pack = store.Get("msbuild-authoring");
        Assert.NotNull(pack);
        Assert.Equal("1.0.0", pack.Version);
        Assert.Equal(["developer", "release"], pack.AppliesTo);
        Assert.Equal(".csproj changes", pack.ConsultWhenAdds);
        SkillRef skill = Assert.Single(pack.Skills);
        Assert.Equal("target-authoring", skill.Name);
        Assert.Equal("dotnet/skills", skill.Source);
        Assert.Equal("plugins/dotnet-msbuild/skills/target-authoring", skill.Path);
        Assert.Empty(store.Problems);
    }

    /// <summary>A user file with the same id replaces the built-in pack.</summary>
    [Fact]
    public void Get_UserFileSameId_OverridesBuiltin()
    {
        using TempDataDir dir = new();
        WriteUser(dir, "msbuild-authoring.json", """{ "id": "msbuild-authoring", "version": "2.0.0", "title": "Mine", "appliesTo": ["developer"], "consultWhenAdds": "x", "skills": [] }""");

        SkillPackStore store = Create(dir);

        Assert.Equal(2, store.All.Count);
        SkillPack? pack = store.Get("msbuild-authoring");
        Assert.NotNull(pack);
        Assert.Equal("Mine", pack.Title);
        Assert.Equal("2.0.0", pack.Version);
    }

    /// <summary>A malformed user file is listed as a problem, does not throw, and leaves the built-in in place.</summary>
    [Fact]
    public void Problems_MalformedJson_ListedNotThrown()
    {
        using TempDataDir dir = new();
        WriteUser(dir, "msbuild-authoring.json", "{ not json");

        SkillPackStore store = Create(dir);

        SkillPackProblem problem = Assert.Single(store.Problems);
        Assert.Equal("msbuild-authoring.json", problem.Source);
        Assert.Equal("The file is not valid pack JSON.", problem.Message);
        Assert.Equal("Built-in MSBuild", store.Get("msbuild-authoring")?.Title);
    }

    /// <summary>A pack that fails validation is a problem with a specific reason, not a pack.</summary>
    /// <param name="json">The pack file text.</param>
    /// <param name="message">The expected problem message.</param>
    [Theory]
    [InlineData("""{ "version": "1.0.0", "title": "T" }""", "The 'id' field is required.")]
    [InlineData("""{ "id": "a", "title": "T" }""", "The 'version' field is required.")]
    [InlineData("""{ "id": "a", "version": "1" }""", "The 'title' field is required.")]
    [InlineData("""{ "id": "a", "version": "1", "title": "T", "skills": [{ "name": "n" }] }""", "Every skill needs a 'name' and a 'source'.")]
    [InlineData("null", "The file is empty.")]
    public void Problems_InvalidPack_ListedWithReason(string json, string message)
    {
        using TempDataDir dir = new();
        WriteUser(dir, "bad.json", json);

        SkillPackStore store = Create(dir);

        SkillPackProblem problem = Assert.Single(store.Problems);
        Assert.Equal("bad.json", problem.Source);
        Assert.Equal(message, problem.Message);
        Assert.Equal(2, store.All.Count);
    }

    /// <summary>Two user files defining the same id report the second as a problem and keep the first.</summary>
    [Fact]
    public void Problems_DuplicateUserId_SecondRejected()
    {
        using TempDataDir dir = new();
        WriteUser(dir, "a.json", """{ "id": "dup", "version": "1", "title": "First" }""");
        WriteUser(dir, "b.json", """{ "id": "dup", "version": "1", "title": "Second" }""");

        SkillPackStore store = Create(dir);

        SkillPackProblem problem = Assert.Single(store.Problems);
        Assert.Equal("b.json", problem.Source);
        Assert.Equal("Another user file already defines pack 'dup'.", problem.Message);
        Assert.Equal("First", store.Get("dup")?.Title);
    }

    /// <summary>A user file that cannot be opened is a problem, not an exception.</summary>
    [Fact]
    public void Problems_LockedFile_ListedNotThrown()
    {
        using TempDataDir dir = new();
        WriteUser(dir, "locked.json", """{ "id": "locked", "version": "1", "title": "L" }""");
        using FileStream hold = new(Path.Combine(dir.Path, "SkillPacks", "locked.json"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        SkillPackStore store = Create(dir);

        SkillPackProblem problem = Assert.Single(store.Problems);
        Assert.Equal("locked.json", problem.Source);
        Assert.Equal("The file could not be read.", problem.Message);
        Assert.Null(store.Get("locked"));
    }

    /// <summary>ForRole returns only the packs whose appliesTo contains the role.</summary>
    [Fact]
    public void ForRole_FiltersByAppliesTo()
    {
        using TempDataDir dir = new();

        SkillPackStore store = Create(dir);

        Assert.Equal("msbuild-authoring", Assert.Single(store.ForRole("release")).Id);
        Assert.Equal("testing", Assert.Single(store.ForRole("tester")).Id);
        Assert.Empty(store.ForRole("designer"));
    }

    /// <summary>Get for an unknown id is null.</summary>
    [Fact]
    public void Get_Unknown_ReturnsNull()
    {
        using TempDataDir dir = new();

        SkillPackStore store = Create(dir);

        Assert.Null(store.Get("nope"));
    }

    /// <summary>Reload picks up a file added after construction.</summary>
    [Fact]
    public void Reload_NewFile_IsPickedUp()
    {
        using TempDataDir dir = new();
        SkillPackStore store = Create(dir);
        Assert.Null(store.Get("extra"));

        WriteUser(dir, "extra.json", """{ "id": "extra", "version": "1.0.0", "title": "Extra", "appliesTo": ["developer"], "consultWhenAdds": "y", "skills": [] }""");
        store.Reload();

        Assert.Equal("Extra", store.Get("extra")?.Title);
        Assert.Equal(3, store.All.Count);
    }
}

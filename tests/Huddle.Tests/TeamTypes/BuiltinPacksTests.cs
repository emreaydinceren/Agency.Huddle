using System.Reflection;
using System.Text.Json;
using Agency.Huddle.App.TeamTypes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.TeamTypes;

/// <summary>Tests for the starter skill packs the Development manifest names (Spec §Pack, Scenario 2).</summary>
public sealed class BuiltinPacksTests
{
    private const string ManifestResource = "TeamTypes/Builtin/development/team-type.json";

    /// <summary>Reads the embedded Development manifest directly, because the catalog hides that type until its process files ship.</summary>
    /// <returns>The parsed manifest.</returns>
    private static TeamTypeManifest LoadDevelopmentManifest()
    {
        Assembly assembly = typeof(TeamTypeCatalog).Assembly;
        string? resource = assembly.GetManifestResourceNames().SingleOrDefault(static name => name.Replace('\\', '/') == ManifestResource);
        Assert.NotNull(resource);
        using Stream? stream = assembly.GetManifestResourceStream(resource);
        Assert.NotNull(stream);
        using StreamReader reader = new(stream);
        TeamTypeManifest? manifest = JsonSerializer.Deserialize<TeamTypeManifest>(reader.ReadToEnd(), TeamTypeJson.Options);
        Assert.NotNull(manifest);
        return manifest;
    }

    /// <summary>Over an empty data folder the embedded store lists both starter packs, in id order, with no problems.</summary>
    [Fact]
    public void Builtins_EmptyDataFolder_ListBothPacks()
    {
        using TempDataDir dir = new();

        SkillPackStore store = new(dir.Options(), NullLogger<SkillPackStore>.Instance);

        Assert.Equal(["code-review-and-quality", "msbuild-authoring"], store.All.Select(pack => pack.Id).ToList());
        Assert.Empty(store.Problems);
    }

    /// <summary>The msbuild-authoring pack carries the Spec §Pack example, with the Development release role id.</summary>
    [Fact]
    public void MsbuildAuthoring_MatchesSpecExample()
    {
        using TempDataDir dir = new();
        SkillPackStore store = new(dir.Options(), NullLogger<SkillPackStore>.Instance);

        SkillPack? pack = store.Get("msbuild-authoring");

        Assert.NotNull(pack);
        Assert.Equal("1.0.0", pack.Version);
        Assert.Equal("MSBuild Authoring", pack.Title);
        Assert.Equal(["developer", "release-engineer"], pack.AppliesTo);
        Assert.Equal(".csproj, .props or .targets changes, build failures, slow or broken incremental builds", pack.ConsultWhenAdds);
        SkillRef skill = Assert.Single(pack.Skills);
        Assert.Equal("target-authoring", skill.Name);
        Assert.Equal("dotnet/skills", skill.Source);
        Assert.Equal("plugins/dotnet-msbuild/skills/target-authoring", skill.Path);
    }

    /// <summary>The code-review-and-quality pack suits the reviewer role and names the Addy Osmani skill.</summary>
    [Fact]
    public void CodeReviewAndQuality_SuitsReviewerRole()
    {
        using TempDataDir dir = new();
        SkillPackStore store = new(dir.Options(), NullLogger<SkillPackStore>.Instance);

        SkillPack? pack = store.Get("code-review-and-quality");

        Assert.NotNull(pack);
        Assert.Equal("1.0.0", pack.Version);
        Assert.Equal("Code Review and Quality", pack.Title);
        Assert.Equal(["reviewer"], pack.AppliesTo);
        Assert.Equal("A change is in Review and needs depth beyond style", pack.ConsultWhenAdds);
        SkillRef skill = Assert.Single(pack.Skills);
        Assert.Equal("code-review-and-quality", skill.Name);
        Assert.Equal("addyosmani/agent-skills", skill.Source);
        Assert.Equal("skills/code-review-and-quality", skill.Path);
    }

    /// <summary>Every role a starter pack applies to is a role the Development manifest declares.</summary>
    [Fact]
    public void Builtins_AppliesToRoles_ExistInDevelopmentManifest()
    {
        using TempDataDir dir = new();
        SkillPackStore store = new(dir.Options(), NullLogger<SkillPackStore>.Instance);
        IReadOnlyList<RoleSpec> roles = LoadDevelopmentManifest().Roles ?? [];
        HashSet<string> roleIds = new(roles.Select(role => role.Id), StringComparer.Ordinal);

        List<string> unknown = store.All
            .SelectMany(pack => pack.AppliesTo)
            .Where(role => !roleIds.Contains(role))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.Empty(unknown);
    }

    /// <summary>Every pack the Development roles name is loaded by the store, and at least one role names a pack.</summary>
    [Fact]
    public void Development_NamedPacks_AllExist()
    {
        using TempDataDir dir = new();
        SkillPackStore store = new(dir.Options(), NullLogger<SkillPackStore>.Instance);
        IReadOnlyList<RoleSpec> roles = LoadDevelopmentManifest().Roles ?? [];

        List<string> named = roles
            .SelectMany(role => role.Packs)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        List<string> missing = named
            .Where(id => store.Get(id) is null)
            .ToList();

        Assert.NotEmpty(named);
        Assert.Empty(missing);
    }
}

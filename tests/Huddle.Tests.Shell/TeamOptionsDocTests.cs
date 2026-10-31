using System.Reflection;
using Agency.Huddle.App;

namespace Agency.Huddle.Tests;

/// <summary>Pins the Engineering Guide's configuration table to <see cref="TeamOptions"/> (Skill Packs spec, task P28): every
/// key set directly on the <c>Team:</c> section has a row, so a new key cannot ship undocumented.</summary>
public sealed class TeamOptionsDocTests
{
    /// <summary>Every string, number or switch declared on <see cref="TeamOptions"/> has a row in the guide's configuration
    /// table, written either as the bare key or with its <c>Team:</c> prefix.</summary>
    [Fact]
    public void EveryScalarKey_HasAGuideRow()
    {
        string[] lines = File.ReadAllLines(RepoPaths.Combine("docs", "Huddle.EngineeringGuide.md"));
        IReadOnlyList<string> keys = [.. typeof(TeamOptions)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(static property => property.PropertyType == typeof(string) || property.PropertyType == typeof(int) || property.PropertyType == typeof(bool))
            .Select(static property => property.Name)];

        IReadOnlyList<string> missing = [.. keys.Where(key => !lines.Any(line =>
            line.StartsWith($"| `{key}` |", StringComparison.Ordinal) || line.StartsWith($"| `Team:{key}` |", StringComparison.Ordinal)))];

        Assert.Empty(missing);
    }

    /// <summary>The four Skill Packs keys are among those checked, so the test above would fail without their rows.</summary>
    [Fact]
    public void SkillPacksKeys_AreScalarKeysOfTeamOptions()
    {
        IReadOnlyList<string> names = [.. typeof(TeamOptions)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(static property => property.PropertyType == typeof(string))
            .Select(static property => property.Name)];

        Assert.Equal(["DataDir", "PipeName", "HumanName", "SkillSourcesDir", "ComputerName", "SkillsRepositoryDir", "HuddlePacksPath"], names);
    }

}
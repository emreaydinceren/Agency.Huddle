using Agency.Huddle.App.Skills;

namespace Agency.Huddle.Tests.Skills;

/// <summary>Tests for <see cref="SkillOrigin"/>.</summary>
public sealed class SkillOriginTests
{
    /// <summary>A catalog Skill reads as its publisher and its place in the repository, with the GitHub address shortened to owner/repo.</summary>
    [Fact]
    public void Describe_CatalogSkill_NamesPublisherAndRelativePath()
    {
        string text = SkillOrigin.Describe("addyosmani", "https://github.com/addyosmani/agent-skills/", "/skills/incremental-implementation/");

        Assert.Equal("addyosmani · addyosmani/agent-skills/skills/incremental-implementation", text);
    }

    /// <summary>With no publisher, the repository and path stand alone; with no path, only the repository.</summary>
    [Fact]
    public void Describe_NoPublisherOrPath_LeavesThemOut()
    {
        Assert.Equal("acme/skills/x", SkillOrigin.Describe(null, "acme/skills", "x"));
        Assert.Equal("acme/skills", SkillOrigin.Describe(" ", "acme/skills", null));
    }

    /// <summary>A built-in Skill says so.</summary>
    [Fact]
    public void Describe_BuiltIn_SaysBuiltIn()
    {
        Assert.Equal("Built in", SkillOrigin.Describe("anyone", "builtin", "team-management"));
    }
}

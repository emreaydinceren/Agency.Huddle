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

    /// <summary>An installed Skill reads from its provenance record when it has one, else from its folder, and a shipped Skill with no folder adds nothing.</summary>
    [Fact]
    public void Of_InstalledSkill_UsesProvenanceOrItsSource()
    {
        SkillProvenance provenance = new("https://github.com/acme/skills", "skills/x", "abc1234", "sha", "Acme", "MIT", DateTimeOffset.UnixEpoch);
        Skill imported = new("x", "d", [], [], SkillSource.Yours, null, provenance);

        Assert.Equal("Acme · acme/skills/skills/x", SkillOrigin.Of(imported));
        Assert.Equal("Skills/x", SkillOrigin.Of(imported with { Provenance = null, FolderPath = Path.Combine("data", "Skills", "x") }));
        Assert.Null(SkillOrigin.Of(imported with { Provenance = null, Source = SkillSource.Default }));
    }

    /// <summary>A built-in Skill says so.</summary>
    [Fact]
    public void Describe_BuiltIn_SaysBuiltIn()
    {
        Assert.Equal("Built in", SkillOrigin.Describe("anyone", "builtin", "team-management"));
    }
}
